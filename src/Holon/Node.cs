using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Holon.Events;
using Holon.Events.Serializers;
using Holon.Metrics;
using Holon.Protocol;
using Holon.Remoting;
using Holon.Remoting.Introspection;
using Holon.Services;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Holon
{
    /// <summary>
    /// Represents an application node.
    /// </summary>
    public sealed class Node : IDisposable
    {
        #region Fields
        private BrokerContext _brokerContext;
        private Broker _broker;

        private Guid _uuid;

        private NodeConfiguration _configuration;
        private TaskCompletionSource<Broker> _brokerWait;

        private bool _disposed;
        private string _appId;
        private string _appVersion;
        private List<Service> _services = new List<Service>();

        private List<string> _declaredEventNamespaces = new List<string>();

        private BrokerQueue _replyQueue;
        private Dictionary<Guid, ReplyWait> _replyWaits = new Dictionary<Guid, ReplyWait>();
        private Service _queryService;
        
        internal static Dictionary<string, string> DefaultTags = new Dictionary<string, string>() {
            { "RPCVersion", RpcHeader.HEADER_VERSION },
            { "RPCSerializers", "pbuf,xml" }
        };
        #endregion

        #region Events
        /// <summary>
        /// Called when a reply is received that is unroutable.
        /// </summary>
        public event EventHandler<UnroutableReplyEventArgs> UnroutableReply;

        /// <summary>
        /// </summary>
        /// <param name="e">The event arguments.</param>
        private void OnUnroutableReply(UnroutableReplyEventArgs e) {
            UnroutableReply?.Invoke(this, e);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the underlying introspection service.
        /// </summary>
        public Service QueryService {
            get {
                return _queryService;
            }
        }

        /// <summary>
        /// Gets the underlying broker.
        /// </summary>
        internal Broker Broker {
            get {
                return _broker;
            }
        }

        /// <summary>
        /// Gets the UUID.
        /// </summary>
        public Guid UUID {
            get {
                return _uuid;
            }
        }

        /// <summary>
        /// Gets the application id.
        /// </summary>
        public string ApplicationId {
            get {
                return _appId;
            }
        }

        /// <summary>
        /// Gets the application version.
        /// </summary>
        public string ApplicationVersion {
            get {
                return _appVersion;
            }
        }

        /// <summary>
        /// Gets the number of registered services.
        /// </summary>
        public int ServiceCount {
            get {
                return _services.Count;
            }
        }

        /// <summary>
        /// Gets the services.
        /// </summary>
        public Service[] Services {
            get {
                lock (_services) {
                    return _services.ToArray();
                }
            }
        }
        #endregion

        #region Service Messaging
        /// <summary>
        /// Replys to a message.
        /// </summary>
        /// <param name="replyTo">The reply to address.</param>
        /// <param name="replyId">The envelope ID.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        internal async Task ReplyAsync(string replyTo, Guid replyId, byte[] body, IDictionary<string, object> headers = null) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // send
            await broker.SendAsync("", replyTo, body, headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, replyId.ToString()).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public Task SendAsync(params Message[] messages) {
            return SendAsync((IEnumerable<Message>)messages);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public async Task SendAsync(IEnumerable<Message> messages) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // send
            await broker.SendAsync(messages.Select(m => {
                return new OutboundMessage(m.Address.Namespace, m.Address.RoutingKey, m.Body, m.Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, null);
            }));
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        public async Task SendAsync(Message message) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // send
            await broker.SendAsync(message.Address.Namespace, message.Address.RoutingKey, message.Body, message.Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, null)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public Task SendAsync(string addr, byte[] body, IDictionary<string, object> headers = null) {
            return SendAsync(new Message() {
                Address = new ServiceAddress(addr),
                Body = body,
                Headers = headers
            });
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public Task SendAsync(ServiceAddress addr, byte[] body, IDictionary<string, object> headers = null) {
            return SendAsync(new Message() {
                Address = addr,
                Body = body,
                Headers = headers
            });
        }

        /// <summary>
        /// Sends the message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Task[]> AskAsync(Message[] messages, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // generate envelope ids
            Guid[] envelopeIDs = new Guid[messages.Length];

            for (int i = 0; i < envelopeIDs.Length; i++)
                envelopeIDs[i] = Guid.NewGuid();

            // generate headers
            IDictionary<string, object>[] envelopeHeaders = new IDictionary<string, object>[messages.Length];

            for (int i = 0; i < envelopeIDs.Length; i++) {
                envelopeHeaders[i] = messages[i].Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

                if (!envelopeHeaders[i].ContainsKey("x-message-ttl"))
                    envelopeHeaders[i]["x-message-ttl"] = timeout.TotalSeconds;
            }

            // setup receive handlers
            Task<Envelope>[] envelopeWaits = envelopeIDs.Select(g => WaitReplyAsync(g, timeout, cancellationToken)).ToArray();

            // create messages
            OutboundMessage[] outboundMessages = new OutboundMessage[messages.Length];

            for (int i = 0; i < outboundMessages.Length; i++) {
                outboundMessages[i] = new OutboundMessage(messages[i].Address.Namespace, messages[i].Address.RoutingKey, messages[i].Body, messages[i].Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), _replyQueue.Name, envelopeIDs[i].ToString());
            }

            // send all messages
            await broker.SendAsync(outboundMessages).ConfigureAwait(false);

            // return the waits for all
            return envelopeWaits;
        }

        /// <summary>
        /// Sends the message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="addr">The service adddress.</param>
        /// <param name="body">The body.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope> envelopeWait = WaitReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            if (!headers.ContainsKey("x-message-ttl"))
                headers["x-message-ttl"] = timeout.TotalSeconds;

            // send
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, body, headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="addr">The service adddress.</param>
        /// <param name="body">The body.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope> AskAsync(string addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return AskAsync(new ServiceAddress(addr), body, timeout, headers, cancellationToken);
        }

        /// <summary>
        /// Broadcasts the message to the provided service address and waits for any responses within the timeout.
        /// </summary>
        /// <param name="addr">The service adddress.</param>
        /// <param name="body">The body.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope[]> BroadcastAsync(ServiceAddress addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            if (timeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentException(nameof(timeout), "The timeout cannot be infinite for a broadcast");

            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task.ConfigureAwait(false);

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope[]> envelopeWait = WaitManyReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            if (!headers.ContainsKey("x-message-ttl"))
                headers["x-message-ttl"] = timeout.TotalSeconds;

            // send
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, body, headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcasts the message to the provided service address and waits for any responses within the timeout.
        /// </summary>
        /// <param name="addr">The service adddress.</param>
        /// <param name="body">The body.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope[]> BroadcastAsync(string addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return BroadcastAsync(new ServiceAddress(addr), body, timeout, headers, cancellationToken);
        }

        /// <summary>
        /// Waits for an envelope to be received on the reply queue with the provided envelope id.
        /// </summary>
        /// <param name="replyId">The envelope id.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        internal async Task<Envelope[]> WaitManyReplyAsync(Guid replyId, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // create completion source
            TaskCompletionSource<Envelope> tcs = new TaskCompletionSource<Envelope>();
            List<Envelope> results = new List<Envelope>();

            lock (_replyWaits) {
                _replyWaits.Add(replyId, new ReplyWait() {
                    CompletionSource = new TaskCompletionSource<Envelope>(),
                    Results = results
                });
            }

            // create the timeout and cancellation task
            try {
                Task timeoutTask = Task.Delay(timeout);
                Task cancelTask = cancellationToken == CancellationToken.None ? null : Task.FromCanceled(cancellationToken);

                // wait until either the operation times out, is cancelled or finishes
                Task resultTask = cancelTask == null ? await Task.WhenAny(timeoutTask, tcs.Task).ConfigureAwait(false) :
                    await Task.WhenAny(timeoutTask, tcs.Task, cancelTask).ConfigureAwait(false);

                if (resultTask == timeoutTask || resultTask == cancelTask) {
                    return results.ToArray();
                } else {
                    // the operation failed
                    throw tcs.Task.Exception;
                }
            } finally {
                lock (_replyWaits) {
                    _replyWaits.Remove(replyId);
                }
            }
        }

        /// <summary>
        /// Waits for an envelope to be received on the reply queue with the provided envelope id.
        /// </summary>
        /// <param name="replyId">The envelope id.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        internal async Task<Envelope> WaitReplyAsync(Guid replyId, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // create completion source
            TaskCompletionSource<Envelope> tcs = new TaskCompletionSource<Envelope>();

            lock(_replyWaits) {
                _replyWaits.Add(replyId, new ReplyWait() {
                    CompletionSource = tcs
                });
            }

            if (timeout == Timeout.InfiniteTimeSpan)
                return await tcs.Task.ConfigureAwait(false);
            else {
                // create the timeout and cancellation task
                Task timeoutTask = Task.Delay(timeout);
                Task cancelTask = cancellationToken == CancellationToken.None ? null : Task.FromCanceled(cancellationToken);

                // wait until either the operation times out, is cancelled or finishes
                Task resultTask = cancelTask == null ? await Task.WhenAny(timeoutTask, tcs.Task).ConfigureAwait(false) :
                    await Task.WhenAny(timeoutTask, tcs.Task, cancelTask).ConfigureAwait(false);

                if (resultTask == timeoutTask)
                    throw new TimeoutException("The operation timed out before a reply was received");
                else if (resultTask == cancelTask)
                    throw new TaskCanceledException("The operation was cancelled before a reply was received");
                else {
                    return tcs.Task.Result;
                }
            }
        }
        #endregion

        #region Other Methods
        /// <summary>
        /// Reconnects the underlying broker.
        /// </summary>
        /// <returns></returns>
        private async Task ReconnectAsync() {
            // shutdown broker
            lock (_broker) {
                // add broker wait
                _brokerWait = new TaskCompletionSource<Broker>();

                // trigger all reply waits
                lock (_replyWaits) {
                    // copy dictionary over
                    foreach (KeyValuePair<Guid, ReplyWait> kv in _replyWaits) {
                        kv.Value.CompletionSource.TrySetException(new Exception("The underlying broker connection was lost"));
                    }

                    _replyWaits.Clear();
                }
                
#if DEBUG
                Debug.WriteLine(":Reconnect -> Cancelled all waits");
#endif
            }

            // wait for reconnect
            _broker = await _broker.Context.CreateBrokerAsync(_configuration.ApplicationId).ConfigureAwait(false);

            // setup
            await SetupAsync().ConfigureAwait(false);

            // resetup services
            List<Task> resetupTasks = new List<Task>();

            lock (_services) {
                foreach (Service service in _services) {
                    resetupTasks.Add(service.ResetupAsync(_broker));
                }
            }

            // wait until all services are setup again
            await Task.WhenAll(resetupTasks).ConfigureAwait(false);

#if DEBUG
            Debug.WriteLine(string.Format(":Reconnect -> Recreated broker with {0} services", resetupTasks.Count));
#endif

            // cancel broker wait
            lock (_broker) {
                _brokerWait.SetResult(_broker);
                _brokerWait = null;
                
#if DEBUG
                Debug.WriteLine(":Reconnect -> Set broker and resumed waits");
#endif
            }
        }

        /// <summary>
        /// Setup the node, called internally.
        /// </summary>
        /// <returns></returns>
        internal async Task SetupAsync() {
            // add shutdown handler
            _broker.Shutdown += async delegate (object s, BrokerShutdownEventArgs e) {
                Debug.WriteLine(":Reconnect -> Reconnecting");

                try {
                    await ReconnectAsync().ConfigureAwait(false);
#if DEBUG
                } catch(Exception ex) {
                    Debug.Fail(":Reconnect -> Failed to reconnect: " + ex.Message);
#else
                } catch (Exception ex) {
#endif
                    Console.WriteLine("messaging", "failed to reconnect to broker: {0}", ex.ToString());
                    Dispose();
                }
            };

            // add returned handler
            _broker.Returned += delegate (object s, BrokerReturnedEventArgs e) {
                if (e.ID != Guid.Empty) {
                    TaskCompletionSource<Envelope> tcs;

                    lock (_replyWaits) {
                        // try and get the reply wait
                        if (!_replyWaits.TryGetValue(e.ID, out ReplyWait replyWait))
                            return;

                        // if this is a multi-reply do nothing
                        if (replyWait.Results == null)
                            return;

                        // remove and set exception
                        tcs = replyWait.CompletionSource;
                        _replyWaits.Remove(e.ID);
                    }

                    tcs.TrySetException(new Exception("The envelope was returned before delivery"));
                }
            };

            // create reply queue
            try {
                // declare queue with unique name
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                    // get unique string
                    byte[] uniqueId = new byte[20];
                    rng.GetBytes(uniqueId);
                    string uniqueIdStr = BitConverter.ToString(uniqueId).Replace("-", "").ToLower();

                    _replyQueue = await _broker.CreateQueueAsync(string.Format("~reply:{1}%{0}", _uuid, uniqueIdStr), false, true, "", "", new Dictionary<string, object>() {
                        { "x-expires", (int)TimeSpan.FromMinutes(15).TotalMilliseconds }
                    }).ConfigureAwait(false);
                }
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to create node reply queue", ex);
            }

            // setup service
            if (_queryService == null) {
                _queryService = await AttachAsync(string.Format("node:{0}", _uuid), ServiceType.Singleton, ServiceExecution.Parallel, RpcBehaviour.Bind<INodeQuery001>(new NodeQueryImpl(this))).ConfigureAwait(false);
            }

            // start reply processor
            ReplyLoop();
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns>The attached service.</returns>
        public async Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {
            // create service
            Service service = new Service(this, _broker, addr, behaviour, configuration);

            if (_configuration.ThrowUnhandledExceptions) {
                service.UnhandledException += (o, e) => throw e.Exception;
            }

            // create queue
            await service.SetupAsync().ConfigureAwait(false);

            lock (_services) {
                _services.Add(service);
            }

            return service;
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns>The attached service.</returns>
        public Task<Service> AttachAsync(string addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), configuration, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="execution">The service execution.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns>The attached service.</returns>
        public Task<Service> AttachAsync(ServiceAddress addr, ServiceType type, ServiceExecution execution, ServiceBehaviour behaviour) {
            return AttachAsync(addr, new ServiceConfiguration() {
                Type = type,
                Execution = execution
            }, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="execution">The service execution.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns>The attached service.</returns>
        public Task<Service> AttachAsync(string addr, ServiceType type, ServiceExecution execution, ServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), type, execution, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(string addr, ServiceType type, ServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), type, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(ServiceAddress addr, ServiceType type, ServiceBehaviour behaviour) {
            return AttachAsync(addr, type, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service behaviour to the address, defaults to a fanout service type.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(ServiceAddress addr, ServiceBehaviour behaviour) {
            return AttachAsync(addr, ServiceType.Fanout, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service behaviour to the address, defaults to a fanout service type.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(string addr, ServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), ServiceType.Fanout, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Detaches a service from the node.
        /// </summary>
        /// <param name="service">The service.</param>
        public void Detach(Service service) {
            // remove service
            lock(_services) {
                if (!_services.Contains(service))
                    return;

                // remove from list
                _services.Remove(service);
            }

            // dispose
            service.Dispose();
        }

        /// <summary>
        /// Node worker to process reply messages.
        /// </summary>
        private async void ReplyLoop() {
            while (!_disposed) {
                // receieve broker message
                InboundMessage msg = null;

                try {
                    msg = await _replyQueue.ReceiveAsync().ConfigureAwait(false);
                } catch(Exception) {
                    break;
                }

                //TODO: cancel on dispose
                Envelope envelope = new Envelope(msg, this);

                // check if we have an correlation
                if (envelope.ID == Guid.Empty) {
                    // trigger event
                    OnUnroutableReply(new UnroutableReplyEventArgs(envelope));

                    continue;
                }

                // get completion source for this envelope
                ReplyWait replyWait = default(ReplyWait);
                bool foundReplyWait = false;

                lock(_replyWaits) {
                    foundReplyWait = _replyWaits.TryGetValue(envelope.ID, out replyWait);
                }

                if (!foundReplyWait) {
                    // log
                    Console.WriteLine("unroutable reply: {0}", envelope.ID);

                    // trigger event
                    OnUnroutableReply(new UnroutableReplyEventArgs(envelope));
                } else {
                    // if it's a multi-reply add to results, if not set completion source
                    if (replyWait.Results == null) {
                        lock (_replyWaits) {
                            _replyWaits.Remove(envelope.ID);
                        }

                        replyWait.CompletionSource.TrySetResult(envelope);
                    } else {
                        replyWait.Results.Add(envelope);
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the node and underlying services.
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;

#if DEBUG_DISPOSE
            Debug.WriteLine("> Node::Dispose: {0}", _uuid);
#endif

            // get services
            Service[] servicesArr = null;

            lock(_services) {
                servicesArr = _services.ToArray();
            }

            // dispose each service
            foreach (Service service in servicesArr) {
                service.Dispose();
            }

            // destroy reply queue
            _replyQueue.Dispose();

#if DEBUG_DISPOSE
            Debug.WriteLine("< Node::Disposed");
#endif
        }
        #endregion

        #region Channels
        /// <summary>
        /// Configures the provided channel for this node.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="channel">The channel.</param>
        /// <returns>The configured channel.</returns>
        public IClientChannel Channel(ServiceAddress address, IClientChannel channel) {
            channel.Reset(this, address);
            return channel;
        }
        #endregion

        #region Proxying
        /// <summary>
        /// Gets the introspection proxy interface for RPC.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public IInterfaceQuery001 QueryProxy(string address) {
            return QueryProxy(new ServiceAddress(address));
        }

        /// <summary>
        /// Gets the introspection proxy interface for RPC.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public IInterfaceQuery001 QueryProxy(ServiceAddress address) {
            return Proxy<IInterfaceQuery001>(address);
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="address">The service address.</param>
        /// <returns></returns>
        public T Proxy<T>(string address) {
            return Proxy<T>(new ServiceAddress(address));
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public IT Proxy<IT>(ServiceAddress address, ProxyConfiguration configuration) {
            // create the channel
            BasicChannel channel = new BasicChannel();
            channel.Reset(this, address);

            // create proxy
            return channel.Proxy<IT>(configuration);
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public IT Proxy<IT>(string address, ProxyConfiguration configuration) {
            return Proxy<IT>(address, configuration);
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="address">The service address.</param>
        /// <returns></returns>
        public IT Proxy<IT>(ServiceAddress address) {
            return Proxy<IT>(address, new ProxyConfiguration() { });
        }

        /// <summary>
        /// Gets a RPC proxy for the provided service address using dynamics.
        /// </summary>
        /// <param name="address">The service address.</param>
        /// <param name="interface">The interface.</param>
        /// <returns></returns>
        public dynamic DynamicProxy(ServiceAddress address, string @interface) {
            throw new NotImplementedException();
        }
        #endregion

        #region Event System
        /// <summary>
        /// Declares the event, creating the namespace and storing the type for future reference.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <note>Currently the only behaviour is to declare the namespace.</note>
        /// <returns></returns>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        private async Task DeclareEventAsync(EventAddress addr) {
            // check if already declared
            lock (_declaredEventNamespaces) {
                if (_declaredEventNamespaces.Contains(addr.Namespace))
                    return;
            }

            // declare exchange
            int retries = 3;

            while (retries > 0) {
                try {
                    await _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false).ConfigureAwait(false);
                    break;
                } catch (Exception) {
                    retries--;
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }

            if (retries == 0)
                return;

            // add to list
            lock (_declaredEventNamespaces) {
                _declaredEventNamespaces.Add(addr.Namespace);
            }
        }
        
        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <param name="data">The event data.</param>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        /// <returns></returns>
        public async Task EmitAsync(EventAddress addr, object data) {
            // check if not declared
            bool declared = false;

            lock (_declaredEventNamespaces) {
                 declared = _declaredEventNamespaces.Contains(addr.Namespace);
            }

            if (!declared)
                await DeclareEventAsync(addr).ConfigureAwait(false);

            // serialize data payload
            Event e = new Event(addr.Resource, addr.Name);
            e.Serialize(data);

            // serialize
            ProtobufEventSerializer serializer = new ProtobufEventSerializer();
            byte[] body = serializer.SerializeEvent(e);

            // send event
            try {
                await _broker.SendAsync(string.Format("!{0}", addr.Namespace), addr.Name, body, new Dictionary<string, object>() {
                    { EventHeader.HEADER_NAME, new EventHeader(EventHeader.HEADER_VERSION, serializer.Name).ToString() }
                }, null, null, false).ConfigureAwait(false);
            } catch (Exception) { }
        }

        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <param name="data">The event data.</param>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        /// <returns></returns>
        public Task EmitAsync(string addr, object data) {
            return EmitAsync(new EventAddress(addr), data);
        }
        
        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public async Task<EventSubscription> SubscribeAsync(EventAddress addr) {
            // create the queue
            await _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false).ConfigureAwait(false);
            BrokerQueue brokerQueue = null;

            // declare queue with unique name
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                // get unique string
                byte[] uniqueId = new byte[20];
                rng.GetBytes(uniqueId);
                string uniqueIdStr = BitConverter.ToString(uniqueId).Replace("-", "").ToLower();

                brokerQueue = await _broker.CreateQueueAsync(string.Format("!{0}%{1}", addr.ToString(), uniqueIdStr), false, true, string.Format("!{0}", addr.Namespace), addr.Name, null).ConfigureAwait(false);
            }

            // create subscription
            return new EventSubscription(addr, this, brokerQueue);
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public Task<EventSubscription> SubscribeAsync(string addr) {
            return SubscribeAsync(new EventAddress(addr));
        }
        #endregion

        #region Setup
        /// <summary>
        /// Creates a new node on the provided endpoint.
        /// </summary>
        /// <param name="endpoint">The broker endpoint.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static async Task<Node> CreateAsync(string endpoint, NodeConfiguration configuration = default(NodeConfiguration)) {
            // fill configuration
            if (configuration == null)
                configuration = new NodeConfiguration();
            
            // create broker context
            BrokerContext ctx = await BrokerContext.CreateAsync(endpoint);

            // create broker
            Broker broker = await ctx.CreateBrokerAsync(configuration.ApplicationId);

            // create node
            Node node = new Node(broker, configuration);
            node._brokerContext = ctx;
            node._broker = broker;

            // setup node
            await node.SetupAsync();

            return node;
        }

        /// <summary>
        /// Creates a new node on the provided endpoint.
        /// </summary>
        /// <param name="configuration">The broker endpoint.</param>
        /// <returns></returns>
        public static Task<Node> CreateFromEnvironmentAsync(NodeConfiguration configuration = default(NodeConfiguration)) {
            // check environment
            string nodeUuid = Environment.GetEnvironmentVariable("NODE_UUID");
            string endpoint = Environment.GetEnvironmentVariable("BROKER_ENDPOINT");

            if (nodeUuid != null)
                configuration.UUID = Guid.Parse(nodeUuid);

            if (endpoint == null)
                throw new ArgumentNullException("The endpoint in environment is null");

            return CreateAsync(endpoint, configuration);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="broker">The broker.</param>
        /// <param name="configuration">The node configuration.</param>
        internal Node(Broker broker, NodeConfiguration configuration) {
            // check app id format
            if (configuration.ApplicationId.IndexOf('.') + configuration.ApplicationId.IndexOf(' ') != -2)
                throw new FormatException("The node application id cannot contains dots or spaces");

            // apply private members
            _broker = broker;
            _appId = configuration.ApplicationId.ToLower();
            _appVersion = configuration.ApplicationVersion;
            _uuid = configuration.UUID == Guid.Empty ? Guid.NewGuid() : configuration.UUID;
            _configuration = configuration;
        }
        #endregion
    }

    /// <summary>
    /// Represents event arguments for an unroutable reply envelope.
    /// </summary>
    public class UnroutableReplyEventArgs
    {
        #region Fields
        private Envelope _envelope;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the envelope.
        /// </summary>
        public Envelope Envelope { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new unrouteable reply event arguments.
        /// </summary>
        /// <param name="envelope">The unroutable envelope.</param>
        public UnroutableReplyEventArgs(Envelope envelope) {
            _envelope = envelope;
        }
        #endregion
    }

    /// <summary>
    /// Represents a reply waiting structure.
    /// </summary>
    struct ReplyWait
    {
        public TaskCompletionSource<Envelope> CompletionSource { get; set; }
        public List<Envelope> Results { get; set; }
    }
}