using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Holon.Remoting.Introspection
{
    /// <summary>
    /// Represents interface information.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{Name}")]
    [ProtoContract]
    public class InterfaceInformation
    {
        /// <summary>
        /// Gets or sets the interface name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the available operations.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public InterfaceOperationInformation[] Operations { get; set; }

        /// <summary>
        /// Gets the string representation of the interface information.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append("interface ");
            sb.Append(Name);
            sb.AppendLine(" {");

            foreach(InterfaceOperationInformation operation in Operations) {
                sb.Append("\t");
                sb.Append(operation.ToString());
                sb.AppendLine(";");
            }

            sb.Append("}");

            return sb.ToString();
        }
    }
}
