using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon
{
    /// <summary>
    /// Represents a result of a routing rule.
    /// </summary>
    public struct RoutingResult
    {
        /// <summary>
        /// A routing result which does not match.
        /// </summary>
        public static readonly RoutingResult NoMatch = new RoutingResult();

        /// <summary>
        /// Gets or sets if the route matched.
        /// </summary>
        public bool Matched { get; set; }

        /// <summary>
        /// Gets or sets the optional translated address.
        /// </summary>
        public ServiceAddress TranslatedAddress { get; set; }

        /// <summary>
        /// Gets or sets the transport.
        /// </summary>
        public Transport Transport { get; set; }

        /// <summary>
        /// Creates a new matched routing result.
        /// </summary>
        /// <param name="transport">The transport.</param>
        public RoutingResult(Transport transport)
        {
            Matched = true;
            TranslatedAddress = null;
            Transport = transport;
        }

        /// <summary>
        /// Creates a new matched routing result.
        /// </summary>
        /// <param name="transport">The transport.</param>
        /// <param name="translatedAddress">The translated service address to pass to the transport.</param>
        public RoutingResult(Transport transport, ServiceAddress translatedAddress)
        {
            Matched = true;
            TranslatedAddress = translatedAddress;
            Transport = transport;
        }
    }
}
