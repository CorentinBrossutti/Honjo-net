using System;
using System.Security.Cryptography;
using System.Text;

namespace Honjo.Framework.Crypt.Hashing
{
    /// <summary>
    /// Sha256 hashing
    /// </summary>
    public static class Sha256
    {
        /// <summary>
        /// Hashes an array of byte using SHA-256
        /// </summary>
        /// <param name="bytes">Bytes to hash</param>
        /// <returns>Hash bytes</returns>
        public static byte[] Hash(byte[] bytes) => new SHA256Managed().ComputeHash(bytes);

        /// <summary>
        /// Hashes a string using SHA-256. Assumes utf-8 encoding.
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>The hashed string</returns>
        public static string Hash(string str) => Hashing.__HashStrUtf8(str, Hash);

        /// <summary>
        /// Hashes an array of bytes <paramref name="iterations"/> times using SHA-256
        /// </summary>
        /// <param name="bytes">Bytes to hash</param>
        /// <param name="iterations">Number of times to hash/re-hash</param>
        /// <param name="recursive">Whether to process iterations recursively. False by default</param>
        /// <returns>Hashed bytes</returns>
        public static byte[] Hash(byte[] bytes, uint iterations, bool recursive = false)
        {
            if (iterations == 0)
                return bytes;

            if (!recursive)
            {
                byte[] temp = (byte[])bytes.Clone();
                for (int i = 0; i < iterations; i++)
                    temp = Hash(temp);
                return temp;
            }

            if (iterations <= 1)
                return Hash(bytes);
            return Hash(bytes, iterations - 1, true);
        }

        /// <summary>
        /// Hashes a string <paramref name="iterations"/> times using SHA-256
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <param name="iterations">Number of times to hash/re-hash</param>
        /// <param name="recursive">Whether to process iterations recursively. False by default</param>
        /// <returns>Hashed string</returns>
        public static string Hash(string str, uint iterations, bool recursive = false)
        {
            if (iterations == 0)
                return str;

            if (!recursive)
            {
                string hash = String.Copy(str);
                for (int i = 0; i < iterations; i++)
                    hash = Hash(hash);
                return hash;
            }

            if (iterations <= 1)
                return Hash(str);
            return Hash(str, iterations - 1, true);
        }
    }

    /// <summary>
    /// Sha512 hashing
    /// </summary>
    public static class Sha512
    {
        /// <summary>
        /// Hashes an array of bytes using SHA-512
        /// </summary>
        /// <param name="bytes">Bytes to hash</param>
        /// <returns>Hashed bytes</returns>
        public static byte[] Hash(byte[] bytes) => new SHA512Managed().ComputeHash(bytes);

        /// <summary>
        /// Hashes a string using SHA-512
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>Hashed string</returns>
        public static string Hash(string str) => Hashing.__HashStrUtf8(str, Hash);

        /// <summary>
        /// Hashes an array of bytes <paramref name="iterations"/> times using SHA-512
        /// </summary>
        /// <param name="bytes">Bytes to hash</param>
        /// <param name="iterations">Number of times to hash/re-hash</param>
        /// <param name="recursive">Whether to process iterations recursively. False by default</param>
        /// <returns>Hashed bytes</returns>
        public static byte[] Hash(byte[] bytes, int iterations, bool recursive = false)
        {
            if (!recursive)
            {
                byte[] temp = (byte[])bytes.Clone();
                for (int i = 0; i < iterations; i++)
                {
                    temp = Hash(temp);
                }
                return temp;
            }

            if (iterations <= 1)
                return Hash(bytes);
            return Hash(bytes, iterations - 1, true);
        }

        /// <summary>
        /// Hashes a string <paramref name="iterations"/> times using SHA-512
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <param name="iterations">Number of times to hash/re-hash</param>
        /// <param name="recursive">Whether to process iterations recursively. False by default</param>
        /// <returns>Hashed string</returns>
        public static string Hash(string str, int iterations, bool recursive = false)
        {
            if (!recursive)
            {
                string temp = String.Copy(str);
                for (int i = 0; i < iterations; i++)
                {
                    temp = Hash(temp);
                }
                return temp;
            }

            if (iterations <= 1)
                return Hash(str);
            return Hash(str, iterations - 1, true);
        }
    }

    /// <summary>
    /// BCrypt hashing, mainly there to interface without having to reference bouncy castle directly
    /// </summary>
    public static class Bcrypt
    {
        /// <summary>
        /// The Bcrypt revision to use by default
        /// </summary>
        public const BCrypt.Net.SaltRevision REVISION = BCrypt.Net.SaltRevision.Revision2A;

        /// <summary>
        /// Hashes a string using bcrypt algorithm. Very secure but not meant to be used in transmission since output depends on plain input password.
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <param name="rounds">Number of hashing rounds to use. Default : 10</param>
        /// <returns>Hashed string using BCrypt</returns>
        public static string Hash(string str, int rounds = 10) => BCrypt.Net.BCrypt.HashPassword(str, rounds, REVISION);

        /// <summary>
        /// Verify the equality between a hash and a given password string
        /// </summary>
        /// <param name="str">String candidate to verify</param>
        /// <param name="hash">Hash to test</param>
        /// <returns>Whether the password matches the hash</returns>
        public static bool Verify(string str, string hash) => BCrypt.Net.BCrypt.Verify(str, hash);

        /// <summary>
        /// Generates a bcrypt salt
        /// </summary>
        /// <param name="factor">Work factor. Default : 10</param>
        /// <returns>BCrypt random generated salt</returns>
        public static string Salt(int factor = 10) => BCrypt.Net.BCrypt.GenerateSalt(factor, REVISION);

        /// <summary>
        /// Basic bcrypt hash function using a given salt
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <param name="salt">Salt to use</param>
        /// <returns>Hashed string</returns>
        public static string Hash(string str, string salt) => BCrypt.Net.BCrypt.HashPassword(str, salt);
    }

    /// <summary>
    /// Base class for all hashing classes. Contains mostly utility methods
    /// </summary>
    public static class Hashing
    {
        /// <summary>
        /// Hashes a string using a given encoding an a given hash function
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <param name="encoding">Encoding to use</param>
        /// <param name="hashFunc">Hash function to call and use</param>
        /// <returns>The hashed string</returns>
        public static string HashString(string str, Encoding encoding, Func<byte[], byte[]> hashFunc) => __HashedBArrayToStr(hashFunc(encoding.GetBytes(str)));

        internal static string __HashedBArrayToStr(byte[] hashedBytes)
        {
            string hashString = string.Empty;
            //using a simple for loop because its cheaper
            for (int i = 0; i < hashedBytes.Length; i++)
                hashString += String.Format("{0:x2}", hashedBytes[i]);

            return hashString;
        }

        internal static string __HashStrUtf8(string str, Func<byte[], byte[]> hashFunction) => __HashedBArrayToStr(hashFunction(Encoding.UTF8.GetBytes(str)));
    }
}
