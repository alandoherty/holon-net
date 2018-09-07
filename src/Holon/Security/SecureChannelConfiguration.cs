using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Holon.Security
{
    /// <summary>
    /// Represents configuration for a secure channel.
    /// </summary>
    public class SecureChannelConfiguration
    {
        /// <summary>
        /// Gets the root authority to validate the certificate during negociation.
        /// </summary>
        public X509Certificate2 RootAuthority { get; set; }

        /// <summary>
        /// Gets or sets if the certificate should be validated.
        /// This should ALWAYS be true in production.
        /// </summary>
        public bool ValidateAuthority { get; set; } = true;

        /// <summary>
        /// Gets or sets if the certificate address should be validated.
        /// This should ALWAYS be true in production.
        /// </summary>
        public bool ValidateAddress { get; set; } = true;

        /// <summary>
        /// Gets or sets the duration to wait for a handshake.
        /// </summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    }
}
