using RabbitMQ.Client;
using RabbitMQ.Client.Impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Holon.Transports.Amqp.Protocol
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
        /// Gets if this broker is closed.
        /// </summary>
        public bool IsClosed {
            get {
                return _channel.IsClosed;
            }
        }

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
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public Task SendAsync(params OutboundMessage[] messages) {
            return SendAsync((IEnumerable<OutboundMessage>)messages);
        }

        /// <summary>
        /// Sends many messages to the broker.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <returns></returns>
        public Task SendAsync(IEnumerable<OutboundMessage> messages) {
            // get message array
            OutboundMessage[] messageArr = messages.ToArray();

            if (messageArr.Length == 0)
                return Task.FromResult(true);
            else if (messageArr.Length == 1) {
                return SendAsync(messageArr[0]);
            } else {
                // create batch
                IBasicPublishBatch batch = _channel.CreateBasicPublishBatch();

                // add all the messages
                foreach (OutboundMessage message in messageArr) {
                    IBasicProperties properties = _channel.CreateBasicProperties();

                    if (message.ReplyTo != null)
                        properties.ReplyTo = message.ReplyTo;

                    if (message.Headers != null) {
                        Dictionary<string, object> headers = new Dictionary<string, object>();

                        foreach (var kv in message.Headers)
                            headers[kv.Key] = kv.Value.ToString();

                        properties.Headers = headers;
                    }

                    if (message.ReplyID != null)
                        properties.CorrelationId = message.ReplyID.ToString();

                    batch.Add(message.Exchange, message.RoutingKey, message.Mandatory, properties, message.Body);
                }

                return _ctx.AskWork(delegate () {
                    batch.Publish();
                    return null;
                });
            }
            
        }

        /// <summary>
        /// Sends the message to the provided exchange and routing key.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        public Task SendAsync(OutboundMessage message) {
            IBasicProperties properties = _channel.CreateBasicProperties();

            if (message.ReplyTo != null)
                properties.ReplyTo = message.ReplyTo;

            if (message.Headers != null) {
                Dictionary<string, object> headers = new Dictionary<string, object>();

                foreach (var kv in message.Headers)
                    headers[kv.Key] = kv.Value.ToString();

                properties.Headers = headers;
            }

            if (message.ReplyID != null)
                properties.CorrelationId = message.ReplyID.ToString();

            return _ctx.AskWork(delegate () {
                _channel.BasicPublish(message.Exchange, message.RoutingKey, message.Mandatory, properties, message.Body);
                return null;
            });
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
        public Task SendAsync(string exchange, string routingKey, byte[] body, IDictionary<string, string> headers = null, string replyTo = null, string correlationId = null, bool mandatory = true) {
            return SendAsync(new OutboundMessage(exchange, routingKey, body, headers, replyTo, correlationId, mandatory));
        }

        /// <summary>
        /// Declares an exchange.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="type">The type.</param>
        /// <param name="durable">The durability.</param>
        /// <param name="autoDelete">If to delete when all queues leave.</param>
        /// <returns></returns>
        public void DeclareExchange(string exchange, string type, bool durable, bool autoDelete) {
            _ctx.QueueWork(delegate () {
                _channel.ExchangeDeclare(exchange, "topic", durable, autoDelete);
                return null;
            });
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
        /// <param name="autoAck">If to automatically acknowledge envelopes.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The broker queue.</returns>
        public async Task<BrokerQueue> CreateQueueAsync(string name = "", bool durable = false, bool exclusive = true, string exchange = "", string routingKey = "", bool autoDelete = true, bool autoAck = false, IDictionary<string, object> arguments = null) {
            // declare queue
            QueueDeclareOk ok = await _ctx.AskWork<QueueDeclareOk>(delegate () {
                return _channel.QueueDeclare(name, durable, exclusive, autoDelete, arguments ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase));
            }).ConfigureAwait(false);

            // create consumer
            ObservableConsumer consumer = new ObservableConsumer(this);

            // consume queue
            string consumerTag = null;

            try {
                consumerTag = (string)await _ctx.AskWork(delegate () {
                    return _channel.BasicConsume(name, autoAck, "", false, exclusive, null, consumer);
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
                // call event
                OnShutdown(new BrokerShutdownEventArgs(e.ReplyText));

                // dispose
                Dispose();
            };
        }
        #endregion
    }
}
