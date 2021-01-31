using System;
using MessagePack;
using ProtoBuf;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet, containing one, or several, string(s) as argument(s)
    /// </summary>
    [Serializable, ProtoContract, MessagePackObject]
    public class StringPacket : TPacket<string>
    {

        /// <summary>
        /// Constructs a new string packet
        /// </summary>
        public StringPacket(sbyte header, byte id, byte specifier, params string[] contents) : base(header, id, specifier, contents)
        {}

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public StringPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public StringPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }
    }
}
