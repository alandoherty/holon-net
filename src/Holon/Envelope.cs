using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Holon.Services;

namespace Holon
{
    /// <summary>
    /// Represents the envelope for a received message.
    /// </summary>
    public class Envelope
    {
        #region Fields
        private Node _node;
        private BrokerMessage _msg;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the contents of the envelope.
        /// </summary>
        internal BrokerMessage Message {
            get {
                return _msg;
            }
        }

        /// <summary>
        /// Gets the message headers.
        /// </summary>
        public IDictionary<string, object> Headers {
            get {
                return _msg.Properties.Headers == null ? new Dictionary<string, object>() : _msg.Properties.Headers;
            }
        }

        /// <summary>
        /// Gets the node the messaged was received on.
        /// </summary>
        public Node Node {
            get {
                return _node;
            }
        }

        /// <summary>
        /// Gets the service address the envelope was received on.
        /// </summary>
        public ServiceAddress Service {
            get {
                return new ServiceAddress(string.Format("{0}:{1}", _msg.Exchange, _msg.RoutingKey));
            }
        }

        /// <summary>
        /// Gets the envelope ID, if any.
        /// </summary>
        public Guid ID {
            get {
                if (_msg.Properties.IsCorrelationIdPresent()) {
                    if (Guid.TryParse(_msg.Properties.CorrelationId, out Guid envelopeId))
                        return envelopeId;
                    else
                        return Guid.Empty;
                } else
                    return Guid.Empty;
            }
        }

        /// <summary>
        /// Gets the reply to address.
        /// </summary>
        public string ReplyTo {
            get {
                if (_msg.Properties.IsReplyToPresent())
                    return _msg.Properties.ReplyTo;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets the envelope payload.
        /// </summary>
        public byte[] Body {
            get {
                return _msg.Body;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a stream from the message body.
        /// </summary>
        /// <returns></returns>
        public Stream AsStream() {
            return new MemoryStream(_msg.Body);
        }

        /// <summary>
        /// Deserializes the body as an XML object.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The deserialized object.</returns>
        public T AsXml<T>() {
            using (Stream bodyStream = AsStream()) {
                return (T)new XmlSerializer(typeof(T)).Deserialize(bodyStream);
            }
        }

        /// <summary>
        /// Deserializes the body as a UTF-8 string.
        /// </summary>
        /// <returns>The body string.</returns>
        public string AsString() {
            return AsString(Encoding.UTF8);
        }

        /// <summary>
        /// Deserializes the body as a string.
        /// </summary>
        /// <param name="encoding">The string encoding.</param>
        /// <returns>The body string.</returns>
        public string AsString(Encoding encoding) {
            return encoding.GetString(Body);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new envelope containing a message.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="node">The receiving node.</param>
        internal Envelope(BrokerMessage msg, Node node) {
            _msg = msg;
            _node = node;
        }
        #endregion
    }
}
