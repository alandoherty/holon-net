using System;
using System.Collections.Generic;
using System.Text;
using Holon.Services;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents information for a service.
    /// </summary>
    [Serializable]
    public class NodeServiceInformation
    {
        /// <summary>
        /// Gets or sets the address of the service.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the type of the service.
        /// </summary>
        public ServiceType Type { get; set; }
    }
}
