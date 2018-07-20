using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event.
    /// </summary>
    internal class Event
    {
        #region Fields
        private string _name;
        private object _data;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name {
            get {
                return _name;
            }
        }

        /// <summary>
        /// Gets the data.
        /// </summary>
        public object Data {
            get {
                return _data;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new event.
        /// </summary>
        /// <param name="name">The event name.</param>
        /// <param name="data">The data.</param>
        internal Event(string name, object data) {
            _name = name;
            _data = data;
        }
        #endregion
    }
}
