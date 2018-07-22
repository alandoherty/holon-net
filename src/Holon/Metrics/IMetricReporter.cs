using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics
{
    /// <summary>
    /// Defines an interface to report metric information.
    /// </summary>
    public interface IMetricReporter
    {
        /// <summary>
        /// Submit the metric data.
        /// </summary>
        /// <param name="metric">The metric data.</param>
        void Submit(IMetric metric);
    }
}
