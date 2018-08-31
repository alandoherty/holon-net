using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting.Security
{
    [ProtoContract]
    class RpcSecureRespondCertificateMsg
    {
        /// <summary>
        /// The secure service certificate, signed by an authority CA.
        /// </summary>
        [ProtoMember(1)]
        public byte[] CertificateData { get; set; }
    }

    [ProtoContract]
    class RpcSecureRequestKeyMsg
    {
        /// <summary>
        /// The handshake key used in the response.
        /// </summary>
        [ProtoMember(1)]
        public byte[] HandshakeKey { get; set; }

        /// <summary>
        /// The handshake IV used in the response.
        /// </summary>
        [ProtoMember(2)]
        public byte[] HandshakeIV { get; set; }
    }

    [ProtoContract]
    class RpcSecureRespondKeyMsg
    {
        /// <summary>
        /// The data key for a specific time slot with the nonce.
        /// </summary>
        [ProtoMember(1)]
        public byte[] ServerKey { get; set; }

        /// <summary>
        /// The server nonce, also used as IV.
        /// </summary>
        [ProtoMember(2)]
        public byte[] ServerNonce { get; set; }

        /// <summary>
        /// The time slot of the key.
        /// </summary>
        [ProtoMember(3)]
        public long KeyTimeSlot { get; set; }
    }

    [ProtoContract]
    class RpcSecureErrorMsg
    {
        [ProtoMember(1)]
        public string Code { get; set; }

        [ProtoMember(2)]
        public string Message { get; set; }
    }

    [ProtoContract]
    class RpcSecureMessageMsg
    {
        /// <summary>
        /// The time slot of the key, this will be validated for expiry.
        /// </summary>
        [ProtoMember(1)]
        public long KeyTimeSlot { get; set; }

        /// <summary>
        /// The server nonce, used to recreate the server key on any service.
        /// </summary>
        [ProtoMember(2)]
        public byte[] ServerNonce { get; set; }

        /// <summary>
        /// The payload encrypted with the server key.
        /// </summary>
        [ProtoMember(3)]
        public byte[] Payload { get; set; }
    }
}
