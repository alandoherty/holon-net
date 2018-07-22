using Holon.Remoting;
using Holon.Services;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Holon.Events.Serializers
{
    /// <summary>
    /// Provides a serializer for events using protobuf.
    /// </summary>
    internal class ProtobufEventSerializer : IEventSerializer
    {
        public const string SerializerName = "pbuf";

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return SerializerName;
            }
        }

        public Event DeserializeEvent(byte[] body, Type type) {
            using (MemoryStream ms = new MemoryStream(body)) {
                EventMsg msg = Serializer.Deserialize<EventMsg>(ms);

                return new Event(msg.Name, msg.Data);
            }
        }

        public byte[] SerializeEvent(Event e) {
            using (MemoryStream ms = new MemoryStream()) {
                EventMsg eventMsg = new EventMsg();
                eventMsg.Name = e.Name;
                eventMsg.Type = RpcArgument.TypeToString(e.Data.GetType());
                eventMsg.Data = e.Data;
                Serializer.Serialize(ms, eventMsg);
                return ms.ToArray();
            }
        }
    }

    [ProtoContract]
    class EventMsg
    {
        [ProtoMember(1, IsRequired = true)]
        public string Name { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string Type { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public byte[] Data { get; set; }
    }
}
