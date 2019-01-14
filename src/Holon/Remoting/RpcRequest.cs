using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents a single RPC request.
    /// </summary>
    public sealed class RpcRequest
    {
        #region Fields
        private string _interface;
        private string _operation;
        private Dictionary<string, object> _arguments;
        private Dictionary<string, Type> _argumentTypes;
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

        /// <summary>
        /// Gets the types of the arguments.
        /// </summary>
        public Dictionary<string, Type> ArgumentTypes {
            get {
                return _argumentTypes;
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
        /// <param name="argumentTypes">The argument types.</param>
        internal RpcRequest(string @interface, string operation, Dictionary<string, object> arguments, Dictionary<string, Type> argumentTypes) {
            _interface = @interface;
            _operation = operation;
            _arguments = arguments;
            _argumentTypes = argumentTypes;
        }

        /// <summary>
        /// Serialization only.
        /// </summary>
        private RpcRequest() { }
        #endregion
    }
}
