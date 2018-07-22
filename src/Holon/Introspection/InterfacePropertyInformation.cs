using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting;
using ProtoBuf;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents interface property information.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class InterfacePropertyInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the property name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the return type.
        /// Note that this does not include the Task generic.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public string PropertyType { get; set; }

        /// <summary>
        /// Gets or sets if the property can be read.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public bool IsReadable { get; set; }

        /// <summary>
        /// Gets or sets if the property can be written.
        /// </summary>
        [ProtoMember(4, IsRequired = true)]
        public bool IsWriteable { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the string representation of the property information.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("Task{0} {1} {{ {2}{3} }}", PropertyType == "void" ? "" : string.Format("<{0}>", RpcArgument.TypeFromString(PropertyType).Name), Name, IsReadable ? "get; " : "", IsWriteable ? "get; " : "");
        }
        #endregion
    }
}
