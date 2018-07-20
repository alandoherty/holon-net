using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Holon.Events.Serializers
{
    /// <summary>
    /// Provides a serializer for events using XML.
    /// </summary>
    internal class XmlEventSerializer : IEventSerializer
    {
        public const string NAME = "xml";

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return NAME;
            }
        }

        /// <summary>
        /// Deserialize the event.
        /// </summary>
        /// <param name="body">The message body.</param>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public Event DeserializeEvent(byte[] body, Type type) {
            using (MemoryStream ms = new MemoryStream(body)) {
                XDocument doc = XDocument.Load(ms);

                // read event
                XElement e = doc.Elements().First();

                if (e.Name != "Event")
                    throw new InvalidDataException("Invalid XML document for Event");

                XAttribute name = e.Attribute(XName.Get("n"));

                // read value
                object val = Remoting.Serializers.XmlRpcSerializer.DeserializeValue(type, e);

                return new Event(name.Value, val);
            }
        }

        /// <summary>
        /// Serialize the event.
        /// </summary>
        /// <param name="e">The event.</param>
        /// <returns></returns>
        public byte[] SerializeEvent(Event e) {
            byte[] body = null;

            using (MemoryStream ms = new MemoryStream()) {
#if DEBUG
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Indent = true, IndentChars = "\t", Encoding = new UTF8Encoding(false) });
#else
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = new UTF8Encoding(false) });
#endif
                writer.WriteStartDocument();

                // write request tag
                writer.WriteStartElement("Event");
                writer.WriteStartAttribute("n");
                writer.WriteString(e.Name);
                writer.WriteStartAttribute("t");
                writer.WriteString(Remoting.RpcArgument.TypeToString(e.Data.GetType()));
                writer.WriteEndAttribute();

                // write value
                Remoting.Serializers.XmlRpcSerializer.WriteValue(writer, e.Data);

                // finish document
                writer.WriteEndDocument();
                writer.Flush();
                body = ms.ToArray();
            }

            return body;
        }
    }
}
