﻿using ProtoBuf;
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

namespace Holon.Remoting.Security
{
    /// <summary>
    /// Provides functionality to proxy calls for an RPC interface.
    /// </summary>
    /// <typeparam name="IT">The interface.</typeparam>
    public class RpcSecureProxy<IT> : RpcProxy<IT>
    {
        private X509Certificate2 _serverCertificate;

        private byte[] _handshakeEncryptionKey;
        private byte[] _handshakeEncryptionIV;

        private byte[] _serverNonce;
        private byte[] _serverEncryptionKey;
        private long _serverEncryptionKeyTimeSlot;

        private SemaphoreSlim _handshakeSemaphore = new SemaphoreSlim(1, 1);

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
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcProxy] Handshake Requesting certificate...");
#endif

                    // request certificate 
                    RpcSecureHeader requestCertificate = new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RequestCertificate);

                    // send request
                    Envelope respondCert = await _node.AskAsync(_addr, new byte[0], new Dictionary<string, object>() {
                        { RpcSecureHeader.HEADER_NAME, requestCertificate.ToString() }
                    }, _configuration.Timeout);

                    // parse response header
                    RpcSecureHeader respondCertHeader = null;

                    try {
                        respondCertHeader = new RpcSecureHeader(Encoding.UTF8.GetString(respondCert.Headers[RpcSecureHeader.HEADER_NAME] as byte[]));
                    } catch (Exception ex) {
                        throw new InvalidDataException("The certificate request response header was invalid", ex);
                    }

                    // check if the certificate response is an error or if it's an incorrect type
                    if (respondCertHeader.Type == RpcSecureMessageType.Error) {
                        RpcSecureErrorMsg errorMsg = respondCert.AsProtoBuf<RpcSecureErrorMsg>();

                        throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
                    } else if (respondCertHeader.Type != RpcSecureMessageType.RespondCertificate) {
                        throw new InvalidDataException("The certificate request response header was invalid");
                    }

                    // decode the certificate response and then check it has actual data
                    RpcSecureRespondCertificateMsg respondCertMsg = respondCert.AsProtoBuf<RpcSecureRespondCertificateMsg>();

                    if (respondCertMsg.CertificateData == null)
                        throw new InvalidDataException("The certificate request response was invalid");

                    // load certificate from response
                    X509Certificate2 cert = new X509Certificate2(respondCertMsg.CertificateData);
                    
                    // validate it's allowed to act as this service
                    if (((SecureProxyConfiguration)_configuration).ValidateAddress) {
                        // check extension is actually there
                        if (cert.Extensions["HolonSecureServices"] == null)
                            throw new InvalidDataException("The service certificate has no claim for the invoked operation");

                        // parse extension
                        X509Extension servicesExt = cert.Extensions["HolonSecureServices"];

                    } else {
#if DEBUG_SECURERPC
                        Console.WriteLine($"[SecureRpcProxy] Handshake WARNING: Not validating services due to configuration!");
#endif
                    }

                    // validate that it's signed by ca authority
                    if (((SecureProxyConfiguration)_configuration).ValidateAuthority) {
                        X509Chain chain = new X509Chain();
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.ChainPolicy.ExtraStore.Add(((SecureProxyConfiguration)_configuration).RootAuthority);
                        chain.Build(cert);

                        // get status
                        X509ChainStatus status = chain.ChainStatus.First();

                        if (status.Status != X509ChainStatusFlags.UntrustedRoot && status.Status != X509ChainStatusFlags.NoError)
                            throw new InvalidDataException("The service certificate is not signed by the root authority");
                    } else {
#if DEBUG_SECURERPC
                        Console.WriteLine($"[SecureRpcProxy] Handshake WARNING: Not validating authority due to configuration!");
#endif
                    }

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcProxy] Handshake Got certifcate {cert.Subject} ({cert.Thumbprint})");
#endif

                    _serverCertificate = cert;
                }

                // log
#if DEBUG_SECURERPC
                Console.WriteLine($"[SecureRpcProxy] Handshake Requesting key... FirstTime: {_serverEncryptionKey == null}");
