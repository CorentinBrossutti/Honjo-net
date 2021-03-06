﻿using ProtoBuf;
using System;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet containing one, or several, double(s) as argument(s)
    /// </summary>
    [Serializable, ProtoContract]
    public class DoublePacket : TPacket<double>
    {
        /// <summary>
        /// Constructs a new double packet
        /// </summary>
        public DoublePacket(sbyte header, byte id, byte specifier, params double[] contents) : base(header, id, specifier, contents)
        {}

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public DoublePacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        public DoublePacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }
    }
}
