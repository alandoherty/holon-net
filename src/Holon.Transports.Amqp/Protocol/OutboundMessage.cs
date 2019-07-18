using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Amqp.Protocol
{
    /// <summary>
    /// Represents a message due for a broker.
    /// </summary>
    internal class OutboundMessage
    {
        #region Fields
        private byte[] _body;
        private string _replyTo;
        private string _replyId;
        private string _exchange;
        private string _routingKey;
        private IDictionary<string, string> _headers;
        private bool _mandatory;
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
        /// Gets the reply to.
        /// </summary>
        public string ReplyTo {
            get {
                return _replyTo;
            }
        }

        /// <summary>
        /// Gets the reply corrleation ID.
        /// </summary>
        public string ReplyID {
            get {
                return _replyId;
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
        /// Gets if the message is mandatory.
        /// </summary>
        public bool Mandatory {
            get {
                return _mandatory;
            }
        }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        public IDictionary<string, string> Headers {
            get {
                return _headers;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new outbound message.
        /// </summary>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="replyTo">The reply queue.</param>
        /// <param name="replyId">The reply correlation ID.</param>
        /// <param name="mandatory">If the message is mandatory.</param>
        internal OutboundMessage(string exchange, string routingKey, byte[] body, IDictionary<string, string> headers = null, string replyTo = null, string replyId = null, bool mandatory = true) {
            _exchange = exchange;
            _routingKey = routingKey;
            _body = body;
            _headers = headers;
            _replyTo = replyTo;
            _replyId = replyId;
            _mandatory = mandatory;
        }
        #endregion
    }
}
