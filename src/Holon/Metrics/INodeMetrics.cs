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
        /// Gets a single metric.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        [RpcOperation]
        Task<MetricInformation> GetMetric(string id);
    }
}
