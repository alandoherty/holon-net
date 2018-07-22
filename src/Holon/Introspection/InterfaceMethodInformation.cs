using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting;
using ProtoBuf;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents an interface method fopr introspection.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class InterfaceMethodInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the method name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets if the method does not provide a reply.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public bool NoReply { get; set; }

        /// <summary>
        /// Gets or sets the available arguments.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public InterfaceArgumentInformation[] Arguments { get; set; }

        /// <summary>
        /// Gets or sets the return type.
        /// Note that this does not include the Task generic.
        /// </summary>
        [ProtoMember(4, IsRequired = true)]
        public string ReturnType { get; set; }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the string representation of the method information.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            string[] args = new string[Arguments.Length];

            for (int i = 0; i < Arguments.Length; i++) {
                args[i] = Arguments[i].ToString();
            }

            return string.Format("Task{0} {1}({2})", ReturnType == "void" ? "" : string.Format("<{0}>", RpcArgument.TypeFromString(ReturnType).Name), Name, string.Join(", ", args));
        }
        #endregion
    }
}
