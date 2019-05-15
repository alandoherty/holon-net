using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Provides extensions for <see cref="IServiceCollection"/> to add a <see cref="Node"/> dependency.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a <see cref="Node"/> to the service collection.
        /// </summary>
        /// <param name="collection">The service collection.</param>
        /// <param name="builderOptions">A delegate to build options for the node.</param>
        /// <returns></returns>
        public static IServiceCollection AddNode(this IServiceCollection collection, Action<NodeBuilder> builderOptions)
        {
            // create builder
            NodeBuilder builder = new NodeBuilder();

            // configure
            builderOptions(builder);

            // add to service collection
            collection.AddSingleton(builder.Build());
            return collection;
        }
    }
}
