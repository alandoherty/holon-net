using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents an exception which occurs when a message cannot be routed with <see cref="RoutingRule"/>.
    /// </summary>
    public class UnroutableException : Exception
    {
        private Address _addr;

        /// <summary>
        /// Gets the address which could not be routed to.
        /// </summary>
        public Address Address {
            get {
                return _addr;
            }
        }

        /// <summary>
        /// Creates a new unroutable exception.
        /// </summary>
        /// <param name="address">The address.</param>
        public UnroutableException(Address address) {
            _addr = address;
        }

        /// <summary>
        /// Creates a new unroutable exception with a message.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="message">The message.</param>
        public UnroutableException(Address address, string message) : base(message) {
            _addr = address;
        }

        /// <summary>
        /// Creates a new unroutable exception with a message and inner exception.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner exception.</param>
        public UnroutableException(Address address, string message, Exception inner) : base(message, inner) {
            _addr = address;
        }
    }
}
