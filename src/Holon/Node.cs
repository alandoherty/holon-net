using Holon.Events;
using Holon.Metrics.Tracing;
using Holon.Remoting;
using Holon.Remoting.Introspection;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Represents an application node, usually a single module of your system. This is the primary entry point to send messages or attach services.
    /// </summary>
    public sealed class Node : IDisposable
    {
        #region Fields
        private Guid _uuid;
        private byte[] _uuidBytes;

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
        /// Gets the UUID.
        /// </summary>
        public Guid UUID {
            get {
                return _uuid;
            } private set {
                _uuid = value;
                _uuidBytes = value.ToByteArray();
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
        /// Gets the routing rules.
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
        /// Sends the messages to their services.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns>The number of messages which were successfully sent.</returns>
        public Task<int> SendAsync(params Message[] messages) {
            return SendAsync((IEnumerable<Message>)messages);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public async Task<int> SendAsync(IEnumerable<Message> messages) {
            // build all the messages into the appropriate transports
            Dictionary<Transport, List<Message>> messageRouting = new Dictionary<Transport, List<Message>>();
            Transport lastTransport = null;
            List<Message> lastList = null;
            int total = 0;

            foreach (Message msg in messages) {
                total++;

                // validate message
                if (msg.Address == null)
                    throw new NullReferenceException("The message address cannot be null");

                if (msg.Body == null)
                    throw new NullReferenceException("The message body cannot be null");

                // find a rule which matches this address
                RoutingResult result = _rules.Select(r => r.Execute(msg.Address))
                    .Where(r => r.Matched)
                    .FirstOrDefault();

                if (result.Matched) {
                    lastTransport = result.Transport;

                    // try and find the list for the transport, if we find it add our message to the list
                    // otherwise create a list with the message as the first member
                    if (messageRouting.TryGetValue(result.Transport, out List<Message> list)) {
                        list.Add(msg);
                    } else {
                        lastList = new List<Message>() {
                            { msg }
                        };

                        messageRouting[result.Transport] = lastList;
                    }
                } else {
                    throw new UnroutableException(msg.Address, "The message could not be routed to the address");
                }
            }

            // emit all the messages, if we only have one transport we can make this slightly more efficient
            if (messageRouting.Count == 1) {
                await lastTransport.SendAsync(lastList[0]).ConfigureAwait(false);
                return total;
            } else {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public Task<int> SendAsync(string addr, byte[] body, IDictionary<string, string> headers = null) {
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
        public Task<int> SendAsync(ServiceAddress addr, byte[] body, IDictionary<string, string> headers = null) {
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
            // validate message
            if (message.Address == null)
                throw new NullReferenceException("The message address cannot be null");

            if (message.Body == null)
                throw new NullReferenceException("The message body cannot be null");

            // find a rule which matches this address
            RoutingResult result = _rules.Select(r => r.Execute(message.Address))
                .Where(r => r.Matched)
                .FirstOrDefault();

            if (result.Matched) {
                return result.Transport.AskAsync(message, timeout, cancellationToken);
            } else {
                throw new UnroutableException(message.Address, "The message could not be routed to the address");
            }
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
        public Task<Envelope> AskAsync(ServiceAddress addr, byte[] body, TimeSpan timeout, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
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
        public Task<Envelope> AskAsync(string addr, byte[] body, TimeSpan timeout, IDictionary<string, string> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return AskAsync(new Message() {
                Address = new ServiceAddress(addr),
                Body = body,
                Headers = headers
            }, timeout, cancellationToken);
        }
        #endregion

        #region Other Methods
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
            // find a rule which matches this address
            RoutingResult result = _rules.Select(r => r.Execute(addr))
                .Where(r => r.Matched)
                .FirstOrDefault();

            // check if not matched
            if (!result.Matched)
                throw new UnroutableException(result.TranslatedAddress ?? addr, "The service attachment cannot be routed");

            // attach to transport
            Service service = await result.Transport.AttachAsync(addr, configuration, behaviour)
                .ConfigureAwait(false);

            // add service
            lock(_services) {
                List<Service> services = new List<Service>(_services);
                services.Add(service);
                _services = services;
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
        /// Generates a random message identifier.
        /// </summary>
        /// <returns>A random 40-character message identifier.</returns>
        public string RandomMessageID()
        {
            // create the string builder for the message ID
            StringBuilder sb = new StringBuilder(40);

            // append the first two bytes of the UUID
            sb.Append(BitConverter.ToUInt16(_uuidBytes, 0).ToString("x4"));

            // append the first two bytes of the system tick
            sb.Append(((ushort)Environment.TickCount).ToString("x4"));

            // append 16 random bytes
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                byte[] randomBytes = new byte[16];
                rng.GetBytes(randomBytes);

                sb.Append(BitConverter.ToString(randomBytes).Replace("-", "").ToLower());
            }

            return sb.ToString();
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
        /// <summary>
        /// Emits an event on the provided address, if one of the events cannot be routed the entire operation will fail and no events will be emitted.
        /// If events fail to be sent to their transports the remainder of the events will still be sent, the operation returns the total number of events
        /// which were able to be emitted.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <exception cref="UnroutableException">If the event address cannot be routed to a transport.</exception>
        /// <returns>The numbers of events emitted.</returns>
        public async Task<int> EmitAsync(IEnumerable<Event> events) {
            // build all the events into the appropriate transports
            Dictionary<Transport, List<Event>> eventRouting = null;
            Transport lastTransport = null;
            List<Event> lastList = null;
            int total = 0;

            foreach(Event e in events) {
                total++;

                // find a rule which matches this address
                RoutingResult result = _rules.Select(r => r.Execute(e.Address))
                    .Where(r => r.Matched)
                    .FirstOrDefault();

                if (result.Matched) {
                    lastTransport = result.Transport;

                    // try and find the list for the transport, if we find it add our event to the list
                    // otherwise create a list with the event as the first member
                    if (eventRouting.TryGetValue(result.Transport, out List<Event> list)) {
                        list.Add(e);
                    } else {
                        lastList = new List<Event>() {
                            { e }
                        };

                        eventRouting[result.Transport] = lastList;
                    }
                } else {
                    throw new UnroutableException(e.Address, "The event could not be routed to the address");
                }
            }

            // emit all the events, if we only have one transport we can make this slightly more efficient
            if (eventRouting.Count == 1) {
                await lastTransport.EmitAsync(lastList).ConfigureAwait(false);
                return total;
            } else {
                // create a map between tasks and the lists so we can add up the successful total later
                Dictionary<Task, List<Event>> tasks = new Dictionary<Task, List<Event>>();

                foreach (var kv in eventRouting) {
                    try {
                        tasks[kv.Key.EmitAsync(kv.Value)] = kv.Value;
                    } catch (Exception) { }
                }

                // wait for all
                try {
                    await Task.WhenAll(tasks.Keys).ConfigureAwait(false);
                } catch (Exception) { }

                // add up the total number of successfully emitted events
                return tasks.Where(kv => !kv.Key.IsFaulted)
                    .Sum(kv => kv.Value.Count);
            }
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
        /// <exception cref="UnroutableException">If the event address cannot be routed to a transport.</exception>
        /// <returns>The subscription.</returns>
        public Task<IEventSubscription> SubscribeAsync(EventAddress addr) {
            // find a rule which matches this address
            RoutingResult result = _rules.Select(r => r.Execute(addr))
                .Where(r => r.Matched)
                .FirstOrDefault();

            if (!result.Matched)
                throw new UnroutableException(addr, "The event subscription could not be obtained on the address");

            return result.Transport.SubscribeAsync(addr);
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
            _configuration = configuration;

            // set UUID
            UUID = configuration.UUID == Guid.Empty ? Guid.NewGuid() : configuration.UUID;
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