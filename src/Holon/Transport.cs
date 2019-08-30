using Holon.Events;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Implements a low-level transport which defines how to send messages to services and how to attach services.
    /// </summary>
    public abstract class Transport
    {
        #region Fields
        private Node _node;
        private string _name;
        #endregion

        #region Properties
        /// <summary>
        /// Gets if this transport supports emitting events.
        /// </summary>
        public virtual bool CanEmit {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports subscribing to events.
        /// </summary>
        public virtual bool CanSubscribe {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports sending messages.
        /// </summary>
        public virtual bool CanSend {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transports supports request/response messages.
        /// </summary>
        public virtual bool CanAsk {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports attaching services.
        /// </summary>
        public virtual bool CanAttach {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets the node this transport is attached to.
        /// </summary>
        public Node Node {
            get {
                return _node;
            } internal set {
                _node = value;
            }
        }

        /// <summary>
        /// Gets the unique name for this transport, can be null.
        /// </summary>
        public string Name {
            get {
                return _name;
            } internal set {
                _name = value;
            }
        }
        #endregion

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        internal protected virtual Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            if (CanAsk)
                throw new NotImplementedException();
            else
                throw new NotSupportedException("This transport does not support ask requests");
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        internal protected virtual Task SendAsync(Message message) {
            if (CanSend)
                throw new NotImplementedException();
            else
                throw new NotSupportedException("This transport does not support sending messages");
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <remarks>This method is optional to implement and should be used when an efficient way of bulk sending messages is possible.</remarks>
        /// <returns></returns>
        internal protected virtual async Task BulkSendAsync(IEnumerable<Message> messages) {
            if (!CanSend)
                throw new NotSupportedException("This transport does not support sending messages");

            foreach (Message msg in messages)
                await SendAsync(msg).ConfigureAwait(false);
        }

        /// <summary>
        /// Attaches a service on the provided address.
        /// </summary>
        /// <param name="addr">The service address.</param>
        /// <param name="configuration">The service configuration.</param>
        /// <param name="behaviour">The service behaviour.</param>
        /// <returns>The attached service.</returns>
        internal protected virtual Task<Service> AttachAsync(ServiceAddress addr, ServiceConfiguration configuration, ServiceBehaviour behaviour) {
            if (CanAttach)
                throw new NotImplementedException();
            else
                throw new NotSupportedException("This transport does not support attaching services");
        }

        /// <summary>
        /// Emits an event on the provided address.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <exception cref="NotSupportedException">If the operation is not supported.</exception>
        /// <returns>The number of events emitted.</returns>
        internal protected virtual Task<int> EmitAsync(IEnumerable<Event> events)
        {
            if (CanEmit)
                throw new NotImplementedException();
            else
                throw new NotSupportedException("This transport does not support emitting");
        }

        /// <summary>
        /// Subscribes to events matching the provided name.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <exception cref="NotSupportedException">If the operation is not supported.</exception>
        /// <returns>The subscription.</returns>
        internal protected virtual Task<IEventSubscription> SubscribeAsync(EventAddress addr)
        {
            if (CanSubscribe)
                throw new NotImplementedException();
            else
                throw new NotSupportedException("This transport does not support subscriptions");
        }

        /// <summary>
        /// Called when the <see cref="NodeBuilder"/> has finished building the node, you should include additional setup here. You may start receiving calls to operation methods now, <see cref="Node"/> will also now be defined.
        /// </summary>
        internal protected virtual void OnBuild() {
        }
    }
}
