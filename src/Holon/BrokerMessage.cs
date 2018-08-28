using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents a message received on a broker.
    /// </summary>
    public sealed class BrokerMessage
    {
        #region Fields
        private IModel _channel;
        private byte[] _body;
        private IBasicProperties _properties;
        private ulong _deliveryTag;
        private bool _redelivered;
        private string _exchange;
        private string _routingKey;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the body.
        /// </summary>
        public byte[] Body {
            get {
                return _body;
            }
        }

        /// <summary>
        /// Gets the broker properties.
        /// </summary>
        internal IBasicProperties Properties {
            get {
                return _properties;
            }
        }

        /// <summary>
        /// Gets the delivery tag.
        /// </summary>
        public ulong DeliveryTag {
            get {
                return _deliveryTag;
            }
        }

        /// <summary>
        /// Gets if this message is a redelivery.
        /// </summary>
        public bool Redelivered {
            get {
                return _redelivered;
            }
        }

        /// <summary>
        /// Gets the destination exchange.
        /// </summary>
        public string Exchange {
            get {
                return _exchange;
            }
        }

        /// <summary>
        /// Gets the destination routing key.
        /// </summary>
        public string RoutingKey {
            get {
                return _routingKey;
            }
        }

        /// <summary>
        /// Gets the underlying RabbitMQ channel.
        /// </summary>
        internal IModel Channel {
            get {
                return _channel;
            }
        }
        #endregion

        #region Constructors
        internal BrokerMessage(IModel channel, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body) {
            _channel = channel;
            _deliveryTag = deliveryTag;
            _redelivered = redelivered;
            _exchange = exchange;
            _routingKey = routingKey;
            _properties = properties;
            _body = body;
        }
        #endregion
    }
}