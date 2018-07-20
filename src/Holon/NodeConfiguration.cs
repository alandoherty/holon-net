using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents the persistent configuration for a node.
    /// </summary>
    [Serializable]
    public class NodeConfiguration
    {
        /// <summary>
        /// Gets or sets the application id.
        /// </summary>
        public string ApplicationId { get; set; } = "";

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string ApplicationVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the UUID.
        /// </summary>
        public Guid UUID { get; set; }

        /// <summary>
        /// Gets or sets if metrics are enabled.
        /// </summary>
        public bool Metrics { get; set; } = true;
    }
}
