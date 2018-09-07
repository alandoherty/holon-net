using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting.Introspection
{
    /// <summary>
    /// Represents a node tag.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class NodeTagInformation
    {
        /// <summary>
        /// Gets or sets the tag name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the tag value.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public string Value { get; set; }
    }
}
