using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Remoting.Introspection
{
    /// <summary>
    /// Implements INodeQuery001
    /// </summary>
    internal class NodeQueryImpl : INodeQuery001
    {
        private Node _node;

        public Task<NodeInformation> GetInfo() {
            // build tags
            Dictionary<string, string> tags = new Dictionary<string, string>(Node.DefaultTags);

            return Task.FromResult(new NodeInformation() {
                ApplicationId = _node.ApplicationId,
                UUID = _node.UUID,
                ApplicationVersion = _node.ApplicationVersion,
                ServiceCount = _node.ServiceCount,
                Tags = tags.Select((kv) => new NodeTagInformation() { Name = kv.Key, Value = kv.Value }).ToArray()
            });
        }

        public Task<NodeServiceInformation[]> GetServices() {
            return Task.FromResult(_node.Services.Select((s) => new NodeServiceInformation() {
                Address = s.Address.ToString(),
                Type = s.Type,
                Execution = s.Execution,
                Uptime = (long)(DateTimeOffset.UtcNow - s.TimeSetup).TotalSeconds,
                RequestsCompleted = s.RequestsCompleted,
                RequestsFaulted = s.RequestsFaulted,
                RequestsPending = s.RequestsPending
            }).ToArray());
        }

        public NodeQueryImpl(Node node) {
            _node = node;
        }
    }
}
