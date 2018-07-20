using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Events
{
    /// <summary>
    /// Represents an event header.
    /// </summary>
    internal class EventHeader
    {
        #region Constants
        internal const string HEADER_NAME = "X-Event";
        internal const string HEADER_VERSION = "1.0";
        #endregion

        #region Fields
        private string _serializer;
        private string _version;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the version.
        /// </summary>
        public string Version {
            get {
                return _version;
            }
        }

        /// <summary>
        /// Gets the serializer.
        /// </summary>
        public string Serializer {
            get {
                return _serializer;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// The internal parse function for an RPC header.
        /// </summary>
        /// <param name="input">The input string.</param>
        internal void InternalParse(string input) {
            // split key pairs
            string[] keyPairs = input.Split(';');

            foreach (string pair in keyPairs) {
                // get keypair
                string key = pair.Substring(0, pair.IndexOf('='));
                string val = pair.Substring(pair.IndexOf('=') + 1);

                if (key.Length == 0 || val.Length == 0)
                    continue;

                // lazily check values
                if (key.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                    _version = val;
                else if (key.Equals("s", StringComparison.CurrentCultureIgnoreCase))
                    _serializer = val;
            }

            if (_version == null || _serializer == null)
                throw new Exception("The event message header did not specify a version or serializer");
        }

        /// <summary>
        /// Converts this header into it's string representation.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("v={0};s={1}", _version, _serializer);
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new header parsed from the provided input.
        /// </summary>
        /// <param name="input">The input.</param>
        public EventHeader(string input) {
            InternalParse(input);
        }

        /// <summary>
        /// Creates a new header from the provided component data.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <param name="serializer">The serializer.</param>
        public EventHeader(string version, string serializer) {
            _version = version;
            _serializer = serializer;
        }
        #endregion
    }
}
