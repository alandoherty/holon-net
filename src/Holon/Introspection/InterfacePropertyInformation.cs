using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents interface property information.
    /// </summary>
    [Serializable]
    public class InterfacePropertyInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the property name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the return type.
        /// Note that this does not include the Task generic.
        /// </summary>
        public string PropertyType { get; set; }

        /// <summary>
        /// Gets or sets if the property can be read.
        /// </summary>
        public bool IsReadable { get; set; }

        /// <summary>
        /// Gets or sets if the property can be written.
        /// </summary>
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
