using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Holon.Events;

namespace Holon.Transports
{
    /// <summary>
    /// Provides a virtual transport which works in-memory. Supports events and services.
    /// </summary>
    public class VirtualTransport : Transport
    {
        private Dictionary<Regex, VirtualEventSubscription> _subscriptions = new Dictionary<Regex, VirtualEventSubscription>();

        #region Capabilities
        public override bool CanEmit {
            get {
                return true;
            }
        }

        public override bool CanSubscribe {
            get {
                return true;
            }
        }

        public override bool CanSend {
            get {
                return false;
            }
        }

        public override bool CanAsk {
            get {
                return false;
            }
        }

        public override bool CanAttach {
            get {
                return false;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <exception cref="NotSupportedException">If the operation is not supported.</exception>
        /// <returns></returns>
        protected internal override Task EmitAsync(IEnumerable<Event> events)
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
            return Task.CompletedTask;
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
