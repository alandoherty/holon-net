using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Holon.Events
{
    /// <summary>
    /// Represents a subscription to an event address for a specific type.
    /// </summary>
    public class Subscription<TData> : IDisposable
    {
        #region Fields
        private Node _node;
        private CancellationTokenSource _readCancel = new CancellationTokenSource();
        private BrokerQueue _queue;
        private int _disposed;
        private EventAddress _address;
        private BufferBlock<TData> _inBuffer = new BufferBlock<TData>();
        #endregion

        #region Fields
        /// <summary>
        /// Gets the event address.
        /// </summary>
        public EventAddress Address {
            get {
                return _address;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the subscription as an observable target.
        /// </summary>
        /// <returns></returns>
        public IObservable<TData> AsObservable() {
            return _inBuffer.AsObservable();
        }

        /// <summary>
        /// Receives asyncronously.
        /// </summary>
        /// <returns>The data.</returns>
        public Task<TData> ReceiveAsync() {
            return _inBuffer.ReceiveAsync();
        }

        /// <summary>
        /// Receives an enve
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task<TData> ReceiveAsync(CancellationToken cancellationToken) {
            return _inBuffer.ReceiveAsync(cancellationToken);
        }

        /// <summary>
        /// Receives an event from the subscription.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        public Task<TData> ReceiveAsync(TimeSpan timeout) {
            return _inBuffer.ReceiveAsync(timeout);
        }

        /// <summary>
        /// Reads the next envelope.
        /// </summary>
        private async void ReadLoop() {
            while (true) {
                // read envelope
                Envelope envelope = null;

                try {
                    envelope = new Envelope(await _queue.ReceiveAsync(_readCancel.Token).ConfigureAwait(false), _node);
                } catch (Exception) {
                    Dispose();
                    return;
                }

                try {
                    // check for header
                    if (!envelope.Headers.ContainsKey(EventHeader.HEADER_NAME)) {
                        continue;
                    }

                    // read header
                    EventHeader header = new EventHeader(Encoding.UTF8.GetString(envelope.Headers[EventHeader.HEADER_NAME] as byte[]));

                    // validate version
                    if (header.Version != "1.0")
                        throw new NotSupportedException("Event version is not supported");

                    // find serializer
                    IEventSerializer serializer = null;

                    if (!EventSerializer.Serializers.TryGetValue(header.Serializer, out serializer))
                        throw new NotSupportedException("Event serializer not supported");

                    // post
                    _inBuffer.Post(serializer.DeserializeEvent(envelope.Body, typeof(TData)).Deserialize<TData>());
#if DEBUG
                } catch (Exception ex) {
                    Console.WriteLine("event", "failed to deserialize event: {0}", ex.Message);
#else
                } catch (Exception) {
#endif
                }

            }
        }

        /// <summary>
        /// Disposes the underlying queue.
        /// </summary>
        public void Dispose() {
            // lock only one disposal
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            // stop read loop
            _inBuffer.Complete();
            _readCancel.Cancel();
            
            // dispose underlying queue
            _queue.Dispose();
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new subscription.
        /// </summary>
        /// <param name="addr">The address.</param>
        /// <param name="node">The node.</param>
        /// <param name="queue">The queue.</param>
        internal Subscription(EventAddress addr, Node node, BrokerQueue queue) {
            _queue = queue;
            _node = node;
            _address = addr;
            ReadLoop();
        }
        #endregion
    }
}
