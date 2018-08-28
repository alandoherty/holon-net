using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Services
{
    /// <summary>
    /// Represents service behaviour.
    /// </summary>
    public interface IServiceBehaviour
    {
        /// <summary>
        /// Handles an incoming envelope asyncronously.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        Task HandleAsync(Envelope envelope);
    }
}
