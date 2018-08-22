using System;
using System.Collections.Generic;
using System.Text;
using Holon.Services;
using ProtoBuf;

namespace Holon.Introspection
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
    }
}
