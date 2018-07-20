using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Holon.Services;

namespace Holon.Remoting.Serializers
{
    internal class XmlRpcSerializer : IRpcSerializer
    {
        public const string SerializerName = "xml";

        public string Name {
            get {
                return SerializerName;
            }
        }

        public RpcRequest DeserializeRequest(byte[] body, RpcSignatureResolver resolver) {
            using (MemoryStream ms = new MemoryStream(body)) {
                XDocument doc = XDocument.Load(ms);

                // read request
                XElement req = doc.Elements().First();

                if (req.Name != "Request")
                    throw new InvalidDataException("Invalid XML document for RPC");

                XAttribute iface = req.Attribute(XName.Get("i"));
                XAttribute op = req.Attribute(XName.Get("o"));
                
                // read arguments
                XElement arguments = req.Elements().SingleOrDefault((e) => e.Name == "Arguments");
                Dictionary<string, object> argumentsData = new Dictionary<string, object>();

                if (arguments != null) {
                    // resolve arguments
                    RpcArgument[] args = resolver(iface.Value, op.Value);
                    Dictionary<string, RpcArgument> argsMap = new Dictionary<string, RpcArgument>();

                    foreach (RpcArgument arg in args)
                        argsMap[arg.Name] = arg;

                    // look for matching elements
                    foreach(XElement argElement in arguments.Elements()) {
                        string argVal = argElement.Value;

                        if (argsMap.TryGetValue(argElement.Name.LocalName, out RpcArgument arg)) {
                            argumentsData[arg.Name] = DeserializeValue(arg.Type, argElement);
                        }
                    }
                }

                return new RpcRequest(iface.Value, op.Value, argumentsData);
            }
        }

        /// <summary>
        /// Deserializes a value with the provided type from the specified element.
        /// </summary>
        /// <param name="type">The data type.</param>
        /// <param name="element">The element.</param>
        /// <returns></returns>
        internal static object DeserializeValue(Type type, XElement element) {
            string argVal = element.Value;

            if (type == typeof(string))
                return argVal;
            else if (type == typeof(sbyte))
                return sbyte.Parse(argVal);
            else if (type == typeof(short))
                return short.Parse(argVal);
            else if (type == typeof(int))
                return int.Parse(argVal);
            else if (type == typeof(long))
                return long.Parse(argVal);
            else if (type == typeof(byte))
                return byte.Parse(argVal);
            else if (type == typeof(ushort))
                return ushort.Parse(argVal);
            else if (type == typeof(uint))
                return uint.Parse(argVal);
            else if (type == typeof(ulong))
                return ulong.Parse(argVal);
            else if (type == typeof(decimal))
                return decimal.Parse(argVal);
            else if (type == typeof(float))
                return float.Parse(argVal);
            else if (type == typeof(bool))
                return bool.Parse(argVal);
            else if (type == typeof(void))
                return null;
            else if (type == typeof(Guid))
                return Guid.Parse(argVal);
            else if (type == typeof(ServiceAddress))
                return ServiceAddress.Parse(argVal);
            else {
                // read xml from argument
                XmlReader contentReader = element.CreateReader();
                contentReader.MoveToContent();
                string xml = contentReader.ReadInnerXml();

                // deserialize
                XmlSerializer valueDeserializer = new XmlSerializer(type);

                using (StringReader valueStream = new StringReader(xml)) {
                   return valueDeserializer.Deserialize(valueStream);
                }
            }
        }

        public RpcRequest[] DeserializeRequestBatch(byte[] body, RpcSignatureResolver resolver) {
            throw new NotImplementedException();
        }

