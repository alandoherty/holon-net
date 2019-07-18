using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Defines the message format used when sending/receiving messages.
    /// </summary>
    public enum MessageFormat
    {
        /// <summary>
        /// The default format, see <see cref="Full"/> for more information.
        /// </summary>
        Default = Full,

        /// <summary>
        /// A full body will be sent, see <see cref="MessageBody"/> for the format. You should use the provided receiver in your Lambda code for simplicitly.
        /// </summary>
        Full = 0,

        /// <summary>
        /// The raw body will be sent, which must be JSON and the client context will contain a <see cref="MessageContext"/> object. This should be used when communicating with non-Holon lambda functions.
        /// </summary>
        RawContext = 1,

        /// <summary>
        /// Only the raw body will be sent, this must be JSON or Amazon will return an error. This should be used when communicating with non-Holon lambda functions.
        /// </summary>
        Raw = 2
    }
}
