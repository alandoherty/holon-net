using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client.Events;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading;
using Holon.Services;

namespace Holon.Protocol
{
    /// <summary>
    /// Represents a basic consumer designed for use with asyncronous TPL code.
    /// </summary>
    public class AsyncConsumer : IBasicConsumer
    {
        #region Fields
        private IModel _channel;
        private int _cancelled;
        private BufferBlock<BrokerMessage> _mailbox = new BufferBlock<BrokerMessage>();
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

            // empty mailbox and complete
            _mailbox.TryReceiveAll(out IList<BrokerMessage> messages);
            _mailbox.Complete();

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
            _mailbox.Post(new BrokerMessage(_channel, deliveryTag, redelivered, exchange, routingKey, properties, body));
        }

        /// <summary>
        /// Handles the consumer shutting down.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="reason">The reason event args.</param>
        public void HandleModelShutdown(object model, ShutdownEventArgs reason) {
        }

        /// <summary>
        /// Receives a message asyncronously from the consumer.
        /// </summary>
        /// <returns>The broker message.</returns>
        public Task<BrokerMessage> ReceiveAsync() {
            return _mailbox.ReceiveAsync();
        }

        /// <summary>
        /// Receives a message asyncronously from the consumer within the timeout.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>The broker message.</returns>
        public Task<BrokerMessage> ReceiveAsync(TimeSpan timeout) {
            return _mailbox.ReceiveAsync(timeout);
        }

        /// <summary>
        /// Receives a message asyncronously from the consumer.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The broker message.</returns>
        public Task<BrokerMessage> ReceiveAsync(CancellationToken cancellationToken) {
            return _mailbox.ReceiveAsync(cancellationToken);
        }

        /// <summary>
        /// Receives a message asyncronously from the consumer.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The broker message.</returns>
        public Task<BrokerMessage> ReceiveAsync(TimeSpan timeout, CancellationToken cancellationToken) {
            return _mailbox.ReceiveAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Creates an observable for this consumer.
        /// </summary>
        /// <returns></returns>
        public IObservable<BrokerMessage> AsObservable() {
            return _mailbox.AsObservable();
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new async consumer.
        /// </summary>
        /// <param name="channel">The channel.</param>
        public AsyncConsumer(IModel channel) {
            _channel = channel;
        }
        #endregion
    }
}
