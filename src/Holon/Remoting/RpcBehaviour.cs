using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Holon.Introspection;
using Holon.Remoting.Serializers;
using Holon.Services;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides RPC functionality for a service
    /// </summary>
    public class RpcBehaviour : ServiceBehaviour
    {
        #region Fields
        internal static string[] StandardErrors = new string[] { "InterfaceNotFound", "OperationNotFound", "ArgumentRequired", "ArgumentMissing", "InvalidOperation", "Exception" };

        private Dictionary<string, Binding> _behaviours = new Dictionary<string, Binding>(StringComparer.CurrentCultureIgnoreCase);
        #endregion

        #region Properties
        /// <summary>
        /// Gets all the registered bindings.
        /// </summary>
        public object[] Bindings {
            get {
                return new List<object>(_behaviours.Values).ToArray();
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Handles the incoming envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        public override Task HandleAsync(Envelope envelope) {
            // try and get the header info
            if (!envelope.Headers.TryGetValue(RpcHeader.HEADER_NAME, out object rpcHeader))
                throw new InvalidOperationException("The incoming envelope is not a valid RPC message");

            // gets the header
            RpcHeader header = new RpcHeader(Encoding.UTF8.GetString(rpcHeader as byte[]));

            if (header.Version == RpcHeader.HEADER_VERSION) {
                return ApplyAsync(header, envelope);
            } else {
                throw new NotSupportedException("The RPC version is not supported");
            }
        }

        /// <summary>
        /// Replies to the provided envelope with the body and headers.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        protected virtual Task ReplyAsync(Envelope envelope, byte[] body, IDictionary<string, object> headers) {
            return envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, headers, body);
        }

        /// <summary>
        /// Takes the parsed header and envelope and applies correct type handler.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        private async Task ApplyAsync(RpcHeader header, Envelope envelope) {
            if (header.Type == RpcMessageType.Single) {
                // find serializer for this request
                RpcRequest req = null;
                IRpcSerializer serializer = null;

                if (!RpcSerializer.Serializers.TryGetValue(header.Serializer, out serializer)) {
                    throw new NotSupportedException("The serializer is not supported by RPC");
                }

                // deserialize into a request
                RpcResponse res = null;

                try {
                    req = serializer.DeserializeRequest(envelope.Body, (i, o) => RpcArgument.FromMember(GetMember(i, o)));
                } catch (KeyNotFoundException) {
                    res = new RpcResponse("InterfaceNotFound", "The interface or operation could not be found");
                } catch(Exception ex) {
                    res = new RpcResponse("ArgumentInvalid", string.Format("The request format is invalid: {0}", ex.Message));
                }

                // apply single request
                MemberInfo memberInfo = null;

                try {
                    if (req != null)
                        memberInfo = GetMember(req.Interface, req.Operation);
                } catch (KeyNotFoundException) {
                    res = new RpcResponse("OperationNotFound", "The interface or operation could not be found");
                }

                // get operation information
                RpcOperationAttribute opAttr = null;

                if (memberInfo != null) {
                    Type interfaceType = GetInterface(req.Interface);
                    MemberInfo[] interfaceMember = interfaceType.GetMember(req.Operation);
                    opAttr = interfaceMember[0].GetCustomAttribute<RpcOperationAttribute>();
                }

                // get if no reply is enabled
                bool noReply = opAttr != null && opAttr.NoReply;

                // check if they have a response ID if no reply isn't enabled
                if (!noReply && envelope.ID == Guid.Empty)
                    res = new RpcResponse("InvalidOperation", "The envelope does not specify a correlation ID");

                // apply request if we don't have a response already, typically an error
                if (res == null)
                    res = await ApplyRequestAsync(req, memberInfo).ConfigureAwait(false);
                
                // send response unless no-reply 
                if (!noReply) {
                    // serialize response
                    byte[] resBody = serializer.SerializeResponse(res);

                    // send reply
                    Dictionary<string, object> headers = new Dictionary<string, object>() {
                        { RpcHeader.HEADER_NAME, new RpcHeader(RpcHeader.HEADER_VERSION, serializer.Name, RpcMessageType.Single).ToString() }
                    };

                    // reply
                    await ReplyAsync(envelope, resBody, headers).ConfigureAwait(false);
                }

                // throw exceptions
            } else {
                throw new NotImplementedException("Batched RPC is not supported currently");
            }
        }

        /// <summary>
        /// Gets the interface
        /// </summary>
        /// <param name="interface">The interface.</param>
        /// <returns></returns>
        private Type GetInterface(string @interface) {
            // find interface behaviour
            if (!_behaviours.TryGetValue(@interface, out Binding binding))
                throw new KeyNotFoundException("The RPC interface could not been found");

            // get type
            return binding.Interface;
        }

        /// <summary>
        /// Gets the member information for the provided request.
        /// </summary>
        /// <param name="interface">The interface</param>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        private MemberInfo GetMember(string @interface, string operation) {
            // find interface behaviour
            if (!_behaviours.TryGetValue(@interface, out Binding binding))
                throw new KeyNotFoundException("The RPC interface could not been found");

            // get type info
            TypeInfo behaviourType = binding.Behaviour.GetType().GetTypeInfo();
            Type interfaceType = _behaviours[@interface].Interface;

            // find interface map
            InterfaceMapping mapping = behaviourType.GetRuntimeInterfaceMap(interfaceType);
            MemberInfo operationMember = mapping.InterfaceMethods.SingleOrDefault(m => m.Name.Equals(operation, StringComparison.CurrentCultureIgnoreCase));

            if (operationMember == null)
                throw new KeyNotFoundException("The RPC operation could not been found");

            return operationMember;
        }

        /// <summary>
        /// Applies a single deserialized rpc request to the behaviour object.
        /// </summary>
        /// <param name="req">The request.</param>
        /// <param name="member">The member information.</param>
        /// <returns>The task upon completion.</returns>
        private async Task<RpcResponse> ApplyRequestAsync(RpcRequest req, MemberInfo member) {
            // find interface behaviour
            if (!_behaviours.TryGetValue(req.Interface, out Binding binding))
                return new RpcResponse("InterfaceNotFound", "The interface binding could not be found");
            
            // get method info
            MethodInfo operationMethod = member as MethodInfo;

            // get parameters
            ParameterInfo[] methodParams = operationMethod.GetParameters();
            object[] methodArgs = new object[methodParams.Length];

            // fill arguments
            for (int i = 0; i < methodArgs.Length; i++) {
                if (!req.Arguments.TryGetValue(methodParams[i].Name, out methodArgs[i])) {
                    if (!methodParams[i].IsOptional)
                        return new RpcResponse("ArgumentRequired", string.Format("The argument {0} is not optional", methodParams[i].Name));
                }
            }

            // invoke method
            object methodResult = null;

            try {
                methodResult = operationMethod.Invoke(binding.Behaviour, methodArgs);
                await ((Task)methodResult).ConfigureAwait(false);
            } catch (RpcException ex) {
                return new RpcResponse(ex.Code, ex.Message);
            } catch (Exception ex) {
                return new RpcResponse("Exception", ex.ToString());
            }
            
            // check if the operation returns anything
            if (operationMethod.ReturnType == typeof(Task)) {
                return new RpcResponse(null);
            } else {
                // get result
                object realRes = methodResult.GetType().GetTypeInfo().GetProperty("Result").GetValue(methodResult);

                return new RpcResponse(realRes);
            }
        }

        /// <summary>
        /// Creates a new RPC behaviour and binds the interface type.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="interfaceBehaviour">The implementation of the interface behaviour.</param>
        /// <returns></returns>
        public static RpcBehaviour Bind<IT>(IT interfaceBehaviour) {
            return Bind(typeof(IT), interfaceBehaviour);
        }

        /// <summary>
        /// Creates a new RPC behaviour and binds the interface type.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="interfaceBehaviour">The implementation of the interface behaviour.</param>
        /// <returns></returns>
        public static RpcBehaviour Bind(Type interfaceType, object interfaceBehaviour) {
            RpcBehaviour rpcBehaviour = new RpcBehaviour();
            rpcBehaviour.Attach(interfaceType, interfaceBehaviour);
            return rpcBehaviour;
        }

        /// <summary>
        /// Creates a new RPC behaviour and binds the interfaces.
        /// </summary>
        /// <param name="interfaceTypes">The interface types.</param>
        /// <param name="interfaceBehaviours">The interface behaviours.</param>
        /// <returns></returns>
        public static RpcBehaviour BindMany(Type[] interfaceTypes, object[] interfaceBehaviours) {
            RpcBehaviour rpcBehaviour = new RpcBehaviour();
            rpcBehaviour.Attach(interfaceTypes, interfaceBehaviours);
            return rpcBehaviour;
        }

        /// <summary>
        /// Binds the provided interface behaviour.
        /// </summary>
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="interfaceBehaviour">The interface behaviour.</param>
        public void Attach<T>(T interfaceBehaviour)  {
            Attach(typeof(T), interfaceBehaviour);
        }

        /// <summary>
        /// Binds the provided interface behaviour.
        /// </summary>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="interfaceBehaviour">The interface behaviours.</param>
        public void Attach(Type interfaceType, object interfaceBehaviour) {
            if (!interfaceType.GetTypeInfo().IsInterface)
                throw new InvalidOperationException("You must bind the interface type, not the behaviour type");

            lock (_behaviours) {
                _behaviours[interfaceType.Name] = new Binding() {
                    Behaviour = interfaceBehaviour,
                    Interface = interfaceType
                };
            }
        }

        /// <summary>
        /// Binds many interfaces onto the behaviour.
        /// </summary>
        /// <param name="interfaceTypes">The interface types.</param>
        /// <param name="interfaceBehaviours">The interface behaviours.</param>
        public void Attach(Type[] interfaceTypes, object[] interfaceBehaviours) {
            for (int i = 0; i < interfaceTypes.Length; i++) {
                if (!interfaceTypes[i].GetTypeInfo().IsInterface)
                    throw new InvalidOperationException("You must bind the interface type, not the behaviour type");
            }

            lock (_behaviours) {
                for (int i = 0; i < interfaceTypes.Length; i++) {
                    _behaviours[interfaceTypes[i].Name] = new Binding() {
                        Behaviour = interfaceBehaviours[i],
                        Interface = interfaceTypes[i]
                    };
                }
            }
        }
        #endregion

        #region Classes
        /// <summary>
        /// Represents a binded interface.
        /// </summary>
        class Binding
        {
            private InterfaceInformation _introspection;

            public string Name {
                get {
                    TypeInfo interfaceType = Interface.GetTypeInfo();

                    // get attribute
                    RpcContractAttribute attr = interfaceType.GetCustomAttribute<RpcContractAttribute>();
                    
                    return attr.Name == null ? interfaceType.Name : attr.Name;
                }
            }

            public object Behaviour { get; set; }

            /// <summary>
            /// Gets the interface type.
            /// </summary>
            public Type Interface { get; set; }
            
            /// <summary>
            /// Gets the interface information for this behaviour.
            /// </summary>
            public InterfaceInformation Introspection {
                get {
                    // get existing information
                    if (_introspection != null)
                        return _introspection;

                    // get type info
                    TypeInfo interfaceType = Interface.GetTypeInfo();
                    MethodInfo[] interfaceMethods = interfaceType.GetMethods();

                    // project operations
                    InterfaceOperationInformation[] operationInfos = interfaceMethods.Where(method => {
                        RpcOperationAttribute attr = method.GetCustomAttribute<RpcOperationAttribute>();

                        return attr != null && attr.AllowIntrospection;
                    }).Select((method) => {
                        RpcOperationAttribute attr = method.GetCustomAttribute<RpcOperationAttribute>();

                        // get arguments
                        RpcArgument[] rpcArgs = RpcArgument.FromMethod(method);
                        InterfaceArgumentInformation[] methodArgs = rpcArgs.Select((a) => new InterfaceArgumentInformation() {
                            Name = a.Name,
                            Optional = a.Optional,
                            Type = RpcArgument.TypeToString(a.Type)
                        }).ToArray();

                        // add method
                        return new InterfaceOperationInformation() {
                            ReturnType = RpcArgument.TypeToString(method.ReturnType == typeof(Task) ? typeof(void) : method.ReturnType.GetGenericArguments()[0]),
                            Name = method.Name,
                            Arguments = methodArgs,
                            NoReply = attr.NoReply,
                            Throws = StandardErrors.Concat(method.GetCustomAttributes<RpcThrowsAttribute>().Select(throwAttr => throwAttr.Error)).ToArray()
                        };
                    }).ToArray();

                    // create interface information
                    _introspection = new InterfaceInformation() {
                        Name = Name,
                        Operations = operationInfos.ToArray(),
                    };

                    return _introspection;
                }
            }

            public bool AllowIntrospection {
                get {
                    TypeInfo interfaceType = Interface.GetTypeInfo();

                    // get attribute
                    RpcContractAttribute attr = interfaceType.GetCustomAttribute<RpcContractAttribute>();

                    if (attr == null) return false;

                    return attr.AllowIntrospection;
                }
            }
        }

        /// <summary>
        /// Provides a behaviour implementation for the introspection query interface.
        /// </summary>
        class QueryInterface : IInterfaceQuery001
        {
            private RpcBehaviour _behaviour;

            public Task<InterfaceInformation> GetInterfaceInfo(string @interface) {
                // get interface binding
                Binding binding = null;

                lock (_behaviour._behaviours) {
                    if (!_behaviour._behaviours.TryGetValue(@interface, out binding) || !binding.AllowIntrospection)
                        throw new RpcException("InterfaceNotFound", "The interface does not exist");
                }

                return Task.FromResult(binding.Introspection);
            }

            public Task<string[]> GetInterfaces() {
                List<string> interfaces = new List<string>();

                lock (_behaviour._behaviours) {
                    foreach(KeyValuePair<string, Binding> kv in _behaviour._behaviours) {
                        if (kv.Value.AllowIntrospection)
                            interfaces.Add(kv.Key);
                    }
                }

                return Task.FromResult(interfaces.ToArray());
            }

            public Task<bool> HasInterface(string name) {
                lock (_behaviour._behaviours) {
                    if (!_behaviour._behaviours.TryGetValue(name, out Binding binding))
                        return Task.FromResult(false);

                    return Task.FromResult(binding.AllowIntrospection);
                }
            }

            public QueryInterface(RpcBehaviour behaviour) {
                _behaviour = behaviour;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC behaviour.
        /// </summary>
        public RpcBehaviour() 
            : this(true) { }

        /// <summary>
        /// Creates a new RPC behaviour and selectively binds the introspection interface.
        /// </summary>
        /// <param name="introspection">If to bind introspection.</param>
        public RpcBehaviour(bool introspection) {
            Attach<IInterfaceQuery001>(new QueryInterface(this));
        }
        #endregion
    }
}
