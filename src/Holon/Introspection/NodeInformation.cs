using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents information about a remote node.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class NodeInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the application ID.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public string ApplicationVersion { get; set; }

        /// <summary>
        /// Gets or sets the UUID.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public Guid UUID { get; set; }

        /// <summary>
        /// Gets or sets the number of services.
        /// </summary>
        [ProtoMember(4, IsRequired = true)]
        public int ServiceCount { get; set; }

        /// <summary>
        /// Gets or sets the extended tag information.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        public NodeTagInformation[] Tags { get; set; }
        #endregion
    }
}
