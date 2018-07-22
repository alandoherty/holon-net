using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Provides an interface to serialize and deserialize events.
    /// </summary>
    public interface IEventSerializer
    {
        /// <summary>
        /// Gets the serializer name.
        /// </summary>
        string Name { get; }

        Event DeserializeEvent(byte[] body, Type type);
        byte[] SerializeEvent(Event e);
    }
}
