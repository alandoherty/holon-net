using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting;

namespace Holon.Introspection
{
    /// <summary>
    /// Provides functionality to query node information.
    /// </summary>
    [RpcContract]
    public interface INodeQuery001
    {
        /// <summary>
        /// Gets the node information.
        /// </summary>
        /// <returns></returns>
        [RpcOperation]
        Task<NodeInformation> GetInfo();

        /// <summary>
        /// Gets the services on the node.
        /// </summary>
        /// <returns></returns>
        [RpcOperation]
        Task<NodeServiceInformation[]> GetServices();
    }
}
