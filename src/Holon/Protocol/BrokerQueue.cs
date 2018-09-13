using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Protocol
{
    /// <summary>
    /// Represents a self-consumed queue.
    /// </summary>
    internal sealed class BrokerQueue : IDisposable
    {
        #region Fields
        private string _queue;
        private Broker _broker;
        private AsyncConsumer _consumer;
        private string _consumerTag;
        private List<string> _exchanges = new List<string>();

        private int _disposed;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the queue name.
        /// </summary>
        public string Name {
            get {
                return _queue;
            }
        }
        
        /// <summary>
        /// Gets the exchanges this queue is bound to.
        /// </summary>
        public string[] Exchanges {
            get {
                lock (_exchanges)
                    return _exchanges.ToArray();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the subscription as an observable target.
        /// </summary>
        /// <returns></returns>
        public IObservable<InboundMessage> AsObservable() {
            return _consumer.AsObservable();
        }

        /// <summary>
        /// Binds to the exchange.
        /// </summary>
        /// <returns></returns>
        public async Task BindAsync(string exchange, string routingKey) {
            try {
                await _broker.Context.AskWork(delegate () {
                    _broker.Channel.QueueBind(_queue, exchange, routingKey);
                    return null;
                }).ConfigureAwait(false);
            } catch (Exception ex) {
                // try and clean up the queue first
                await _broker.Context.AskWork<QueueDeclareOk>(delegate () {
                    return _broker.Channel.QueueDelete(_queue, true, true);
                }).ConfigureAwait(false);

                // rethrow
                throw new InvalidOperationException("Failed to bind queue to exchange", ex);
            }

            // add to exchange list
            lock (_exchanges) {
                if (!_exchanges.Contains(exchange))
                    _exchanges.Add(exchange);
            }
        }

        /// <summary>
        /// Unbinds from an exchange.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        public void Unbind(string exchange, string routingKey) {
            _broker.Context.QueueWork(delegate () {
                _broker.Channel.QueueUnbind(_queue, exchange, routingKey);
                return null;
            });
        }

        /// <summary>
        /// Receives a message asyncronously.
        /// </summary>
        /// <returns>The message.</returns>
        public Task<InboundMessage> ReceiveAsync() {
            return ReceiveAsync(CancellationToken.None);
        }

        /// <summary>
        /// Receives a message asyncronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<InboundMessage> ReceiveAsync(CancellationToken cancellationToken) {
            return await _consumer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Receives a message asyncronously.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public async Task<InboundMessage> ReceiveAsync(TimeSpan timeout) {
            return await _consumer.ReceiveAsync(timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes 
        /// </summary>
        public void Dispose() {
            // prevent disposing multiple times
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            // delete consumer
            if (_consumer != null) {
                _broker.Context.QueueWork(() => {
                    _broker.Channel.BasicCancel(_consumerTag);
                    return null;
                });
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new broker queue.
        /// </summary>
        /// <param name="broker">The broker.</param>
        /// <param name="queue">The queue name.</param>
        /// <param name="consumerTag">The consumer tag.</param>
        /// <param name="consumer">The consumer.</param>
        internal BrokerQueue(Broker broker, string queue, string consumerTag, AsyncConsumer consumer) {
            _broker = broker;
            _queue = queue;
            _consumer = consumer;
            _consumerTag = consumerTag;
        }
        #endregion
    }
}
