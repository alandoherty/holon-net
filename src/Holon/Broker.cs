using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Holon
{
    /// <summary>
    /// Represents a broker attached to the messaging system.
    /// </summary>
    internal sealed class Broker : IDisposable
    {
        #region Fields
        private bool _disposed;
        private IModel _channel;
        private BrokerContext _ctx;
        private string _appId;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the broker context.
        /// </summary>
        internal BrokerContext Context {
            get {
                return _ctx;
            }
        }

        /// <summary>
        /// Gets the channel.
        /// </summary>
        internal IModel Channel {
            get {
                return _channel;
            }
        }
        #endregion

        #region Events
        public event EventHandler<BrokerShutdownEventArgs> Shutdown;
        public event EventHandler<BrokerReturnedEventArgs> Returned;

        private void OnShutdown(BrokerShutdownEventArgs e) {
            Shutdown?.Invoke(this, e);
        }

        private void OnReturned(BrokerReturnedEventArgs e) {
            Returned?.Invoke(this, e);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Sends the message to the provided exchange and routing key.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="body">The body.</param>
        /// <returns></returns>
        public Task SendAsync(string exchange, string routingKey, byte[] body) {
            return SendAsync(exchange, routingKey, null, null, null, body);
        }

        /// <summary>
        /// Sends the message to the provided exchange and routing key.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="body">The body string.</param>
        /// <returns></returns>
        public Task SendAsync(string exchange, string routingKey, string body) {
            return SendAsync(exchange, routingKey, null, null, null, body);
        }

        /// <summary>
        /// Sends the message to the provided exchange and routing key.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="replyTo">The reply queue, can be null.</param>
        /// <param name="correlationId">The correlation id, can be null.</param>
        /// <param name="headers">The headers, can be null.</param>
        /// <param name="body">The body.</param>
        /// <returns></returns>
        public Task SendAsync(string exchange, string routingKey, string replyTo, string correlationId, IDictionary<string, object> headers, byte[] body) {
            return SendAsync(exchange, routingKey, replyTo, correlationId, headers, body, true);
        }

        /// <summary>
        /// Sends the message to the provided exchange and routing key.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="replyTo">The reply queue, can be null.</param>
        /// <param name="correlationId">The correlation id, can be null.</param>
        /// <param name="headers">The headers, can be null.</param>
        /// <param name="body">The body.</param>
        /// <param name="mandatory">If the message is mandatory.</param>
        /// <returns></returns>
        public Task SendAsync(string exchange, string routingKey, string replyTo, string correlationId, IDictionary<string, object> headers, byte[] body, bool mandatory) {
            IBasicProperties properties = _channel.CreateBasicProperties();

            if (replyTo != null)
                properties.ReplyTo = replyTo;
            if (headers != null)
                properties.Headers = headers;
            if (correlationId != null)
                properties.CorrelationId = correlationId;

            return _ctx.AskWork(delegate () {
                _channel.BasicPublish(exchange, routingKey, mandatory, properties, body);
                return null;
            });
        }

        /// <summary>
        /// Sends the message as a UTF-8 encoded string to the provided exchange and routing key.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="replyTo">The reply queue, can be null.</param>
        /// <param name="correlationId">The correlation id, can be null.</param>
        /// <param name="headers">The headers, can be null.</param>
        /// <param name="body">The body string.</param>
        /// <returns></returns>
        public Task SendAsync(string exchange, string routingKey, string replyTo, string correlationId, IDictionary<string, object> headers, string body) {
            return SendAsync(exchange, routingKey, replyTo, correlationId, headers, Encoding.UTF8.GetBytes(body));
        }

        /// <summary>
        /// Declares an exchange.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="type">The type.</param>
        /// <param name="durable">The durability.</param>
        /// <param name="autoDelete">If to delete when all queues leave.</param>
        /// <returns></returns>
        public Task DeclareExchange(string exchange, string type, bool durable, bool autoDelete) {
            return _ctx.AskWork(delegate () {
                _channel.ExchangeDeclare(exchange, "topic", durable, autoDelete);
                return null;
            });
        }
        
        /// <summary>
        /// Creates a new queue on this broker.
        /// </summary>
        /// <param name="durable">If the queue is durable.</param>
        /// <param name="exclusive">If the queue is exclusive.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <returns>The broker queue.</returns>
        public Task<BrokerQueue> CreateQueueAsync(bool durable, bool exclusive, string exchange, string routingKey) {
            return CreateQueueAsync("", durable, exclusive, exchange, routingKey, true, null);
        }

        /// <summary>
        /// Creates a new queue on this broker.
        /// </summary>
        /// <param name="durable">If the queue is durable.</param>
        /// <param name="exclusive">If the queue is exclusive.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The broker queue.</returns>
        public Task<BrokerQueue> CreateQueueAsync(bool durable, bool exclusive, string exchange, string routingKey, IDictionary<string, object> arguments) {
            return CreateQueueAsync("", durable, exclusive, exchange, routingKey, true, arguments);
        }

        /// <summary>
        /// Creates a new queue on this broker.
        /// </summary>
        /// <param name="name">The queue name.</param>
        /// <param name="durable">If the queue is durable.</param>
        /// <param name="exclusive">If the queue is exclusive.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The broker queue.</returns>
        public Task<BrokerQueue> CreateQueueAsync(string name, bool durable, bool exclusive, string exchange, string routingKey, IDictionary<string, object> arguments) {
            return CreateQueueAsync(name, durable, exclusive, exchange, routingKey, true, arguments);
        }

        /// <summary>
        /// Creates a new queue on this broker.
        /// </summary>
        /// <param name="name">The queue name.</param>
        /// <param name="durable">If the queue is durable.</param>
        /// <param name="exclusive">If the queue is exclusive.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="autoDelete">If to auto delete when all consumers unbind.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The broker queue.</returns>
        public async Task<BrokerQueue> CreateQueueAsync(string name, bool durable, bool exclusive, string exchange, string routingKey, bool autoDelete, IDictionary<string, object> arguments) {
            // declare queue
            QueueDeclareOk ok = await _ctx.AskWork<QueueDeclareOk>(delegate () {
                return _channel.QueueDeclare(name, durable, exclusive, autoDelete, arguments);
            }).ConfigureAwait(false);

            // create consumer
            AsyncConsumer consumer = new AsyncConsumer(_channel);

            // consume queue
            string consumerTag = null;

            try {
                consumerTag = (string)await _ctx.AskWork(delegate () {
                    return _channel.BasicConsume(name, false, "", false, false, null, consumer);
                }).ConfigureAwait(false);
            } catch (Exception) {
                throw;
            }

            // create queue object
            BrokerQueue queue = new BrokerQueue(this, ok.QueueName, consumerTag, consumer);

            // bind to exchange
            if (exchange != "" && routingKey != "") {
                await queue.BindAsync(exchange, routingKey).ConfigureAwait(false);
            }

            return queue;
        }

        /// <summary>
        /// Disposes the broker.
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;

#if DEBUG_DISPOSE
            Debug.WriteLine("< Broker::Dispose");
#endif

            // dispose the channel
            _channel.Dispose();

#if DEBUG_DISPOSE
            Debug.WriteLine("> Broker::Disposed");
#endif
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates the broker.
        /// </summary>
        /// <param name="ctx">The context.</param>
        /// <param name="channel">The channel.</param>
        /// <param name="appId">The application ID.</param>
        internal Broker(BrokerContext ctx, IModel channel, string appId) {
            _ctx = ctx;
            _appId = appId;
            _channel = channel;
            _channel.BasicReturn += delegate (object s, RabbitMQ.Client.Events.BasicReturnEventArgs e) {
                OnReturned(new BrokerReturnedEventArgs(e.BasicProperties, e.Body, e.ReplyText));
            };
            _channel.ModelShutdown += delegate (object s, ShutdownEventArgs e) {
                // dispose
                Dispose();

                // call event
                OnShutdown(new BrokerShutdownEventArgs(e.ReplyText));
            };
        }
        #endregion
    }
}
