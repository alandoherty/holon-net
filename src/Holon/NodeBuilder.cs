using Holon.Services;
using Holon.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Holon
{
    /// <summary>
    /// Provides functionality to create <see cref="Node"/> objects with a fluid API.
    /// </summary>
    public class NodeBuilder
    {
        private List<RoutingRule> _rules = new List<RoutingRule>();
        private List<Transport> _transports = new List<Transport>();
        private NodeConfiguration _configuration = new NodeConfiguration() {
            ApplicationId = "holon-app",
            ApplicationVersion = "1.0.0"
        };

        #region Methods
        /// <summary>
        /// Adds a routing rule for this node.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Rule(RoutingRule rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// Adds a catch all routing rule. There must be exactly one of the transport registered.
        /// </summary>
        /// <typeparam name="TTransport">The transport type.</typeparam>
        /// <returns>The node builder.</returns>
        public NodeBuilder All<TTransport>()
            where TTransport : Transport
        {
            Transport transport = _transports.Single(t => t is TTransport);
            return Rule(new FunctionRule(s => new RoutingResult(transport)));
        }

        /// <summary>
        /// Adds a regex routing rule for the specified transport. There must be exactly one of the transport registered.
        /// </summary>
        /// <typeparam name="TTransport">The transport type.</typeparam>
        /// <param name="regex">The regex.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Rule<TTransport>(Regex regex)
            where TTransport : Transport
        {
            return Rule(new RegexRule(regex, _transports.Single(t => t is TTransport)));
        }

        /// <summary>
        /// Adds a regex routing rule for this node.
        /// </summary>
        /// <param name="regex">The regex</param>
        /// <param name="transport">The transport.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Rule(Regex regex, Transport transport)
        {
            return Rule(new RegexRule(regex, transport));
        }

        /// <summary>
        /// Adds a regex routing rule for this node.
        /// </summary>
        /// <param name="func">The function.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Rule(Func<Address, RoutingResult> func)
        {
            return Rule(new FunctionRule(func));
        }

        /// <summary>
        /// Adds configuration to the node, overrides any previous configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="ArgumentNullException">If the configuration argument is null.</exception>
        /// <returns>The node builder.</returns>
        public NodeBuilder ApplyConfiguration(NodeConfiguration configuration)
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
        public NodeBuilder WithApplicationId(string applicationId)
        {
            _configuration.ApplicationId = applicationId;
            return this;
        }

        /// <summary>
        /// Adds the application version to the configuration.
        /// </summary>
        /// <param name="applicationVersion">The application version.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder WithApplicationVersion(string applicationVersion)
        {
            _configuration.ApplicationVersion = applicationVersion;
            return this;
        }

        /// <summary>
        /// Adds the specified transport to the node, you may add multiple of the same type.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Transport(Transport transport)
        {
            _transports.Add(transport);
            return this;
        }

        /// <summary>
        /// Adds a virtual transport to the node.
        /// </summary>
        /// <param name="configuration">The optional configuration.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddVirtual(Action<NodeBuilder, VirtualTransport> configuration = null)
        {
            VirtualTransport virtualTransport = new VirtualTransport();

            // add configuration if present
            configuration?.Invoke(this, virtualTransport);

            return Transport(virtualTransport);
        }

        /// <summary>
        /// Adds the specified transport to the node, you may add multiple of the same type.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Transport<TTransport>(Transport transport)
            where TTransport : Transport, new()
        {
            if (_transports.Any(t => t is TTransport))
                throw new InvalidOperationException("You can not add two of the same transport via activation");

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
            node._rules = _rules;
            node._transports = _transports;

            // assign transports to the node
            foreach(Transport transport in node._transports)
            {
                if (transport.Node != null)
                    throw new InvalidOperationException("The transport is already attached to another node");

                transport.Node = node;
            }

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
