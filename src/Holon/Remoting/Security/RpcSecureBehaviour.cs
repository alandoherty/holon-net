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

        /// <summary>
        /// Sends a secure error reply to the envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <param name="errorMsg">The error message.</param>
        /// <returns></returns>
        private async Task ReplyErrorAsync(Envelope envelope, RpcSecureErrorMsg errorMsg) {
            using (MemoryStream ms = new MemoryStream()) {
                Serializer.Serialize(ms, errorMsg);

                // reply
                await envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, new Dictionary<string, object>() {
                    {RpcSecureHeader.HEADER_NAME, new RpcSecureHeader(RpcSecureHeader.HEADER_VERSION, RpcSecureMessageType.Error).ToString() }
                }, ms.ToArray());
            }
        }

        /// <summary>
        /// Replies to the envelope.
        /// </summary>
        /// <param name="envelope">The nevelope.</param>
        /// <param name="body">The response body.</param>
        /// <param name="headers">The headers.</param>
        /// <returns></returns>
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

            try {
                if (envelope.Headers.ContainsKey(RpcSecureHeader.HEADER_NAME))
                    secureHeader = new RpcSecureHeader(Encoding.UTF8.GetString(envelope.Headers[RpcSecureHeader.HEADER_NAME] as byte[]));
            } catch(Exception) {
                if (envelope.ID != Guid.Empty) {
                    await ReplyErrorAsync(envelope, new RpcSecureErrorMsg() {
                        Code = "ProtocolInvalid",
                        Message = "The secure message header format is invalid"
                    });
                }

                return;
            }

            // check if it's a secure message or not
            if (secureHeader != null && envelope.ID != Guid.Empty) {
                if (secureHeader.Type == RpcSecureMessageType.RequestCertificate) {
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcBehaviour] {nameof(RpcSecureMessageType.RequestCertificate)}");
#endif
                    
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

                    // validate that the data is there and is the correct size, if not send an error back
                    if (requestKeyMsg.HandshakeIV == null || requestKeyMsg.HandshakeKey == null || requestKeyMsg.HandshakeIV.Length != 16 || requestKeyMsg.HandshakeKey.Length != 16) {
                        await ReplyErrorAsync(envelope, new RpcSecureErrorMsg() {
                            Code = "ProtocolInvalid",
                            Message = "The request key data was invalid"
                        });

                        return;
                    }

                    // generate random nonce
                    byte[] nonceBytes = new byte[16];

                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                        rng.GetBytes(nonceBytes);
                    }

                    // process key request
                    long timeSlot = SecureUtils.GetNextTimeSlot();
                    byte[] keyBytes = GenerateKey(nonceBytes, timeSlot);

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcBehaviour] {nameof(RpcSecureMessageType.RequestKey)} TimeSlot: {timeSlot} Nonce: {BitConverter.ToString(nonceBytes).Replace("-", "")}");
#endif

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

                    // validate the data is there and is correct length, if not send invalid data
                    if (msg.Payload == null || msg.ServerNonce == null || msg.ServerNonce.Length != 16) {
                        await ReplyErrorAsync(envelope, new RpcSecureErrorMsg() {
                            Code = "ProtocolInvalid",
                            Message = "The request message data was invalid"
                        });

                        return;
                    }

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcBehaviour] {nameof(RpcSecureMessageType.RequestMessage)} TimeSlot: {msg.KeyTimeSlot} Nonce: {BitConverter.ToString(msg.ServerNonce).Replace("-", "")}");
#endif

                    // validate expiry of time slot
                    if (SecureUtils.HasTimeSlotExpired(msg.KeyTimeSlot, true)) {
                        await ReplyErrorAsync(envelope, new RpcSecureErrorMsg() {
                            Code = "KeyExpired",
                            Message = "The secure message is encrypted with an outdated key"
                        });

                        return;
                    }

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
                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureRpcBehaviour] Unimplemented message type!");
#endif

                    await ReplyErrorAsync(envelope, new RpcSecureErrorMsg() {
                        Code = "ProtocolViolation",
                        Message = "The message type is not relevant or is invalid"
                    });
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
