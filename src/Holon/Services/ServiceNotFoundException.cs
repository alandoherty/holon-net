using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Represents an exception which occurs when a service cannot be found.
    /// </summary>
    public class ServiceNotFoundException : Exception
    {
        /// <summary>
        /// Creates a new service not found exception.
        /// </summary>
        public ServiceNotFoundException() {
        }

        /// <summary>
        /// Creates a new service not found exception.
        /// </summary>
        /// <param name="message">The message.</param>
        public ServiceNotFoundException(string message) : base(message) {
        }

        /// <summary>
        /// Creates a new service not found exception.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServiceNotFoundException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
