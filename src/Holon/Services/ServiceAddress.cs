using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Represents a service address.
    /// </summary>
    public class ServiceAddress : Address
    {
        #region Fields
        private string _addr;
        private int _divIdx;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the address namespace.
        /// </summary>
        public override string Namespace {
            get {
                return _addr.Substring(0, _divIdx);
            }
        }

        /// <summary>
        /// Gets the address key.
        /// </summary>
        public override string Key {
            get {
                return _addr.Substring(_divIdx + 1);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Trys to parse the provided address.
        /// </summary>
        /// <param name="addr">The address string.</param>
        /// <param name="address">The output address.</param>
        /// <returns></returns>
        public static bool TryParse(string addr, out ServiceAddress address)
        {
            address = new ServiceAddress();
            return address.InternalTryParse(addr);
        }

        /// <summary>
        /// Parses the provided address.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <exception cref="FormatException">The format is invalid.</exception>
        /// <returns></returns>
        public static ServiceAddress Parse(string addr)
        {
            if (!TryParse(addr, out ServiceAddress servAddr))
                throw new FormatException("The service address format is invalid");

            return servAddr;
        }

        internal override bool InternalTryParse(string addr)
        {
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
        /// Gets the string representation of this address.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _addr;
        }

        /// <summary>
        /// Gets the hash code of this address.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _addr.GetHashCode();
        }
        #endregion

        #region Constructors
        private ServiceAddress() { }

        /// <summary>
        /// Creates a new address with the provided string representation.
        /// </summary>
        /// <param name="addr">The service address.</param>
        public ServiceAddress(string addr) {
            _addr = addr;
        }

        /// <summary>
        /// Creates a new service address with the provided components.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="key">The key.</param>
        public ServiceAddress(string @namespace, string key)
            : this($"{@namespace}:{key}") { }
        #endregion
    }
}
