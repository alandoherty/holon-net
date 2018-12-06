using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents configuration for a proxy.
    /// </summary>
    public class ProxyConfiguration
    {
        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the trace ID.
        /// </summary>
        public string TraceId { get; set; } = null;
    }
}
