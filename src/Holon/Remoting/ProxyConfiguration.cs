using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents configuration for a proxy.
    /// </summary>
    public sealed class ProxyConfiguration
    {
        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    }
}
