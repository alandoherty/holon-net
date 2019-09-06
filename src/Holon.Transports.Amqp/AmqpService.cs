using Holon.Services;
using Holon.Transports.Amqp.Protocol;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Holon.Transports.Amqp
{
    /// <summary>
    /// Provides a AMQP service implementation.
    /// </summary>
    public sealed class AmqpService : Service, IObserver<InboundMessage>
    {
        private Broker _broker;
        private BrokerQueue _queue;

        /// <summary>
        /// Creates the queue and internal consumer for this service.
        /// </summary>
        /// <param name="broker">The broker.</param>
        /// <exception cref="InvalidOperationException">If the queue already exists.</exception>
        /// <returns></returns>
        internal async Task SetupAsync(Broker broker) {
            // check if queue has already been created
            if (_queue != null)
                throw new InvalidOperationException("The broker queue has already been created");

            // set broker
            _broker = broker;

            // create queue
            _broker.DeclareExchange(Address.Namespace, "topic", true, false);

            if (Type == ServiceType.Singleton) {
                // declare one exclusive queue
                _queue = await _broker.CreateQueueAsync(Address.ToString(), false, true, Address.Namespace, Address.Key).ConfigureAwait(false);
            } else if (Type == ServiceType.Fanout) {
                // declare queue with unique name
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                    // get unique string
                    byte[] uniqueId = new byte[20];
                    rng.GetBytes(uniqueId);
                    string uniqueIdStr = BitConverter.ToString(uniqueId).Replace("-", "").ToLower();

                    _queue = await _broker.CreateQueueAsync(string.Format("{0}%{1}", Address.ToString(), uniqueIdStr), false, false, Address.Namespace, Address.Key).ConfigureAwait(false);
                }
            } else if (Type == ServiceType.Balanced) {
                // declare one queue shared between many
                _queue = await _broker.CreateQueueAsync(Address.ToString(), false, false, Address.Namespace, Address.Key).ConfigureAwait(false);
            }

            // begin observing
            _queue.AsObservable().Subscribe(this);
        }

        void IObserver<InboundMessage>.OnCompleted()
        {
        }

        void IObserver<InboundMessage>.OnError(Exception error)
        {
        }

        async void IObserver<InboundMessage>.OnNext(InboundMessage value)
        {


            // acknowledge the message
            _broker.Context.QueueWork(() => {
                _broker.Channel.BasicAck(envelope.Message.DeliveryTag, false);
                return null;
            });
        }

        internal AmqpService(Transport transport, ServiceAddress addr, ServiceBehaviour behaviour, ServiceConfiguration configuration) : base(transport, addr, behaviour, configuration) {
        }
    }
}
