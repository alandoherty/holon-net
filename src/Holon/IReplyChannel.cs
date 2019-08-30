using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Defines an interface for temprorary state-based channels of communication.
    /// </summary>
    public interface IReplyChannel
    { 
        /// <summary>
        /// Replys to an envelope.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        Task ReplyAsync(byte[] body, IDictionary<string, object> headers = null);

        /// <summary>
        /// Gets the reply ID.
        /// </summary>
        Guid ReplyID { get; }

        /// <summary>
        /// Gets if this channel is encrypted and can be used for sensitive communications.
        /// </summary>
        bool IsEncrypted { get; }
    }
}
