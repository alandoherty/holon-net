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
        /// <returns>The node builder.</returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder)
        {
            return AddAmqp(nodeBuilder, new Uri(Environment.GetEnvironmentVariable(EndpointEnvironmentVariable)));
        }

        /// <summary>
        /// Adds an AMQP transport to the node.
        /// </summary>
        /// <param name="nodeBuilder">The node builder.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns></returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder, Uri endpoint)
        {
            return nodeBuilder.AddTransport(new AmqpTransport());
        }
    }
}
