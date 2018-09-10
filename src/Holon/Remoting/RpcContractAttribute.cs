using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Defines the target interface as a service contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
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

        /// <summary>
        /// Gets or sets if operations in this contract must communicate on an encrypted channel.
        /// </summary>
        public bool RequireEncryption { get; set; } = false;
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
