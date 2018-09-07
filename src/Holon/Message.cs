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
        /// Gets or sets the body.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }

        /// <summary>
        /// Gets or sets the address.
        /// </summary>
        public ServiceAddress Address { get; set; }

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        public Message(ServiceAddress address, byte[] body, IDictionary<string, object> headers = null) {
            Address = address;
            Body = body;
            Headers = headers;
        }

        /// <summary>
        /// Creates a new message.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        public Message(string address, byte[] body, IDictionary<string, object> headers = null)
            : this(new ServiceAddress(address), body, headers) {
        }
    }
}
