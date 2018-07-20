using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents information about a remote node.
    /// </summary>
    [Serializable]
    public class NodeInformation
    {
        #region Properties
        /// <summary>
        /// Gets or sets the application ID.
        /// </summary>
        public string ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string ApplicationVersion { get; set; }

        /// <summary>
        /// Gets or sets the UUID.
        /// </summary>
        public Guid UUID { get; set; }

        /// <summary>
        /// Gets or sets the number of services.
        /// </summary>
        public int ServiceCount { get; set; }

        /// <summary>
        /// Gets or sets the extended tag information.
        /// </summary>
        public NodeTagInformation[] Tags { get; set; }
        #endregion
    }
}
