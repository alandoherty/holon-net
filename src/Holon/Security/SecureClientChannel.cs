using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Holon.Remoting;
using Holon.Services;
using ProtoBuf;

namespace Holon.Security
{
    /// <summary>
    /// Represents a secure encrypted channel with man-in-middle protection.
    /// </summary>
    public class SecureClientChannel : IClientChannel
    {
        #region Fields
        private SecureChannelConfiguration _configuration;
        private X509Certificate2 _serverCertificate;

        private byte[] _handshakeEncryptionKey;
        private byte[] _handshakeEncryptionIV;

        private byte[] _serverNonce;
        private byte[] _serverEncryptionKey;
        private long _serverEncryptionKeyTimeSlot;

        private SemaphoreSlim _handshakeSemaphore = new SemaphoreSlim(1, 1);

        private Node _node;
        private ServiceAddress _address;
        #endregion

        #region Properties
        /// <summary>
        /// Gets if the channel is encrypted, secure channels always are.
        /// </summary>
        public bool IsEncrypted {
            get {
                return true;
            }
        }

        /// <summary>
        /// Gets the node.
        /// </summary>
        public Node Node {
            get {
                return _node;
            }
        }

        /// <summary>
        /// Gets the service address.
        /// </summary>
        public ServiceAddress ServiceAddress {
            get {
                return _address;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Generates a new handshake key.
        /// </summary>
        private void GenerateHandshakeKey() {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                _handshakeEncryptionKey = new byte[16];
                _handshakeEncryptionIV = new byte[16];

                rng.GetBytes(_handshakeEncryptionKey);
                rng.GetBytes(_handshakeEncryptionIV);
            }
        }

        /// <summary>
        /// Performs the secure handshake so we can start sending messages.
        /// </summary>
        /// <returns></returns>
        private async Task HandshakeAsync() {
            // wait on semaphore
            await _handshakeSemaphore.WaitAsync();

            // check if we got raced
            if (_serverEncryptionKey != null && !SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false))
                return;

            try {
                // create client key
                GenerateHandshakeKey();

                // get certificate if we don't have it yet
                if (_serverCertificate == null) {
                    // log
#if DEBUG_SECURE
                    Console.WriteLine($"[Secure] Handshake Requesting certificate...");
#endif

                    // request certificate 
                    SecureHeader requestCertificate = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RequestCertificate);

                    // send request
                    Envelope respondCert = await _node.AskAsync(_address, new byte[0], _configuration.HandshakeTimeout, new Dictionary<string, string>() {
                        { SecureHeader.HeaderName, requestCertificate.ToString() }
                    });

                    // parse response header
                    SecureHeader respondCertHeader = null;

                    try {
                        respondCertHeader = new SecureHeader(respondCert.Headers[SecureHeader.HeaderName]);
                    } catch (Exception ex) {
                        throw new InvalidDataException("The certificate request response header was invalid", ex);
                    }

                    // check if the certificate response is an error or if it's an incorrect type
                    if (respondCertHeader.Type == SecureMessageType.Error) {
                        SecureErrorMsg errorMsg = respondCert.AsProtoBuf<SecureErrorMsg>();

                        throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
                    } else if (respondCertHeader.Type != SecureMessageType.RespondCertificate) {
                        throw new InvalidDataException("The certificate request response header was invalid");
                    }

                    // decode the certificate response and then check it has actual data
                    SecureRespondCertificateMsg respondCertMsg = respondCert.AsProtoBuf<SecureRespondCertificateMsg>();

                    if (respondCertMsg.CertificateData == null)
                        throw new InvalidDataException("The certificate request response was invalid");

                    // load certificate from response
                    X509Certificate2 cert = new X509Certificate2(respondCertMsg.CertificateData);

                    // validate it's allowed to act as this service
                    if (_configuration.ValidateAddress) {
                        // check extension is actually there
                        if (cert.Extensions["HolonSecureServices"] == null)
                            throw new InvalidDataException("The service certificate has no claim for the invoked operation");

                        // parse extension
                        X509Extension servicesExt = cert.Extensions["HolonSecureServices"];

                    } else {
#if DEBUG_SECURE
                        Console.WriteLine($"[Secure] Handshake WARNING: Not validating services due to configuration!");
#endif
                    }

                    // validate that it's signed by ca authority
                    if (_configuration.ValidateAuthority) {
                        X509Chain chain = new X509Chain();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.ChainPolicy.ExtraStore.Add(_configuration.RootAuthority);
                        chain.Build(cert);

                        // get status
                        X509ChainStatus status = chain.ChainStatus.First();

                        if (status.Status != X509ChainStatusFlags.UntrustedRoot && status.Status != X509ChainStatusFlags.NoError)
                            throw new InvalidDataException("The service certificate is not signed by the root authority");
                    } else {
#if DEBUG_SECURE
                        Console.WriteLine($"[Secure] Handshake WARNING: Not validating authority due to configuration!");
#endif
                    }

                    // log
#if DEBUG_SECURE
                    Console.WriteLine($"[Secure] Handshake Got certifcate {cert.Subject} ({cert.Thumbprint})");
#endif

                    _serverCertificate = cert;
                }

                // log
#if DEBUG_SECURE
                Console.WriteLine($"[Secure] Handshake Requesting key... FirstTime: {_serverEncryptionKey == null}");
#endif

                // key request
                SecureRequestKeyMsg requestKeyMsg = new SecureRequestKeyMsg() {
                    HandshakeIV = _handshakeEncryptionIV,
                    HandshakeKey = _handshakeEncryptionKey
                };
                SecureHeader requestKey = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RequestKey);

