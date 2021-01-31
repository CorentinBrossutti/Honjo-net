using System.IO;
using Org.BouncyCastle.Crypto;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Top-level (inheritance) for all crypting helpers
    /// </summary>
    public interface ICryptHelper
    {
        /// <summary>
        /// The name of the algorithms/methods used by this helper
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Encrypts data
        /// </summary>
        /// <param name="data">Data to encrypt as a byte array</param>
        /// <param name="ckey">Encryption key</param>
        /// <returns>The data as its encrypted form</returns>
        byte[] Encrypt(byte[] data, ICipherParameters ckey);
        /// <summary>
        /// Reads data from a stream and encrypts it
        /// </summary>
        /// <param name="data_in">Stream to read from</param>
        /// <param name="ckey">Encryption key</param>
        /// <param name="length">Number of bytes to read from the stream for encryption</param>
        /// <returns>The data as its encrypted form</returns>
        byte[] Encrypt(Stream data_in, ICipherParameters ckey, int length);
        /// <summary>
        /// Reads data from a stream, encrypts it, and writes to an output stream
        /// </summary>
        /// <param name="data_in">Stream to read the data from</param>
        /// <param name="data_out">Stream to write the encrypted data to</param>
        /// <param name="ckey">Encryption key</param>
        /// <param name="length">Number of bytes to read from the stream for encryption</param>
        /// <param name="offset">Offset to write to the output stream. Default : 0</param>
        /// <param name="seekBack">Whether to put the position in the stream back where it was (just before the encrypted data). Default: true. Does not work if the stream cannot seek.</param>
        void Encrypt(Stream data_in, Stream data_out, ICipherParameters ckey, int length, int offset = 0, bool seekBack = true);

        /// <summary>
        /// Decrypts data that was previously encrypted
        /// </summary>
        /// <param name="encryptedData">Encrypted data as a byte array</param>
        /// <param name="dkey">Decryption key</param>
        /// <returns>The raw data, decrypted</returns>
        byte[] Decrypt(byte[] encryptedData, ICipherParameters dkey);
        /// <summary>
        /// Reads data from a stream and decrypts it
        /// </summary>
        /// <param name="data_in">The stream to read the encrypted data from</param>
        /// <param name="dkey">Decryption key</param>
        /// <param name="length">Number of bytes to read from stream for decryption</param>
        /// <returns>The raw, decrypted data as a byte array</returns>
        byte[] Decrypt(Stream data_in, ICipherParameters dkey, int length);
        /// <summary>
        /// Reads data from a stream, decrypts it, and writes it to an output stream
        /// </summary>
        /// <param name="data_in">Stream to read the encrypted data from</param>
        /// <param name="data_out">Stream to write the decrypted data to</param>
        /// <param name="dkey">Decryption key</param>
        /// <param name="length">Number of bytes to read from the stream for decryption</param>
        /// <param name="offset">Offset to write to the output stream. Default : 0</param>
        /// <param name="seekBack">Whether to put the position in the stream back where it was (just before the decrypted data). Default: true. Does not work if the stream cannot seek.</param>
        void Decrypt(Stream data_in, Stream data_out, ICipherParameters dkey, int length, int offset = 0, bool seekBack = true);
    }
}
