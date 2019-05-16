using System;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Lambda
{
    /// <summary>
    /// Provides a transport for AWS based Lambda functions.
    /// </summary>
    public class LambdaTransport : Transport
    {
        /// <summary>
        /// Gets if this transport supports emitting events.
        /// </summary>
        public override bool CanEmit {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports subscribing to events.
        /// </summary>
        public override bool CanSubscribe {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports sending messages.
        /// </summary>
        public override bool CanSend {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transports supports request/response messages.
        /// </summary>
        public override bool CanAsk {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports attaching services.
        /// </summary>
        public override bool CanAttach {
            get {
                return true;
            }
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns></returns>
        public async Task SendAsync(Message message)
        {

        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            return null;
        }

        /// <summary>
        /// Broadcasts the envelope message to the provided service address and waits for any responses.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The responses.</returns>
        public async Task<Envelope[]> BroadcastAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            return null;
        }
    }
}
