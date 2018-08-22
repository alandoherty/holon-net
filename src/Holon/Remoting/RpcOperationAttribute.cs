using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Defines the target method as a valid RPC operation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RpcOperationAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets if this operation is visible to introspection.
        /// </summary>
        public bool AllowIntrospection { get; set; } = true;

        /// <summary>
        /// Gets or sets if no response should be expected.
        /// </summary>
        public bool NoReply { get; set; } = false;
    }
}
