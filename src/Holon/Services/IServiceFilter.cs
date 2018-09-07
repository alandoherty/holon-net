using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Services
{
    /// <summary>
    /// Defines an interface for service filters.
    /// </summary>
    public interface IServiceFilter
    {
        /// <summary>
        /// Handles an incoming envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns>If the envelope should continue to be processed.</returns>
        Task<bool> HandleAsync(Envelope envelope);
    }
}
