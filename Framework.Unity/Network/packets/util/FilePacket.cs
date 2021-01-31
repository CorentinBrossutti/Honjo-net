using ProtoBuf;
using System;
using System.IO;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet transfering a file as a byte array
    /// Should be compressed with large file size
    /// </summary>
    [Serializable, ProtoContract]
    [ProtoInclude(1, typeof(ImagePacket))]
    public class FilePacket : UtilPacket
    {
        /// <summary>
        /// Transfer option for the file in this packet
        /// </summary>
        [ProtoMember(2)]
        public FileTransferOption TransferOption { get; protected set; }

        /// <summary>
        /// Always returns the content at first index (file contents)
        /// </summary>
        public new object Contents => base.Contents[0];

        /// <summary>
        /// Always returns wrapped file contents independently of the index
        /// </summary>
        public new object this[int _] => Contents;

        /// <summary>
        /// Constructs a new file packet
        /// Filepath is the local path of the file to transfer
        /// </summary>
        public FilePacket(sbyte header, byte id, byte specifier, string filePath, FileTransferOption transferOption) :
            base(header, id, specifier, transferOption.ReadyFile(filePath)) => TransferOption = transferOption;

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public FilePacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        public FilePacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }

        /// <summary>
        /// Always returns file contents independently of index
        /// </summary>
        public new object Get(int _) => Contents;

        /// <summary>
        /// Writes the transferred file to a local given file
        /// </summary>
        /// <param name="destination">File path to transfer to</param>
        public void WriteTo(string destination) => TransferOption.WriteTo(Contents, destination);
    }

    /// <summary>
    /// Options for file transfers
    /// </summary>
    public enum FileTransferOption
    {
        /// <summary>
        /// The file is binary and should be transferred as a byte array
        /// </summary>
        BINARY,
        /// <summary>
        /// The file is a plain text content and should be read as-is
        /// </summary>
        TEXT,
        /// <summary>
        /// The file is composed of ordered lines and should be treated as such
        /// </summary>
        LINES
    }

    internal static class FileTransferOptionExtensions
    {
        internal static object ReadyFile(this FileTransferOption option, string path)
        {
            switch(option)
            {
                case FileTransferOption.BINARY:
                    return File.ReadAllBytes(path);
                case FileTransferOption.LINES:
                    return File.ReadAllLines(path);
                case FileTransferOption.TEXT:
                    return File.ReadAllText(path);
            }
            //default is binary, works for pretty much everything anyway
            return ReadyFile(FileTransferOption.BINARY, path);
        }

        internal static void WriteTo(this FileTransferOption option, object content, string path)
        {
            switch(option)
            {
                case FileTransferOption.BINARY:
                    if (!(content is byte[] bytes))
                        throw new ArgumentException("Cannot write binary data with non-binary content");
                    File.WriteAllBytes(path, bytes);
                    break;
                case FileTransferOption.LINES:
                    if (!(content is string[] lines))
                        throw new ArgumentException("Cannot write line-ordered text data with wrong content type");
                    File.WriteAllLines(path, lines);
                    break;
                case FileTransferOption.TEXT:
                    if (!(content is string text))
                        throw new ArgumentException("Cannot write text data with non-string content");
                    File.WriteAllText(path, text);
                    break;
            }
        }
    }
}
