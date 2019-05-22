using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents the response to a RPC operation.
    /// </summary>
    public sealed class RpcResponse
    {
        #region Fields
        private object _data;
        private Type _dataType;
        private RpcError _error;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the data.
        /// </summary>
        public object Data {
            get {
                return _data;
            }
        }

        /// <summary>
        /// Gets the data type.
        /// </summary>
        public Type DataType {
            get {
                return _dataType;
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
        /// <param name="type">The data type.</param>
        internal RpcResponse(object data, Type type) {
            _data = data;
            _dataType = type;
            _error = null;
        }

        /// <summary>
        /// Creates a new RPC response.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="details">The details.</param>
        internal RpcResponse(string code, string message, string details) {
            _error = new RpcError(code, message, details);
            _data = null;
        }

        /// <summary>
        /// Serialization only.
        /// </summary>
        private RpcResponse() { }
        #endregion
    }
}
