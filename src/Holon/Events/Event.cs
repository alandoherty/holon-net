using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    public class Event
    {
        #region Constants
        private const string HeaderId = "X-Holon-ID";
        #endregion

        #region Fields
        private EventAddress _addr;
        private object _data;
        private DateTimeOffset _timestamp;
        private IDictionary<string, string> _headers;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the optional identifier.
        /// </summary>
        public string Id {
            get {
                if (_headers.TryGetValue(HeaderId, out string val))
                    return val;
                else
                    return null;
            }
        }

        /// <summary>
        /// Gets if this event contains an ID.
        /// </summary>
        public bool HasId {
            get {
                return _headers.ContainsKey(HeaderId);
            }
        }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers {
            get {
                return (IReadOnlyDictionary<string, string>)_headers;
            }
        }

        /// <summary>
        /// Gets the address.
        /// </summary>
        public EventAddress Address {
            get {
                return _addr;
            }
        }

        /// <summary>
        /// Gets the raw payload.
        /// </summary>
        public object Data {
            get {
                return _data;
            }
        }

        /// <summary>
        /// Gets the timestamp.
        /// </summary>
        public DateTimeOffset Timestamp {
            get {
                return _timestamp;
            }
        }
        #endregion

        #region Methods
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new event.
        /// </summary>
        /// <param name="addr">The event address.</param>
        /// <param name="headers">The headers.</param>
        /// <param name="data">The data.</param>
        public Event(EventAddress addr, IDictionary<string, string> headers, object data) {
            _addr = addr;
            _headers = headers;
            _data = data;
            _timestamp = DateTime.UtcNow;
        }
        #endregion
    }
}
