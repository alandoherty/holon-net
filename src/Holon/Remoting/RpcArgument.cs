using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Holon.Services;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents an operation argument.
    /// </summary>
    public class RpcArgument
    {
        #region Fields
        private Type _type;
        private bool _optional;
        private string _name;
        #endregion

        #region Properties
        /// <summary>
        /// Gets if the argument is optional.
        /// </summary>
        public bool Optional {
            get {
                return _optional;
            }
        }

        /// <summary>
        /// Gets the argument name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the argument type.
        /// </summary>
        public Type Type {
            get {
                return _type;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the ordered arguments for a member.
        /// </summary>
        /// <param name="info">The member info.</param>
        /// <returns>The arguments.</returns>
        public static RpcArgument[] FromMember(MemberInfo info) {
            if (info is PropertyInfo)
                return new RpcArgument[] { FromProperty(info as PropertyInfo) };
            else if (info is MethodInfo)
                return FromMethod(info as MethodInfo);
            else
                throw new NotSupportedException(string.Format("The member type {0} is not supported by RPC", info.GetType().Name));
        }

        /// <summary>
        /// Gets the argument for a property.
        /// </summary>
        /// <param name="info">The property info.</param>
        /// <returns>The arguments.</returns>
        public static RpcArgument FromProperty(PropertyInfo info) {
            return new RpcArgument(info.Name, info.PropertyType, true);
        }

        /// <summary>
        /// Gets the ordered arguments for a method.
        /// </summary>
        /// <param name="info">The method info.</param>
        /// <returns>The arguments.</returns>
        public static RpcArgument[] FromMethod(MethodInfo info) {
            // get parameters
            ParameterInfo[] paramInfo = info.GetParameters();
            List<RpcArgument> argInfo = new List<RpcArgument>();

            for (int i = 0; i < paramInfo.Length; i++) {
                if (paramInfo[i].ParameterType == typeof(Envelope))
                    continue;

                argInfo.Add(new RpcArgument(paramInfo[i].Name, paramInfo[i].ParameterType, paramInfo[i].IsOptional));
            }

            return argInfo.ToArray();
        }

        /// <summary>
        /// Converts a .NET type into an RPC type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        internal static string TypeToString(Type type) {
            if (type == typeof(string))
                return "string";
            else if (type == typeof(sbyte))
                return "int8";
            else if (type == typeof(short))
                return "int16";
            else if (type == typeof(int))
                return "int32";
            else if (type == typeof(long))
                return "int64";
            else if (type == typeof(byte))
                return "uint8";
            else if (type == typeof(ushort))
                return "uint16";
            else if (type == typeof(uint))
                return "uint32";
            else if (type == typeof(ulong))
                return "uint64";
            else if (type == typeof(decimal))
                return "decimal";
            else if (type == typeof(float))
                return "float";
            else if (type == typeof(bool))
                return "bool";
            else if (type == typeof(void))
                return "void";
            else if (type == typeof(Guid))
                return "guid";
            else if (type == typeof(ServiceAddress))
                return "address";
            else
                return "serialized";
        }

        /// <summary>
        /// Converts a RPC type into it's .NET version.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        internal static Type TypeFromString(string type) {
            switch(type) {
                case "string":
                    return typeof(string);
                case "int8":
                    return typeof(sbyte);
                case "int16":
                    return typeof(short);
                case "int32":
                    return typeof(int);
                case "int64":
                    return typeof(long);
                case "uint8":
                    return typeof(byte);
                case "uint16":
                    return typeof(ushort);
                case "uint32":
                    return typeof(uint);
                case "uint64":
                    return typeof(ulong);
                case "decimal":
                    return typeof(decimal);
                case "float":
                    return typeof(float);
                case "bool":
                    return typeof(bool);
                case "void":
                    return typeof(void);
                case "guid":
                    return typeof(Guid);
                case "address":
                    return typeof(ServiceAddress);
                default:
                    return typeof(object);
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new RPC argument.
        /// </summary>
        /// <param name="name">The argument name.</param>
        /// <param name="type">The argument type.</param>
        /// <param name="optional">If the argument is optional.</param>
        public RpcArgument(string name, Type type, bool optional) {
            _name = name;
            _type = type;
            _optional = optional;
        }
        #endregion
    }
}
