using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Holon.Events;
using Holon.Events.Serializers;
using Holon.Introspection;
using Holon.Metrics;
using Holon.Remoting;
using Holon.Services;

namespace Holon
{
    /// <summary>
    /// Represents an application node.
    /// </summary>
    public class Node : IDisposable
    {
        #region Fields
        private Guid _uuid;

        private NodeConfiguration _configuration;
        private Broker _broker;
        private TaskCompletionSource<Broker> _brokerWait;

        private bool _disposed;
        private string _appId;
        private string _appVersion;
        private List<Service> _services = new List<Service>();

        private List<string> _declaredEventNamespaces = new List<string>();

        private BrokerQueue _replyQueue;
        private Dictionary<Guid, TaskCompletionSource<Envelope>> _replyWaits = new Dictionary<Guid, TaskCompletionSource<Envelope>>();
        private Service _nodeService;

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
        protected void OnUnroutableReply(UnroutableReplyEventArgs e) {
            UnroutableReply?.Invoke(this, e);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the underlying introspection service.
        /// </summary>
        public Service Service {
            get {
                return _nodeService;
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

        #region Methods
        /// <summary>
        /// Replys to the message.
        /// </summary>
        /// <param name="replyTo">The reply to address.</param>
        /// <param name="envelopeId">The envelope ID.</param>
        /// <param name="body">The body.</param>
        /// <returns></returns>
        internal Task ReplyAsync(string replyTo, Guid envelopeId, byte[] body) {
            return ReplyAsync(replyTo, envelopeId, null, body);
        }

        /// <summary>
        /// Replys to a message.
        /// </summary>
        /// <param name="replyTo">The reply to address.</param>
        /// <param name="envelopeId">The envelope ID.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="body">The body.</param>
        /// <returns></returns>
        internal async Task ReplyAsync(string replyTo, Guid envelopeId, IDictionary<string, object> headers, byte[] body) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task;
            
            // send
            await broker.SendAsync("", replyTo, null, envelopeId.ToString(), headers, body);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <returns></returns>
        public Task SendAsync(ServiceAddress addr, byte[] body) {
            return SendAsync(addr, body, null);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public async Task SendAsync(ServiceAddress addr, byte[] body, IDictionary<string, object> headers) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task;

            // send
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, null, null, headers, body);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, TimeSpan timeout) {
            return AskAsync(addr, body, null, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, IDictionary<string, object> headers, TimeSpan timeout) {
            return AskAsync(addr, body, headers, timeout, CancellationToken.None);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="addr">The service adddress.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, IDictionary<string, object> headers, TimeSpan timeout, CancellationToken cancellationToken) {
            // wait for broker to become available
            TaskCompletionSource<Broker> wait = null;
            Broker broker = _broker;

            lock (_broker) {
                if (_brokerWait != null)
                    wait = _brokerWait;
            }

            if (wait != null)
                broker = await wait.Task;

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope> envelopeWait = WaitReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            if (!headers.ContainsKey("x-message-ttl"))
                headers["x-message-ttl"] = timeout.TotalSeconds;

            // send
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, _replyQueue.Name, envelopeId.ToString(), headers, body);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait;
        }

        /// <summary>
        /// Waits for an envelope to be received on the reply queue with the provided envelope id.
        /// </summary>
        /// <param name="envelopeId">The envelope id.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellation">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope> WaitReplyAsync(Guid envelopeId, TimeSpan timeout, CancellationToken cancellation) {
            // create completion source
            TaskCompletionSource<Envelope> tcs = new TaskCompletionSource<Envelope>();

            lock(_replyWaits) {
                _replyWaits.Add(envelopeId, tcs);
            }

            if (timeout == TimeSpan.Zero)
                return await tcs.Task;
            else {
                // create the timeout and cancellation task
                Task timeoutTask = Task.Delay(timeout);
                Task cancelTask = cancellation == CancellationToken.None ? null : Task.FromCanceled(cancellation);

                // wait until either the operation times out, is cancelled or finishes
                Task resultTask = cancelTask == null ? await Task.WhenAny(timeoutTask, tcs.Task) :
                    await Task.WhenAny(timeoutTask, tcs.Task, cancelTask);

                if (resultTask == timeoutTask)
                    throw new TimeoutException("The operation timed out before a reply was received");
                else if (resultTask == cancelTask)
                    throw new TaskCanceledException("The operation was cancelled before a reply was received");
                else {
                    return tcs.Task.Result;
                }
            }
        }

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
                    Dictionary<Guid, TaskCompletionSource<Envelope>> replyWaits = new Dictionary<Guid, TaskCompletionSource<Envelope>>(_replyWaits);

                    foreach (KeyValuePair<Guid, TaskCompletionSource<Envelope>> kv in replyWaits) {
                        kv.Value.TrySetException(new Exception("The underlying broker connection was lost"));
                    }

                    _replyWaits.Clear();
                }
                
#if DEBUG
                Debug.WriteLine(":Reconnect -> Cancelled all waits");
#endif
            }

