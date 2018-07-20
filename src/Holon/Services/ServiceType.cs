using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Defines the possible service types.
    /// </summary>
    public enum ServiceType
    {
        /// <summary>
        /// One of many services receives a message sent to their address.
        /// </summary>
        Balanced,
        
        /// <summary>
        /// All services receives a copy of the message sent to their address.
        /// </summary>
        Fanout,

        /// <summary>
        /// Only one service can receive a copy sent to an address.
        /// </summary>
        Singleton
    }
}
