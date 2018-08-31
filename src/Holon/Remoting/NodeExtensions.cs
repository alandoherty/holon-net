using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Holon.Introspection;
using Holon.Metrics;
using Holon.Remoting.Security;
using Holon.Services;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides functionality to generate transparent RPC proxy's.
    /// </summary>
    public static class NodeExtensions
    {
        #region Extension Methods
        /// <summary>
        /// Gets the introspection proxy interface for RPC.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public static IInterfaceQuery001 QueryProxy(this Node node, string address) {
            return QueryProxy(node, new ServiceAddress(address));
        }

        /// <summary>
        /// Gets the introspection proxy interface for RPC.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        public static IInterfaceQuery001 QueryProxy(this Node node, ServiceAddress address) {
            return Proxy<IInterfaceQuery001>(node, address);
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <returns></returns>
        public static T Proxy<T>(this Node node, string address) {
            return Proxy<T>(node, new ServiceAddress(address));
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT Proxy<IT>(this Node node, ServiceAddress address, ProxyConfiguration configuration) {
            // check type is interface
            TypeInfo typeInfo = typeof(IT).GetTypeInfo();

            if (!typeInfo.IsInterface)
                throw new InvalidOperationException("A static RPC proxy must be derived from an interface");

            // get contract attribute
            RpcContractAttribute contractAttr = typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (contractAttr == null)
                throw new InvalidOperationException("A static RPC proxy must be decorated with a contract attribute");

            // create proxy
            IT proxy = DispatchProxy.Create<IT, RpcProxy<IT>>();
            RpcProxy<IT> rpcProxy = (RpcProxy<IT>)(object)proxy;

            rpcProxy.Address = address ?? throw new ArgumentNullException(nameof(address), "The proxy address cannot be null");
            rpcProxy.Node = node;
            rpcProxy.Configuration = configuration;

            return proxy;
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT Proxy<IT>(this Node node, string address, ProxyConfiguration configuration) {
            return Proxy<IT>(node, address, configuration);
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <returns></returns>
        public static IT Proxy<IT>(this Node node, ServiceAddress address) {
            return Proxy<IT>(node, address, new ProxyConfiguration() { });
        }

        /// <summary>
        /// Gets a secure RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT SecureProxy<IT>(this Node node, ServiceAddress address, SecureProxyConfiguration configuration) {
            // check type is interface
            TypeInfo typeInfo = typeof(IT).GetTypeInfo();

            if (!typeInfo.IsInterface)
                throw new InvalidOperationException("A static RPC proxy must be derived from an interface");

            // get contract attribute
            RpcContractAttribute contractAttr = typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (contractAttr == null)
                throw new InvalidOperationException("A static RPC proxy must be decorated with a contract attribute");

            // create proxy
            IT proxy = DispatchProxy.Create<IT, RpcSecureProxy<IT>>();
            RpcSecureProxy<IT> rpcProxy = (RpcSecureProxy<IT>)(object)proxy;

            rpcProxy.Address = address ?? throw new ArgumentNullException(nameof(address), "The proxy address cannot be null");
            rpcProxy.Node = node;
            rpcProxy.Configuration = configuration;

            return proxy;
        }

        /// <summary>
        /// Gets a secure RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IT SecureProxy<IT>(this Node node, string address, SecureProxyConfiguration configuration) {
            return SecureProxy<IT>(node, new ServiceAddress(address), configuration);
        }

        /// <summary>
        /// Gets a RPC proxy for the provided service address using dynamics.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <param name="interface">The interface.</param>
        /// <returns></returns>
        public static dynamic DynamicProxy(this Node node, ServiceAddress address, string @interface) {
            throw new NotImplementedException();
        }
        #endregion
    }
}
