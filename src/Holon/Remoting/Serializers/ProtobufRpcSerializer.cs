using Holon.Services;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Holon.Remoting.Serializers
{
    /// <summary>
    /// Implements the new improved ProtoBuf serializer, with more efficient sub-serialization and additional type support.
    /// </summary>
    internal sealed class ProtobufRpcSerializer : IRpcSerializer
    {
        public const string SerializerName = "pbuf";

        public string Name {
            get {
                return SerializerName;
            }
        }

        public RpcRequest DeserializeRequest(byte[] body, RpcSignatureResolver resolver) {
            using (MemoryStream ms = new MemoryStream(body)) {
                // deserialize response message
                RequestMsg msg = Serializer.Deserialize<RequestMsg>(ms);

                // convert arguments
                Dictionary<string, object> args = new Dictionary<string, object>();
                RpcArgument[] rpcArgs = resolver(msg.Interface, msg.Operation);

                if (msg.Arguments != null) {
                    foreach (ValueMsg arg in msg.Arguments) {
                        if (arg.Key == null)
                            continue;

                        // add argument
                        args.Add(arg.Key, arg.GetData(rpcArgs.Single(a => a.Name.Equals(arg.Key, StringComparison.CurrentCultureIgnoreCase)).Type));
                    }
                }

                return new RpcRequest(msg.Interface, msg.Operation, args);
            }
        }

        public RpcRequest[] DeserializeRequestBatch(byte[] body, RpcSignatureResolver resolver) {
            throw new NotImplementedException();
        }

        public RpcResponse DeserializeResponse(byte[] body, Type responseType) {
            using (MemoryStream ms = new MemoryStream(body)) {
                // deserialize response message
                ResponseMsg msg = Serializer.Deserialize<ResponseMsg>(ms);

                if (msg.IsSuccess)
                    return new RpcResponse(msg.Data.GetData(responseType));
                else
                    return new RpcResponse(msg.Error.Code, msg.Error.Message);
            }
        }

        public RpcResponse[] DeserializeResponseBatch(byte[] body, Type[] responseTypes) {
            throw new NotImplementedException();
        }

        public byte[] SerializeRequest(RpcRequest request) {
            using (MemoryStream ms = new MemoryStream()) {
                // create request message
                RequestMsg req = new RequestMsg();
                req.Interface = request.Interface;
                req.Operation = request.Operation;
                req.Arguments = request.Arguments.Select(kv => {
                    ValueMsg value = new ValueMsg() { Key = kv.Key };
                    value.SetData(kv.Value);
                    return value;
                }).ToArray();

                Serializer.Serialize(ms, req);
                return ms.ToArray();
            }
        }

        public byte[] SerializeRequestBatch(RpcRequest[] batch) {
            throw new NotImplementedException();
        }

        public byte[] SerializeResponse(RpcResponse response) {
            using (MemoryStream ms = new MemoryStream()) {
                // create response message
                ResponseMsg res = new ResponseMsg();
                res.IsSuccess = response.IsSuccess;

                if (response.IsSuccess) {
                    ValueMsg result = new ValueMsg();
                    result.SetData(response.Data);
                    res.Data = result;
                } else {
                    res.Error = new ErrorMsg() {
                        Code = response.Error.Code,
                        Message = response.Error.Message
                    };
                }

                Serializer.Serialize(ms, res);
                return ms.ToArray();
            }
        }

        public byte[] SerializeResponseBatch(RpcResponse[] batch) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Defines a RPC request.
    /// </summary>
    [ProtoContract]
    class RequestMsg
    {
        [ProtoMember(1, IsRequired = true)]
        public string Interface { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string Operation { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public ValueMsg[] Arguments { get; set; }
    }

    /// <summary>
    /// Defines an RPC value (argument/return data).
    /// </summary>
    [ProtoContract]
    class ValueMsg
    {
        [ProtoMember(1, IsRequired = false)]
        public string Key { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public byte[] Data { get; set; }

        /// <summary>
        /// Sets the data from a .NET type.
        /// </summary>
        /// <param name="val">The value.</param>
        public void SetData(object val) {
            if (val is string)
                Data = Encoding.UTF8.GetBytes((string)val);
            else if (val is sbyte)
                Data = new byte[] { (byte)(sbyte)val };
            else if (val is short)
                Data = BitConverter.GetBytes((short)val);
            else if (val is int)
                Data = BitConverter.GetBytes((int)val);
            else if (val is long)
                Data = BitConverter.GetBytes((long)val);
            else if (val is byte)
                Data = new byte[] { (byte)val };
            else if (val is ushort)
                Data = BitConverter.GetBytes((ushort)val);
            else if (val is uint)
                Data = BitConverter.GetBytes((uint)val);
            else if (val is ulong)
                Data = BitConverter.GetBytes((ulong)val);
            else if (val is decimal) {
                int[] bits = decimal.GetBits((decimal)val);
                byte[] bytes = new byte[16];

                for (int i = 0; i < 4; i++) {
                    byte[] bitBytes = BitConverter.GetBytes(bits[i]);
                    Buffer.BlockCopy(bitBytes, 0, bytes, i * 4, 4);
                }

                Data = bytes;
            } else if (val is float)
                Data = BitConverter.GetBytes((float)val);
            else if (val is double)
                Data = BitConverter.GetBytes((double)val);
            else if (val is bool)
                Data = new byte[] { (bool)val ? (byte)1 : (byte)0 };
            else if (val == null)
                Data = null;
            else if (val is Guid)
                Data = ((Guid)val).ToByteArray();
            else if (val is ServiceAddress)
                Data = Encoding.UTF8.GetBytes(((ServiceAddress)val).ToString());
            else if (val is byte[])
                Data = ((byte[])val);
            else if (val is DateTime)
                Data = BitConverter.GetBytes(((DateTime)val).ToBinary());
            else {
                TypeInfo typeInfo = val.GetType().GetTypeInfo();

                if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                    using (MemoryStream ms = new MemoryStream()) {
                        RuntimeTypeModel.Default.Serialize(ms, val);
                        Data = ms.ToArray();
                    }
                } else {
                    Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(val));
                }
            }
        }

        /// <summary>
        /// Gets the data as a .NET type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public object GetData(Type type) {
            if (type == typeof(string))
                return Encoding.UTF8.GetString(Data);
            else if (type == typeof(sbyte))
                return (sbyte)Data[0];
            else if (type == typeof(short))
                return BitConverter.ToInt16(Data, 0);
            else if (type == typeof(int))
                return BitConverter.ToInt32(Data, 0);
            else if (type == typeof(long))
                return BitConverter.ToInt64(Data, 0);
            else if (type == typeof(byte))
                return Data[0];
            else if (type == typeof(ushort))
                return BitConverter.ToUInt16(Data, 0);
            else if (type == typeof(uint))
                return BitConverter.ToUInt32(Data, 0);
            else if (type == typeof(ulong))
                return BitConverter.ToUInt64(Data, 0);
            else if (type == typeof(decimal)) {
                int[] bits = new int[4];
                for (int i = 0; i < 16; i += 4) {
                    bits[i / 4] = BitConverter.ToInt32(Data, i);
                }
                return new decimal(bits);
            } else if (type == typeof(float))
                return BitConverter.ToSingle(Data, 0);
            else if (type == typeof(double))
                return BitConverter.ToDouble(Data, 0);
            else if (type == typeof(bool))
                return Data[0] == 1;
            else if (type == typeof(void))
                return null;
            else if (type == typeof(Guid))
                return new Guid(Data);
            else if (type == typeof(ServiceAddress))
                return ServiceAddress.Parse(Encoding.UTF8.GetString(Data));
            else if (type == typeof(byte[]))
                return Data;
            else if (type == typeof(DateTime))
                return DateTime.FromBinary(BitConverter.ToInt64(Data, 0));
            else {
                TypeInfo typeInfo = type.GetTypeInfo();

                if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                    using (MemoryStream ms = new MemoryStream(Data)) {
                        return Serializer.Deserialize(type, ms);
                    }
                } else {
                    return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(Data), type);
                }
            }
        }
    }

    /// <summary>
    /// Defines an RPC response.
    /// </summary>
    [ProtoContract]
    class ResponseMsg
    {
        [ProtoMember(1, IsRequired = true)]
        public bool IsSuccess { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public ValueMsg Data { get; set; }

        [ProtoMember(3, IsRequired = false)]
        public ErrorMsg Error { get; set; }
    }

    /// <summary>
    /// Defines an error message.
    /// </summary>
    [ProtoContract]
    class ErrorMsg
    {
        [ProtoMember(1, IsRequired = true)]
        public string Code { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string Message { get; set; }
    }
}
