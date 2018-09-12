using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Represents a service address
    /// </summary>
    public sealed class ServiceAddress : IEquatable<ServiceAddress>
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
        /// Gets the routing key.
        /// </summary>
        public string RoutingKey {
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

            // check namespace
            if (Namespace.IndexOf('!') != -1)
                return false;

            return true;
        }

        /// <summary>
        /// Trys to parse the provided service address.
        /// </summary>
        /// <param name="addr">The address string.</param>
        /// <param name="servAddr">The output service address.</param>
        /// <returns></returns>
        public static bool TryParse(string addr, out ServiceAddress servAddr) {
            servAddr = new ServiceAddress();
            return servAddr.InternalTryParse(addr);
        }

        /// <summary>
        /// Parses the provided service address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <exception cref="FormatException">The format is invalid.</exception>
        /// <returns></returns>
        public static ServiceAddress Parse(string addr) {
            if (!TryParse(addr, out ServiceAddress servAddr))
                throw new FormatException("The service address format is invalid");

            return servAddr;
        }

        /// <summary>
        /// Gets the string representation of this service address.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return _addr;
        }

        /// <summary>
        /// Gets the hash code of this service address.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return _addr.GetHashCode();
        }

        /// <summary>
        /// Determines whether this instance is equal to the provided object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>If the two objects are equal.</returns>
        public override bool Equals(object obj) {
            ServiceAddress addr = obj as ServiceAddress;

            if (addr == null)
                return false;

            return addr._addr.Equals(_addr, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Determines whether this address is equal to the provided address.
        /// </summary>
        /// <param name="other">The address.</param>
        /// <returns>If the two addresses are equal.</returns>
        public bool Equals(ServiceAddress other) {
            return other._addr.Equals(_addr, StringComparison.CurrentCultureIgnoreCase);
        }
        #endregion

        #region Constructors
        private ServiceAddress() { }

        /// <summary>
        /// Creates a new service address with the provided string representation.
        /// </summary>
        /// <param name="addr">The service address.</param>
        public ServiceAddress(string addr) {
            if (!InternalTryParse(addr))
                throw new FormatException("The service address format is invalid");
        }
        #endregion
    }
}
