using System;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Lambda
{
    public class LambdaTransport
    {
        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns></returns>
        public Task SendAsync(Message message)
        {

        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {

        }

        /// <summary>
        /// Broadcasts the envelope message to the provided service address and waits for any responses.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The responses.</returns>
        public Task<Envelope[]> BroadcastAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {

        }
    }
}
