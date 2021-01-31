using System;
using MessagePack;
using ProtoBuf;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// Base class for all authentication packets
    /// Mainly to centralize proto includes at top-packet level and keep ids on 1byte
    /// </summary>
    [Serializable, ProtoContract, MessagePackObject]
    [ProtoInclude(1, typeof(PgpPublicKeyPacket))]
    [ProtoInclude(2, typeof(SymKeyPacket))]
    public class AuthPacket : Packet
    {
        /// <summary>
        /// Constructs a new authentication packet. Header is always OK and id is always AUTH
        /// </summary>
        public AuthPacket(byte specifier, params object[] contents) : base(OK_HEADER, AUTH_ID, specifier, contents) { }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public AuthPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public AuthPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) : 
            base(key0, key1, key2, key3, key4, key5, key6) { }
    }
}
