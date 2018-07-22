using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting;
using ProtoBuf;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents an interface argument definition.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class InterfaceArgumentInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the interface name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets if the argument is optional.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public bool Optional { get; set; }

        /// <summary>
        /// Gets or sets the argument type.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
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
