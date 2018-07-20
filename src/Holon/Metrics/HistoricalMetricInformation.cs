using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Represents metric information with history.
    /// </summary>
    public class HistoricalMetricInformation : MetricInformation
    {
        /// <summary>
        /// Gets or sets the history.
        /// </summary>
        public MetricHistoryInformation[] History { get; set; }
    }
}
