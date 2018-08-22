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

        /// <summary>
        /// Deserializes the event.
        /// </summary>
        /// <param name="body">The event body.</param>
        /// <param name="type">The event type.</param>
        /// <returns>The event.</returns>
        Event DeserializeEvent(byte[] body, Type type);

        /// <summary>
        /// Serializes the event.
        /// </summary>
        /// <param name="e">The event.</param>
        /// <returns>The event body.</returns>
        byte[] SerializeEvent(Event e);
    }
}
