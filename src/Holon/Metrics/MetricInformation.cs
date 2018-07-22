using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Represents a trackable metric.
    /// </summary>
    [Serializable]
    [ProtoContract]
    public class MetricInformation
    {
        /// <summary>
        /// Gets or sets the metric name.
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        [ProtoMember(2, IsRequired = true)]
        public string Identifier { get; set; }

        /// <summary>
        /// Gets or sets the current/last known value.
        /// </summary>
        [ProtoMember(3, IsRequired = true)]
        public double Value { get; set; }
    }
}
