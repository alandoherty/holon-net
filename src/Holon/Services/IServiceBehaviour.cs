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
        /// Handles the incoming envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        void Handle(Envelope envelope);
    }
}
