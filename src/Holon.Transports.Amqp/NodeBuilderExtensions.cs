using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Amqp
{
    /// <summary>
    /// Extends the <see cref="NodeBuilder"/> providing convinence methods to add AMQP support.
    /// </summary>
    public static class NodeBuilderExtensions
    {
        /// <summary>
        /// Adds an AMQP transport to the node.
        /// </summary>
        /// <param name="nodeBuilder">The node builder.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns>The node builder.</returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder, Uri endpoint) {
            return AddAmqp(nodeBuilder, endpoint, null);
        }

        /// <summary>
        /// Adds an AMQP transport to the node.
        /// </summary>
        /// <param name="nodeBuilder">The node builder.</param>
        /// <param name="endpoint">The endpoint.</param>
        /// <param name="name">The name.</param>
        /// <returns>The node builder.</returns>
        public static NodeBuilder AddAmqp(this NodeBuilder nodeBuilder, Uri endpoint, string name) {
            return nodeBuilder.AddTransport(new AmqpTransport(endpoint), name);
        }
    }
}
