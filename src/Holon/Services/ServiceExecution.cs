using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Services
{
    /// <summary>
    /// Defines the possible execution strategies for services.
    /// </summary>
    public enum ServiceExecution
    {
        /// <summary>
        /// Executes service behaviours in serial, useful for ensuring safe access.
        /// </summary>
        Serial = 0,

        /// <summary>
        /// Executes service behaviours in parallel.
        /// </summary>
        Parallel
    }
}
