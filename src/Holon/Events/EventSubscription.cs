using Holon.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Holon.Events
{
    /// <summary>
    /// Represents a subscription to an event address for a specific type.
    /// </summary>
    public class EventSubscription : IDisposable
    {
        #region Fields
        private Node _node;
        private BrokerQueue _queue;
        private int _disposed;
        private EventAddress _address;
        #endregion

        #region Fields
        /// <summary>
        /// Gets the event address.
        /// </summary>
        public EventAddress Address {
            get {
                return _address;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Processes an incoming message and returns the event output.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The event.</returns>
        private Event ProcessMessage(InboundMessage message) {
            // read envelope
            Envelope envelope = new Envelope(message, _node);
            
            // check for header
            if (!envelope.Headers.ContainsKey(EventHeader.HEADER_NAME))
                throw new InvalidDataException("Invalid event header");

            // read header
            EventHeader header = new EventHeader(Encoding.UTF8.GetString(envelope.Headers[EventHeader.HEADER_NAME] as byte[]));

            // validate version
            if (header.Version != "1.0")
                throw new NotSupportedException("Event version is not supported");

            // find serializer
            IEventSerializer serializer = null;

            if (!EventSerializer.Serializers.TryGetValue(header.Serializer, out serializer))
                throw new NotSupportedException("Event serializer not supported");

            // post
            Event e = serializer.DeserializeEvent(envelope.Body);

            return e;
        }

        /// <summary>
        /// Receives an event asyncronously.
        /// </summary>
        /// <returns>The event.</returns>
        public async Task<Event> ReceiveAsync() {
            while (true) {
                InboundMessage message = await _queue.ReceiveAsync();

                try {
                    return ProcessMessage(message);
                } catch (Exception) { }
            }
        }

        /// <summary>
        /// Receives an event asyncronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The event.</returns>
        public async Task<Event> ReceiveAsync(CancellationToken cancellationToken) {
            while (true) {
                InboundMessage message = await _queue.ReceiveAsync(cancellationToken);

                try {
                    return ProcessMessage(message);
                } catch (Exception) { }
            }
        }

        /// <summary>
        /// Receives an event asyncronously.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>The event.</returns>
        public async Task<Event> ReceiveAsync(TimeSpan timeout) {
            while (true) {
                InboundMessage message = await _queue.ReceiveAsync(timeout);

                try {
                    return ProcessMessage(message);
                } catch (Exception) { }
            }
        }
        
        /// <summary>
        /// Disposes the underlying queue.
        /// </summary>
        public void Dispose() {
            // lock only one disposal
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            
            // dispose underlying queue
            _queue.Dispose();
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new subscription.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="node">The node.</param>
        /// <param name="queue">The queue.</param>
        internal EventSubscription(EventAddress addr, Node node, BrokerQueue queue) {
            _queue = queue;
            _node = node;
            _address = addr;
        }
        #endregion
    }
}
