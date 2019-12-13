using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Represents configuration for a service.
    /// </summary>
    public sealed class ServiceConfiguration
    {
        /// <summary>
        /// Gets or sets the service type.
        /// </summary>
        public ServiceType Type { get; set; } = ServiceType.Balanced;

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations.
        /// </summary>
        public int MaxConcurrency { get; set; } = 1;

        /// <summary>
        /// Gets or sets the ordered filters.
        /// </summary>
        public IServiceFilter[] Filters { get; set; } = new IServiceFilter[0];
    }
}
