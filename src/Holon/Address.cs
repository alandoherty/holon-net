using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Provides the base class for an address.
    /// </summary>
    public abstract class Address
    {
        #region Fields
        private int _divIdx;
        private string _addr;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the address namespace.
        /// </summary>
        public string Namespace {
            get {
                return _addr.Substring(0, _divIdx);
            }
        }

        /// <summary>
        /// Gets the address key.
        /// </summary>
        public string Key {
            get {
                return _addr.Substring(_divIdx + 1);
            }
        }
        #endregion

        #region Methods
        internal bool InternalTryParse(string addr)
        {
            // validate divider
            int divIdx = _divIdx = -1;

            for (int i = 0; i < addr.Length; i++)
            {
                if (addr[i] == ':')
                {
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
        internal Address() { }

        /// <summary>
        /// Creates a new address with the provided string representation.
        /// </summary>
        /// <param name="addr">The service address.</param>
        protected Address(string addr)
        {
            if (!InternalTryParse(addr))
                throw new FormatException("The address format is invalid");
        }

        /// <summary>
        /// Creates a new address with the provided components.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="key">The key.</param>
        protected Address(string @namespace, string key)
        {
            if (@namespace.IndexOf(':') != -1)
                throw new FormatException("The namespace format is invalid");

            _addr = $"{@namespace}:{key}";
        }
        #endregion
    }
}
