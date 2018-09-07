using System;
using System.Collections.Generic;
using System.Text;
using Holon.Services;
using ProtoBuf;

namespace Holon.Remoting.Introspection
{
    /// <summary>
    /// Represents information for a service.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class NodeServiceInformation
    {
        /// <summary>
        /// Gets or sets the address of the service.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the type of the service.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public ServiceType Type { get; set; }

        /// <summary>
        /// Gets or sets the execution type.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public ServiceExecution Execution { get; set; }

        /// <summary>
        /// Gets or sets the number of pending requests.
        /// </summary>
        [ProtoMember(4, IsRequired = true)]
        public int RequestsPending { get; set; }

        /// <summary>
        /// Gets or sets the number of faulted requests.
        /// </summary>
        [ProtoMember(5, IsRequired = true)]
        public int RequestsFaulted { get; set; }

        /// <summary>
        /// Gets or sets the number of completed requests.
        /// </summary>
        [ProtoMember(6, IsRequired = true)]
        public int RequestsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the uptime of the service in seconds.
        /// </summary>
        [ProtoMember(7, IsRequired = true)]
        public long Uptime { get; set; }
    }
}
