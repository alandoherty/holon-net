using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Amqp
{
    /// <summary>
    /// Extends the <see cref="NodeBuilder"/> providing convinence methods to add AMQP support.
    /// </summary>
    public static class NodeBuilderExtensions
    {
        private const string EndpointEnvironmentVariable = "BROKER_ENDPOINT";

        /// <summary>
        /// Adds an AMQP transport to the node using environment variables.
        /// </summary>
        /// <param name="nodeBuilder">The node builder.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The node builder.</returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder, Action<NodeBuilder, AmqpTransport> configuration = null)
        {
            return AddAmqp(nodeBuilder, new Uri(Environment.GetEnvironmentVariable(EndpointEnvironmentVariable)), configuration);
        }

        /// <summary>
        /// Adds an AMQP transport to the node.
        /// </summary>
        /// <param name="nodeBuilder">The node builder.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder, Uri endpoint, Action<NodeBuilder, AmqpTransport> configuration = null)
        {
            AmqpTransport amqpTransport = new AmqpTransport();

            // add configuration if present
            configuration?.Invoke(nodeBuilder, amqpTransport);

            return nodeBuilder.Transport(amqpTransport);
        }
    }
}