                // send request
                Envelope respondKey = null;

                using (RSA rsa = _serverCertificate.GetRSAPublicKey()) {
                    using (MemoryStream ms = new MemoryStream()) {
                        // serialize to stream
                        Serializer.Serialize(ms, requestKeyMsg);

                        // encrypt with server certificate
                        byte[] keyRequestBody = rsa.Encrypt(ms.ToArray(), RSAEncryptionPadding.Pkcs1);

                        respondKey = await _node.AskAsync(_address, keyRequestBody, _configuration.HandshakeTimeout, new Dictionary<string, string>() {
                            { SecureHeader.HeaderName, requestKey.ToString() }
                        });
                    }
                }

                // parse response
                SecureHeader respondKeyHeader = null;

                try {
                    respondKeyHeader = new SecureHeader(respondKey.Headers[SecureHeader.HeaderName]);
                } catch (Exception ex) {
                    throw new InvalidDataException("The key request response header was invalid", ex);
                }

                // check if the key response is an error or if it's an incorrect type
                if (respondKeyHeader.Type == SecureMessageType.Error) {
                    SecureErrorMsg errorMsg = respondKey.AsProtoBuf<SecureErrorMsg>();

                    throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
                } else if (respondKeyHeader.Type != SecureMessageType.RespondKey) {
                    throw new InvalidDataException("The key request response header was invalid");
                }

                // try and decrypt
                using (MemoryStream decryptedStream = new MemoryStream()) {
                    using (Aes aes = Aes.Create()) {
                        aes.Key = _handshakeEncryptionKey;
                        aes.IV = _handshakeEncryptionIV;

                        using (CryptoStream decryptStream = new CryptoStream(respondKey.AsStream(), aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                            decryptStream.CopyTo(decryptedStream);
                        }
                    }

                    // seek to beginning
                    decryptedStream.Seek(0, SeekOrigin.Begin);

                    // deserialize
                    SecureRespondKeyMsg respondKeyMsg = Serializer.Deserialize<SecureRespondKeyMsg>(decryptedStream);

                    // validate key
                    if (respondKeyMsg.ServerKey == null || respondKeyMsg.ServerNonce == null || respondKeyMsg.ServerKey.Length != 16)
                        throw new InvalidDataException("The secure key is invalid");

                    // log
#if DEBUG_SECURE
                    Console.WriteLine($"[Secure] Handshake Got key Timeslot: {respondKeyMsg.KeyTimeSlot} Nonce: {BitConverter.ToString(respondKeyMsg.ServerNonce).Replace("-", "")}");
#endif

                    // set server encryption key
                    _serverEncryptionKey = respondKeyMsg.ServerKey;
                    _serverNonce = respondKeyMsg.ServerNonce;
                    _serverEncryptionKeyTimeSlot = respondKeyMsg.KeyTimeSlot;
                }
            } finally {
                // make sure to release semaphore no matter what
                _handshakeSemaphore.Release();
            }
        }