#endif

                // key request
                RpcSecureRequestKeyMsg requestKeyMsg = new RpcSecureRequestKeyMsg() {
                    HandshakeIV = _handshakeEncryptionIV,
                    HandshakeKey = _handshakeEncryptionKey
                };
                RpcSecureHeader requestKey = new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RequestKey);

                // send request
                Envelope respondKey = null;

                using (RSA rsa = _serverCertificate.GetRSAPublicKey()) {
                    using (MemoryStream ms = new MemoryStream()) {
                        // serialize to stream
                        Serializer.Serialize(ms, requestKeyMsg);

                        // encrypt with server certificate
                        byte[] keyRequestBody = rsa.Encrypt(ms.ToArray(), RSAEncryptionPadding.Pkcs1);

                        respondKey = await _node.AskAsync(_addr, keyRequestBody, new Dictionary<string, object>() {
                            { RpcSecureHeader.HEADER_NAME, requestKey.ToString() }
                        }, _configuration.Timeout);
                    }
                }

                // parse response
                RpcSecureHeader respondKeyHeader = null;

                try {
                    respondKeyHeader = new RpcSecureHeader(Encoding.UTF8.GetString(respondKey.Headers[RpcSecureHeader.HEADER_NAME] as byte[]));
                } catch (Exception ex) {
                    throw new InvalidDataException("The key request response header was invalid", ex);
                }

                // check if the key response is an error or if it's an incorrect type
                if (respondKeyHeader.Type == RpcSecureMessageType.Error) {
                    RpcSecureErrorMsg errorMsg = respondKey.AsProtoBuf<RpcSecureErrorMsg>();

                    throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
                } else if (respondKeyHeader.Type != RpcSecureMessageType.RespondKey) {
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
                    RpcSecureRespondKeyMsg respondKeyMsg = Serializer.Deserialize<RpcSecureRespondKeyMsg>(decryptedStream);

                    // validate key
                    if (respondKeyMsg.ServerKey == null || respondKeyMsg.ServerNonce == null || respondKeyMsg.ServerKey.Length != 16)
                        throw new InvalidDataException("The secure key is invalid");

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcProxy] Handshake Got key Timeslot: {respondKeyMsg.KeyTimeSlot} Nonce: {BitConverter.ToString(respondKeyMsg.ServerNonce).Replace("-", "")}");
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
        /// Transforms the request body.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns>The body.</returns>
        protected override byte[] TransformRequest(byte[] body, IDictionary<string, object> headers) {
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

                // add header
                headers[RpcSecureHeader.HEADER_NAME] = new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RequestMessage).ToString();

                // get payload
                byte[] payload = outputStream.ToArray();

                // build message
                using (MemoryStream msgStream = new MemoryStream()) {
                    Serializer.Serialize<RpcSecureMessageMsg>(msgStream, new RpcSecureMessageMsg() {
                        KeyTimeSlot = _serverEncryptionKeyTimeSlot,
                        Payload = payload,
                        ServerNonce = _serverNonce
                    });

                    return msgStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Transforms the response body.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns>The body.</returns>
        protected override byte[] TransformResponse(Envelope envelope) {
            // check if we need to decrypt this
            if (!envelope.Headers.ContainsKey(RpcSecureHeader.HEADER_NAME))
                return envelope.Body;

            // decode header
            RpcSecureHeader header = new RpcSecureHeader(Encoding.UTF8.GetString(envelope.Headers[RpcSecureHeader.HEADER_NAME] as byte[]));

            if (header.Type == RpcSecureMessageType.RespondMessage) {
                using (MemoryStream outputStream = new MemoryStream()) {
                    // decrypt
                    using (MemoryStream inputStream = new MemoryStream(envelope.Body)) {
                        using (Aes aes = Aes.Create()) {
                            aes.Key = _serverEncryptionKey;
                            aes.IV = _serverNonce;

                            using (CryptoStream decryptStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                                decryptStream.CopyTo(outputStream);
                            }
                        }
                    }

                    return outputStream.ToArray();
                }
            } else if (header.Type == RpcSecureMessageType.Error) {
                // deserialize
                RpcSecureErrorMsg errorMsg = null;

                try {
                    errorMsg = envelope.AsProtoBuf<RpcSecureErrorMsg>();
                } catch(Exception ex) {
                    throw new InvalidDataException("The secure service sent an invalid error respsonse", ex);
                }

                throw new SecurityException($"{errorMsg.Message} ({errorMsg.Code})");
            } else {
                throw new InvalidDataException($"The secure service sent an invalid response ({header.Type})");
            }
        }

        /// <summary>
        /// Invokes an operation method.
        /// </summary>
        /// <typeparam name="TT">The task return type.</typeparam>
        /// <param name="method">The method.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="returnType">The real return type.</param>
        /// <returns></returns>
        protected override async Task<TT> InvokeOperationAsync<TT>(MethodInfo method, object[] args, Type returnType) {
            // perform handshake if we don't have our key yet or it has expired
            if (_serverEncryptionKey == null || SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)) {
#if DEBUG_SECURERPC
                Console.WriteLine($"[SecureRpcProxy] InvokeOperation KeyNull: {_serverEncryptionKey == null} Expired: {SecureUtils.HasTimeSlotExpired(_serverEncryptionKeyTimeSlot, false)}");
#endif

                // perform handshake (partial if required)
                await HandshakeAsync();
            }

            return await base.InvokeOperationAsync<TT>(method, args, returnType);
        }

        /// <summary>
        /// Creates a new secure proxy.
        /// Do not create this directly.
        /// </summary>
        public RpcSecureProxy() {
        }
    }
}
