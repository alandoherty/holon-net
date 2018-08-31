using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Remoting.Security
{
    /// <summary>
    /// Provides secure RPC functionality for a service.
    /// </summary>
    public class RpcSecureBehaviour : RpcBehaviour
    {
        private X509Certificate2 _certificate;
        private byte[] _secret;

        /// <summary>
        /// Creates a new RPC behaviour and binds the interface type.
        /// </summary>
        /// <typeparam name="IT">The interface type.</typeparam>
        /// <param name="certificate">The certificate.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="interfaceBehaviour">The implementation of the interface behaviour.</param>
        /// <returns></returns>
        public static RpcSecureBehaviour BindSecure<IT>(X509Certificate2 certificate, string secret, IT interfaceBehaviour) {
            return BindSecure(certificate, secret, typeof(IT), interfaceBehaviour);
        }

        /// <summary>
        /// Creates a new RPC behaviour and binds the interface type.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="interfaceType">The interface type.</param>
        /// <param name="interfaceBehaviour">The implementation of the interface behaviour.</param>
        /// <returns></returns>
        public static RpcSecureBehaviour BindSecure(X509Certificate2 certificate, string secret, Type interfaceType, object interfaceBehaviour) {
            RpcSecureBehaviour rpcBehaviour = new RpcSecureBehaviour(certificate, secret);
            rpcBehaviour.Attach(interfaceType, interfaceBehaviour);
            return rpcBehaviour;
        }

        /// <summary>
        /// Creates a new RPC behaviour and binds the interfaces.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <param name="secret">The secret.</param>
        /// <param name="interfaceTypes">The interface types.</param>
        /// <param name="interfaceBehaviours">The interface behaviours.</param>
        /// <returns></returns>
        public static RpcSecureBehaviour BindManySecure(X509Certificate2 certificate, string secret, Type[] interfaceTypes, object[] interfaceBehaviours) {
            RpcSecureBehaviour rpcBehaviour = new RpcSecureBehaviour(certificate, secret);
            rpcBehaviour.Attach(interfaceTypes, interfaceBehaviours);
            return rpcBehaviour;
        }


        /// <summary>
        /// Generate the AES128 key for the provided nonce and time slot.
        /// </summary>
        /// <param name="nonceBytes">The nonce bytes.</param>
        /// <param name="timeSlot">The time slot.</param>
        /// <returns>The key bytes.</returns>
        private byte[] GenerateKey(byte[] nonceBytes, long timeSlot) {
            // build input bytes
            byte[] inputBytes = new byte[nonceBytes.Length + 8];

            Buffer.BlockCopy(nonceBytes, 0, inputBytes, 0, nonceBytes.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(timeSlot), 0, inputBytes, nonceBytes.Length, 8);

            // get time slot
            byte[] keyBytes = new byte[16];

            using (HMACSHA256 hmac = new HMACSHA256(_secret)) {
                byte[] hashBytes = hmac.ComputeHash(inputBytes);
                Buffer.BlockCopy(hashBytes, 0, keyBytes, 0, 16);
            }

            return keyBytes;
        }

        protected override Task ReplyAsync(Envelope envelope, byte[] body, IDictionary<string, object> headers) {
            // only apply encryption if this is a response message
            if (!envelope.Headers.ContainsKey(RpcSecureHeader.HEADER_NAME))
                return base.ReplyAsync(envelope, body, headers);

            // deserialize message request
            // it has already been validated at this point!
            RpcSecureMessageMsg msg = null;

            using (MemoryStream ms = new MemoryStream(envelope.RawBody)) {
                msg = Serializer.Deserialize<RpcSecureMessageMsg>(ms);
            }

            // get key
            byte[] keyBytes = GenerateKey(msg.ServerNonce, msg.KeyTimeSlot);

            using (MemoryStream outputStream = new MemoryStream()) {
                // encrypt
                using (MemoryStream inputStream = new MemoryStream(body)) {
                    using (Aes aes = Aes.Create()) {
                        aes.Key = keyBytes;
                        aes.IV = msg.ServerNonce;

                        using (CryptoStream decryptStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                            inputStream.CopyTo(decryptStream);
                        }
                    }
                }

                // add header
                headers[RpcSecureHeader.HEADER_NAME] = new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RespondMessage).ToString();

                return base.ReplyAsync(envelope, outputStream.ToArray(), headers);
            }
        }

        /// <summary>
        /// Handles the incoming envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        public override async Task HandleAsync(Envelope envelope) {
            // check for secure header
            RpcSecureHeader secureHeader = null;

            if (envelope.Headers.ContainsKey(RpcSecureHeader.HEADER_NAME))
                secureHeader = new RpcSecureHeader(Encoding.UTF8.GetString(envelope.Headers[RpcSecureHeader.HEADER_NAME] as byte[]));

            // check if it's a secure message or not
            if (secureHeader != null) {
                if (secureHeader.Type == RpcSecureMessageType.RequestCertificate) {
                    // check for correlation
                    if (envelope.ID == Guid.Empty)
                        throw new InvalidOperationException("The secure certificate request has no envelope ID");

                    // build response
                    RpcSecureRespondCertificateMsg respondCertificateMsg = new RpcSecureRespondCertificateMsg();
                    respondCertificateMsg.CertificateData = _certificate.Export(X509ContentType.Cert);

                    // build reply
                    using (MemoryStream ms = new MemoryStream()) {
                        Serializer.Serialize(ms, respondCertificateMsg);

                        // reply
                        await envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, new Dictionary<string, object>() {
                            { RpcSecureHeader.HEADER_NAME, new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RespondCertificate).ToString() }
                        }, ms.ToArray());
                    }
                } else if (secureHeader.Type == RpcSecureMessageType.RequestKey) {
                    // check for correlation
                    if (envelope.ID == Guid.Empty)
                        throw new InvalidOperationException("The secure key request has no envelope ID");

                    // decrypt
                    byte[] decryptedBody = null;

                    using (RSA rsa = _certificate.GetRSAPrivateKey()) {
                        decryptedBody = rsa.Decrypt(envelope.Body, RSAEncryptionPadding.Pkcs1);
                    }

                    // deserialize key request
                    RpcSecureRequestKeyMsg requestKeyMsg = null;

                    using (MemoryStream ms = new MemoryStream(decryptedBody)) {
                        requestKeyMsg = Serializer.Deserialize<RpcSecureRequestKeyMsg>(ms);
                    }

                    if (requestKeyMsg.HandshakeIV == null || requestKeyMsg.HandshakeKey == null || requestKeyMsg.HandshakeIV.Length != 16 || requestKeyMsg.HandshakeKey.Length != 16)
                        throw new InvalidDataException("The secure key request is invalid");

                    // generate random nonce
                    byte[] nonceBytes = new byte[16];

                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                        rng.GetBytes(nonceBytes);
                    }

                    // process key request
                    long timeSlot = SecureUtils.GetNextTimeSlot();
                    byte[] keyBytes = GenerateKey(nonceBytes, timeSlot);

                    // build response
                    RpcSecureRespondKeyMsg respondKeyMsg = new RpcSecureRespondKeyMsg();
                    respondKeyMsg.ServerNonce = nonceBytes;
                    respondKeyMsg.ServerKey = keyBytes;
                    respondKeyMsg.KeyTimeSlot = timeSlot;

                    // encode and encrypt
                    byte[] respondKeyBody = null;

                    using (Aes aes = Aes.Create()) {
                        // setup aes
                        aes.Key = requestKeyMsg.HandshakeKey;
                        aes.IV = requestKeyMsg.HandshakeIV;

                        // build body
                        using (MemoryStream ms = new MemoryStream()) {
                            // encrypt using client key
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                                Serializer.Serialize(cs, respondKeyMsg);
                            }

                            // get output
                            respondKeyBody = ms.ToArray();
                        }
                    }

                    // reply
                    await envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, new Dictionary<string, object>() {
                        { RpcSecureHeader.HEADER_NAME, new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.RespondKey).ToString() }
                    }, respondKeyBody);
                } else if (secureHeader.Type == RpcSecureMessageType.RequestMessage) {
                    // deserialize key request
                    RpcSecureMessageMsg msg = null;

                    using (MemoryStream ms = new MemoryStream(envelope.Body)) {
                        msg = Serializer.Deserialize<RpcSecureMessageMsg>(ms);
                    }

                    if (msg.Payload == null || msg.ServerNonce == null || msg.ServerNonce.Length != 16)
                        throw new InvalidDataException("The secure message is invalid");

                    // validate expiry of time slot
                    if (SecureUtils.HasTimeSlotExpired(msg.KeyTimeSlot))
                        throw new InvalidDataException("The secure message is encrypted with an expired key");

                    // get key
                    byte[] keyBytes = GenerateKey(msg.ServerNonce, msg.KeyTimeSlot);

                    using (MemoryStream decryptedPayloadStream = new MemoryStream()) {
                        using (MemoryStream payloadStream = new MemoryStream(msg.Payload)) {
                            using (Aes aes = Aes.Create()) {
                                aes.Key = keyBytes;
                                aes.IV = msg.ServerNonce;

                                using (CryptoStream decryptStream = new CryptoStream(payloadStream, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                                    decryptStream.CopyTo(decryptedPayloadStream);
                                }
                            }
                        }

                        // create new envelope
                        await base.HandleAsync(envelope.Transform(decryptedPayloadStream.ToArray()));
                    }
                } else {
                    throw new NotSupportedException("The secure request type is not supported");
                }
            } else {
                await base.HandleAsync(envelope);
            }
        }

        public RpcSecureBehaviour(X509Certificate2 certificate, string secret) 
            : this(true, certificate, secret) {
        }

        public RpcSecureBehaviour(bool introspection, X509Certificate2 certificate, string secret) 
            : base(introspection) {
            // set certificate
            _certificate = certificate;

            // derive secret
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(secret, new byte[8])) {
                _secret = deriveBytes.GetBytes(32);
            }
        }
    }
}
