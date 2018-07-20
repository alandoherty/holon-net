using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Represents a context for interacting and exposing distributed systems.
    /// </summary>
    public class DistributedContext : IDisposable
    {
        #region Fields
        private BrokerContext _brokerContext;
        private List<Node> _nodes = new List<Node>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the underlying broker context.
        /// </summary>
        internal BrokerContext Context {
            get {
                return _brokerContext;
            }
        }

        /// <summary>
        /// Gets the node in the context.
        /// </summary>
        public Node[] Nodes {
            get {
                lock (_nodes) {
                    return _nodes.ToArray();
                }
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Attaches a new node to the context.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <returns></returns>
        public async Task<Node> AttachAsync(NodeConfiguration config = null) {
            // create
            Node node = new Node(await _brokerContext.CreateBrokerAsync(), config);

            // setup node
            await node.SetupAsync();

            // add to list
            lock (_nodes) {
                _nodes.Add(node);
            }

            return node;
        }

        /// <summary>
        /// Detaches the node from the context.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public Task DetachAsync(Node node) {
            _nodes.Remove(node);
            return Task.FromResult(true);
        }

        /// <summary>
        /// Disposes the context.
        /// </summary>
        public void Dispose() {
#if DEBUG_DISPOSE
            Debug.WriteLine("DistributedContext::Dispose");
#endif

            // get nodes
            Node[] nodesArr = null;

            lock(_nodes) {
                nodesArr = _nodes.ToArray();
            }

            // dispose all nodes
            foreach(Node n in nodesArr) {
                n.Dispose();
            }

            _brokerContext.Dispose();
        }

        /// <summary>
        /// Creates a new distributed context.
        /// </summary>
        /// <param name="endpoint">The broker endpoint.</param>
        /// <returns></returns>
        public static async Task<DistributedContext> CreateAsync(string endpoint) {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint), "The distributed context endpoint cannot be null");

            DistributedContext ctx = new DistributedContext {
                _brokerContext = await BrokerContext.CreateAsync(endpoint)
            };

            return ctx;
        }

        /// <summary>
        /// Creates a new distributed context from environment variables.
        /// </summary>
        /// <returns></returns>
        public static Task<DistributedContext> FromEnvironmentAsync() {
            // get environment
            string brokerEndpoint = Environment.GetEnvironmentVariable("BROKER_ENDPOINT");

            // validate
            if (brokerEndpoint == null)
                throw new InvalidOperationException("The environment is not complete, missing BROKER_ENDPOINT");

            return CreateAsync(brokerEndpoint);
        }
        #endregion

        #region Constructors
        private DistributedContext() {
        }
        #endregion
    }
}
