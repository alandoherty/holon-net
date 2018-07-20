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

        public Task<HistoricalMetricInformation> GetHistoricalMetric(string id, int amount = 60) {
            if (!_node.TryGetMetric(id, out IMetric metric))
                throw new RpcException("NotFound", "The metric was not found");

            // create array for all history requested
            MetricHistoryInformation[] history = new MetricHistoryInformation[amount];

            // try and get as much as possible
            IMetricValue[] values = metric.GetHistory(amount);
            DateTime timeOffset = DateTime.UtcNow;

            // update time offset if we have some data
            if (values.Length > 0)
                timeOffset = values[0].DateTime;

            // fill in empty values with 1 minute timestamps
            for (int i = 0; i < history.Length; i++) {
                history[i] = new MetricHistoryInformation() {
                    Timestamp = ((DateTimeOffset)(timeOffset + (TimeSpan.FromMinutes(i)))).ToUnixTimeSeconds(),
                    Value = metric.DefaultValue
                };
            }

            // copy in data we do have
            for (int i = 0; i < values.Length; i++) {
                history[i].Timestamp = ((DateTimeOffset)values[i].DateTime).ToUnixTimeSeconds();
                history[i].Value = values[i].Value;
            }

            return Task.FromResult(new HistoricalMetricInformation() {
                Identifier = metric.Identifier,
                Name = metric.Name,
                Value = metric.Value,
                History = history
            });
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
