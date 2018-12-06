using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting.Serializers;
using Holon.Services;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides functionality to proxy calls for an RPC interface.
    /// </summary>
    /// <typeparam name="IT">The interface.</typeparam>
    public class RpcProxy<IT> : DispatchProxy
    {
        #region Fields
        /// <summary>
        /// The underlying channel.
        /// </summary>
        private IClientChannel _channel;

        /// <summary>
        /// The interface type info.
        /// </summary>
        protected TypeInfo _typeInfo;

        private MethodInfo _invokeMethodInfo;

        private RpcContractAttribute _contractAttr;

        /// <summary>
        /// The configuration.
        /// </summary>
        protected ProxyConfiguration _configuration;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the communication channel.
        /// </summary>
        public IClientChannel Channel {
            get {
                return _channel;
            }set {
                _channel = value;
            }
        }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        public ProxyConfiguration Configuration {
            get {
                return _configuration;
            } internal set {
                _configuration = value;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Handles invokations on the proxy.
        /// </summary>
        /// <param name="targetMethod">The target method.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        protected override object Invoke(MethodInfo targetMethod, object[] args) {
            // get operation attribute
            RpcOperationAttribute attr = targetMethod.GetCustomAttribute<RpcOperationAttribute>();

            if (attr == null)
                throw new InvalidOperationException("The interface member must be decorated with an operation attribute");

            // check encryption requirement
            if ((_contractAttr.RequireEncryption || attr.RequireEncryption) && !_channel.IsEncrypted)
                throw new SecurityException("The contract or operation requires an encrypted channel");

            // generate generic method
            Type genericType = typeof(bool);
            Type memberType = targetMethod.ReturnType;
            TypeInfo memberTypeInfo = memberType.GetTypeInfo();

            //TODO: add support for sync methods
            if (memberType != typeof(Task) && memberTypeInfo.BaseType != typeof(Task))
                throw new InvalidOperationException("The interface member must return an awaitable task");

            if (memberTypeInfo.IsGenericType) {
                if (attr.NoReply)
                    throw new InvalidOperationException("The method result cannot be retrieved with no reply on");

                genericType = memberTypeInfo.GetGenericArguments()[0];
            }

            // invoke
            return _invokeMethodInfo.MakeGenericMethod(genericType).Invoke(this, new object[] { targetMethod, args, targetMethod.ReturnType });
        }

        /// <summary>
        /// Invokes an operation method.
        /// </summary>
        /// <typeparam name="TT">The task return type.</typeparam>
        /// <param name="method">The method.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="returnType">The real return type.</param>
        /// <returns></returns>
        protected virtual async Task<TT> InvokeOperationAsync<TT>(MethodInfo method, object[] args, Type returnType) {
            // build arguments
            Dictionary<string, object> argsPayload = new Dictionary<string, object>();
            ParameterInfo[] argsMethod = method.GetParameters();

            for (int i = 0; i < args.Length; i++) {
                argsPayload[argsMethod[i].Name] = args[i];
            }

            // create request
            RpcRequest req = new RpcRequest(_contractAttr.Name != null ? _contractAttr.Name : _typeInfo.Name, method.Name, argsPayload);

            // serialize
            byte[] requestBody = new ProtobufRpcSerializer().SerializeRequest(req);
            RpcHeader header = new RpcHeader(RpcHeader.HEADER_VERSION, ProtobufRpcSerializer.SerializerName, RpcMessageType.Single);

            // create headers
            IDictionary<string, object> headers = new Dictionary<string, object>() {
                { RpcHeader.HEADER_NAME, header.ToString() },
                { RpcHeader.HEADER_NAME_LEGACY, header.ToString() }
            };

            // ask or send
            if (method.GetCustomAttribute<RpcOperationAttribute>().NoReply) {
                // send operation
                await _channel.SendAsync(new Message() {
                    Body = requestBody,
                    Headers = headers,
                    TraceId = _configuration.TraceId
                });

                return default(TT);
            } else {
                Envelope res = await _channel.AskAsync(new Message() {
                    Body = requestBody,
                    Headers = headers,
                    TraceId = _configuration.TraceId
                }, _configuration.Timeout);

                // transform response
                byte[] responseBody = res.Body;

                // try and get response header
                if (!res.Headers.TryGetValue(RpcHeader.HEADER_NAME, out object resHeaderData))
                    throw new InvalidOperationException("The response envelope is not a valid RPC message");

                RpcHeader resHeader = new RpcHeader(Encoding.UTF8.GetString(resHeaderData as byte[]));

                // deserialize response
                if (!RpcSerializer.Serializers.TryGetValue(resHeader.Serializer, out IRpcSerializer deserializer))
                    throw new NotSupportedException("The response serializer is not supported");

                // deserialize
                RpcResponse resPayload = null;

                if (method.ReturnType == typeof(Task)) {
                    // deserialize
                    resPayload = deserializer.DeserializeResponse(responseBody, typeof(void));

                    // return result
                    if (resPayload.IsSuccess) {
                        return (TT)(object)true;
                    }
                } else {
                    // deserialize
                    Type taskType = method.ReturnType.GetGenericArguments()[0];
                    resPayload = deserializer.DeserializeResponse(responseBody, taskType);

                    // return result
                    if (resPayload.IsSuccess) {
                        return (TT)resPayload.Data;
                    }
                }

                // throw error
                throw new RpcException(resPayload.Error);
            }
        }

        /// <summary>
        /// Gets the string representation of this proxy.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _typeInfo.Name;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC proxy.
        /// </summary>
        public RpcProxy() {
            // get type info
            _typeInfo = typeof(IT).GetTypeInfo();

            // get contract attribute
            _contractAttr = _typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (_contractAttr == null)
                throw new InvalidOperationException("The interface must be decorated with a contract attribute");

            _invokeMethodInfo = GetType().GetTypeInfo().GetMethod(nameof(InvokeOperationAsync), BindingFlags.Instance | BindingFlags.NonPublic);
        }
        #endregion
    }
}
