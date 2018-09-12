using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Holon.Remoting;
using Holon.Services;

namespace Holon
{  
    /// <summary>
    /// Represents a pass-through channel.
    /// </summary>
    public class BasicChannel : IClientChannel
    {
        #region Fields
        private Node _node;
        private ServiceAddress _address;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the node.
        /// </summary>
        public Node Node {
            get {
                return _node;
            }
        }

        /// <summary>
        /// Gets the service address.
        /// </summary>
        public ServiceAddress ServiceAddress {
            get {
                return _address;
            }
        }

        /// <summary>
        /// Gets if the channel is encrypted.
        /// </summary>
        public bool IsEncrypted {
            get {
                return false;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public IT Proxy<IT>(ProxyConfiguration configuration) {
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
            
            rpcProxy.Channel = this;
            rpcProxy.Configuration = configuration;

            return proxy;
        }

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <returns>The proxied interface.</returns>
        public IT Proxy<IT>() {
            return Proxy<IT>(new ProxyConfiguration() { });
        }

        /// <summary>
        /// Resets the channel.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The address.</param>
        public void Reset(Node node, ServiceAddress address) {
            _node = node;
            _address = address;
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope> AskAsync(byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return _node.AskAsync(_address, body, timeout, headers, cancellationToken);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        public Task SendAsync(byte[] body, IDictionary<string, object> headers = null) {
            return _node.SendAsync(_address, body, headers);
        }

        /// <summary>
        /// Broadcasts the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<Envelope[]> BroadcastAsync(byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken)) {
            return _node.BroadcastAsync(_address, body, timeout, headers, cancellationToken);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a basic channel.
        /// </summary>
        public BasicChannel() { }
        #endregion
    }
}
