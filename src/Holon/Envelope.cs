using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Holon.Services;
using ProtoBuf;
using System.Threading.Tasks;
using Holon.Metrics.Tracing;

namespace Holon
{
    /// <summary>
    /// Represents a message which has been received.
    /// </summary>
    public sealed class Envelope
    {
        #region Fields
        private Transport _transport;
        private byte[] _body;
        private IReplyChannel _channel;
        private Dictionary<string, string> _headers;
        private Guid _id;
        private ServiceAddress _destArr;
        private object _data;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the target service address.
        /// </summary>
        public ServiceAddress Destination {
            get {
                return _destArr;
            }
        }

        /// <summary>
        /// Gets the message headers.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers {
            get {
                return _headers ?? new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Gets the namespace the messaged was received on.
        /// </summary>
        internal Transport Transport {
            get {
                return _transport;
            }
        }

        /// <summary>
        /// Gets the envelope ID, if any.
        /// </summary>
        public Guid ID {
            get {
                return _id;
            }
        }

        /// <summary>
        /// Gets or sets the trace id.
        /// </summary>
        public string TraceId {
            get {
                if (Headers.TryGetValue(TraceHeader.HeaderName, out string traceId))
                    return traceId;
                else
                    return null;
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
                // check if we should use the raw body
                if (_body == null)
                    return _msg.Body;

                // we have an altered body
                return _body;
            } set {
                _body = value;
            }
        }

        /// <summary>
        /// Gets or sets the reply channel.
        /// </summary>
        public IReplyChannel Channel {
            get {
                return _channel;
            } set {
                _channel = value;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Replies to this envelope.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public Task ReplyAsync(byte[] body, IDictionary<string, object> headers = null) {
            // validate arguments
            if (body == null)
                throw new ArgumentNullException(nameof(body), "The body cannot be null");
            else if (ID == Guid.Empty || ReplyTo == null)
                throw new InvalidOperationException("The envelope does not have sufficient reply information");

            if (_channel != null)
                return _channel.ReplyAsync(body, headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase));
            else
                throw new NotImplementedException();
            //return _namespace.ReplyAsync(ReplyTo, ID, body, headers ?? new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// Deserializes the body as a protobuf contract.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The deserialized object.</returns>
        public T AsProtoBuf<T>() {
            using (Stream bodyStream = AsStream()) {
                return Serializer.Deserialize<T>(bodyStream);
            }
        }

        /// <summary>
        /// Creates a stream from the message body.
        /// </summary>
        /// <returns></returns>
        public Stream AsStream() {
            return new MemoryStream(Body);
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
        /// <param name="namespace">The namespace.</param>
        internal Envelope(InboundMessage msg, Namespace @namespace) {
            _msg = msg;
            _namespace = @namespace;
        }
        #endregion
    }
}
