using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    public class Event
    {
        #region Fields
        private string _name;
        private byte[] _data;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the raw payload.
        /// </summary>
        public byte[] Data {
            get {
                return _data;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Serializes the data from a .NET type.
        /// </summary>
        /// <param name="val">The value.</param>
        public void Serialize(object val) {
            TypeInfo typeInfo = val.GetType().GetTypeInfo();

            if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                using (MemoryStream ms = new MemoryStream()) {
                    RuntimeTypeModel.Default.Serialize(ms, val);
                    _data = ms.ToArray();
                }
            } else {
                _data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(val));
            }
        }

        /// <summary>
        /// Serializes the data from a .NET type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="val">The value.</param>
        public void Serialize<T>(T val) {
            TypeInfo typeInfo = val.GetType().GetTypeInfo();

            if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                using (MemoryStream ms = new MemoryStream()) {
                    Serializer.Serialize(ms, val);
                    _data = ms.ToArray();
                }
            } else {
                _data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(val));
            }
        }

        /// <summary>
        /// Deserializes the data as a .NET type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public object Deserialize(Type type) {
            TypeInfo typeInfo = type.GetTypeInfo();

            if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                using (MemoryStream ms = new MemoryStream(Data)) {
                    return Serializer.Deserialize(type, ms);
                }
            } else {
                return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(Data), type);
            }
        }
        
        /// <summary>
        /// Deserializes the data as a .NET type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The deserialized object.</returns>
        public T Deserialize<T>() {
            TypeInfo typeInfo = typeof(T).GetTypeInfo();

            if (typeInfo.GetCustomAttribute<ProtoContractAttribute>() != null) {
                using (MemoryStream ms = new MemoryStream(Data)) {
                    return Serializer.Deserialize<T>(ms);
                }
            } else {
                return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Data));
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new event.
        /// </summary>
        /// <param name="name">The event name.</param>
        /// <param name="data">The data.</param>
        internal Event(string name, byte[] data) {
            _name = name;
            _data = data;
        }
        #endregion
    }
}
