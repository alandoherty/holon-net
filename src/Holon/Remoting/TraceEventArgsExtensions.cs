using Holon.Metrics.Tracing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Provides extensions for <see cref="TraceEventArgs"/>.
    /// </summary>
    public static class TraceEventArgsExtensions
    {
        /// <summary>
        /// Tries to get an <see cref="RpcTrace"/> from the trace, will return false if the trace is not a valid RPC message.
        /// </summary>
        /// <param name="e">The event args.</param>
        /// <param name="data">The output trace.</param>
        /// <returns>If the trace was extracted.</returns>
        public static bool TryAsRpcTrace(this TraceEventArgs e, out RpcTrace data)
        {
            // extract the rpc header from the envelope, if we can't find it we return false
            if (!e.Envelope.Headers.TryGetValue(RpcHeader.HEADER_NAME, out string rpcHeaderStr))
            {
                data = null;
                return false;
            }

            // parse the header
            RpcHeader rpcHeader = null;

            try
            {
                rpcHeader = new RpcHeader(rpcHeaderStr);
            } catch(Exception)  {
                // if we can't parse we silently fail, not ideal but this should never happen
                data = null;
                return false;
            }

            // build the trace
            data = new RpcTrace(rpcHeader);
            return true;
        }
    }
}
