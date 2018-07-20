using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents an error occuring during RPC invocation.
    /// </summary>
    public class RpcException : Exception
    {
        #region Fields
        private string _code;
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
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC excpetion with the provided code and message.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        public RpcException(string code, string message)
            : base(message) {
            _code = code;
        }

        /// <summary>
        /// Creates a new RPC exception with the provided code, message and inner exception.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public RpcException(string code, string message, Exception innerException)
            : base(message, innerException) {
            _code = code;
        }

        /// <summary>
        /// Creates a new RPC exception with the RPC error object.
        /// </summary>
        /// <param name="error">The error.</param>
        public RpcException(RpcError error)
            : this(error.Code, error.Message) {
        }

        /// <summary>
        /// Creates a new RPC exception with the RPC error object and inner exception.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="innerException">The inner exception.</param>
        public RpcException(RpcError error, Exception innerException)
          : this(error.Code, error.Message, innerException) {
        }
        #endregion
    }
}
