using System;
using System.IO;
using System.Net.Sockets;

namespace Honjo.Framework.Util
{
    /// <summary>
    /// Utility class for stream methods
    /// </summary>
    public static class StreamUtil
    {
        /// <summary>
        /// Do not use it for large streams, from which you haven't read from awhile!
        /// Also does not work for streams for which you can't get length
        /// </summary>
        /// <param name="stream">The stream</param>
        /// <returns>The remaining number of bytes to read</returns>
        [Obsolete("Heavy and does not work for pretty much anything")]
        public static int GetRemainingBytes(this Stream stream)
        {
            try
            {
                if (stream is NetworkStream)
                    throw new InvalidOperationException("Cannot get remanining bytes for a network stream!");

                return (int) (stream.Length - stream.Position);
            }catch (Exception e)
            {
                if (e is NotSupportedException || e is InvalidCastException)
                    return -1;
                throw;
            }
        }

        /// <summary>
        /// Reads all remaining bytes in the stream. Very heavy and does not work for streams without length (network)
        /// </summary>
        [Obsolete("Based on a poor implementation to get the remaining bytes of a stream")]
        public static byte[] ReadRemaining(this Stream stream_in)
        {
            int rem = GetRemainingBytes(stream_in);
            if (rem <= 0)
                return null;

            return Read(stream_in, rem);
        }

        /// <summary>
        /// Reads <b>length</b> bytes from the given stream
        /// </summary>
        public static byte[] Read(this Stream stream_in, int length)
        {
            byte[] buf = new byte[length];
            stream_in.Read(buf, 0, length);

            return buf;
        }

        /// <summary>
        /// Copies the contents of a stream to another.
        /// Returns the number of bytes copied
        /// </summary>
        /// <param name="stream_in">Input stream</param>
        /// <param name="stream_out">Output stream</param>
        /// <param name="bufferSize">Buffer size to use if the input stream does not support length query. 512 by default.</param>
        /// <param name="seekBack">Whether to roll the stream back at its previous position if it supports it</param>
        public static int CopyTo(this Stream stream_in, Stream stream_out, int bufferSize = 512, bool seekBack = true)
        {
            byte[] buffer;
            int back = 0;

            if(stream_in.CanSeek)
            {
                buffer = Read(stream_in, (int)stream_in.Length);
                stream_out.Write(buffer, 0, buffer.Length);
                back = buffer.Length;
            }
            else
            {
                int read;
                buffer = new byte[bufferSize];
                while ((read = stream_in.Read(buffer, 0, bufferSize)) > 0)
                {
                    stream_out.Write(buffer, 0, read);
                    back += read;
                }
            }

            if (seekBack && stream_out.CanSeek)
                stream_out.Seek(-back, SeekOrigin.Current);
            return back;
        }
    }
}
