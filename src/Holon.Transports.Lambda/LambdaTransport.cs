using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Provides a transport for AWS based Lambda functions.
    /// </summary>
    public class LambdaTransport : Transport
    {
        private AmazonLambdaClient _client;
        private MessageFormat _format = MessageFormat.Default;

        /// <summary>
        /// Gets or sets the global message format.
        /// </summary>
        public MessageFormat MessageFormat {
            get {
                return _format;
            } set {
                _format = value;
            }
        }

        /// <summary>
        /// Gets if this transport supports emitting events.
        /// </summary>
        /// <remarks>False.</remarks>
        public override bool CanEmit {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports subscribing to events.
        /// </summary>
        /// <remarks>False.</remarks>
        public override bool CanSubscribe {
            get {
                return false;
            }
        }

        /// <summary>
        /// Gets if this transport supports sending messages.
        /// </summary>
        /// <remarks>True.</remarks>
        public override bool CanSend {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transports supports request/response messages.
        /// </summary>
        /// <remarks>True.</remarks>
        public override bool CanAsk {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets if this transport supports attaching services.
        /// </summary>
        /// <remarks>True.</remarks>
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
        protected override async Task SendAsync(Message message)
        {
            // get message format
            MessageFormat format = MessageFormat;

            // build the context
            MessageContext context = null;

            // build the stream
            MemoryStream bodyStream = new MemoryStream();

            if (format == MessageFormat.Raw || format == MessageFormat.RawContext) {
                bodyStream = new MemoryStream(message.Body);
            } else if (format == MessageFormat.Full) {
                JsonConvert.SerializeObject(new MessageBody() {
                    Body = Convert.ToBase64String(message.Body),
                    Version = 1,
                    Context = context
#if DEBUG
                }, Formatting.None);
#else
                }, Formatting.Indented);
#endif
            } else {
                throw new NotImplementedException("The message format is not implemented");
            }

            // build the invoke request
            InvokeRequest req = new InvokeRequest() {
                FunctionName = message.Address.Key,
                PayloadStream = bodyStream,
                InvocationType = InvocationType.Event
            };

            if (format == MessageFormat.RawContext) {
                req.ClientContext = JsonConvert.SerializeObject(context
#if DEBUG
                , Formatting.None);
#else
                , Formatting.Indented);
#endif
            }


            // invoke the function
            await _client.InvokeAsync(req);
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

        /// <summary>
        /// Creates a new Lambda transport with the specified client.
        /// </summary>
        /// <param name="client">The configured Lambda client.</param>
        public LambdaTransport(AmazonLambdaClient client) {
            _client = client;
        }
    }
}
