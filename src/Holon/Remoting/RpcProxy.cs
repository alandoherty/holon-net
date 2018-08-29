using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Holon.Remoting.Serializers;
using Holon.Services;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides functionality to proxy calls for an RPC interface.
    /// </summary>
    /// <typeparam name="T">The interface.</typeparam>
    public class RpcProxy<T> : DispatchProxy
    {
        #region Fields
        private Node _node;
        private ServiceAddress _addr;
        private TypeInfo _typeInfo;
        private MethodInfo _invokeMethodInfo;
        private MethodInfo _invokePropertyInfo;

        private RpcContractAttribute _contractAttr;

        private ProxyConfiguration _configuration;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the target node.
        /// </summary>
        public Node Node {
            get {
                return _node;
            }set {
                _node = value;
            }
        }

        /// <summary>
        /// Gets or sets the target service address.
        /// </summary>
        public ServiceAddress Address {
            get {
                return _addr;
            } set {
                _addr = value;
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
            // determine if this is a propety invocation, we'll need to obtain the real property data if so
            PropertyInfo targetProperty = null;
            MemberInfo targetMember = targetMethod;

            if (targetMethod.IsSpecialName) {
                if (targetMethod.Name.StartsWith("get_")) {
                    targetProperty = _typeInfo.GetProperty(targetMethod.Name.Substring(4));
                    targetMember = targetProperty;
                } else if (targetMethod.Name.StartsWith("set_")) {
                    throw new NotSupportedException("The property does not support writing");
                }
            }

            // get operation attribute
            RpcOperationAttribute attr = targetMember.GetCustomAttribute<RpcOperationAttribute>();

            if (attr == null)
                throw new InvalidOperationException("The interface member must be decorated with an operation attribute");

            if (attr.NoReply && targetProperty != null)
                throw new InvalidOperationException("The property value cannot be retrieved with no reply on");

            // generate generic method
            Type genericType = typeof(bool);
            Type memberType = targetProperty == null ? targetMethod.ReturnType : targetProperty.PropertyType;
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
            if (targetProperty != null) {
                return _invokePropertyInfo.MakeGenericMethod(genericType).Invoke(this, new object[] { targetProperty, args.Length == 0 ? null : args[0] });
            } else {
                return _invokeMethodInfo.MakeGenericMethod(genericType).Invoke(this, new object[] { targetMethod, args, targetMethod.ReturnType });
            }
        }

        /// <summary>
        /// Invokes a method.
        /// </summary>
        /// <typeparam name="TT">The task return type.</typeparam>
        /// <param name="method">The method.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="returnType">The real return type.</param>
        /// <returns></returns>
        public async Task<TT> InvokeMethodAsync<TT>(MethodInfo method, object[] args, Type returnType) {
            // build arguments
            Dictionary<string, object> argsPayload = new Dictionary<string, object>();
            ParameterInfo[] argsMethod = method.GetParameters();

            for (int i = 0; i < args.Length; i++) {
                argsPayload[argsMethod[i].Name] = args[i];
            }

            // create request
            RpcRequest req = new RpcRequest(_contractAttr.Name != null ? _contractAttr.Name : _typeInfo.Name, method.Name, argsPayload);

            // serialize
            byte[] body = new ProtobufRpcSerializer().SerializeRequest(req);
            RpcHeader header = new RpcHeader(RpcHeader.HEADER_VERSION, ProtobufRpcSerializer.SerializerName, RpcMessageType.Single);

            // ask or send
            if (method.GetCustomAttribute<RpcOperationAttribute>().NoReply) {
                await _node.SendAsync(_addr, body, new Dictionary<string, object>() {
                    { RpcHeader.HEADER_NAME, header.ToString() }
                });

                return default(TT);
            } else {
                Envelope res = await _node.AskAsync(_addr, body, new Dictionary<string, object>() {
                    { RpcHeader.HEADER_NAME, header.ToString() }
                }, _configuration.Timeout);

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
                    resPayload = deserializer.DeserializeResponse(res.Body, typeof(void));

                    // return result
                    if (resPayload.IsSuccess) {
                        return (TT)(object)true;
                    }
                } else {
                    // deserialize
                    Type taskType = method.ReturnType.GetGenericArguments()[0];
                    resPayload = deserializer.DeserializeResponse(res.Body, taskType);

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
        /// Invokes a getter or setter property operation.
        /// </summary>
        /// <typeparam name="TT">The task return type.</typeparam>
        /// <param name="property">The property name.</param>
        /// <param name="val">The property value.</param>
        /// <returns></returns>
        public async Task<TT> InvokePropertyAsync<TT>(PropertyInfo property, object val) {
            // build arguments
            Dictionary<string, object> argsPayload = new Dictionary<string, object>();

            if (val != null)
                argsPayload["Property"] = val;

            // create request
            RpcRequest req = new RpcRequest(_contractAttr.Name != null ? _contractAttr.Name : _typeInfo.Name, property.Name, argsPayload);

            // serialize
            byte[] body = new ProtobufRpcSerializer().SerializeRequest(req);
            RpcHeader header = new RpcHeader(RpcHeader.HEADER_VERSION, ProtobufRpcSerializer.SerializerName, RpcMessageType.Single);

            // ask
            Envelope res = await _node.AskAsync(_addr, body, new Dictionary<string, object>() {
                { RpcHeader.HEADER_NAME, header.ToString() }
            }, _configuration.Timeout);

            // try and get response header
            if (!res.Headers.TryGetValue(RpcHeader.HEADER_NAME, out object resHeaderData))
                throw new InvalidOperationException("The response envelope is not a valid RPC message");

            RpcHeader resHeader = new RpcHeader(Encoding.UTF8.GetString(resHeaderData as byte[]));

            // deserialize response
            if (!RpcSerializer.Serializers.TryGetValue(resHeader.Serializer, out IRpcSerializer deserializer))
                throw new NotSupportedException("The response serializer is not supported");

            // deserialize
            RpcResponse resPayload = null;

            if (property.PropertyType.GetGenericTypeDefinition() == typeof(Task<>)) {
                // deserialize
                Type taskType = property.PropertyType.GetGenericArguments()[0];
                resPayload = deserializer.DeserializeResponse(res.Body, taskType);

                // return result
                if (resPayload.IsSuccess) {
                    return (TT)resPayload.Data;
                }
            } else {
                throw new NotSupportedException("The property has a void type");
            }

            // throw error
            throw new RpcException(resPayload.Error);
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
        [Obsolete("Do not create this class directly")]
        public RpcProxy() {
            // get type info
            _typeInfo = typeof(T).GetTypeInfo();

            // get contract attribute
            _contractAttr = _typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (_contractAttr == null)
                throw new InvalidOperationException("The interface must be decorated with a contract attribute");

            _invokeMethodInfo = GetType().GetTypeInfo().GetMethod(nameof(InvokeMethodAsync));
            _invokePropertyInfo = GetType().GetTypeInfo().GetMethod(nameof(InvokePropertyAsync));
        }
        #endregion
    }
}
