using ProtoBuf;
using System;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A base class for all utility packets
    /// As with auth, mainly there to keep top-level packets proto id in 1byte
    /// </summary>
    [Serializable, ProtoContract]
    [ProtoInclude(1, typeof(FilePacket))]
    public class UtilPacket : Packet
    {
        /// <summary>
        /// Constructs a new utility packet, params are same as a base packet
        /// </summary>
        public UtilPacket(sbyte header, byte id, byte specifier, params object[] contents) : base(header, id, specifier, contents) { }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public UtilPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        public UtilPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }
    }
}