        /// <summary>
        /// Encrypts the body with the current keys.
        /// </summary>
        /// <param name="body">The raw body.</param>
        /// <returns>The encrypted body.</returns>
        private byte[] EncryptBody(byte[] body) {
            using (MemoryStream outputStream = new MemoryStream()) {
                // encrypt
                using (MemoryStream inputStream = new MemoryStream(body)) {
                    using (Aes aes = Aes.Create()) {
                        aes.Key = _serverEncryptionKey;
                        aes.IV = _serverNonce;

                        using (CryptoStream decryptStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                            inputStream.CopyTo(decryptStream);
                        }
                    }
                }

                // get payload
                byte[] payload = outputStream.ToArray();

                // build message
                using (MemoryStream msgStream = new MemoryStream()) {
                    Serializer.Serialize<SecureMessageMsg>(msgStream, new SecureMessageMsg() {
                        KeyTimeSlot = _serverEncryptionKeyTimeSlot,
                        Payload = payload,
                        ServerNonce = _serverNonce
                    });

                    return msgStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Creates a secure proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <returns>The proxy.</returns>
        public IT Proxy<IT>() {
            return Proxy<IT>(new ProxyConfiguration() { });
        }

        /// <summary>
        /// Creates a secuure proxy for the provided interface.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The proxy.</returns>
        public IT Proxy<IT>(ProxyConfiguration configuration) {
            // check type is interface
            TypeInfo typeInfo = typeof(IT).GetTypeInfo();

            if (!typeInfo.IsInterface)
                throw new InvalidOperationException("A static RPC proxy must be derived from an interface");

            // get contract attribute
            RpcContractAttribute contractAttr = typeInfo.GetCustomAttribute<RpcContractAttribute>();

            if (contractAttr == null)
                throw new InvalidOperationException("A static RPC proxy must be decorated with a contract attribute");

            // create proxy
            IT proxy = DispatchProxy.Create<IT, RpcProxy<IT>>();
            RpcProxy<IT> rpcProxy = (RpcProxy<IT>)(object)proxy;

            rpcProxy.Channel = this;
            rpcProxy.Configuration = configuration;

            return proxy;
        }

        /// <summary>
        /// Sends the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope> AskAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // perform handshake if we don't have our key yet or it has expired
            if (_serverEncryptionKey == null || SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)) {
#if DEBUG_SECURE
                Console.WriteLine($"[Secure] InvokeOperation KeyNull: {_serverEncryptionKey == null} Expired: {SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)}");
#endif

                // perform handshake (partial if required)
                await HandshakeAsync();
            }

            // add secure header
            if (message.Headers == null)
                message.Headers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            message.Headers[SecureHeader.HeaderName] = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RequestMessage).ToString();
            message.Address = _address;
            message.Body = EncryptBody(message.Body);

            // send the envelope and wait for the response
            Envelope response = await _node.AskAsync(message, timeout, cancellationToken);

            if (!response.Headers.ContainsKey(SecureHeader.HeaderName))
                throw new InvalidDataException("The secure service sent an invalid response");

            // decode header and take action depending on the response
            SecureHeader header = new SecureHeader(response.Headers[SecureHeader.HeaderName]);

            if (header.Type == SecureMessageType.RespondMessage) {
                using (MemoryStream outputStream = new MemoryStream()) {
                    // decrypt
                    using (MemoryStream inputStream = new MemoryStream(response.Data)) {
                        using (Aes aes = Aes.Create()) {
                            aes.Key = _serverEncryptionKey;
                            aes.IV = _serverNonce;

                            using (CryptoStream decryptStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                                decryptStream.CopyTo(outputStream);
                            }
                        }
                    }

                    //response.Data = outputStream.ToArray();
                    return response;
                }
            } else if (header.Type == SecureMessageType.Error) {
                // deserialize
                SecureErrorMsg errorMsg = null;

                try {
                    errorMsg = response.AsProtoBuf<SecureErrorMsg>();
                } catch (Exception ex) {
                    throw new InvalidDataException("The secure service sent an invalid error respsonse", ex);
                }

                throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
            } else {
                throw new InvalidDataException($"The secure service sent an invalid response ({header.Type})");
            }
        }

