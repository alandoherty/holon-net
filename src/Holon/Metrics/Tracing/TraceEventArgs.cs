using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Metrics.Tracing
{
    /// <summary>
    /// Represents event arguments for the beginning of a trace.
    /// </summary>
    public class TraceEventArgs
    {
        #region Properties
        /// <summary>
        /// Gets the envelope.
        /// </summary>
        public Envelope Envelope { get; private set; }

        /// <summary>
        /// Gets the node.
        /// </summary>
        public Node Node {
            get {
                return Envelope.Node;
            }
        }

        /// <summary>
        /// Gets the trace ID.
        /// </summary>
        public string TraceId {
            get {
                return Envelope.TraceId;
            }
        }

        /// <summary>
        /// Gets the service which is handling the request.
        /// </summary>
        public Service Service { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new trace begin event arguments.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <param name="service">The service.</param>
        public TraceEventArgs(Envelope envelope, Service service) {
            Envelope = envelope;
            Service = service;
        }
        #endregion
    }
}
