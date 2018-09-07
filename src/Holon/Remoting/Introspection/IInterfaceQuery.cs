using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting;

namespace Holon.Remoting.Introspection
{
    /// <summary>
    /// Provides functionality to query RPC interface information.
    /// </summary>
    [RpcContract(AllowIntrospection = false)]
    public interface IInterfaceQuery001
    {
        /// <summary>
        /// Gets all interfaces for this service.
        /// </summary>
        /// <returns></returns>
        [RpcOperation]
        Task<string[]> GetInterfaces();

        /// <summary>
        /// Gets information for a single interface on this service.
        /// </summary>
        /// <param name="interface">The interface.</param>
        /// <returns></returns>
        [RpcOperation]
        Task<InterfaceInformation> GetInterfaceInfo(string @interface);

        /// <summary>
        /// Gets if the interface exists.
        /// </summary>
        /// <param name="name">The interface name.</param>
        /// <returns></returns>
        [RpcOperation]
        Task<bool> HasInterface(string name);
    }
}
