using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Holon.Introspection;
using Holon.Metrics;
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
            return GetRpcQuery(node, new ServiceAddress(address));
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
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="address">The service address.</param>
        /// <returns></returns>
        public static T Proxy<T>(this Node node, ServiceAddress address) {
            // check type is interface
            TypeInfo typeInfo = typeof(T).GetTypeInfo();

            if (!typeInfo.IsInterface)
                throw new InvalidOperationException("A static RPC proxy must be derived from an interface");

            // get contract attribute
            RpcContractAttribute contractAttr = typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (contractAttr == null)
                throw new InvalidOperationException("A static RPC proxy must be decorated with a contract attribute");
            
            // create proxy
            T proxy = DispatchProxy.Create<T, RpcProxy<T>>();
            RpcProxy<T> rpcProxy = (RpcProxy<T>)(object)proxy;

            rpcProxy.Address = address;
            rpcProxy.Node = node;
            rpcProxy.Timeout = TimeSpan.FromMinutes(1);

            return proxy;
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
