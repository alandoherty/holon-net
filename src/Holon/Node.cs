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
using Holon.Metrics.Tracing;
using Holon.Remoting;
using Holon.Remoting.Introspection;
using Holon.Services;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Holon
{
    /// <summary>
    /// Represents an application node, usually a single module of your system. This is the primary entry point to send messages or attach services.
    /// </summary>
    public sealed class Node : IDisposable
    {
        #region Fields
        private Guid _uuid;

        private NodeConfiguration _configuration;

        private bool _disposed;
        private string _appId;
        private string _appVersion;
        private List<Service> _services = new List<Service>();

        internal List<Transport> _transports = new List<Transport>();
        internal List<RoutingRule> _rules = new List<RoutingRule>();

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
        /// Called when a trace begins.
        /// </summary>
        public event EventHandler<TraceEventArgs> TraceBegin;
        
        /// <summary>
        /// Called when a trace ends.
        /// </summary>
        public event EventHandler<TraceEventArgs> TraceEnd;

        /// <summary>
        /// </summary>
        /// <param name="e">The event arguments.</param>
        internal void OnUnroutableReply(UnroutableReplyEventArgs e) {
            UnroutableReply?.Invoke(this, e);
        }
        
        /// <summary>
        /// </summary>
        /// <param name="e">The event arguments.</param>
        internal void OnTraceBegin(TraceEventArgs e) {
            TraceBegin?.Invoke(this, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="e">The event arguments.</param>
        internal void OnTraceEnd(TraceEventArgs e) {
            TraceEnd?.Invoke(this, e);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        internal NodeConfiguration Configuration {
            get {
                return _configuration;
            }
        }

        /// <summary>
        /// Gets the underlying introspection service.
        /// </summary>
        public Service QueryService {
            get {
                return _queryService;
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

        /// <summary>
        /// Gets the rules.
        /// </summary>
        public IEnumerable<RoutingRule> Rules {
            get {
                return _rules;
            }
        }

        /// <summary>
        /// Gets all the transports.
        /// </summary>
        public IEnumerable<Transport> Transports {
            get {
                return _transports;
            }
        }
        #endregion

        #region Service Messaging
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
        public Task SendAsync(IEnumerable<Message> messages) {
            // group by namespaces, hopefully they are all same namespace so we get that sweet sweet performance
            var groupedMessages = messages.GroupBy(m => m.Address.Namespace, StringComparer.CurrentCultureIgnoreCase);
            List<Task> groupTasks = new List<Task>(1);

            foreach (IGrouping<string, Message> group in groupedMessages) {
                // get the namespace
                Namespace @namespace = GetNamespace(group.Key);

                groupTasks.Add(@namespace.SendAsync(group));
            }

            return Task.WhenAll(groupTasks);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        public Task SendAsync(Message message) {
            // get namespace
            Namespace @namespace = GetNamespace(message.Address.Namespace);

            return @namespace.SendAsync(message);
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
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // get namespace
            Namespace @namespace = GetNamespace(message.Address.Namespace);

            return @namespace.AskAsync(message, timeout, cancellationToken);
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
        public Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return AskAsync(new Message() {
                Address = addr,
                Body = body,
                Headers = headers
            }, timeout, cancellationToken);
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
            return AskAsync(new Message() {
                Address = new ServiceAddress(addr),
                Body = body,
                Headers = headers
            }, timeout, cancellationToken);
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
        public Task<Envelope[]> BroadcastAsync(ServiceAddress addr, byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return BroadcastAsync(new Message() {
                Address = addr,
                Body = body,
                Headers = headers
            }, timeout, cancellationToken);
        }

        /// <summary>
        /// Broadcasts the message to the provided service address and waits for any responses within the timeout.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope[]> BroadcastAsync(Message message, TimeSpan timeout,CancellationToken cancellationToken = default(CancellationToken)) {
            Namespace @namespace = GetNamespace(message.Address.Namespace);

            return @namespace.BroadcastAsync(message, timeout, cancellationToken);
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
            return BroadcastAsync(new Message() {
                Address = new ServiceAddress(addr),
                Body = body,
                Headers = headers
            }, timeout, cancellationToken);
        }
        #endregion

        #region Other Methods
        class observer : IObserver<Event>
        {
            public void OnCompleted()
            {
                Console.WriteLine("[Event] OnCompleted");
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(Event value)
            {
                Console.WriteLine($"[Event] {value.Address} : {value.Data.ToString()}");
            }
        }

        public async void Wow()
        {
            Transport t = _transports.First();

            var sub = await t.SubscribeAsync(new EventAddress("device:wow.*"));
            sub.AsObservable().Subscribe(new observer());

            await t.EmitAsync(new Event[]
            {
                new Event(new EventAddress("device:wow.w"), new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase), "wow")
            });
        }

        /*
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
        }*/

        /// <summary>
        /// Setup the node, called internally.
        /// </summary>
        /// <returns></returns>
        internal async Task SetupAsync() {
            // setup all namespaces
            //Task[] setupTasks = _namespaces.Select(n => n.SetupAsync()).ToArray();

            //await Task.WhenAll(setupTasks);

            // setup service
            if (_queryService == null) {
                _queryService = await AttachAsync(string.Format("node:{0}", _uuid), ServiceType.Singleton, ServiceExecution.Parallel, RpcBehaviour.Bind<INodeQuery001>(new NodeQueryImpl(this))).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Attaches the service provider to the address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns>The attached service.</returns>
        public async Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {
            // get namespace
            Namespace @namespace = GetNamespace(addr.Namespace);

            // create service
            Service service = new Service(@namespace, addr, behaviour, configuration);

            if (_configuration.ThrowUnhandledExceptions) {
                service.UnhandledException += (o, e) => throw e.Exception;
            }

            // setup service
            await @namespace.SetupServiceAsync(service).ConfigureAwait(false);

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
        /// Detaches a service from the node, waiting for complete cancellation.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <returns></returns>
        public async Task DetachAsync(Service service) {
            // remove service
            lock (_services) {
                if (!_services.Contains(service))
                    return;

                // remove from list
                _services.Remove(service);
            }

            await service.ShutdownAsync().ConfigureAwait(false);
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
            return Proxy<IT>(new ServiceAddress(address), configuration);
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
        private Task<(bool success, int count)> EmitSingularAsync(IEnumerable<Event> events)
        {
            int total = 0;

            // emits events on the assumption they are all in the same transport
            foreach (Event e in events) {
                // increment the total
                total++;

                // determine if we are in multi-transport mode yet
                if (multiTransport) {

                } else {
                    // find a rule which matches this address
                    RoutingResult result = _rules.Select(r => r.Execute(addr))
                        .Where(r => r.Matched)
                        .FirstOrDefault();

                    // if we find a result we can store the transport
                    // if the singularTransport is null we set it and move on
                    // if the singularTransport is not null we verify it's the same, if it's not we have to switch
                    // to multi transport mode
                    if (result.Matched) {
                        if (singularTransport == null) {
                            singularTransport = result.Transport;
                        } else if (singularTransport != null && result.Transport != singularTransport) {
                            multiTransport = new Dictionary<Transport, List<Event>>();
                            multiTransport.
                        }
                    }
                }
            }

        /// <summary>
        /// Emits an event on the provided address, if one of the events cannot be routed the entire operation will fail and no events will be emitted.
        /// If events fail to be sent to their transports the remainder of the events will still be sent, the operation returns the total number of events
        /// which were able to be emitted.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <exception cref="UnroutableException">If the event address is invalid.</exception>
        /// <returns>The numbers of events emitted.</returns>
        public Task<int> EmitAsync(IEnumerable<Event> events) {
            // we store one transport initially, if all events are going to the same transport we can be efficient, otherwise we have a dictionary
            // which is created once we match two seperate transports
            Dictionary<Transport, List<Event>> multiTransport = null;
            int total = 0;

            foreach(Event e in events) {
                // increment the total
                total++;

                // determine if we are in multi-transport mode yet
                if (multiTransport)  {

                } else {
                    // find a rule which matches this address
                    RoutingResult result = _rules.Select(r => r.Execute(addr))
                        .Where(r => r.Matched)
                        .FirstOrDefault();

                    // if we find a result we can store the transport
                    // if the singularTransport is null we set it and move on
                    // if the singularTransport is not null we verify it's the same, if it's not we have to switch
                    // to multi transport mode
                    if (result.Matched) {
                        if (singularTransport == null) {
                            singularTransport = result.Transport;
                        } else if (singularTransport != null && result.Transport != singularTransport) {
                            multiTransport = new Dictionary<Transport, List<Event>>();
                            multiTransport.
                        }
                    }
                }
            }

            

            if (result.Transport == null)
                throw new UnroutableException(addr, "The event could not be routed to the address");

            return result.Transport.EmitAsync();
        }

        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="e">The event.</param>
        /// <returns></returns>
        public Task EmitAsync(Event e) {
            return EmitAsync(new Event[] { e });
        }
        
        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public Task<IEventSubscription> SubscribeAsync(EventAddress addr) {
            // get namespace
            Namespace @namespace = GetNamespace(addr.Namespace);

            return @namespace.SubscribeAsync(addr);
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        public Task<IEventSubscription> SubscribeAsync(string addr) {
            return SubscribeAsync(new EventAddress(addr));
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new node.
        /// </summary>
        /// <param name="configuration">The node configuration.</param>
        internal Node(NodeConfiguration configuration) {
            // check app id format
            if (configuration.ApplicationId.IndexOf('.') + configuration.ApplicationId.IndexOf(' ') != -2)
                throw new FormatException("The node application id cannot contains dots or spaces");

            // apply private members
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
        #region Properties
        /// <summary>
        /// Gets the envelope.
        /// </summary>
        public Envelope Envelope { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new unrouteable reply event arguments.
        /// </summary>
        /// <param name="envelope">The unroutable envelope.</param>
        public UnroutableReplyEventArgs(Envelope envelope) {
            Envelope = envelope;
        }
        #endregion
    }
}