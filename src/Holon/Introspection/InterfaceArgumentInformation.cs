using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents an interface argument definition.
    /// </summary>
    [Serializable]
    public class InterfaceArgumentInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the interface name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets if the argument is optional.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Gets or sets the argument type.
        /// </summary>
        public string Type { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Gets this information structure as an RPC argument class.
        /// </summary>
        /// <returns></returns>
        public RpcArgument AsArgument() {
            return new RpcArgument(Name, RpcArgument.TypeFromString(Type), Optional);
        }

        /// <summary>
        /// Gets the string representation of the argument information.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("{0} {1}", RpcArgument.TypeFromString(Type).Name, Name);
        }
        #endregion
    }
}
