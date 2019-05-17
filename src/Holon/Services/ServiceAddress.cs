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

        private ServiceAddress() { }

        /// <summary>
        /// Creates a new address with the provided string representation.
        /// </summary>
        /// <param name="addr">The service address.</param>
        public ServiceAddress(string addr)
            : base(addr) { }

        /// <summary>
        /// Creates a new service address with the provided components.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="key">The key.</param>
        public ServiceAddress(string @namespace, string key)
            : base(@namespace, key) { }
    }
}
