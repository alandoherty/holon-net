using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents configuration for a namespace connection.
    /// </summary>
    public class NamespaceEndpoint
    {
        #region Fields
        private string _name;
        private Uri _connectionUri;
        #endregion

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the connection URI.
        /// </summary>
        public Uri ConnectionUri {
            get {
                return _connectionUri;
            }
        }

        #region Constructors
        /// <summary>
        /// Creates a new namespace configuration.
        /// </summary>
        /// <param name="name">The namespace, supports wildcards.</param>
        /// <param name="connectionUri">The connection URI.</param>
        public NamespaceEndpoint(string name, Uri connectionUri) {
            _name = name;
            _connectionUri = connectionUri;
        }

        /// <summary>
        /// Creates a new namespace configuration.
        /// </summary>
        /// <param name="name">The namespace, supports wildcards.</param>
        /// <param name="connectionUri">The connection URI.</param>
        public NamespaceEndpoint(string name, string connectionUri) 
            : this(name, new Uri(connectionUri)) {
        }
        #endregion
    }
}
