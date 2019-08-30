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
        /// <param name="message">The message</param>
        /// <returns></returns>
        Task SendAsync(Message message);

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken));

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
