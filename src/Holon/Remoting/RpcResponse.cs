using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents the response to a RPC operation.
    /// </summary>
    public class RpcResponse
    {
        #region Fields
        private object _data;
        private RpcError _error;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public object Data {
            get {
                return _data;
            }
        }

        /// <summary>
        /// Gets the error, if any.
        /// </summary>
        public RpcError Error {
            get {
                return _error;
            }
        }

        /// <summary>
        /// Gets if the response is successful.
        /// </summary>
        public bool IsSuccess {
            get {
                return _error == null;
            }
        }
        #endregion


        #region Constructors
        /// <summary>
        /// Creates a new RPC response.
        /// </summary>
        /// <param name="data">The provided data.</param>
        internal RpcResponse(object data) {
            _data = data;
            _error = null;
        }

        /// <summary>
        /// Creates a new RPC response.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        internal RpcResponse(string code, string message) {
            _error = new RpcError(code, message);
            _data = null;
        }

        /// <summary>
        /// Serialization only.
        /// </summary>
        private RpcResponse() { }
        #endregion
    }
}
