using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Security
{
    /// <summary>
    /// Represents the type of secure message.
    /// </summary>
    public enum SecureMessageType
    {
        /// <summary>
        /// Requests the certificate from the secure service.
        /// </summary>
        RequestCertificate,

        /// <summary>
        /// The certificate response.
        /// </summary>
        RespondCertificate,

        /// <summary>
        /// Requests a key from the secure service.
        /// </summary>
        RequestKey,

        /// <summary>
        /// The key response.
        /// </summary>
        RespondKey,

        /// <summary>
        /// A secure protocol error.
        /// </summary>
        Error,

        /// <summary>
        /// The payload contains a secure RPC request message.
        /// </summary>
        RequestMessage,

        /// <summary>
        /// The payload contains a secure RPC response message.
        /// </summary>
        RespondMessage
    }
}
