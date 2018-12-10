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
        private string _resource;
        private string _namespace;
        private byte[] _data;
        private DateTimeOffset _timestamp;
        private string _id;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the ID.
        /// </summary>
        public string ID {
            get {
                return _id;
            }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace {
            get {
                return _namespace;
            }
        }

        /// <summary>
        /// Gets the resource.
        /// </summary>
        public string Resource {
            get {
                return _resource;
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

        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public DateTimeOffset Timestamp {
            get {
                return _timestamp;
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
        /// <param name="id">The ID.</param>
        /// <param name="namespace">The namespace.</param>
        /// <param name="resource">The resource.</param>
        /// <param name="name">The event name.</param>
        /// <param name="data">The data.</param>
        internal Event(string id, string @namespace, string resource, string name, byte[] data = null) {
            _id = id;
            _resource = resource;
            _namespace = @namespace;
            _name = name;
            _data = data;
            _timestamp = DateTime.UtcNow;
        }
        #endregion
    }
}
