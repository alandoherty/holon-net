using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Holon.Security
{
    /// <summary>
    /// Provides a secure reply channel.
    /// </summary>
    class SecureReplyChannel : IReplyChannel
    {
        private Envelope _envelope;
        private byte[] _key;
        private byte[] _nonce;

        public string ReplyTo {
            get {
                throw new NotImplementedException();
                //return _envelope.ReplyTo;
            }
        }

        public string ReplyID {
            get {
                return _envelope.ID;
            }
        }

        public bool IsEncrypted {
            get {
                return true;
            }
        }

        public Task ReplyAsync(byte[] body, IDictionary<string, object> headers = null) {
            throw new NotImplementedException();
            /*
            // ensure headers created
            if (headers == null)
                headers = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);

            // get key
            byte[] keyBytes = _key;

            using (MemoryStream outputStream = new MemoryStream()) {
                // encrypt
                using (MemoryStream inputStream = new MemoryStream(body)) {
                    using (Aes aes = Aes.Create()) {
                        aes.Key = keyBytes;
                        aes.IV = _nonce;

                        using (CryptoStream decryptStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write)) {
                            inputStream.CopyTo(decryptStream);
                        }
                    }
                }

                // add header
                headers[SecureHeader.HeaderName] = new SecureHeader(SecureHeader.HeaderVersion, SecureMessageType.RespondMessage).ToString();

                return _envelope.Namespace.ReplyAsync(ReplyTo, ReplyID, outputStream.ToArray(), headers);
            }*/
        }

        public SecureReplyChannel(Envelope envelope, byte[] key, byte[] nonce) {
            _envelope = envelope;
            _key = key;
            _nonce = nonce;
        }

        ~SecureReplyChannel() {
            for (int i = 0; i < _key.Length; i++)
                _key[i] = 0;
            for (int i = 0; i < _nonce.Length; i++)
                _nonce[i] = 0;
        }
    }
}
