using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Amqp.Protocol
{
    /// <summary>
    /// Provides data for a Broker.Shutdown event.
    /// </summary>
    public class BrokerShutdownEventArgs : EventArgs
    {
        #region Fields
        private string _message;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the message for the shutdown.
        /// </summary>
        public string Message {
            get {
                return _message;
            }
        }
        #endregion

        #region Constructors
        internal BrokerShutdownEventArgs(string message) {
            _message = message;
        }
        #endregion
    }
}
