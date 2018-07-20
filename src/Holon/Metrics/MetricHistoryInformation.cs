using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Represents a datapoint of metric hsitory.
    /// </summary>
    public class MetricHistoryInformation
    {
        /// <summary>
        /// Gets or sets the timestamp (in seconds).
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public object Value { get; set; }
    }
}
