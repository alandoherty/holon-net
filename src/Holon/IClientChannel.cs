using Holon.Remoting;
using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon
{
    /// <summary>
    /// Defines an interface for temprorary state-based channels of communication.
    /// </summary>
    public interface IClientChannel
    {
        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
        Task SendAsync(byte[] body, IDictionary<string, object> headers = null);

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        Task<Envelope> AskAsync(byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Broadcasts the envelope message to the provided service address and waits for any responses.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The responses.</returns>
        Task<Envelope[]> BroadcastAsync(byte[] body, TimeSpan timeout, IDictionary<string, object> headers = null, CancellationToken cancellationToken = default(CancellationToken));

        // BatchedProxy BatchedProxy(ServiceAddress address);
        // BatchedProxy BatchedProxy(string address);
        // BatchedProxy<IT> BatchedProxy<IT>(ServiceAddress address);
        // BatchedProxy<IT> BatchedProxy<IT>(string address);

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <returns>The proxied interface.</returns>
        IT Proxy<IT>();

        /// <summary>
        /// Gets an RPC proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        IT Proxy<IT>(ProxyConfiguration configuration);

        /// <summary>
        /// Reset the channel state and set the node and target address.
        /// Called once after instantiation can optionally be called later depending on implementation.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The address.</param>
        void Reset(Node node, ServiceAddress address);

        /// <summary>
        /// Gets the node.
        /// </summary>
        Node Node { get; }

        /// <summary>
        /// Gets the service address.
        /// </summary>
        ServiceAddress ServiceAddress { get; }

        /// <summary>
        /// Gets if this channel is encrypted and can be used for sensitive communications.
        /// </summary>
        bool IsEncrypted { get; }
    }
}
