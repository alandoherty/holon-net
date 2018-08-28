using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Defines the target interface as a service contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class RpcContractAttribute : Attribute
    {
        #region Properties
        /// <summary>
        /// Gets or sets the overriding interface name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets if this operation is visible to introspection.
        /// </summary>
        public bool AllowIntrospection { get; set; } = true;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a rpc service contract attribute.
        /// </summary>
        public RpcContractAttribute() {
        }
        #endregion
    }
}
