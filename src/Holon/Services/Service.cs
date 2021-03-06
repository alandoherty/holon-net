﻿using Holon.Metrics.Tracing;
using Holon.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Services
{
    /// <summary>
    /// Represents an service.
    /// </summary>
    public sealed class Service : IDisposable, IObserver<InboundMessage>
    {
        #region Fields
        private Namespace _namespace;
        private ServiceAddress _addr;
        private Broker _broker;
        private BrokerQueue _queue;
        private ServiceBehaviour _behaviour;
        private ServiceConfiguration _configuration;

        private DateTimeOffset _timeSetup;
        private int _requestsPending = 0;
        private int _requestsCompleted = 0;
        private int _requestsFaulted = 0;

        private SemaphoreSlim _concurrencySlim;

        private int _disposed;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the time the service was setup.
        /// </summary>
        public DateTimeOffset TimeSetup {
            get {
                return _timeSetup;
            }
        }

        /// <summary>
        /// Gets the number of completed requests.
        /// </summary>
        public int RequestsCompleted {
            get {
                return _requestsCompleted;
            }
        }

        /// <summary>
        /// Gets the number of pending requests.
        /// </summary>
        public int RequestsPending {
            get {
                return _requestsPending;
            }
        }

        /// <summary>
        /// Gets the number of faulted requests.
        /// </summary>
        public int RequestsFaulted {
            get {
                return _requestsFaulted;
            }
        }

        /// <summary>
        /// Gets the service execution strategy.
        /// </summary>
        public ServiceExecution Execution {
            get {
                return _configuration.Execution;
            }
        }

        /// <summary>
        /// Gets the service type.
        /// </summary>
        public ServiceType Type {
            get {
                return _configuration.Type;
            }
        }

        /// <summary>
        /// Gets the underlying behaviour.
        /// </summary>
        public ServiceBehaviour Behaviour {
            get {
                return _behaviour;
            }
        }

        /// <summary>
        /// Gets the service address.
        /// </summary>
        public ServiceAddress Address {
            get {
                return _addr;
            }
        }

        /// <summary>
        /// Gets the broker.
        /// </summary>
        internal Broker Broker {
            get {
                return _broker;
            }
        }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        internal Namespace Namespace {
            get {
                return _namespace;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Called when a service behaviour creates an unhandled exception.
        /// </summary>
        public event EventHandler<ServiceExceptionEventArgs> UnhandledException;

        /// <summary>
        /// Handles unhandled exceptions.
        /// </summary>
        /// <param name="e">The exception event args.</param>
        /// <returns>If the exception was handled.</returns>
        private bool OnUnhandledException(ServiceExceptionEventArgs e) {
            if (UnhandledException == null)
                return false;

            UnhandledException.Invoke(this, e);
            return true;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Disposes the service.
        /// </summary>
        public void Dispose() {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            // dispose of queue
            _queue.Dispose();

            // detach from node
            _namespace.Node.Detach(this);
        }

        /// <summary>
        /// Shutdown the service.
        /// </summary>
        /// <returns></returns>
        internal async Task ShutdownAsync() {
            // cancel
            await _queue.CancelAsync().ConfigureAwait(false);

            // dispose
            Dispose();
        }

        /// <summary>
        /// Creates the queue and internal consumer for this service.
        /// </summary>
        /// <param name="broker">The broker.</param>
        /// <exception cref="InvalidOperationException">If the queue already exists.</exception>
        /// <returns></returns>
        internal async Task<BrokerQueue> SetupAsync(Broker broker) {
            // check if queue has already been created
            if (_queue != null)
                throw new InvalidOperationException("The broker queue has already been created");

            // set broker
            _broker = broker;

            // create queue
            _broker.DeclareExchange(_addr.Namespace, "topic", true, false);

            // check if already declared
            if (_namespace.Node.Services.Any(s => s.Type == ServiceType.Singleton && s._addr == _addr))
                throw new InvalidOperationException("The service is already in use as a singleton");

            if (Type == ServiceType.Singleton) {
                // declare one exclusive queue
                _queue = await _broker.CreateQueueAsync(_addr.ToString(), false, true, _addr.Namespace, _addr.RoutingKey).ConfigureAwait(false);
            } else if (Type == ServiceType.Fanout) {
                // declare queue with unique name
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                    // get unique string
                    byte[] uniqueId = new byte[20];
                    rng.GetBytes(uniqueId);
                    string uniqueIdStr = BitConverter.ToString(uniqueId).Replace("-", "").ToLower();

                    _queue = await _broker.CreateQueueAsync(string.Format("{0}%{1}", _addr.ToString(), uniqueIdStr), false, false, _addr.Namespace, _addr.RoutingKey).ConfigureAwait(false);
                }
            } else if (Type == ServiceType.Balanced) {
                // declare one queue shared between many
                _queue = await _broker.CreateQueueAsync(_addr.ToString(), false, false, _addr.Namespace, _addr.RoutingKey).ConfigureAwait(false);
            }

            // setup semaphore
            _concurrencySlim = new SemaphoreSlim(_configuration.MaxConcurrency, _configuration.MaxConcurrency);

            // begin observing
            _queue.AsObservable().Subscribe(this);

            // set uptime
            _timeSetup = DateTimeOffset.UtcNow;

            return _queue;
        }

        /// <summary>
        /// Explicitly binds this service to another routing key in the namespace.
        /// </summary>
        /// <param name="routingKey">The routing key.</param>
        /// <returns></returns>
        public Task BindAsync(string routingKey) {
            return _queue.BindAsync(_addr.Namespace, routingKey);
        }

        /// <summary>
        /// Explicitly binds this service to another routing key in the namespace. This binding will occur asyncronously in the underlying broker.
        /// </summary>
        /// <param name="routingKey">The routing key.</param>
        public void Bind(string routingKey) {
            _queue.Bind(_addr.Namespace, routingKey);
        }

        /// <summary>
        /// Handles a single envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        private async void ServiceHandle(Envelope envelope) {
            try {
                // increment pending metric
                Interlocked.Increment(ref _requestsPending);
                
                // handle
                await ServiceHandleAsync(envelope).ConfigureAwait(false);

                // release semaphore
                try {
                    _concurrencySlim.Release();
                } catch (Exception) { }

                // decrement pending metric and increment completed
                Interlocked.Decrement(ref _requestsPending);
                Interlocked.Increment(ref _requestsCompleted);
            } catch (Exception ex) {
                // release semaphore
                _concurrencySlim.Release();

                // increment faulted metric
                Interlocked.Increment(ref _requestsFaulted);

                // decrement pending metric
                Interlocked.Decrement(ref _requestsPending);

                OnUnhandledException(new ServiceExceptionEventArgs(_behaviour, ex));
            } finally {
                // acknowledge
                _broker.Context.QueueWork(() => {
                    _broker.Channel.BasicAck(envelope.Message.DeliveryTag, false);
                    return null;
                });
            }
        }

        /// <summary>
        /// Handles a single envelope asyncronously.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        private async Task ServiceHandleAsync(Envelope envelope) {
            // process filters, if any handler in the chain returns false we ditch this envelope
            if (_configuration.Filters.Length > 0) {
                foreach (IServiceFilter filter in _configuration.Filters) {
                    if (!await filter.HandleAsync(envelope).ConfigureAwait(false))
                        return;
                }
            }

            // trace begin
            _namespace.Node.OnTraceBegin(new TraceEventArgs(envelope, _behaviour));

            // actually run handler
            await _behaviour.HandleAsync(envelope).ConfigureAwait(false);

            // trace end
            _namespace.Node.OnTraceEnd(new TraceEventArgs(envelope, _behaviour));
        }

        void IObserver<InboundMessage>.OnCompleted() {
        }

        void IObserver<InboundMessage>.OnError(Exception error) {
        }

        async void IObserver<InboundMessage>.OnNext(InboundMessage message) {
            Envelope envelope = new Envelope(message, _namespace);

            // wait for a free request slot, this ensures ServiceConfiguration.MaxConcurrency is kept to
            await _concurrencySlim.WaitAsync().ConfigureAwait(false);

            // handle
            try {
                if (Execution == ServiceExecution.Serial) {
                    // increment pending metric
                    Interlocked.Increment(ref _requestsPending);

                    // handle the operaration
                    await ServiceHandleAsync(envelope).ConfigureAwait(false);

                    // release semaphore
                    try {
                        _concurrencySlim.Release();
                    } catch (Exception) { }

                    // decrement pending metric and increment completed
                    Interlocked.Decrement(ref _requestsPending);
                    Interlocked.Increment(ref _requestsCompleted);
                } else {
                    ServiceHandle(envelope);
                    return;
                }
            } catch (Exception ex) {
                // release semaphore
                _concurrencySlim.Release();

                // increment faulted metric
                Interlocked.Increment(ref _requestsFaulted);
                // decrement pending metric
                Interlocked.Decrement(ref _requestsPending);

                // call exception handler
                OnUnhandledException(new ServiceExceptionEventArgs(_behaviour, ex));
            }

            // acknowledge
            _broker.Context.QueueWork(() => {
                _broker.Channel.BasicAck(envelope.Message.DeliveryTag, false);
                return null;
            });
        }
        #endregion

        #region Constructors
        internal Service(Namespace @namespace, ServiceAddress addr, ServiceBehaviour behaviour, ServiceConfiguration configuration) {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _addr = addr;
            _configuration = configuration;
            _namespace = @namespace ?? throw new ArgumentNullException(nameof(@namespace));
        }
        #endregion
    }

    /// <summary>
    /// Represents arguments for an unhandled service exception event.
    /// </summary>
    public class ServiceExceptionEventArgs
    {
        #region Fields
        private Exception _exception;
        private ServiceBehaviour _behaviour;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the exception.
        /// </summary>
        public Exception Exception {
            get {
                return _exception;
            }
        }

        /// <summary>
        /// Gets the behaviour which raised the exception.
        /// </summary>
        public ServiceBehaviour Behaviour {
            get {
                return _behaviour;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new service exception event argument object.
        /// </summary>
        /// <param name="behaviour">The behaviour which raised the exception.</param>
        /// <param name="ex">The exception.</param>
        public ServiceExceptionEventArgs(ServiceBehaviour behaviour, Exception ex) {
            _behaviour = behaviour;
            _exception = ex;
        }
        #endregion
    }
}
