using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting;

namespace Holon.Metrics
{
    /// <summary>
    /// Provides functionality to retrieve node metrics.
    /// </summary>
    [RpcContract]
    public interface INodeMetrics001
    {
        /// <summary>
        /// Gets the available metrics.
        /// </summary>
        [RpcOperation]
        Task<MetricInformation[]> Metrics { get; }

        /// <summary>
        /// Gets the history for a metric.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="amount">The amount of datapoints to request.</param>
        /// <note>The amount units is the interval for measurement, currently 1 minute. You will always get the exact amount requested.</note>
        /// <returns></returns>
        [RpcOperation]
        Task<HistoricalMetricInformation> GetHistoricalMetric(string id, int amount=60);
        
        /// <summary>
        /// Gets a single metric.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [RpcOperation]
        Task<MetricInformation> GetMetric(string id);
    }
}
