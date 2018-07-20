using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Introspection
{
    /// <summary>
    /// Represents a node tag.
    /// </summary>
    [Serializable]
    public class NodeTagInformation
    {
        /// <summary>
        /// Gets or sets the tag name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the tag value.
        /// </summary>
        public string Value { get; set; }
    }
}
