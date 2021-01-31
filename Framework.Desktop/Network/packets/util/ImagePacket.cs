using System;
using MessagePack;
using ProtoBuf;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet transferring an image.
    /// Should be compressed
    /// </summary>
    [Serializable, ProtoContract, MessagePackObject]
    public class ImagePacket : FilePacket
    {
        /// <summary>
        /// Constructs a new image packet. The imagePath is the local path of the image
        /// </summary>
        public ImagePacket(sbyte header, byte id, byte specifier, string imagePath) : base(header, id, specifier, imagePath, FileTransferOption.BINARY) { }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public ImagePacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public ImagePacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }
    }
}