            // wait for reconnect
            _broker = await _broker.Context.CreateBrokerAsync();

            // setup
            await SetupAsync();

            // resetup services
            List<Task> resetupTasks = new List<Task>();

            lock (_services) {
                foreach (Service service in _services) {
                    resetupTasks.Add(service.ResetupAsync(_broker));
                }
            }

            // wait until all services are setup again
            await Task.WhenAll(resetupTasks);

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
                    await ReconnectAsync();
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
                        if (!_replyWaits.TryGetValue(e.ID, out tcs))
                            return;

                        _replyWaits.Remove(e.ID);
                    }

                    tcs.SetException(new Exception("The envelope was returned before delivery"));
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
                    });
                }
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to create node reply queue", ex);
            }

            // setup service
            if (_nodeService == null) {
                _nodeService = await AttachAsync(string.Format("node:{0}", _uuid), ServiceType.Singleton, ServiceExecution.Parallel, RpcBehaviour.BindOne<INodeQuery001>(new NodeQueryImpl(this)));
            }

            // start reply processor
            ReplyLoop();
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="execution">The service execution.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns></returns>
        public async Task<Service> AttachAsync(ServiceAddress addr, ServiceType type, ServiceExecution execution, IServiceBehaviour behaviour) {
            // create service
            Service service = new Service(this, _broker, addr, behaviour, type, execution);

            // create queue
            await service.SetupAsync();

            lock(_services) {
                _services.Add(service);
            }

            return service;
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="execution">The service execution.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(string addr, ServiceType type, ServiceExecution execution, IServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), type, execution, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(string addr, ServiceType type, IServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), type, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="type">The service type.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(ServiceAddress addr, ServiceType type, IServiceBehaviour behaviour) {
            return AttachAsync(addr, type, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service behaviour to the address, defaults to a fanout service type.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(ServiceAddress addr, IServiceBehaviour behaviour) {
            return AttachAsync(addr, ServiceType.Fanout, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Attaches the service behaviour to the address, defaults to a fanout service type.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns></returns>
        public Task<Service> AttachAsync(string addr, IServiceBehaviour behaviour) {
            return AttachAsync(new ServiceAddress(addr), ServiceType.Fanout, ServiceExecution.Serial, behaviour);
        }

        /// <summary>
        /// Detaches a service from the node.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <returns></returns>
        public Task DetachAsync(Service service) {
            // dispose
            service.Dispose();

            // remove service
            lock(_services) {
                _services.Remove(service);
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Node worker to process reply messages.
        /// </summary>
        private async void ReplyLoop() {
            while (!_disposed) {
                // receieve broker message
                BrokerMessage msg = null;

                try {
                    msg = await _replyQueue.ReceiveAsync();
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
                TaskCompletionSource<Envelope> tcs = null;

                lock(_replyWaits) {
                    if (_replyWaits.TryGetValue(envelope.ID, out tcs))
                        _replyWaits.Remove(envelope.ID);
                }

                if (tcs == null) {
                    // log
                    Console.WriteLine("messaging", "unroutable reply: {0}", envelope.ID);

                    // trigger event
                    OnUnroutableReply(new UnroutableReplyEventArgs(envelope));
                } else {
                    if (!tcs.TrySetResult(envelope)) {
                        Console.WriteLine("messaging", "failed to route reply for {0}", envelope.ID);
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

        #region Event System
        /// <summary>
        /// Declares the event, creating the namespace and storing the type for future reference.
        /// </summary>
        /// <typeparam name="TData">The data type.</typeparam>
        /// <param name="addr">The event address.</param>
        /// <note>Currently the only behaviour is to declare the namespace.</note>
        /// <returns></returns>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        private async Task DeclareEventAsync<TData>(EventAddress addr) {
            // check if already declared
            lock (_declaredEventNamespaces) {
                if (_declaredEventNamespaces.Contains(addr.Namespace))
                    return;
            }

            // declare exchange
            int retries = 3;

            while (retries > 0) {
                try {
                    await _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false);
                    break;
                } catch (Exception) {
                    retries--;
                    await Task.Delay(1000);
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
        /// <typeparam name="TData">The event data type.</typeparam>
        /// <param name="addr">The event address.</param>
        /// <param name="data">The event data.</param>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        /// <returns></returns>
        public async Task EmitAsync<TData>(EventAddress addr, TData data) {
            // check if not declared
            bool declared = false;

            lock (_declaredEventNamespaces) {
                 declared = _declaredEventNamespaces.Contains(addr.Namespace);
            }

            if (!declared)
                await DeclareEventAsync<TData>(addr);

            // serialize
            ProtobufEventSerializer serializer = new ProtobufEventSerializer();
            byte[] body = serializer.SerializeEvent(new Event(addr.Name, null/*data*/));

            // send event
            try {
                await _broker.SendAsync(string.Format("!{0}", addr.Namespace), addr.Name, null, null, new Dictionary<string, object>() {
                    { EventHeader.HEADER_NAME, new EventHeader(EventHeader.HEADER_VERSION, serializer.Name).ToString() }
                }, body, false);
            } catch (Exception) { }
        }

        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <typeparam name="TData">The event data type.</typeparam>
        /// <param name="addr">The event address.</param>
        /// <param name="data">The event data.</param>
        /// <returns></returns>
        public Task EmitAsync<TData>(string addr, TData data) {
            return EmitAsync(new EventAddress(addr), data);
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <typeparam name="TData">The data type.</typeparam>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public async Task<Subscription<TData>> SubscribeAsync<TData>(EventAddress addr) {
            // create the queue
            await _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false);
            BrokerQueue brokerQueue = null;

            // declare queue with unique name
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                // get unique string
                byte[] uniqueId = new byte[20];
                rng.GetBytes(uniqueId);
                string uniqueIdStr = BitConverter.ToString(uniqueId).Replace("-", "").ToLower();

                brokerQueue = await _broker.CreateQueueAsync(string.Format("!{0}%{1}", addr.ToString(), uniqueIdStr), false, true, string.Format("!{0}", addr.Namespace), addr.Name, null);
            }
            

            // create subscription
            return new Subscription<TData>(addr, this, brokerQueue);
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <typeparam name="TData">The data type.</typeparam>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public Task<Subscription<TData>> SubscribeAsync<TData>(string addr) {
            return SubscribeAsync<TData>(new EventAddress(addr));
        }
        #endregion

        #region Metrics
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="broker">The broker.</param>
        /// <param name="configuration">The node configuration.</param>
        internal Node(Broker broker, NodeConfiguration configuration) {
            // create default config
            if (configuration == null)
                configuration = new NodeConfiguration() { };

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
}
