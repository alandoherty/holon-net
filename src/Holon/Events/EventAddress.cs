using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event address.
    /// </summary>
    public class EventAddress : IEquatable<EventAddress>
    {
        #region Fields
        private int _divIdx;
        private string _addr;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the namespace.
        /// </summary>
        public string Namespace {
            get {
                return _addr.Substring(0, _divIdx);
            }
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _addr.Substring(_divIdx + 1);
            }
        }
        #endregion

        #region Methods
        private bool InternalTryParse(string addr) {
            // validate divider
            int divIdx = _divIdx = -1;

            for (int i = 0; i < addr.Length; i++) {
                if (addr[i] == ':') {
                    if (divIdx > -1)
                        divIdx = -2;
                    else
                        divIdx = i;
                }
            }

            // failed to find the dividing colon, or we found too many colons
            if (divIdx < 0)
                return false;

            // check routing key is present
            if (divIdx == addr.Length - 1)
                return false;

            // assign
            _divIdx = divIdx;
            _addr = addr;
            return true;
        }

        /// <summary>
        /// Trys to parse the provided event address.
        /// </summary>
        /// <param name="addr">The address string.</param>
        /// <param name="eventAddr">The output event address.</param>
        /// <returns></returns>
        public static bool TryParse(string addr, out EventAddress eventAddr) {
            eventAddr = new EventAddress();
            return eventAddr.InternalTryParse(addr);
        }

        /// <summary>
        /// Parses the provided event address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <exception cref="FormatException">The format is invalid.</exception>
        /// <returns></returns>
        public static EventAddress Parse(string addr) {
            if (!TryParse(addr, out EventAddress servAddr))
                throw new FormatException("The event address format is invalid");

            return servAddr;
        }

        /// <summary>
        /// Gets the string representation of this event address.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _addr;
        }

        /// <summary>
        /// Gets the hash code of this event address.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return _addr.GetHashCode();
        }

        /// <summary>
        /// Compares two event addresses for equality.
        /// </summary>
        /// <param name="other">The other address.</param>
        /// <returns></returns>
        public bool Equals(EventAddress other) {
            return _addr == other._addr;
        }
        #endregion

        #region Constructors
        private EventAddress() { }

        /// <summary>
        /// Creates a new event address with the provided string representation.
        /// </summary>
        /// <param name="addr">The event address.</param>
        public EventAddress(string addr) {
            if (!InternalTryParse(addr))
                throw new FormatException("The event address format is invalid");
        }
        #endregion
    }
}
