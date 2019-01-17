using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Events;
using System.Threading;
using Holon.Services;
using System.Linq;

namespace Holon.Protocol
{
    /// <summary>
    /// Represents a basic consumer designed for use with asyncronous TPL code.
    /// </summary>
    internal class ObservableConsumer : IBasicConsumer, IObservable<InboundMessage>
    {
        #region Fields
        private IModel _channel;
        private int _cancelled;
        private List<SubscribedObserver> _subscriptions = new List<SubscribedObserver>(1);
        #endregion

        /// <summary>
        /// Gets the underlying channel.
        /// </summary>
        public IModel Model {
            get {
                return _channel;
            }
        }

        #region Events
        /// <summary>
        /// Called when the consumer is cancelled.
        /// </summary>
        public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

        private void HandleCancelled(object sender, ConsumerEventArgs e) {
            if (Interlocked.CompareExchange(ref _cancelled, 1, 0) == 1)
                return;

            // call completed on all observers
            lock (_subscriptions) {
                foreach (SubscribedObserver observer in _subscriptions)
                    observer.Observer.OnCompleted();
            }

            // call event
            ConsumerCancelled?.Invoke(sender, e);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Handles the consumer being cancelled.
        /// </summary>
        /// <param name="consumerTag">The consumer tag.</param>
        public void HandleBasicCancel(string consumerTag) {
            HandleCancelled(this, new ConsumerEventArgs(consumerTag));
        }

        /// <summary>
        /// Handles the consumer being cancelled ok.
        /// </summary>
        /// <param name="consumerTag">The consumer tag.</param>
        public void HandleBasicCancelOk(string consumerTag) {
            HandleCancelled(this, new ConsumerEventArgs(consumerTag));
        }

        /// <summary>
        /// Handles the consumer beginning consuming.
        /// </summary>
        /// <param name="consumerTag">The consumer tag.</param>
        public void HandleBasicConsumeOk(string consumerTag) {
        }

        /// <summary>
        /// Handles the consumer having a message delivered.
        /// </summary>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="deliveryTag">The delivery tag.</param>
        /// <param name="redelivered">If the message is redelivered.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="properties">The properties.</param>
        /// <param name="body">The body.</param>
        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body) {
            lock(_subscriptions) {
                foreach (SubscribedObserver observer in _subscriptions)
                    observer.Observer.OnNext(new InboundMessage(_channel, deliveryTag, redelivered, exchange, routingKey, properties, body));
            }
        }

        /// <summary>
        /// Handles the consumer shutting down.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="reason">The reason event args.</param>
        public void HandleModelShutdown(object model, ShutdownEventArgs reason) {
        }

        /// <summary>
        /// Subscribes to this consumer.
        /// </summary>
        /// <param name="observer">The observer.</param>
        /// <returns></returns>
        public IDisposable Subscribe(IObserver<InboundMessage> observer) {
            lock (_subscriptions) {
                // find the next identification
                int nextId = -1;

                for (int i = 0; i < int.MaxValue; i++) {
                    if (_subscriptions.Any(s => s.ID == i))
                        continue;

                    nextId = i;
                    break;
                }

                // check we got an ID, essentially impossible not to though
                if (nextId == -1)
                    throw new OutOfMemoryException("No more slots available for subscribers");

                // add subscription
                SubscribedObserver subscription = new SubscribedObserver() {
                    ID = nextId,
                    Consumer = this,
                    Observer = observer
                };

                _subscriptions.Add(subscription);

                // return our disposer
                return new SubscribedObserverDisposer(this, subscription.ID);
            }
        }
        #endregion

        #region Structures
        struct SubscribedObserverDisposer : IDisposable
        {
            public int ID { get; set; }
            public ObservableConsumer Consumer { get; set; }

            /// <summary>
            /// Disposes of the subscribed observer.
            /// </summary>
            public void Dispose() {
                lock (Consumer._subscriptions) {
                    int thisId = ID;
                    Consumer._subscriptions.RemoveAll(o => o.ID == thisId);
                }
            }

            public SubscribedObserverDisposer(ObservableConsumer consumer, int id) {
                ID = id;
                Consumer = consumer;
            }
        }

        struct SubscribedObserver
        {
            public int ID { get; set; }
            public IObserver<InboundMessage> Observer { get; set; }
            public ObservableConsumer Consumer { get; set; }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new observable consumer.
        /// </summary>
        /// <param name="channel">The channel.</param>
        public ObservableConsumer(IModel channel) {
            _channel = channel;
        }
        #endregion
    }
}
