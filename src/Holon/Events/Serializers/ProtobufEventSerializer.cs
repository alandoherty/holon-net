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

        public Event DeserializeEvent(byte[] body) {
            using (MemoryStream ms = new MemoryStream(body)) {
                EventMsg msg = Serializer.Deserialize<EventMsg>(ms);

                return new Event(msg.ID, msg.Namespace, msg.Resource, msg.Name, msg.Data);
            }
        }

        public byte[] SerializeEvent(Event e) {
            using (MemoryStream ms = new MemoryStream()) {
                EventMsg eventMsg = new EventMsg();
                eventMsg.Name = e.Name;
                eventMsg.Type = "serialized";
                eventMsg.Data = e.Data;
                eventMsg.Resource = e.Resource;
                eventMsg.Namespace = e.Namespace;
                eventMsg.ID = e.ID;
                Serializer.Serialize(ms, eventMsg);
                return ms.ToArray();
            }
        }
    }

    [ProtoContract]
    class EventMsg
    {
        [ProtoMember(1, IsRequired = true)]
        public string Resource { get; set; }

        [ProtoMember(2, IsRequired = true)]
        public string Name { get; set; }

        [ProtoMember(3, IsRequired = true)]
        public string Type { get; set; }

        [ProtoMember(4, IsRequired = true)]
        public byte[] Data { get; set; }

        [ProtoMember(5, IsRequired = true)]
        public string Namespace { get; set; }

        [ProtoMember(6, IsRequired = true)]
        public string ID { get; set; }
    }
}
