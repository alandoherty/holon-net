using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides trace data for an RPC message.
    /// </summary>
    public sealed class RpcTrace
    {
        private readonly string _version;
        private readonly RpcMessageType _type;

        /// <summary>
        /// Gets the version.
        /// </summary>
        public string Version {
            get {
                return _version;
            }
        }

        /// <summary>
        /// Gets the message type.
        /// </summary>
        public RpcMessageType Type {
            get {
                return _type;
            }
        }

        /// <summary>
        /// Creates a new RPC trace data object.
        /// </summary>
        /// <param name="header">The header.</param>
        internal RpcTrace(RpcHeader header)
        {
            _version = header.Version;
            _type = header.Type;
        }
    }
}
