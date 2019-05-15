using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Provides functionality to create <see cref="Node"/> objects with a fluid API.
    /// </summary>
    public class NodeBuilder
    {
        private List<Transport> _transports = new List<Transport>();
        private NodeConfiguration _configuration = new NodeConfiguration() { };

        #region Methods
        public NodeBuilder AddConfiguration(NodeConfiguration configuration)
        {

        }

        /// <summary>
        /// Adds the specified transport to the node.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <returns></returns>
        public NodeBuilder AddTransport(Transport transport)
        {

        }

        public Node Build()
        {
            return null;
        }
        #endregion
    }
}