        public RpcResponse DeserializeResponse(byte[] body, Type dataType) {
            using (MemoryStream ms = new MemoryStream(body)) {
                XDocument xml = XDocument.Load(ms);

                // read request
                XElement req = xml.Elements().First();

                if (req.Name != "Response")
                    throw new InvalidDataException("Invalid XML document, root element not Response");

                // read data
                XElement data = req.Element(XName.Get("Data"));

                if (data != null) {
                    return new RpcResponse(DeserializeValue(dataType, data));
                } else {
                    // find the error element then
                    XElement err = req.Element(XName.Get("Error"));

                    if (err == null)
                        throw new InvalidDataException("Invalid XML document, missing Error or Data element");

                    // find the code and message
                    XElement errCode = err.Element(XName.Get("Code"));
                    XElement errMsg = err.Element(XName.Get("Message"));

                    if (errCode == null || errMsg == null)
                        throw new InvalidDataException("Invalid XML document, error missing Code and Message elements");

                    // get data
                    return new RpcResponse(errCode.Value, errMsg.Value);
                }
            }
        }

        public RpcResponse[] DeserializeResponseBatch(byte[] body, Type[] dataTypes) {
            throw new NotImplementedException();
        }

        public byte[] SerializeRequest(RpcRequest request) {
            byte[] body = null;

            using (MemoryStream ms = new MemoryStream()) {
#if DEBUG
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Indent = true, IndentChars = "\t", Encoding = new UTF8Encoding(false) });
#else
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = new UTF8Encoding(false) });
#endif
                writer.WriteStartDocument();

                // write request tag
                writer.WriteStartElement("Request");
                writer.WriteStartAttribute("i");
                writer.WriteString(request.Interface);
                writer.WriteStartAttribute("o");
                writer.WriteString(request.Operation);

                // write payload
                writer.WriteStartElement("Arguments");

                if (request.Arguments !=null) {
                    foreach(KeyValuePair<string, object> kv in request.Arguments) {
                        writer.WriteStartElement(kv.Key);
                        WriteValue(writer, kv.Value);
                        writer.WriteEndElement();
                    }
                }

                // finish document
                writer.WriteEndDocument();
                writer.Flush();
                body = ms.ToArray();
            }
            
            return body;
        }

        public byte[] SerializeRequestBatch(RpcRequest[] batch) {
            throw new NotImplementedException();
        }

        public byte[] SerializeResponse(RpcResponse response) {
            byte[] body = null;

            using (MemoryStream ms = new MemoryStream()) {
#if DEBUG
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Indent = true, IndentChars = "\t", Encoding = new UTF8Encoding(false) });
#else
                XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = new UTF8Encoding(false) });
#endif
                writer.WriteStartDocument();

                // write request tag
                writer.WriteStartElement("Response");

                // write error or data
                if (response.IsSuccess) {
                    writer.WriteStartElement("Data");
                    WriteValue(writer, response.Data);
                    writer.WriteEndElement();
                } else {
                    writer.WriteStartElement("Error");
                    writer.WriteStartElement("Code");
                    writer.WriteValue(response.Error.Code);
                    writer.WriteEndElement();
                    writer.WriteStartElement("Message");
                    writer.WriteValue(response.Error.Message);
                }

                // finish document
                writer.WriteEndDocument();
                writer.Flush();
                body = ms.ToArray();
            }

            return body;
        }

        public byte[] SerializeResponseBatch(RpcResponse[] batch) {
            throw new NotImplementedException();
        }

        internal static void WriteValue(XmlWriter writer, object val) {
            if (val == null)
                return;
            else if (val is string || val.GetType().GetTypeInfo().IsValueType)
                writer.WriteString(val.ToString());
            else { 
                // create settings
                XmlSerializerNamespaces emptyNamepsaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
                XmlSerializer serializer = new XmlSerializer(val.GetType());
                XmlWriterSettings settings = new XmlWriterSettings() {
                        OmitXmlDeclaration = true
                };

                // write value
                using (StringWriter stream = new StringWriter())
                using (XmlWriter valueWriter = XmlWriter.Create(stream, settings)) {
                    serializer.Serialize(valueWriter, val, emptyNamepsaces);
                    writer.WriteRaw(stream.ToString());
                }
            }
        }
    }
}
