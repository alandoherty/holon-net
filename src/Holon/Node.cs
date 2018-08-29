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
using Holon.Introspection;
using Holon.Metrics;
using Holon.Remoting;
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
        private Dictionary<Guid, TaskCompletionSource<Envelope>> _replyWaits = new Dictionary<Guid, TaskCompletionSource<Envelope>>();
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
                broker = await wait.Task.ConfigureAwait(false);

            // send
            await broker.SendAsync("", replyTo, null, envelopeId.ToString(), headers, body).ConfigureAwait(false);
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
                broker = await wait.Task.ConfigureAwait(false);

            // send
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, null, null, headers, body).ConfigureAwait(false);
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
            await broker.SendAsync(addr.Namespace, addr.RoutingKey, _replyQueue.Name, envelopeId.ToString(), headers, body).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
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
                return await tcs.Task.ConfigureAwait(false);
            else {
                // create the timeout and cancellation task
                Task timeoutTask = Task.Delay(timeout);
                Task cancelTask = cancellation == CancellationToken.None ? null : Task.FromCanceled(cancellation);

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
        public async Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, IServiceBehaviour behaviour) {
            // create service
            Service service = new Service(this, _broker, addr, behaviour, configuration);

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
        public Task<Service> AttachAsync(string addr, ServiceConfiguration configuration, IServiceBehaviour behaviour) {
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
        public Task<Service> AttachAsync(ServiceAddress addr, ServiceType type, ServiceExecution execution, IServiceBehaviour behaviour) {
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
                BrokerMessage msg = null;

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
                await _broker.SendAsync(string.Format("!{0}", addr.Namespace), addr.Name, null, null, new Dictionary<string, object>() {
                    { EventHeader.HEADER_NAME, new EventHeader(EventHeader.HEADER_VERSION, serializer.Name).ToString() }
                }, body, false).ConfigureAwait(false);
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
            return EmitAsync(addr, data);
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

        #region Metrics
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
}
