using Holon.Events;
using Holon.Transports.Amqp.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Transports.Amqp
{
    /// <summary>
    /// Represents a subscription to an event address for a specific type.
    /// </summary>
    class AmqpEventSubscription : IEventSubscription
    {
        #region Fields
        private Transport _transport;
        private BrokerQueue _queue;
        private int _disposed;
        private EventAddress _address;
        #endregion

        #region Properties
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
            Envelope envelope = null;// new Envelope(message, transport);
            
            // check for header
            if (!envelope.Headers.ContainsKey(AmqpEventHeader.HEADER_NAME))
                throw new InvalidDataException("Invalid event header");

            // read header
            AmqpEventHeader header = new AmqpEventHeader(envelope.Headers[AmqpEventHeader.HEADER_NAME]);

            // validate version
            if (header.Version != "1.1")
                throw new NotSupportedException("Event version is not supported");

            // find serializer
            IEventSerializer serializer = null;

            //if (!EventSerializer.Serializers.TryGetValue(header.Serializer, out serializer))
            //    throw new NotSupportedException("Event serializer not supported");

            // post
            Event e = serializer.DeserializeEvent(envelope.Body);

            return e;
        }

        /// <summary>
        /// Gets the subscription as an observable target.
        /// </summary>
        /// <returns>The observerable.</returns>
        public IObservable<Event> AsObservable() {
            return new EventObservable(this);
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

        /// <summary>
        /// A pass through for observing events.
        /// </summary>
        class EventObservable : IObservable<Event>
        {
            private AmqpEventSubscription _sub;
            private IObservable<InboundMessage> _observable;

            public IDisposable Subscribe(IObserver<Event> observer) {
                return _observable.Subscribe(new EventObserver(_sub, observer));
            }

            /// <summary>
            /// Creates an observable event producer.
            /// </summary>
            /// <param name="sub">The subscription.</param>
            public EventObservable(AmqpEventSubscription sub) {
                _sub = sub;
                _observable = sub._queue.AsObservable();
            }
        }

        class EventObserver : IObserver<InboundMessage>
        {
            private IObserver<Event> _observer;
            private AmqpEventSubscription _sub;

            public EventObserver(AmqpEventSubscription sub, IObserver<Event> observer) {
                _sub = sub;
                _observer = observer;
            }

            public void OnCompleted() {
                _observer.OnCompleted();
            }

            public void OnError(Exception error) {
                _observer.OnError(error);
            }

            public void OnNext(InboundMessage value) {
                _observer.OnNext(_sub.ProcessMessage(value));
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new subscription.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="transport">The transport.</param>
        /// <param name="queue">The queue.</param>
        internal AmqpEventSubscription(EventAddress addr, Transport transport, BrokerQueue queue) {
            _queue = queue;
            _transport = transport;
            _address = addr;
        }
        #endregion
    }
}
