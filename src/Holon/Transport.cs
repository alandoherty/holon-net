using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Implements a low-level transport which defines how to send messages to services and how to attach services.
    /// </summary>
    public abstract class Transport
    {
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
    }
}
