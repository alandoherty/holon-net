using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Lambda
{
    /// <summary>
    /// Extends the <see cref="NodeBuilder"/> providing convinence methods to add Lambda support.
    /// </summary>
    public static class NodeBuilderExtensions
    {
        public static NodeBuilder AddLambda(this NodeBuilder nodeBuilder)
        {
            return nodeBuilder.Transport(new LambdaTransport());
        }
    }
}
