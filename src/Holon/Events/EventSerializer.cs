using System;
using System.Collections.Generic;
using System.Text;
using Holon.Events.Serializers;

namespace Holon.Events
{
    /// <summary>
    /// The event serializers.
    /// </summary>
    internal static class EventSerializer
    {
        #region Fields
        public static readonly Dictionary<string, IEventSerializer> Serializers = new Dictionary<string, IEventSerializer>(StringComparer.CurrentCultureIgnoreCase) {
            { ProtobufEventSerializer.SerializerName, new ProtobufEventSerializer() }
        };
        #endregion
    }
}
