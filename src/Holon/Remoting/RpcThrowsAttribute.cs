using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Describes the target operation as capable of throwing the provided error code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RpcThrowsAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Creates a new RPC throws attribute.
        /// </summary>
        public RpcThrowsAttribute() {
        }

        /// <summary>
        /// Creates a new RPC throws attribute.
        /// </summary>
        /// <param name="code">The code.</param>
        public RpcThrowsAttribute(string code) {
            Error = code;
        }
    }
}
