using Holon.Security;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides secure proxy extensions.
    /// </summary>
    public static class SecureExtensions
    {
        /// <summary>
        /// Gets a secure RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT SecureProxy<IT>(this Node node, ServiceAddress address, SecureChannelConfiguration configuration) {
            // create channel
            SecureClientChannel channel = new SecureClientChannel(configuration);
            channel.Reset(node, address);

            // create proxy
            return channel.Proxy<IT>();
        }

        /// <summary>
        /// Gets a secure RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT SecureProxy<IT>(this Node node, string address, SecureChannelConfiguration configuration) {
            return SecureProxy<IT>(node, new ServiceAddress(address), configuration);
        }
    }
}
