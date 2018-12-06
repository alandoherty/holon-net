using Holon.Metrics.Tracing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents the current RPC call context.
    /// </summary>
    public class RpcContext
    {
        #region Fields
        private static AsyncLocal<RpcContext> _current = new AsyncLocal<RpcContext>();
        #endregion

        #region Static
        /// <summary>
        /// Gets the current context, if any.
        /// </summary>
        public static RpcContext Current {
            get {
                return _current.Value;
            }
            internal set {
                _current.Value = value;
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the current envelope.
        /// </summary>
        public Envelope Envelope {
            get; internal set;
        }

        /// <summary>
        /// Gets the current trace ID.
        /// </summary>
        public string TraceId {
            get {
                return Envelope.TraceId;
            }
        }

        /// <summary>
        /// Gets the current node.
        /// </summary>
        public Node Node {
            get {
                return Envelope.Node;
            }
        }
        #endregion

        #region Constructors
        internal RpcContext() { }
        #endregion
    }
}
