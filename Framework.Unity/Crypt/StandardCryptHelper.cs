using System.IO;
using Honjo.Framework.Util;
using Org.BouncyCastle.Crypto;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Abstract basic interface implementation for all crypting helpers
    /// </summary>
    public abstract class StandardCryptHelper : ICryptHelper
    {
        /// <summary>
        /// The name of the algorithms/methods used by this helper
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Process data
        /// </summary>
        /// <param name="encrypting">Whether the goal is to encrypt (or to decrypt, if false)</param>
        /// <param name="in_data">Input data, as byte array</param>
        /// <param name="key">Encryption/decryption key</param>
        /// <returns>Processed byte array (data)</returns>
        protected abstract byte[] _Process(bool encrypting, byte[] in_data, ICipherParameters key);

        /// <summary>
        /// Encrypts data
        /// </summary>
        /// <param name="data">Data to encrypt as a byte array</param>
        /// <param name="ckey">Encryption key</param>
        /// <returns>The data as its encrypted form</returns>
        public byte[] Encrypt(byte[] data, ICipherParameters ckey) => _Process(true, data, ckey);

        /// <summary>
        /// Reads data from a stream and encrypts it
        /// </summary>
        /// <param name="data_in">Stream to read from</param>
        /// <param name="ckey">Encryption key</param>
        /// <param name="length">Number of bytes to read from the stream for encryption</param>
        public byte[] Encrypt(Stream data_in, ICipherParameters ckey, int length) => Encrypt(StreamUtil.Read(data_in, length), ckey);

        /// <summary>
        /// Reads data from a stream, encrypts it, and writes to an output stream
        /// </summary>
        /// <param name="data_in">Stream to read the data from</param>
        /// <param name="data_out">Stream to write the encrypted data to</param>
        /// <param name="ckey">Encryption key</param>
        /// <param name="length">Number of bytes to read from the stream for encryption</param>
        /// <param name="offset">Offset to write to the output stream. Default : 0</param>
        /// <param name="seekBack">Whether to put the position in the stream back where it was (just before the encrypted data). Default: true. Does not work if the stream cannot seek.</param>
        public void Encrypt(Stream data_in, Stream data_out, ICipherParameters ckey, int length, int offset = 0, bool seekBack = true)
        {
            byte[] bout = Encrypt(data_in, ckey, length);
            data_out.Write(bout, offset, bout.Length);

            if (data_out.CanSeek && seekBack)
                data_out.Seek(-bout.Length, SeekOrigin.Current);
        }

        /// <summary>
        /// Decrypts data that was previously encrypted
        /// </summary>
        /// <param name="encryptedData">Encrypted data as a byte array</param>
        /// <param name="dkey">Decryption key</param>
        /// <returns>The raw data, decrypted</returns>
        public byte[] Decrypt(byte[] encryptedData, ICipherParameters dkey) => _Process(false, encryptedData, dkey);

        /// <summary>
        /// Reads data from a stream and decrypts it
        /// </summary>
        /// <param name="data_in">The stream to read the encrypted data from</param>
        /// <param name="dkey">Decryption key</param>
        /// <param name="length">Number of bytes to read from stream for decryption</param>
        /// <returns>The raw, decrypted data as a byte array</returns>
        public byte[] Decrypt(Stream data_in, ICipherParameters dkey, int length) => Decrypt(StreamUtil.Read(data_in, length), dkey);

        /// <summary>
        /// Reads data from a stream, decrypts it, and writes it to an output stream
        /// </summary>
        /// <param name="data_in">Stream to read the encrypted data from</param>
        /// <param name="data_out">Stream to write the decrypted data to</param>
        /// <param name="dkey">Decryption key</param>
        /// <param name="length">Number of bytes to read from the stream for decryption</param>
        /// <param name="offset">Offset to write to the output stream. Default : 0</param>
        /// <param name="seekBack">Whether to put the position in the stream back where it was (just before the decrypted data). Default: true. Does not work if the stream cannot seek.</param>
        public void Decrypt(Stream data_in, Stream data_out, ICipherParameters dkey, int length, int offset = 0, bool seekBack = true)
        {
            byte[] bout = Decrypt(data_in, dkey, length);
            data_out.Write(bout, offset, bout.Length);

            if (data_out.CanSeek && seekBack)
                data_out.Seek(-bout.Length, SeekOrigin.Current);
        }

        /// <summary>
        /// Decrypts data from a byte array, and writes it to a stream
        /// </summary>
        /// <param name="encryptedData">Encrypted data</param>
        /// <param name="data_out">Stream to write the decrypted data to</param>
        /// <param name="dkey">Decryption key</param>
        /// <param name="offset">Offset to write to the output stream. Default : 0</param>
        /// <param name="seekBack">Whether to put the position in the stream back where it was (just before the decrypted data). Default: true. Does not work if the stream cannot seek.</param>
        public void Decrypt(byte[] encryptedData, Stream data_out, ICipherParameters dkey, int offset = 0, bool seekBack = true)
        {
            byte[] bout = Decrypt(encryptedData, dkey);
            data_out.Write(bout, offset, bout.Length);

            if (data_out.CanSeek && seekBack)
                data_out.Seek(-bout.Length, SeekOrigin.Current);
        }

        /// <summary>
        /// String representation of this crypting helper
        /// </summary>
        public override string ToString() => "Crypt helper ; using " + Name;
    }
}
