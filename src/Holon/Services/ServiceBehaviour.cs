using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Services
{
    /// <summary>
    /// Represents service behaviour.
    /// </summary>
    public abstract class ServiceBehaviour
    {
        #region Properties
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        public ServiceConfiguration Configuration { get; internal set; }
        #endregion

        #region Methods
        /// <summary>
        /// Handles an incoming envelope asyncronously.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        public abstract Task HandleAsync(Envelope envelope);
        #endregion

        /// <summary>
        /// Creates a new service behaviour.
        /// </summary>
        protected ServiceBehaviour() { }
    }
}
