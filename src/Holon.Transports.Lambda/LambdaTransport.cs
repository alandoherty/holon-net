using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        private void ApplyInvoke(Message message, InvokeRequest req) {
            // get message format
            MessageFormat format = MessageFormat;

            // build the context
            MessageContext context = new MessageContext {
                Headers = message.Headers ?? new Dictionary<string, string>()
            };

            // build the stream
            MemoryStream bodyStream = null;

            if (format == MessageFormat.Raw) {
                bodyStream = new MemoryStream(message.Body);
            } else if (format == MessageFormat.Full) {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new MessageBody() {
                    Body = Convert.ToBase64String(message.Body),
                    Version = 1,
                    Context = context
#if DEBUG
                }, Formatting.None));
#else
                }, Formatting.Indented));
#endif

                bodyStream = new MemoryStream(bodyBytes);
            } else {
                throw new NotImplementedException("The message format is not implemented");
            }

            // add the client context is required
            if (format == MessageFormat.Raw) {
                req.ClientContext = JsonConvert.SerializeObject(context
#if DEBUG
                , Formatting.None);
#else
                , Formatting.Indented);
#endif
            }
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="message">The message</param>
        /// <returns></returns>
        protected override Task SendAsync(Message message)
        {
            // build the invoke request
            InvokeRequest req = new InvokeRequest() {
                FunctionName = message.Address.Key,
                InvocationType = InvocationType.Event
            };

            // applies the message to the invocation request
            ApplyInvoke(message, req);

            // invoke the function
            return _client.InvokeAsync(req);
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        protected override async Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            // build the invoke request
            InvokeRequest req = new InvokeRequest() {
                FunctionName = message.Address.Key,
                InvocationType = InvocationType.RequestResponse
            };

            // applies the message to the invocation request
            ApplyInvoke(message, req);

            // invoke the function
            InvokeResponse res = await _client.InvokeAsync(req);
            string payload = Encoding.UTF8.GetString(res.Payload.ToArray());

            if (res.FunctionError != null) {
                // deserialize
                LambdaError err = JsonConvert.DeserializeObject<LambdaError>(payload);

                throw new Exception($"Exception occured in function, RequestId: {res.ResponseMetadata.RequestId} Message: {err.ErrorMessage}");
            }

            // process the response payload
            throw new NotImplementedException();
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
