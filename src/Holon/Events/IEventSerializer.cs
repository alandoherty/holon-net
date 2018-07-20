using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Provides an interface to serialize and deserialize events.
    /// </summary>
    internal interface IEventSerializer
    {
        string Name { get; }

        Event DeserializeEvent(byte[] body, Type type);
        byte[] SerializeEvent(Event e);
    }
}
