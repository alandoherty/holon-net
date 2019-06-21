using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Holon.Events;
using Holon.Services;

namespace Holon.Transports
{
    /// <summary>
    /// Provides a virtual transport which works in-memory. Supports events and services.
    /// </summary>
    public class VirtualTransport : Transport
    {
        private Dictionary<Regex, VirtualEventSubscription> _subscriptions = new Dictionary<Regex, VirtualEventSubscription>();

        #region Capabilities
        /// <summary>
        /// Gets if this transport supports emitting events.
        /// </summary>
        public override bool CanEmit {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports subscribing to events.
        /// </summary>
        public override bool CanSubscribe {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports sending messages.
        /// </summary>
        public override bool CanSend {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transports supports request/response messages.
        /// </summary>
        public override bool CanAsk {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports attaching services.
        /// </summary>
        public override bool CanAttach {
            get {
                return true;
            }
        }
        #endregion

        #region Events
        protected internal override Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {

            return base.AttachAsync(addr, configuration, behaviour);
        }

        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <exception cref="NotSupportedException">If the operation is not supported.</exception>
        /// <returns></returns>
        protected internal override Task<int> EmitAsync(IEnumerable<Event> events)
        {
            foreach (Event e in events)
            {
                // get the event address as a string
                string addrStr = e.Address.ToString();

                // notify any subscriptions
                foreach (var kv in _subscriptions)
                {
                    if (kv.Key.Match(addrStr).Success)
                        kv.Value.Post(e);
                }
            }

            // no async required here
            return Task.FromResult(events.Count());
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <exception cref="NotSupportedException">If the operation is not supported.</exception>
        /// <returns>The subscription.</returns>
        protected internal override Task<IEventSubscription> SubscribeAsync(EventAddress addr)
        {
            // create a regex for the event
            Regex regex = new Regex($"{Regex.Escape(addr.Namespace)}\\:{Regex.Escape(addr.Resource).Replace("\\*", ".*")}\\.{Regex.Escape(addr.Name).Replace("\\*", ".*")}",
                RegexOptions.Compiled);

            // add subscription
            var sub = new VirtualEventSubscription(this, addr);

            lock (_subscriptions)
            {
                Dictionary<Regex, VirtualEventSubscription> subscriptions = new Dictionary<Regex, VirtualEventSubscription>(_subscriptions);
                subscriptions.Add(regex, sub);
                _subscriptions = subscriptions;
            }

            return Task.FromResult((IEventSubscription)sub);
        }
        #endregion
    }
}
