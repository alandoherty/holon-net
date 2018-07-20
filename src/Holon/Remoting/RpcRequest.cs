using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents a single RPC request.
    /// </summary>
    public class RpcRequest
    {
        #region Fields
        private string _interface;
        private string _operation;
        private Dictionary<string, object> _arguments;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the target interface.
        /// </summary>
        public string Interface {
            get {
                return _interface;
            }
        }

        /// <summary>
        /// Gets the target operation.
        /// </summary>
        public string Operation {
            get {
                return _operation;
            }
        }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        public Dictionary<string, object> Arguments {
            get {
                return _arguments;
            }
        }
        #endregion

        #region Methods
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC request.
        /// </summary>
        /// <param name="interface">The target interface.</param>
        /// <param name="operation">The target operation.</param>
        /// <param name="arguments">The arguments.</param>
        internal RpcRequest(string @interface, string operation, Dictionary<string, object> arguments) {
            _interface = @interface;
            _operation = operation;
            _arguments = arguments;
        }

        /// <summary>
        /// Serialization only.
        /// </summary>
        private RpcRequest() { }
        #endregion
    }
}
