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
        private IReplyChannel _channel;
        private Dictionary<string, string> _headers;
        private Guid _id;
        private ServiceAddress _destArr;
        private byte[] _data;
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
        /// Gets the node this message was received on.
        /// </summary>
        public Node Node {
            get {
                return _transport.Node;
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
        /// Gets the data.
        /// </summary>
        public byte[] Data {
            get {
                return _data;
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
            else if (ID == Guid.Empty)
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
            return new MemoryStream(_data);
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
            return encoding.GetString(_data);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new envelope containing a message.
        /// </summary>
        internal Envelope() {
        }
        #endregion
    }
}
