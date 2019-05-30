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
        #region Fields
        private List<RoutingRule> _rules = new List<RoutingRule>();
        private List<Transport> _transports = new List<Transport>();
        private NodeConfiguration _configuration = new NodeConfiguration() {
            ApplicationId = "holon-app",
            ApplicationVersion = "1.0.0"
        };
        #endregion

        #region Methods
        /// <summary>
        /// Routes all requests to the specified transport type, there must be exactly one of the specified type.
        /// </summary>
        /// <typeparam name="TTransport">The transport type.</typeparam>
        /// <returns>The node builder.</returns>
        public NodeBuilder RouteAll<TTransport>()
            where TTransport : Transport {
            Transport transport = _transports.Single(t => t is TTransport);
            return Route(new FunctionRule(s => new RoutingResult(transport))); 
        }

        /// <summary>
        /// Routes all requests to the specified transport by name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder RouteAll(string name) {
            Transport transport = _transports.Single(t => t.Name == name);
            return Route(new FunctionRule(s => new RoutingResult(transport)));
        }

        /// <summary>
        /// Routes all requests according to the specified rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder Route(RoutingRule rule)
        {
            _rules.Add(rule);
            return this;
        }

        /// <summary>
        /// Routes requests that match the regex to the specified transport type, there must be exactly one of the specified type.
        /// </summary>
        /// <typeparam name="TTransport">The transport type.</typeparam>
        /// <param name="regex">The regex.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder RouteRegex<TTransport>(Regex regex)
            where TTransport : Transport  {
            return RouteRegex(new RegexRule(regex, _transports.Single(t => t is TTransport)));
        }

        /// <summary>
        /// Routes requests that match the regex to the transport by name.
        /// </summary>
        /// <param name="regex">The regex.</param>
        /// <param name="name">The name.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder RouteRegex(Regex regex, string name) {
            return RouteRegex(new RegexRule(regex, _transports.Single(t => t.Name == name)));
        }

        /// <summary>
        /// Routes requests that match the regex to the specified transport.
        /// </summary>
        /// <param name="regex">The regex</param>
        /// <param name="transport">The transport.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder RouteRegex(Regex regex, Transport transport)
        {
            return RouteRegex(new RegexRule(regex, transport));
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
        public NodeBuilder AddTransport(Transport transport) {
            return AddTransport(transport, null);
        }

        /// <summary>
        /// Adds the specified transport to the node, you may add multiple of the same type.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <param name="name">The name.</param>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddTransport(Transport transport, string name) {
            transport.Name = name;
            _transports.Add(transport);
            return this;
        }

        /// <summary>
        /// Adds a virtual transport to the node.
        /// </summary>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddVirtual() {
            return AddVirtual(null);
        }

        /// <summary>
        /// Adds a virtual transport to the node.
        /// </summary>
        /// <returns>The node builder.</returns>
        public NodeBuilder AddVirtual(string name) {
            return AddTransport(new VirtualTransport(), name);
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
            foreach(Transport transport in node._transports) {
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
