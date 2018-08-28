using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Represents a self-consumed queue.
    /// </summary>
    internal sealed class BrokerQueue : IDisposable
    {
        #region Fields
        private string _queue;
        private Broker _broker;
        private bool _disposed;
        private AsyncConsumer _consumer;
        private string _consumerTag;
        private bool _autoAck;
        private List<string> _exchanges = new List<string>();
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
        /// Receives a message asyncronously.
        /// </summary>
        /// <returns>The message.</returns>
        public Task<BrokerMessage> ReceiveAsync() {
            return ReceiveAsync(CancellationToken.None);
        }

        /// <summary>
        /// Receives a message asyncronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<BrokerMessage> ReceiveAsync(CancellationToken cancellationToken) {
            // create consumer
            if (_consumer == null) {
                _consumer = new AsyncConsumer(_broker.Channel);

                // consume queue
                try {
                    _consumerTag = (string)await _broker.Context.AskWork(delegate () {
                        return _broker.Channel.BasicConsume(_queue, false, "", false, false, null, _consumer);
                    }).ConfigureAwait(false);
                } catch(Exception) {
                    _consumer = null;
                    throw;
                }
            }
            
            return await _consumer.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes 
        /// </summary>
        public void Dispose() {
            // prevent disposing multiple times
            if (_disposed)
                return;
            _disposed = true;

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
        public BrokerQueue(Broker broker, string queue) {
            _broker = broker;
            _queue = queue;
        }
        #endregion
    }
}
