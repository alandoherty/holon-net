using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents the persistent configuration for a node.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public sealed class NodeConfiguration
    {
        /// <summary>
        /// Gets or sets the application id.
        /// </summary>
        [ProtoMember(1)]
        public string ApplicationId { get; set; } = "";

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        [ProtoMember(2)]
        public string ApplicationVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the UUID.
        /// </summary>
        [ProtoMember(3)]
        public Guid UUID { get; set; }

        /// <summary>
        /// Gets or sets if metrics are enabled.
        /// </summary>
        [ProtoMember(4)]
        public bool Metrics { get; set; } = true;

        /// <summary>
        /// Gets or sets if unhandled service exceptions should be thrown.
        /// </summary>
        [IgnoreDataMember]
        public bool ThrowUnhandledExceptions { get; set; } = false;
    }
}
