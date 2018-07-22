using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting;

namespace Holon.Metrics
{
    internal class NodeMetricsImpl : INodeMetrics001
    {
        private Node _node;

        public Task<MetricInformation[]> Metrics {
            get {
                return Task.FromResult(_node.Metrics.Select(m => new MetricInformation() {
                    Identifier = m.Identifier,
                    Value = m.Value,
                    Name = m.Name
                }).ToArray());
            }
        }

        public Task<MetricInformation> GetMetric(string id) {
            if (!_node.TryGetMetric(id, out IMetric metric))
                throw new RpcException("NotFound", "The metric was not found");

            return Task.FromResult(new MetricInformation() {
                Name = metric.Name,
                Identifier = metric.Identifier,
                Value = metric.Value
            });
        }

        public NodeMetricsImpl(Node node) {
            _node = node;
        }
    }
}
