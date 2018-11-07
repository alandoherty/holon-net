using Holon.Events;
using Holon.Events.Serializers;
using Holon.Protocol;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Represents a namespace on the node.
    /// </summary>
    internal class Namespace : IDisposable
    {
        #region Fields
        private string _match;
        private Regex _regex;
        private Uri _connectionUri;

        private volatile int _disposed;

        private BrokerContext _brokerContext;
        private Broker _broker;
        
        private BrokerQueue _replyQueue;
        private Dictionary<Guid, ReplyWait> _replyWaits = new Dictionary<Guid, ReplyWait>();

        private Node _node;
        private Task _replyProcessor;
        
        private List<string> _declaredEventNamespaces = new List<string>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the node.
        /// </summary>
        public Node Node {
            get {
                return _node;
            }
        }
        #endregion

        #region Methods
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

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope[]> envelopeWait = WaitManyReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            if (!headers.ContainsKey("x-message-ttl"))
                headers["x-message-ttl"] = timeout.TotalSeconds;

            // send
            await _broker.SendAsync(addr.Namespace, addr.RoutingKey, body, headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }

        /// <summary>
        /// Replys to a message.
        /// </summary>
        /// <param name="replyTo">The reply to address.</param>s
        /// <param name="replyId">The envelope ID.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        internal Task ReplyAsync(string replyTo, Guid replyId, byte[] body, IDictionary<string, object> headers = null) {
            return _broker.SendAsync("", replyTo, body, headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, replyId.ToString());
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public Task SendAsync(IEnumerable<Message> messages) {
            return _broker.SendAsync(messages.Select(m => {
                return new OutboundMessage(m.Address.Namespace, m.Address.RoutingKey, m.Body, m.Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, null);
            }));
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        public Task SendAsync(Message message) {
            return _broker.SendAsync(message.Address.Namespace, message.Address.RoutingKey, message.Body, message.Headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase), null, null);
        }

        /// <summary>
        /// Checks if this namespace matches.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <returns>If the namespace matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Match(string @namespace) {
            return _regex.Match(@namespace).Success;
        }

        /// <summary>
        /// Setup the namespace.
        /// </summary>
        /// <returns></returns>
        public async Task SetupAsync() {
            // create broker context
            _brokerContext = await BrokerContext.CreateAsync(_connectionUri.ToString());

            // create broker
            _broker = await _brokerContext.CreateBrokerAsync(_node.Configuration.ApplicationId);

            // add returned handler
            _broker.Returned += delegate (object s, BrokerReturnedEventArgs e) {
                if (e.ID != Guid.Empty) {
                    TaskCompletionSource<Envelope> tcs;

                    lock (_replyWaits) {
                        // try and get the reply wait
                        if (!_replyWaits.TryGetValue(e.ID, out ReplyWait replyWait))
                            return;

                        // if this is a multi-reply do nothing
                        if (replyWait.Results != null)
                            return;

                        // remove and set exception
                        tcs = replyWait.CompletionSource;
                        _replyWaits.Remove(e.ID);
                    }

                    tcs.TrySetException(new ServiceNotFoundException("The envelope was returned before delivery"));
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

                    // add the reply queue
                    _replyQueue = await _broker.CreateQueueAsync(string.Format("~reply:{1}%{0}", _node.UUID, uniqueIdStr), false, true, "", "", true, true, new Dictionary<string, object>() {
                        { "x-expires", (int)TimeSpan.FromMinutes(15).TotalMilliseconds }
                    }).ConfigureAwait(false);

                    // subscribe to reply queue
                    _replyQueue.AsObservable().Subscribe(new ReplyObserver(this));
                }
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to create node reply queue", ex);
            }
        }

        /// <summary>
        /// Setup the service on this namespace.
        /// </summary>
        /// <param name="service">The service.</param>
        /// <returns></returns>
        public async Task SetupServiceAsync(Service service) {
            await service.SetupAsync(_broker).ConfigureAwait(false);
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
            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope> envelopeWait = WaitReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            headers["x-message-ttl"] = timeout.TotalSeconds;

            // send
            await _broker.SendAsync(addr.Namespace, addr.RoutingKey, body, headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Task<Envelope>[]> AskAsync(Message[] messages, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
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
            await _broker.SendAsync(outboundMessages).ConfigureAwait(false);

            // return the waits for all
            return envelopeWaits;
        }

        /// <summary>
        /// Node worker to process reply messages.
        /// </summary>
        /// <param name="msg">The inbound message.</param>
        private void ReplyProcess(InboundMessage msg) {
            //TODO: cancel on dispose
            Envelope envelope = new Envelope(msg, this);

            // check if we have an correlation
            if (envelope.ID == Guid.Empty) {
                // trigger event
                _node.OnUnroutableReply(new UnroutableReplyEventArgs(envelope));

                return;
            }

            // get completion source for this envelope
            ReplyWait replyWait = default(ReplyWait);
            bool foundReplyWait = false;

            lock (_replyWaits) {
                foundReplyWait = _replyWaits.TryGetValue(envelope.ID, out replyWait);
            }

            if (!foundReplyWait) {
                // log
                Console.WriteLine("unroutable reply: {0}", envelope.ID);

                // trigger event
                _node.OnUnroutableReply(new UnroutableReplyEventArgs(envelope));
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

            lock (_replyWaits) {
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

        /// <summary>
        /// Dispose the namespace.
        /// </summary>
        public void Dispose() {
            // mark disposed
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            // dispose reply queue
            _replyQueue.Dispose();
        }

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

                brokerQueue = await _broker.CreateQueueAsync(string.Format("!{0}%{1}", addr.ToString(), uniqueIdStr), false, true, string.Format("!{0}", addr.Namespace), $"{addr.Resource}.{addr.Name}", true, true).ConfigureAwait(false);
            }

            // create subscription
            return new EventSubscription(addr, this, brokerQueue);
        }
        #endregion

        class ReplyObserver : IObserver<InboundMessage>
        {
            public Namespace Namespace { get; set; }
                
            public void OnCompleted() {
            }

            public void OnError(Exception error) {
            }

            public void OnNext(InboundMessage msg) {
                Namespace.ReplyProcess(msg);
            }

            /// <summary>
            /// Creates a new reply observer for the provided namespace.
            /// </summary>
            /// <param name="namespace">The namespace.</param>
            public ReplyObserver(Namespace @namespace) {
                Namespace = @namespace;
            }
        }

        #region Constructors
        /// <summary>
        /// Creates a new namespace configuration.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="match">The namespace match.</param>
        /// <param name="connectionUri">The connection URI.</param>
        public Namespace(Node node, string match, Uri connectionUri) {
            _node = node;
            _match = match;
            _connectionUri = connectionUri;
            _regex = new Regex(match.Replace("*", ".*"), RegexOptions.Compiled);
        }
        #endregion
    }
}
