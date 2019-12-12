using Holon.Events;
using Holon.Services;
using Holon.Transports.Amqp.Protocol;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Transports.Amqp
{
    public class AmqpTransport : Transport
    {
        #region Fields
        private BrokerContext _brokerContext;
        private Broker _broker;

        private BrokerQueue _replyQueue;

        private volatile int _disposed;


        private Dictionary<Guid, ReplyWait> _replyWaits = new Dictionary<Guid, ReplyWait>();
        private Task _replyProcessor;

        private List<string> _declaredEventNamespaces = new List<string>();

        private SemaphoreSlim _setupBrokerSemaphore = new SemaphoreSlim(1, 1);
        #endregion

        #region Properties
        /// <summary>
        /// Gets if this transport supports emitting events.
        /// </summary>
        public override bool CanEmit {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports subscribing to events.
        /// </summary>
        public override bool CanSubscribe {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports sending messages.
        /// </summary>
        public override bool CanSend {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transports supports request/response messages.
        /// </summary>
        public override bool CanAsk {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports attaching services.
        /// </summary>
        public override bool CanAttach {
            get {
                return true;
            }
        }
        #endregion

        #region Transport Methods
        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        protected override async Task BulkSendAsync(IEnumerable<Message> messages) {
            // setup the broker
            if (ShouldSetupBroker())
                await SetupBrokerAsync().ConfigureAwait(false);

            // send the messages
            await _broker.SendAsync(messages.Select(m => {
                return new OutboundMessage(m.Address.Namespace, m.Address.Key, m.Body, m.Headers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase), null, null);
            })).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        protected override async Task SendAsync(Message message) {
            // setup the broker
            if (ShouldSetupBroker())
                await SetupBrokerAsync().ConfigureAwait(false);

            // send the message
            await _broker.SendAsync(message.Address.Namespace, message.Address.Key, message.Body, message.Headers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase), null, null)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected async override Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // setup the broker
            if (ShouldSetupBroker())
                await SetupBrokerAsync().ConfigureAwait(false);

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope> envelopeWait = WaitReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (message.Headers == null)
                message.Headers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            message.Headers["x-message-ttl"] = timeout.TotalSeconds.ToString();

            // send
            await _broker.SendAsync(message.Address.Namespace, message.Address.Key, message.Body, message.Headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <returns>The subscription.</returns>
        protected override async Task<IEventSubscription> SubscribeAsync(EventAddress addr) {
            // create the queue
            _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false);
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
            return new AmqpEventSubscription(addr, this, brokerQueue);
        }

        /// <summary>
        /// Attaches a service to the Amqp transport.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="behaviour">The behaviour.</param>
        /// <returns></returns>
        protected override async Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {
            // setup the broker
            if (ShouldSetupBroker())
                await SetupBrokerAsync().ConfigureAwait(false);

            AmqpService service = new AmqpService(this, addr, behaviour, configuration);

            // setup the service
            await service.SetupAsync(_broker)
                .ConfigureAwait(false);

            return service;
        }
        #endregion

        #region Methods
        /*
        /// <summary>
        /// Broadcasts the message to the provided service address and waits for any responses within the timeout.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope[]> BroadcastAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (timeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentException(nameof(timeout), "The timeout cannot be infinite for a broadcast");

            // setup receive handler
            Guid envelopeId = Guid.NewGuid();
            Task<Envelope[]> envelopeWait = WaitManyReplyAsync(envelopeId, timeout, cancellationToken);

            // add timeout header
            if (message.Headers == null)
                message.Headers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            if (!message.Headers.ContainsKey("x-message-ttl"))
                message.Headers["x-message-ttl"] = timeout.TotalSeconds.ToString();

            // send
            await _broker.SendAsync(message.Address.Namespace, message.Address.Key, message.Body, message.Headers, _replyQueue.Name, envelopeId.ToString()).ConfigureAwait(false);

            // the actual receiver handler is setup since it's syncronous, but now we wait
            return await envelopeWait.ConfigureAwait(false);
        }*/

        /// <summary>
        /// Replys to a message.
        /// </summary>
        /// <param name="replyTo">The reply to address.</param>s
        /// <param name="replyId">The envelope ID.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        internal Task ReplyAsync(string replyTo, Guid replyId, byte[] body, IDictionary<string, string> headers = null)
        {
            return _broker.SendAsync("", replyTo, body, headers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase), null, replyId.ToString());
        }

        /*
        /// <summary>
        /// Sends the message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Task<Envelope>[]> AskAsync(Message[] messages, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            // generate envelope ids
            Guid[] envelopeIDs = new Guid[messages.Length];

            for (int i = 0; i < envelopeIDs.Length; i++)
                envelopeIDs[i] = Guid.NewGuid();

            // generate headers
            IDictionary<string, string>[] envelopeHeaders = new IDictionary<string, string>[messages.Length];

            for (int i = 0; i < envelopeIDs.Length; i++)
            {
                envelopeHeaders[i] = messages[i].Headers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

                if (!envelopeHeaders[i].ContainsKey("x-message-ttl"))
                    envelopeHeaders[i]["x-message-ttl"] = timeout.TotalSeconds.ToString();
            }

            // setup receive handlers
            Task<Envelope>[] envelopeWaits = envelopeIDs.Select(g => WaitReplyAsync(g, timeout, cancellationToken)).ToArray();

            // create messages
            OutboundMessage[] outboundMessages = new OutboundMessage[messages.Length];

            for (int i = 0; i < outboundMessages.Length; i++)
            {
                outboundMessages[i] = new OutboundMessage(messages[i].Address.Namespace, messages[i].Address.Key, messages[i].Body, messages[i].Headers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase), _replyQueue.Name, envelopeIDs[i].ToString());
            }

            // send all messages
            await _broker.SendAsync(outboundMessages).ConfigureAwait(false);

            // return the waits for all
            return envelopeWaits;
        }
        */

        /// <summary>
        /// Node worker to process reply messages.
        /// </summary>
        /// <param name="msg">The inbound message.</param>
        private void ReplyProcess(InboundMessage msg)
        {
            //TODO: cancel on dispose
            Envelope envelope = null;// new Envelope(msg, this);

            // check if we have an correlation
            if (envelope.ID == Guid.Empty)
            {
                // trigger event
                //_node.OnUnroutableReply(new UnroutableReplyEventArgs(envelope));

                return;
            }

            // get completion source for this envelope
            ReplyWait replyWait = default(ReplyWait);
            bool foundReplyWait = false;

            lock (_replyWaits)
            {
                foundReplyWait = _replyWaits.TryGetValue(envelope.ID, out replyWait);
            }

            if (!foundReplyWait)
            {
                // log
                Console.WriteLine("unroutable reply: {0}", envelope.ID);

                // trigger event
                //_node.OnUnroutableReply(new UnroutableReplyEventArgs(envelope));
            }
            else
            {
                // if it's a multi-reply add to results, if not set completion source
                if (replyWait.Results == null)
                {
                    lock (_replyWaits)
                    {
                        _replyWaits.Remove(envelope.ID);
                    }

                    replyWait.CompletionSource.TrySetResult(envelope);
                }
                else
                {
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
        internal async Task<Envelope[]> WaitManyReplyAsync(Guid replyId, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            // create completion source
            TaskCompletionSource<Envelope> tcs = new TaskCompletionSource<Envelope>();
            List<Envelope> results = new List<Envelope>();

            lock (_replyWaits)
            {
                _replyWaits.Add(replyId, new ReplyWait()
                {
                    CompletionSource = new TaskCompletionSource<Envelope>(),
                    Results = results
                });
            }

            // create the timeout and cancellation task
            try
            {
                Task timeoutTask = Task.Delay(timeout);
                Task cancelTask = cancellationToken == CancellationToken.None ? null : Task.FromCanceled(cancellationToken);

                // wait until either the operation times out, is cancelled or finishes
                Task resultTask = cancelTask == null ? await Task.WhenAny(timeoutTask, tcs.Task).ConfigureAwait(false) :
                    await Task.WhenAny(timeoutTask, tcs.Task, cancelTask).ConfigureAwait(false);

                if (resultTask == timeoutTask || resultTask == cancelTask)
                {
                    return results.ToArray();
                }
                else
                {
                    // the operation failed
                    throw tcs.Task.Exception;
                }
            }
            finally
            {
                lock (_replyWaits)
                {
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
        internal async Task<Envelope> WaitReplyAsync(Guid replyId, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            // create completion source
            TaskCompletionSource<Envelope> tcs = new TaskCompletionSource<Envelope>();

            lock (_replyWaits)
            {
                _replyWaits.Add(replyId, new ReplyWait()
                {
                    CompletionSource = tcs
                });
            }

            if (timeout == Timeout.InfiniteTimeSpan)
                return await tcs.Task.ConfigureAwait(false);
            else
            {
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
                else
                {
                    return tcs.Task.Result;
                }
            }
        }

        /// <summary>
        /// Declares the event, creating the namespace and storing the type for future reference.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <note>Currently the only behaviour is to declare the namespace.</note>
        /// <returns></returns>
        /// <exception cref="FormatException">If the event address is invalid.</exception>
        private async Task DeclareEventAsync(EventAddress addr)
        {
            // check if already declared
            lock (_declaredEventNamespaces)
            {
                if (_declaredEventNamespaces.Contains(addr.Namespace))
                    return;
            }

            // declare exchange
            int retries = 3;

            while (retries > 0)
            {
                try
                {
                    _broker.DeclareExchange(string.Format("!{0}", addr.Namespace), "topic", false, false);
                    break;
                }
                catch (Exception)
                {
                    retries--;
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }

            if (retries == 0)
                return;

            // add to list
            lock (_declaredEventNamespaces)
            {
                _declaredEventNamespaces.Add(addr.Namespace);
            }
        }

        /// <summary>
        /// Emits an event.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <returns></returns>
        protected override async Task<int> EmitAsync(IEnumerable<Event> events)
        {
            foreach (Event e in events)
            {
                // check if not declared
                bool declared = false;

                lock (_declaredEventNamespaces)
                {
                    declared = _declaredEventNamespaces.Contains(e.Address.Namespace);
                }

                if (!declared)
                    await DeclareEventAsync(e.Address).ConfigureAwait(false);

                // serialize data payload
                //e.Serialize(data);

                // serialize
                //ProtobufEventSerializer serializer = new ProtobufEventSerializer();
                byte[] body = null;// serializer.SerializeEvent(e);

                // send event
                try
                {
                    await _broker.SendAsync(string.Format("!{0}", e.Address.Namespace), $"{e.Address.Resource}.{e.Address.Name}", body, new Dictionary<string, string>() {
                    { AmqpEventHeader.HEADER_NAME, new AmqpEventHeader(AmqpEventHeader.HEADER_VERSION, "pbuf"/*serializer.Name*/).ToString() }
                }, null, null, false).ConfigureAwait(false);
                }
                catch (Exception) { }
            }

            return events.Count();
        }
        #endregion

        class ReplyObserver : IObserver<InboundMessage>
        {
            //public Namespace Namespace { get; set; }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(InboundMessage msg)
            {
               // Namespace.ReplyProcess(msg);
            }

            /// <summary>
            /// Creates a new reply observer for the provided namespace.
            /// </summary>
            /// <param name="transport">The transport.</param>
            public ReplyObserver(AmqpTransport transport)
            {
                //transport = transport;
            }
        }

        #region Broker Setup
        /// <summary>
        /// Setup a broker.
        /// </summary>
        /// <returns></returns>
        private async Task SetupBrokerAsync() {
            // wait for semaphore
            await _setupBrokerSemaphore.WaitAsync().ConfigureAwait(false);

            try {
                // check if we still need to setup the broker
                if (!ShouldSetupBroker())
                    return;

                // setup the broker
                _broker = await _brokerContext.CreateBrokerAsync(Node.ApplicationId)
                    .ConfigureAwait(false);

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
                        _replyQueue = await _broker.CreateQueueAsync(string.Format("~reply:{1}%{0}", Node.UUID, uniqueIdStr), false, true, "", "", true, true, new Dictionary<string, object>() {
                            { "x-expires", (int)TimeSpan.FromMinutes(15).TotalMilliseconds }
                        }).ConfigureAwait(false);

                        // subscribe to reply queue
                        _replyQueue.AsObservable().Subscribe(new ReplyObserver(this));
                    }
                } catch (Exception ex) {
                    throw new InvalidOperationException("Failed to create node reply queue", ex);
                }
            } finally {
                _setupBrokerSemaphore.Release();
            }
        }

        /// <summary>
        /// Checks if the transport should setup a new broker, you should call <see cref="SetupBrokerAsync"/> which includes it's own thread safety to ensure only
        /// broker is created.
        /// </summary>
        /// <returns></returns>
        public bool ShouldSetupBroker() {
            return _broker == null || _broker.IsClosed;
        }
        #endregion

        /// <summary>
        /// Creates the AMQP transport.
        /// </summary>
        public AmqpTransport(Uri endpoint) {
            _brokerContext = new BrokerContext(endpoint);
        }
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