        /// <summary>
        /// Sends the envelope message to the provided service address.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns></returns>
        public async Task SendAsync(Message message) {
            // perform handshake if we don't have our key yet or it has expired
            if (_serverEncryptionKey == null || SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)) {
#if DEBUG_SECURE
                Console.WriteLine($"[Secure] InvokeOperation KeyNull: {_serverEncryptionKey == null} Expired: {SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)}");
#endif

                // perform handshake (partial if required)
                await HandshakeAsync();
            }

            // add secure header
            if (message.Headers == null)
                message.Headers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            message.Headers[SecureHeader.HeaderName] = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RequestMessage).ToString();

            // send the envelope
            message.Body = EncryptBody(message.Body);
            message.Address = _address;

            await _node.SendAsync(message);
        }

        /// <summary>
        /// Resets the internal state of the secure channel.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="address">The address.</param>
        public void Reset(Node node, ServiceAddress address) {
            // validate arguments
            if (node == null)
                throw new ArgumentNullException(nameof(node), "The node cannot be null");
            else if (address == null)
                throw new ArgumentNullException(nameof(node), "The address cannot be null");

            // check if already setup
            if (_node != null)
                throw new InvalidOperationException("The secure channel is already configured");

            // set
            _node = node;
            _address = address;

            // reset internal keys
            _serverCertificate = null;
            _handshakeEncryptionKey = null;
            _handshakeEncryptionIV = null;
            _serverNonce = null;
            _serverEncryptionKey = null;
        }

        /// <summary>
        /// Broadcasts the envelope message to the provided service address and waits for a response.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="timeout">The timeout to receive all replies.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<Envelope[]> BroadcastAsync(Message message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken)) {
            // perform handshake if we don't have our key yet or it has expired
            if (_serverEncryptionKey == null || SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)) {
#if DEBUG_SECURE
                Console.WriteLine($"[Secure] InvokeOperation KeyNull: {_serverEncryptionKey == null} Expired: {SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)}");
#endif

                // perform handshake (partial if required)
                await HandshakeAsync();
            }

            // add secure header
            if (message.Headers == null)
                message.Headers = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            message.Headers[SecureHeader.HeaderName] = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RequestMessage).ToString();
            message.Address = _address;
            message.Body = EncryptBody(message.Body);

            // send the envelope and wait for the response
            Envelope[] responses = await _node.BroadcastAsync(message, timeout, cancellationToken);

            return responses.Select(response => {
                if (!response.Headers.ContainsKey(SecureHeader.HeaderName))
                    throw new InvalidDataException("The secure service sent an invalid response");

                // decode header and take action depending on the response
                SecureHeader header = new SecureHeader(response.Headers[SecureHeader.HeaderName]);

                try {
                    if (header.Type == SecureMessageType.RespondMessage) {
                        using (MemoryStream outputStream = new MemoryStream()) {
                            // decrypt
                            using (MemoryStream inputStream = new MemoryStream(response.Data)) {
                                using (Aes aes = Aes.Create()) {
                                    aes.Key = _serverEncryptionKey;
                                    aes.IV = _serverNonce;

                                    using (CryptoStream decryptStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                                        decryptStream.CopyTo(outputStream);
                                    }
                                }
                            }

                            //response.Data = outputStream.ToArray();
                            return response;
                        }
                    } else {
                        return null;
                    }
                } catch(Exception) {
                    return null;
                }
            }).Where(e => e != null).ToArray();
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new secure channel.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public SecureClientChannel(SecureChannelConfiguration configuration) {
            _configuration = configuration;
        }
        #endregion
    }
}
