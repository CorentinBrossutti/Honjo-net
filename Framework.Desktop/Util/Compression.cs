using System;
using System.IO;
using System.IO.Compression;

namespace Honjo.Framework.Util.Data
{
    /// <summary>
    /// Delegate for compression processers methods
    /// </summary>
    /// <param name="source">Source stream</param>
    /// <param name="dest">Destination stream</param>
    /// <param name="compressing">Whether the process operation is compressing (false = decompressing)</param>
    /// <param name="bufferSize">The buffer size to use when processing a potentially non-length-supporting stream. 512 is default</param>
    /// <param name="seekBack">Whether to go back at the start of the written data on the destination stream if supported. True by default</param>
    public delegate void CompressionProcesser(Stream source, Stream dest, bool compressing, int bufferSize = Compression.DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true);

    /// <summary>
    /// Compression pseudo-enum for dynamic registered compression methods
    /// </summary>
    public sealed class Compression : GenPseudoEnum<Compression>
    {
        /// <summary>
        /// Default buffer size when processing compressed data/data to compress
        /// </summary>
        public const int DEFAULT_COMPRESSION_BUFFER_SIZE = 512;
        /// <summary>
        /// Default framework-supported compressions
        /// </summary>
        public static readonly Compression
            UNKNOWN = new Compression(null),
            NONE = new Compression((Stream source, Stream dest, bool _, int bufferSize, bool seekBack) => source.CopyTo(dest, bufferSize, seekBack)),
            GZIP = new Compression(GzipCompression.Process),
            DEFLATE_RAW = new Compression(DeflateCompression.Process);

        internal CompressionProcesser Processer { get; private set; }

        private Compression(CompressionProcesser processer)
        {
            Processer = processer;
        }

        /// <summary>
        /// Process a byte array, returns its processed counterpart
        /// </summary>
        /// <param name="from">Source byte array to process</param>
        /// <param name="compressing">Whether to compress or not (false : decompressing)</param>
        public byte[] Process(byte[] from, bool compressing)
        {
            using (MemoryStream from_stream = new MemoryStream(from, 0, from.Length))
            {
                using (MemoryStream to_stream = new MemoryStream())
                {
                    //memory streams, no buffer needed
                    Processer(from_stream, to_stream, compressing);
                    return to_stream.ToArray();
                }
            }
        }

        /// <summary>
        /// Process data from a stream and writes its processed counterpart to another
        /// </summary>
        public void Process(Stream from, Stream to, bool compressing, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true) =>
            Processer?.Invoke(from, to, compressing, bufferSize, seekBack);

        /// <summary>
        /// Process a byte array, and writes its processed counterpart to a stream
        /// </summary>
        public void Process(byte[] from, Stream to, bool compressing, bool seekBack = true)
        {
            using (MemoryStream ms = new MemoryStream(from, 0, from.Length))
                Processer?.Invoke(ms, to, compressing, seekBack: seekBack);
        }

        /// <summary>
        /// Process data from a stream and returns its processed counterpart as a byte array
        /// </summary>
        public byte[] Process(Stream from, bool compressing, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Processer(from, ms, compressing, DEFAULT_COMPRESSION_BUFFER_SIZE);
                return ms.ToArray();
            }
        }


        /// <summary>
        /// Compresses data and returns it
        /// </summary>
        public byte[] Compress(byte[] data) => Process(data, true);

        /// <summary>
        /// Compresses data from a stream and writes it to another
        /// </summary>
        public void Compress(Stream from, Stream to, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true) =>
            Process(from, to, true, bufferSize, seekBack);

        /// <summary>
        /// Compresses data and writes it to a stream
        /// </summary>
        public void Compress(byte[] data, Stream to, bool seekBack = true) => Process(data, to, true, seekBack);

        /// <summary>
        /// Compresses data from a stream and returns it (the compressed data)
        /// </summary>
        public byte[] Compress(Stream from, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE) => Process(from, true, bufferSize);

        /// <summary>
        /// Decompresses data and returns it
        /// </summary>
        public byte[] Decompress(byte[] data) => Process(data, false);

        /// <summary>
        /// Decompresses data from a stream and writes it to another
        /// </summary>
        public void Decompress(Stream from, Stream to, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true) =>
            Process(from, to, false, bufferSize, seekBack);

        /// <summary>
        /// Decompresses data and writes it to a stream
        /// </summary>
        public void Decompress(byte[] data, Stream to, bool seekBack = true) => Process(data, to, false, seekBack);

        /// <summary>
        /// Decompresses data from a stream and returns it
        /// </summary>
        public byte[] Decompress(Stream from, int bufferSize = DEFAULT_COMPRESSION_BUFFER_SIZE) => Process(from, false, bufferSize);

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The string value of this serialization</returns>
        public override string ToString()
        {
            if (this == UNKNOWN)
                return "Unknown compression method (ERROR ?)";
            if (this == NONE)
                return "No compression method (as-is)";
            if (this == GZIP)
                return "GZip (deflate with CRC) compression method";
            if (this == DEFLATE_RAW)
                return "Raw deflate compression method";

            return "Custom non-string-referenced serialization method";
        }

        /// <summary>
        /// Registers a new compression method
        /// </summary>
        /// <param name="processer">The delegate to call when processing data to compress, <see cref="CompressionProcesser"/></param>
        /// <param name="setup">Delegate void to call when setting up the compression method</param>
        public static Compression Register(CompressionProcesser processer, Action setup = null)
        {
            setup?.Invoke();
            return new Compression(processer);
        }
    }

    internal static class GzipCompression
    {
        internal static void Process(Stream source, Stream dest, bool compressing, int bufferSize = Compression.DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true)
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
        internal static void Process(Stream source, Stream dest, bool compressing, int bufferSize = Compression.DEFAULT_COMPRESSION_BUFFER_SIZE, bool seekBack = true)
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
