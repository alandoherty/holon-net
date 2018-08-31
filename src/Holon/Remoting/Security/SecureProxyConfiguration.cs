using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents configuration for a secure proxy.
    /// </summary>
    public class SecureProxyConfiguration : ProxyConfiguration
    {
        /// <summary>
        /// Gets the root authority to validate the certificate during negociation.
        /// </summary>
        public X509Certificate2 RootAuthority { get; set; }
    }
}
