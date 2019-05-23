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
        #region Properties
        /// <summary>
        /// Gets the address namespace.
        /// </summary>
        public abstract string Namespace { get; }

        /// <summary>
        /// Gets the address key.
        /// </summary>
        public abstract string Key { get; }
        #endregion

        #region Methods
        internal abstract bool InternalTryParse(string addr);
        #endregion

        #region Constructors
        internal Address() { }
        #endregion
    }
}
