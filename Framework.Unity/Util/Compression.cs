using System;
using System.IO;
using System.IO.Compression;

namespace Honjo.Framework.Util
{
    /// <summary>
    /// Enumeration for compression methods
    /// </summary>
    public enum Compression
    {
        /// <summary>
        /// Unknown compression method (parsing error ?)
        /// </summary>
        UNKNOWN,
        /// <summary>
        /// No compression
        /// </summary>
        NONE,
        /// <summary>
        /// Gzip compression
        /// </summary>
        GZIP,
        /// <summary>
        /// Raw (no CRC) deflate compression
        /// </summary>
        DEFLATE_RAW
    }

    /// <summary>
    /// Extensions methods for compression
    /// </summary>
    public static class CompressionExtensions
    {
        internal const int DEFAULT_COMPRESSION_BUFFER_SIZE = 512;

        /// <summary>
        /// Process data from a stream and writes its processed (compressed) counterpart to another
        /// </summary>
        public static void Process(this Compression compression, Stream source, Stream dest, bool compressing, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true)
        {
            switch(compression)
            {
                case Compression.NONE:
                    source.CopyTo(dest, bufferSize, seekBack);
                    return;
                case Compression.GZIP:
                    GzipCompression.Process(source, dest, compressing, bufferSize, seekBack);
                    return;
                case Compression.DEFLATE_RAW:
                    DeflateCompression.Process(source, dest, compressing, bufferSize, seekBack);
                    return;
            }
            throw new ArgumentException("Unrecognized compression method: " + compression);
        }

        /// <summary>
        /// Process a byte array, returns its processed (compressed) counterpart
        /// </summary>
        public static byte[] Process(this Compression compression, byte[] from, bool compressing)
        {
            using (MemoryStream from_stream = new MemoryStream(from, 0, from.Length))
            {
                using (MemoryStream to_stream = new MemoryStream())
                {
                    //memory streams, no buffer needed
                    Process(compression, from_stream, to_stream, compressing);
                    return to_stream.ToArray();
                }
            }
        }

        /// <summary>
        /// Process a byte array, and writes its processed (compressed) counterpart to a stream
        /// </summary>
        public static void Process(this Compression compression, byte[] from, Stream to, bool compressing, bool seekBack = true)
        {
            using (MemoryStream ms = new MemoryStream(from, 0, from.Length))
                Process(compression, ms, to, compressing, seekBack: seekBack);
        }

        /// <summary>
        /// Process data from a stream and returns its processed (compressed) counterpart as a byte array
        /// </summary>
        public static byte[] Process(this Compression compression, Stream from, bool compressing, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Process(compression, from, ms, compressing, DEFAULT_COMPRESSION_BUFFER_SIZE);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Compresses data and returns it
        /// </summary>
        public static byte[] Compress(this Compression compression, byte[] data) => Process(compression, data, true);

        /// <summary>
        /// Compresses data from a stream and writes it to another
        /// </summary>
        public static void Compress(this Compression compression, Stream from, Stream to, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true) =>
            Process(compression, from, to, true, bufferSize, seekBack);

        /// <summary>
        /// Compresses data and writes it to a stream
        /// </summary>
        public static void Compress(this Compression compression, byte[] data, Stream to, bool seekBack = true) => Process(compression, data, to, true, seekBack);

        /// <summary>
        /// Compresses data from a stream and returns it (the compressed data)
        /// </summary>
        public static byte[] Compress(this Compression compression, Stream from, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE) => Process(compression, from, true, bufferSize);

        /// <summary>
        /// Decompresses data and returns it
        /// </summary>
        public static byte[] Decompress(this Compression compression, byte[] data) => Process(compression, data, false);

        /// <summary>
        /// Decompresses data from a stream and writes it to another
        /// </summary>
        public static void Decompress(this Compression compression, Stream from, Stream to, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true) =>
            Process(compression, from, to, false, bufferSize, seekBack);

        /// <summary>
        /// Decompresses data and writes it to a stream
        /// </summary>
        public static void Decompress(this Compression compression, byte[] data, Stream to, bool seekBack = true) => Process(compression, data, to, false, seekBack);

        /// <summary>
        /// Decompresses data from a stream and returns it
        /// </summary>
        public static byte[] Decompress(this Compression compression, Stream from, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE) => Process(compression, from, false, bufferSize);
    }

    internal static class GzipCompression
    {
        internal static void Process(Stream source, Stream dest, bool compressing, int bufferSize = CompressionExtensions.DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true)
        {
            int copied;
            using (GZipStream gzs = new GZipStream(compressing ? dest : source, compressing ? CompressionMode.Compress : CompressionMode.Decompress, true))
            {
                if (compressing)
                    copied = source.CopyTo(gzs, bufferSize, false);
                else
                    copied = gzs.CopyTo(dest, bufferSize, false);
            }
            if (seekBack && dest.CanSeek)
            {
                if (dest.Position < copied)
                    dest.Position = 0;
                else
                    dest.Seek(-copied, SeekOrigin.Current);
            }
        }
    }

    internal static class DeflateCompression
    {
        internal static void Process(Stream source, Stream dest, bool compressing, int bufferSize = CompressionExtensions.DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true)
        {
            int copied;
            using (DeflateStream dfs = new DeflateStream(compressing ? dest : source, compressing ? CompressionMode.Compress : CompressionMode.Decompress, true))
            {
                if (compressing)
                    copied = source.CopyTo(dfs, bufferSize, false);
                else
                    copied = dfs.CopyTo(dest, bufferSize, false);
            }
            if (seekBack && dest.CanSeek)
            {
                if (dest.Position <= copied)
                    dest.Position = 0;
                else
                    dest.Seek(-copied, SeekOrigin.Current);
            }
        }
    }
}
