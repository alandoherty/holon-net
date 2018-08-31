using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting.Security
{
    /// <summary>
    /// Represents an RPC header.
    /// </summary>
    internal sealed class RpcSecureHeader
    {
        #region Constants
        internal const string HEADER_NAME = "X-Secure-RPC";
        internal const string HEADER_VERSION = "1.0";
        #endregion

        #region Fields
        private RpcSecureMessageType _type;
        private string _version;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the message type.
        /// </summary>
        public RpcSecureMessageType Type {
            get {
                return _type;
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

            foreach (string pair in keyPairs) {
                // get keypair
                string key = pair.Substring(0, pair.IndexOf('='));
                string val = pair.Substring(pair.IndexOf('=') + 1);

                if (key.Length == 0 || val.Length == 0)
                    continue;

                // lazily check values
                if (key.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                    _version = val;
                else if (key.Equals("t", StringComparison.CurrentCultureIgnoreCase)) {
                    if (val.Equals(nameof(RpcSecureMessageType.RequestCertificate), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RequestCertificate;
                    else if (val.Equals(nameof(RpcSecureMessageType.RespondCertificate), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RespondCertificate;
                    else if (val.Equals(nameof(RpcSecureMessageType.RequestKey), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RequestKey;
                    else if (val.Equals(nameof(RpcSecureMessageType.RespondKey), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RespondKey;
                    else if (val.Equals(nameof(RpcSecureMessageType.Error), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.Error;
                    else if (val.Equals(nameof(RpcSecureMessageType.RequestMessage), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RequestMessage;
                    else if (val.Equals(nameof(RpcSecureMessageType.RespondMessage), StringComparison.CurrentCultureIgnoreCase))
                        _type = RpcSecureMessageType.RespondMessage;
                    else
                        throw new NotImplementedException("The secure RPC message type is unsupported");
                }
            }

            if (_version == null)
                throw new Exception("The secure RPC message header did not specify a version and a serializer");
        }

        /// <summary>
        /// Converts this header into it's string representation.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return string.Format("v={0};t={1}", _version, _type.ToString().ToLower());
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new header parsed from the provided input.
        /// </summary>
        /// <param name="input">The input.</param>
        public RpcSecureHeader(string input) {
            InternalParse(input);
        }

        /// <summary>
        /// Creates a new header from the provided component data.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <param name="type">The message type.</param>
        public RpcSecureHeader(string version, RpcSecureMessageType type) {
            _type = type;
            _version = version;
        }
        #endregion
    }
}
