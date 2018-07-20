using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Represents an RPC header.
    /// </summary>
    internal class RpcHeader
    {
        #region Constants
        internal const string HEADER_NAME = "X-RPC";
        internal const string HEADER_VERSION = "1.0";
        #endregion

        #region Fields
        private RpcMessageType _type;
        private string _serializer;
        private string _version;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the message type.
        /// </summary>
        public RpcMessageType Type {
            get {
                return _type;
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

        /// <summary>
        /// Gets the version.
        /// </summary>
        public string Version {
            get {
                return _version;
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

            foreach(string pair in keyPairs) {
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
                else if (key.Equals("t", StringComparison.CurrentCultureIgnoreCase)) {
                    if (val.Equals("single", StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcMessageType.Single;
                    else if (val.Equals("batch", StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcMessageType.Batch;
                    else
                        throw new NotImplementedException("The RPC message type is unsupported");
                }
            }

            if (_version == null || _serializer == null)
                throw new Exception("The RPC message header did not specify a version and a serializer");
        }

        /// <summary>
        /// Converts this header into it's string representation.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("v={0};s={1};t={2}", _version, _serializer, _type.ToString().ToLower());
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new header parsed from the provided input.
        /// </summary>
        /// <param name="input">The input.</param>
        public RpcHeader(string input) {
            InternalParse(input);
        }

        /// <summary>
        /// Creates a new header from the provided component data.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <param name="serializer">The serializer used.</param>
        /// <param name="type">The message type.</param>
        public RpcHeader(string version, string serializer, RpcMessageType type) {
            _type = type;
            _version = version;
            _serializer = serializer;
        }
        #endregion
    }
}
