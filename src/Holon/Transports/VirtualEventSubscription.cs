using Holon.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports
{
    class VirtualEventSubscription : IEventSubscription
    {
        #region Fields
        private EventAddress _addr;
        private VirtualTransport _transport;
        private List<EventObserverDisposable> _observers = new List<EventObserverDisposable>();
        #endregion

        #region Properties
        public EventAddress Address {
            get {
                return _addr;
            }
        }
        #endregion

        #region Public Interface
        IObservable<Event> IEventSubscription.AsObservable()
        {
            return new EventObservable(this);
        }

        /// <summary>
        /// Disposes the subscription.
        /// </summary>
        void IDisposable.Dispose()
        {
            foreach (var observer in _observers)
                observer.Observer.OnCompleted();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Post an event to any observers on this subscription.
        /// </summary>
        /// <param name="e">The event.</param>
        internal void Post(Event e)
        {
            foreach (var observer in _observers)
                observer.Observer.OnNext(e);
        }
        #endregion

        #region Observer Plumbing
        class EventObservable : IObservable<Event>
        {
            private VirtualEventSubscription _sub;

            public IDisposable Subscribe(IObserver<Event> observer)
            {
                // create a new observer with the subscription and passed observer target
                EventObserverDisposable eventObserver = new EventObserverDisposable()
                {
                    Subscription = _sub,
                    Observer = observer
                };

                // add to list
                lock(_sub._observers)
                {
                    List<EventObserverDisposable> observers = new List<EventObserverDisposable>(_sub._observers);
                    observers.Add(eventObserver);
                    _sub._observers = observers;
                }

                return eventObserver;
            }

            public EventObservable(VirtualEventSubscription sub)
            {
                _sub = sub;
            }
        }

        struct EventObserverDisposable : IDisposable
        {
            internal IObserver<Event> Observer { get; set; }
            internal VirtualEventSubscription Subscription { get; set; }

            /// <summary>
            /// Removes this from the subscription list of observers.
            /// </summary>
            public void Dispose()
            {
                lock (Subscription._observers)
                {
                    List<EventObserverDisposable> observers = new List<EventObserverDisposable>(Subscription._observers);
                    observers.Remove(this);
                    Subscription._observers = observers;
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Create a new virtual transport event subscription.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <param name="addr">The event address.</param>
        internal VirtualEventSubscription(VirtualTransport transport, EventAddress addr)
        {
            _addr = addr;
            _transport = transport;
        }
        #endregion
    }
}
