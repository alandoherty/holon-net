using System;
using System.Collections.Generic;
using System.Text;
using Holon.Remoting.Serializers;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides instances of serializers.
    /// </summary>
    internal static class RpcSerializer
    {
        #region Fields
        public static readonly Dictionary<string, IRpcSerializer> Serializers = new Dictionary<string, IRpcSerializer>(StringComparer.CurrentCultureIgnoreCase) {
            { ProtobufRpcSerializer.SerializerName, new ProtobufRpcSerializer() },
            { XmlRpcSerializer.SerializerName, new XmlRpcSerializer() }
        };
        #endregion
    }
}
