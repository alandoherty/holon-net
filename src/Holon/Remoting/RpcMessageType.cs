using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents the type of RPC message.
    /// </summary>
    public enum RpcMessageType
    {
        /// <summary>
        /// A single request.
        /// </summary>
        Single,

        /// <summary>
        /// A batch of multiple requests.
        /// </summary>
        Batch
    }
}
