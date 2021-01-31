using System;
using MessagePack;
using ProtoBuf;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet of a generic type known at compile-time
    /// </summary>
    /// <typeparam name="T">Generic type</typeparam>
    [Serializable, ProtoContract, MessagePackObject]
    [ProtoInclude(1, typeof(BytePacket))]
    [ProtoInclude(2, typeof(DoublePacket))]
    [ProtoInclude(3, typeof(FloatPacket))]
    [ProtoInclude(4, typeof(IntPacket))]
    [ProtoInclude(5, typeof(StringPacket))]
    public class TPacket<T> : Packet
    {
        [NonSerialized, IgnoreMember]
        private T[] __genContents;
        /// <summary>
        /// Generic-typed arguments of this packet
        /// </summary>
        [IgnoreMember]
        public new T[] Contents => __genContents;

        /// <summary>
        /// Square bracket simplification. Retrieves the argument at the given index.
        /// </summary>
        /// <param name="i">Index of the argument (content index)</param>
        /// <returns>The argument at the given index</returns>
        [IgnoreMember]
        public new T this[int i] => Get(i);

        /// <summary>
        /// Constructs a new generic packet
        /// </summary>
        public TPacket(sbyte header, byte id, byte specifier, params T[] contents) : base(header, id, specifier, Array.ConvertAll(contents, _OCVRT))
        {}

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public TPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public TPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }

        /// <summary>
        /// Set up packet
        /// </summary>
        public override void _PostSetup(Serialization serialization)
        {
            base._PostSetup(serialization);
            if (base.Contents == null)
                __genContents = new T[0];
            else
                __genContents = Array.ConvertAll(base.Contents, (object content) => (T)content);
        }

        /// <summary>
        /// Retrieves the argument at the given index
        /// </summary>
        /// <returns>The argument at the given index</returns>
        public new T Get(int index) => Contents[index];

        /// <summary>
        /// Retrieves the argument at the given index under converted form
        /// </summary>
        /// <typeparam name="U">Expected type</typeparam>
        /// <param name="index">Index of the argument</param>
        /// <param name="converter">Converter function. Takes an argument of the packet type and returns the expected type</param>
        /// <returns>Converted argument at given index</returns>
        public U Get<U>(int index, Func<T, U> converter) => converter(Get(index));
    }
}
