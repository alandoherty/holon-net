using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Represents the message body object sent to Lambda when <see cref="MessageFormat.Full"/> is used.
    /// </summary>
    public class MessageBody
    {
        /// <summary>
        /// Gets or sets the body version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the Base64 encoded body.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets the context
        /// </summary>
        public MessageContext Context { get; set; }
    }
}
