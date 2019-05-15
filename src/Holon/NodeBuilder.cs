using Holon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Defines a delegate which selects a transport based off the wildcard.
    /// </summary>
    /// <param name="addr">The address.</param>
    /// <param name="transports">The configured transports.</param>
    /// <param name="transport">The output transport.</param>
    /// <returns>If a transport was found.</returns>
    public delegate bool TransportBinding(ServiceAddress addr, IEnumerable<Transport> transports, out Transport transport);

    /// <summary>
    /// Provides functionality to create <see cref="Node"/> objects with a fluid API.
    /// </summary>
    public class NodeBuilder
    {
        private List<Transport> _transports = new List<Transport>();
        private NodeConfiguration _configuration = new NodeConfiguration() {
            ApplicationId = "holon-app",
            ApplicationVersion = "1.0.0"
        };

        #region Methods
        /// <summary>
        /// Adds a static binding to this node, bindings explain the link between a <see cref="Transport"/> and a namespace.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddBinding(TransportBinding binding)
        {
            return this;
        }

        /// <summary>
        /// Adds configuration to the node, overrides any previous configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="ArgumentNullException">If the configuration argument is null.</exception>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddConfiguration(NodeConfiguration configuration)
        {
            if (_configuration == null)
                throw new ArgumentNullException(nameof(configuration), "The configuration cannot be null");

            _configuration = configuration;
            return this;
        }

        /// <summary>
        /// Adds the application ID to the configuration.
        /// </summary>
        /// <param name="applicationId">The application ID.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddApplicationId(string applicationId)
        {
            _configuration.ApplicationId = applicationId;
            return this;
        }

        /// <summary>
        /// Adds the application version to the configuration.
        /// </summary>
        /// <param name="applicationVersion">The application version.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddApplicationVersion(string applicationVersion)
        {
            _configuration.ApplicationVersion = applicationVersion;
            return this;
        }

        /// <summary>
        /// Adds the specified transport to the node, you may add multiple of the same type.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <returns></returns>
        public NodeBuilder AddTransport(Transport transport)
        {
            _transports.Add(transport);
            return this;
        }

        /// <summary>
        /// Builds a node.
        /// </summary>
        /// <returns></returns>
        public Node Build()
        {
            // create node
            Node node = new Node(_configuration);

            return node;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new node builder.
        /// </summary>
        public NodeBuilder() { }
        #endregion
    }
}
