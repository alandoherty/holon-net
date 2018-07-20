using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Represents a trackable metric.
    /// </summary>
    public class MetricInformation
    {
        /// <summary>
        /// Gets or sets the metric name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Gets or sets the current/last known value.
        /// </summary>
        public object Value { get; set; }
    }
}
