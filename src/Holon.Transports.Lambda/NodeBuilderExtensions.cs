using Amazon.Lambda;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Extends the <see cref="NodeBuilder"/> providing convinence methods to add Lambda support.
    /// </summary>
    public static class NodeBuilderExtensions
    {
        public static NodeBuilder AddLambda(this NodeBuilder nodeBuilder, AmazonLambdaClient client)  {
            return nodeBuilder.AddTransport(new LambdaTransport(client));
        }

        public static NodeBuilder AddLambda(this NodeBuilder nodeBuilder, AmazonLambdaClient client, string name) {
            return nodeBuilder.AddTransport(new LambdaTransport(client), name);
        }
    }
}
