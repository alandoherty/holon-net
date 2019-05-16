using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Defines an interface for event subscriptions.
    /// </summary>
    public interface IEventSubscription : IDisposable
    {
        /// <summary>
        /// Gets the event address.
        /// </summary>
        EventAddress Address { get; }

        /// <summary>
        /// Gets the subscription as an observable target.
        /// </summary>
        /// <returns>The observerable.</returns>
        IObservable<Event> AsObservable();
    }
}
