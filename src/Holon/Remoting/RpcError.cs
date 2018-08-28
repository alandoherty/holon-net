using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents an error during an RPC request.
    /// </summary>
    public sealed class RpcError
    {
        #region Fields
        private string _code;
        private string _message;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the error code.
        /// </summary>
        public string Code {
            get {
                return _code;
            }
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message {
            get {
                return _message;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC error with the provided code and message.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        internal RpcError(string code, string message) {
            _code = code;
            _message = message;
        }
        #endregion
    }
}
