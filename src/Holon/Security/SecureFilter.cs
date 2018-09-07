using Holon.Services;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Security
{
    /// <summary>
    /// Implements a secure filter will handles handshakes, message decryption and setting the correct channel for responses.
    /// </summary>
    public class SecureFilter : IServiceFilter
    {
        private X509Certificate2 _certificate;
        private byte[] _secret;

        /// <summary>
        /// Sends a secure error reply to the envelope.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <param name="errorMsg">The error message.</param>
        /// <returns></returns>
        private async Task ReplyErrorAsync(Envelope envelope, SecureErrorMsg errorMsg) {
            using (MemoryStream ms = new MemoryStream()) {
                Serializer.Serialize(ms, errorMsg);

                // reply
                await envelope.ReplyAsync(ms.ToArray(), new Dictionary<string, object>() {
                    {SecureHeader.HEADER_NAME, new SecureHeader(SecureHeader.HEADER_VERSION, SecureMessageType.Error).ToString() }
                });
            }
        }

        /// <summary>
        /// Handles handshake and encrypted envelopes.
        /// </summary>
        /// <param name="envelope">The envelope.</param>
        /// <returns></returns>
        public async Task<bool> HandleAsync(Envelope envelope) {
            // check for secure header
            SecureHeader secureHeader = null;

            try {
                if (envelope.Headers.ContainsKey(SecureHeader.HEADER_NAME))
                    secureHeader = new SecureHeader(Encoding.UTF8.GetString(envelope.Headers[SecureHeader.HEADER_NAME] as byte[]));
            } catch (Exception) {
                if (envelope.ID != Guid.Empty) {
                    await ReplyErrorAsync(envelope, new SecureErrorMsg() {
                        Code = "ProtocolInvalid",
                        Message = "The secure message header format is invalid"
                    });
                }

                return false;
            }

            // check if it's a secure message or not
            if (secureHeader != null && envelope.ID != Guid.Empty) {
                if (secureHeader.Type == SecureMessageType.RequestCertificate) {
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureFilter] {nameof(RpcSecureMessageType.RequestCertificate)}");
#endif

                    // build response
                    SecureRespondCertificateMsg respondCertificateMsg = new SecureRespondCertificateMsg();
                    respondCertificateMsg.CertificateData = _certificate.Export(X509ContentType.Cert);

                    // build reply
                    using (MemoryStream ms = new MemoryStream()) {
                        Serializer.Serialize(ms, respondCertificateMsg);

                        // reply
                        await envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, ms.ToArray(), new Dictionary<string, object>() {
                            { SecureHeader.HEADER_NAME, new SecureHeader(SecureHeader.HEADER_VERSION, SecureMessageType.RespondCertificate).ToString() }
                        });
                    }

                    return false;
                } else if (secureHeader.Type == SecureMessageType.RequestKey) {
                    // decrypt
                    byte[] decryptedBody = null;

                    using (RSA rsa = _certificate.GetRSAPrivateKey()) {
                        decryptedBody = rsa.Decrypt(envelope.Body, RSAEncryptionPadding.Pkcs1);
                    }

                    // deserialize key request
                    SecureRequestKeyMsg requestKeyMsg = null;

                    using (MemoryStream ms = new MemoryStream(decryptedBody)) {
                        requestKeyMsg = Serializer.Deserialize<SecureRequestKeyMsg>(ms);
                    }

                    // validate that the data is there and is the correct size, if not send an error back
                    if (requestKeyMsg.HandshakeIV == null || requestKeyMsg.HandshakeKey == null || requestKeyMsg.HandshakeIV.Length != 16 || requestKeyMsg.HandshakeKey.Length != 16) {
                        await ReplyErrorAsync(envelope, new SecureErrorMsg() {
                            Code = "ProtocolInvalid",
                            Message = "The request key data was invalid"
                        });

                        return false;
                    }

                    // generate random nonce
                    byte[] nonceBytes = new byte[16];

                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                        rng.GetBytes(nonceBytes);
                    }

                    // process key request
                    long timeSlot = SecureUtils.GetNextTimeSlot();
                    byte[] keyBytes = SecureUtils.GenerateKey(nonceBytes, timeSlot, _secret);

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureFilter] {nameof(RpcSecureMessageType.RequestKey)} TimeSlot: {timeSlot} Nonce: {BitConverter.ToString(nonceBytes).Replace("-", "")}");
#endif

                    // build response
                    SecureRespondKeyMsg respondKeyMsg = new SecureRespondKeyMsg();
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
                    await envelope.Node.ReplyAsync(envelope.ReplyTo, envelope.ID, respondKeyBody, new Dictionary<string, object>() {
                        { SecureHeader.HEADER_NAME, new SecureHeader(SecureHeader.HEADER_VERSION, SecureMessageType.RespondKey).ToString() }
                    });

                    return false;
                } else if (secureHeader.Type == SecureMessageType.RequestMessage) {
                    // deserialize key request
                    SecureMessageMsg msg = null;

                    using (MemoryStream ms = new MemoryStream(envelope.Body)) {
                        msg = Serializer.Deserialize<SecureMessageMsg>(ms);
                    }

                    // validate the data is there and is correct length, if not send invalid data
                    if (msg.Payload == null || msg.ServerNonce == null || msg.ServerNonce.Length != 16) {
                        await ReplyErrorAsync(envelope, new SecureErrorMsg() {
                            Code = "ProtocolInvalid",
                            Message = "The request message data was invalid"
                        });

                        return false;
                    }

                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureFilter] {nameof(RpcSecureMessageType.RequestMessage)} TimeSlot: {msg.KeyTimeSlot} Nonce: {BitConverter.ToString(msg.ServerNonce).Replace("-", "")}");
#endif

                    // validate expiry of time slot
                    if (SecureUtils.HasTimeSlotExpired(msg.KeyTimeSlot, true)) {
                        await ReplyErrorAsync(envelope, new SecureErrorMsg() {
                            Code = "KeyExpired",
                            Message = "The secure message is encrypted with an outdated key"
                        });

                        return false;
                    }

                    // get key
                    byte[] keyBytes = SecureUtils.GenerateKey(msg.ServerNonce, msg.KeyTimeSlot, _secret);

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

                        // modify body and set reply channel
                        envelope.Body = decryptedPayloadStream.ToArray();
                        envelope.Channel = new SecureReplyChannel(envelope, keyBytes, msg.ServerNonce);

                        return true;
                    }
                } else {
                    // log
#if DEBUG_SECURERPC
                    Console.WriteLine($"[SecureFilter] Unimplemented message type!");
#endif

                    await ReplyErrorAsync(envelope, new SecureErrorMsg() {
                        Code = "ProtocolViolation",
                        Message = "The message type is not relevant or is invalid"
                    });

                    return false;
                }
            } else {
                return true;
            }
        }

        /// <summary>
        /// Creates a new secure service filter to handle handshakes and encrypted messages.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <param name="secret">The shared secret.</param>
        public SecureFilter(X509Certificate2 certificate, string secret) {
            // set certificate
            _certificate = certificate;

            // derive secret
            using (Rfc2898DeriveBytes deriveBytes = new Rfc2898DeriveBytes(secret, new byte[8])) {
                _secret = deriveBytes.GetBytes(32);
            }
        }
    }
}
