using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Holon.Transports.Amqp.Protocol
{
    /// <summary>
    /// Represents a broker connection.
    /// </summary>
    internal sealed class BrokerContext : IDisposable
    {
        #region Fields
        private bool _disposed;
        private IConnection _connection;

        private Thread _workThread;
        private BufferBlock<WorkItem> _workQueue = new BufferBlock<WorkItem>();
        private CancellationTokenSource _workCancel;
        private List<Broker> _brokers = new List<Broker>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the connection.
        /// </summary>
        public IConnection Connection {
            get {
                return _connection;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new broker context.
        /// </summary>
        /// <param name="endpoint">The AMQP endpoint.</param>
        /// <returns></returns>
        public static async Task<BrokerContext> CreateAsync(string endpoint) {
            // create broker
            BrokerContext ctx = new BrokerContext();

            // amqp factory
            var factory = new ConnectionFactory() { Uri = new Uri(endpoint) };

            // create connection
            ctx._connection = await Task.Run(() => factory.CreateConnection()).ConfigureAwait(false);

            // start
            ctx._workCancel = new CancellationTokenSource();
            ctx._workThread = new Thread(ctx.WorkLoop) {
                Name = "BrokerContext",
                IsBackground = true
            };
            ctx._workThread.Start();
            
            return ctx;
        }

        /// <summary>
        /// Attaches a new broker to the context.
        /// </summary>
        /// <param name="appId">The application ID.</param>
        /// <returns></returns>
        public async Task<Broker> CreateBrokerAsync(string appId) {
            // create a new channel
            IModel channel = await AskWork<IModel>(delegate () {
                return _connection.CreateModel();
            }).ConfigureAwait(false);

            // create broker
            Broker broker = new Broker(this, channel, appId);

            // add to brokers list
            _brokers.Add(broker);

            return broker;
        }

        /// <summary>
        /// Disposes the broker context and underlying transports.
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;
            _disposed = true;

#if DEBUG_DISPOSE
            Debug.WriteLine("> BrokerContext::Dispose");
#endif

            // dispose all brokers
            Broker[] brokersArr = null;

            lock (_brokers) {
                brokersArr = _brokers.ToArray();
            }

            foreach (Broker broker in brokersArr) {
                try {
                    broker.Dispose();
                } catch(Exception) {
#if DEBUG_DISPOSE
                    Debug.WriteLine("! BrokerContext::Dispose: Failed to dispose broker");
#endif
                }
            }

            // cancel work loop
            if (_workCancel != null) {
                // cancel
                try {
                    _workCancel.Cancel();
                } catch (Exception) {
#if DEBUG_DISPOSE
                    Debug.WriteLine("! BrokerContext::Dispose: Failed to cancel work loop");
#endif
                }
            }

            // destroy connection
            if (_connection != null) {
                try {
                    _connection.Dispose();
                } catch(Exception) {
#if DEBUG_DISPOSE
                    Debug.WriteLine("! BrokerContext::Dispose: Failed to dispose connection");
#endif
                }
            }

#if DEBUG_DISPOSE
            Debug.WriteLine("> BrokerContext::Disposed");
#endif
        }

        /// <summary>
        /// Represents the loop used for performing syncronous work.
        /// </summary>
        private async void WorkLoop() {
            while (_connection.IsOpen) {
                // receive a work item
                WorkItem item = null;

                try {
                    item = await _workQueue.ReceiveAsync(_workCancel.Token).ConfigureAwait(false);

                    if (item == null)
                        continue;
                } catch (TaskCanceledException) {
                    return;
                }

                // determine action
                object o = null;

                try {
                    o = item.Action();
                } catch(Exception ex) {
                    // try and find a task to throw the exception for, if not this is bad
                    if (item.TaskSource != null) {
                        item.TaskSource.SetException(ex);
                        continue;
                    } else {
                        Console.Error.Write(ex.ToString());
                    }
                }

                // check for a task, set a generic result since job handlers should return something
                // if they have anything to report back
                if (item.TaskSource != null) {
                    item.TaskSource.SetResult(o);
                }
            }

            // receive all remaining items and cancel
            while (_workQueue.Count > 0) {
                WorkItem item = await _workQueue.ReceiveAsync().ConfigureAwait(false);

                if (item.TaskSource != null)
                    item.TaskSource.SetCanceled();
            }

            // mark as completed
            _workQueue.Complete();
        }

        internal void QueueWork(Func<object> action) {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrokerContext));

            // create work item
            WorkItem workItem = new WorkItem() {
                Action = action
            };

            // post to work queue
            _workQueue.Post(workItem);
        }

        internal async Task<object> AskWork(Func<object> action) {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrokerContext));

            // create completion source
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            // create work item
            WorkItem workItem = new WorkItem() {
                Action = action,
                TaskSource = tcs
            };

            // post to work queue
            await _workQueue.SendAsync(workItem).ConfigureAwait(false);

            return await tcs.Task.ConfigureAwait(false);
        }
        
        internal async Task<T> AskWork<T>(Func<object> action) {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrokerContext));

            object o = await AskWork(action).ConfigureAwait(false);
            return (T)o;
        }
        #endregion

        #region Classes
        /// <summary>
        /// Represents an item for processing on the work thread.
        /// </summary>
        private class WorkItem
        {
            public TaskCompletionSource<object> TaskSource { get; set; }
            public Func<object> Action { get; set; }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Empty constructor, our factory method does all the work.
        /// </summary>
        private BrokerContext() { }
        #endregion
    }
}
