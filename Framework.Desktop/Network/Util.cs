using Honjo.Framework.Network.Packets;
using System;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Utility class for network operations
    /// </summary>
    public static class NetworkUtil
    {
        //for pre-packet lengths below, this is a simple conversion to a 3-digit base 255 number instead of the old, cost-heavy, concatenation
        /// <summary>
        /// Unwraps the size in a pre-packet length header
        /// </summary>
        public static int UnwrapSize(this byte[] from)
        {
            if (from.Length != Packet.SBYTES_HEADER_SIZE)
                throw new ArgumentException("Invalid length for given byte array");

            int result = 0;
            for (int i = 0; i < from.Length; i++)
                //the cast is safe, this is a product
                result += from[i] * (int)Math.Pow(byte.MaxValue, i);

            return result;
        }

        /// <summary>
        /// Wraps a given size into a pre-packet length header
        /// </summary>
        public static byte[] WrapSize(this int size)
        {
            if (size > Packet.HEADER_MAX_VALUE)
                throw new ArgumentException("Given size is too great");

            byte[] result = new byte[Packet.SBYTES_HEADER_SIZE];
            for (int i = result.Length - 1; i >= 0; i--)
            {
                result[i] = (byte)(size / (int)Math.Pow(byte.MaxValue, i));
                size = size % (int)Math.Pow(byte.MaxValue, i);
            }

            return result;
        }
    }
}
