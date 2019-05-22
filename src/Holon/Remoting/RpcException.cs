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
        private string _details;
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
        /// Gets the details.
        /// </summary>
        public string Details {
            get {
                return _details;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC excpetion with the provided code and message.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="details">The details.</param>
        public RpcException(string code, string message, string details)
            : base(message) {
            _code = code;
            _details = details;
        }

        /// <summary>
        /// Creates a new RPC exception with the RPC error object.
        /// </summary>
        /// <param name="error">The error.</param>
        public RpcException(RpcError error)
            : this(error.Code, error.Message, error.Details) {
        }
        #endregion
    }
}
