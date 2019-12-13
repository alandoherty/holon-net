using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Amqp.Protocol
{
    /// <summary>
    /// Provides data for the Broker.Returned event.
    /// </summary>
    public class BrokerReturnedEventArgs : EventArgs
    {
        #region Fields
        private IBasicProperties _properties;
        private byte[] _body;
        private string _message;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the envelope headers.
        /// </summary>
        public IDictionary<string, object> Headers {
            get {
                return !_properties.IsHeadersPresent() ? new Dictionary<string, object>() : _properties.Headers;
            }
        }

        /// <summary>
        /// Gets the envelope ID.
        /// </summary>
        public string ID {
            get {
                if (_properties.IsCorrelationIdPresent()) {
                    return _properties.CorrelationId;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the returned envelope body.
        /// </summary>
        public byte[] Body {
            get {
                return _body;
            }
        }

        /// <summary>
        /// Gets the message for the return reason.
        /// </summary>
        public string Message {
            get {
                return _message;
            }
        }
        #endregion

        #region Constructors
        internal BrokerReturnedEventArgs(IBasicProperties properties, byte[] body, string message) {
            _properties = properties;
            _message = message;
            _body = body;
        }
        #endregion
    }
}
