using Holon.Metrics.Tracing;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents an outgoing message.
    /// </summary>
    public struct Message
    {
        /// <summary>
        /// Gets or sets the message ID, if null the identifier will be randomly generated.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the body.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        public ServiceAddress Address { get; set; }

        /// <summary>
        /// Gets or sets the trace id.
        /// </summary>
        public string TraceId {
            get {
                if (Headers.TryGetValue(TraceHeader.HeaderName, out string traceId))
                    return traceId;
                else
                    return null;
            } set {
                if (value != null)
                    Headers[TraceHeader.HeaderName] = value;
                else
                    Headers.Remove(TraceHeader.HeaderName);
            }
        }

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        public Message(ServiceAddress address, byte[] body, IDictionary<string, string> headers = null) {
            Address = address;
            Body = body;
            Headers = headers;
            Id = null;
        }

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        public Message(string address, byte[] body, IDictionary<string, string> headers = null)
            : this(new ServiceAddress(address), body, headers) {
        }
    }
}
