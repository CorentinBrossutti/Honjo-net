using ProtoBuf;
using System;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet, containing one, or several, float(s) as argument(s)
    /// </summary>
    [Serializable, ProtoContract]
    public class FloatPacket : TPacket<float>
    {
        /// <summary>
        /// Constructs a new float packet
        /// </summary>
        public FloatPacket(sbyte header, byte id, byte specifier, params float[] contents) : base(header, id, specifier, contents)
        {}

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public FloatPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        public FloatPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }
    }
}
