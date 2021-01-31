using System;
using System.IO;
using Honjo.Framework.Util;
using Org.BouncyCastle.Crypto;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Enum for natively supported encryption methods
    /// </summary>
    public enum Encryption
    {
        /// <summary>
        /// Unknown encryption method (parsing error ?)
        /// </summary>
        UNKNOWN,
        /// <summary>
        /// No encryption
        /// </summary>
        NONE,
        /// <summary>
        /// Asymmetric rsa 2048bit encryption
        /// </summary>
        ASYM_RSA2048,
        /// <summary>
        /// Summetric aes 256bit encryption
        /// </summary>
        SYM_AES256
    }

    /// <summary>
    /// Extensions methods for encryption
    /// </summary>
    public static class EncryptionExtensions
    {
        private static readonly NoEncryptionHelper noCrypt = new NoEncryptionHelper();

        /// <summary>
        /// Gets the type of this encryption method
        /// </summary>
        public static EncryptionType GetEncryptionType(this Encryption encryption)
        {
            switch(encryption)
            {
                case Encryption.NONE:
                    return EncryptionType.NONE;
                case Encryption.ASYM_RSA2048:
                    return EncryptionType.ASYMMETRIC;
                case Encryption.SYM_AES256:
                    return EncryptionType.SYMMETRIC;
            }
            return EncryptionType.UNDEFINED;
        }

        /// <summary>
        /// Gets the helper for this encryption
        /// </summary>
        public static ICryptHelper GetHelper(this Encryption encryption)
        {
            switch(encryption)
            {
                case Encryption.NONE:
                    return noCrypt;
                case Encryption.ASYM_RSA2048:
                    return RsaCrypt.INSTANCE;
                case Encryption.SYM_AES256:
                    return AesCrypt.INSTANCE;
            }
            throw new ArgumentException("Unrecognized encryption method: " + encryption);
        }
    }

    internal class NoEncryptionHelper : ICryptHelper
    {
        public string Name => "NONE";

        public byte[] Decrypt(byte[] encryptedData, ICipherParameters dkey) => encryptedData;

        public byte[] Decrypt(Stream data_in, ICipherParameters dkey, int length) => data_in.Read(length);

        public void Decrypt(Stream data_in, Stream data_out, ICipherParameters dkey, int length, int offset = 0, bool seekBack = true)
        {
            byte[] output = Decrypt(data_in, dkey, length);
            data_out.Write(output, offset, length);

            if (seekBack && data_out.CanSeek)
                data_out.Seek(-length, SeekOrigin.Current);
        }

        public byte[] Encrypt(byte[] data, ICipherParameters ckey) => data;

        public byte[] Encrypt(Stream data_in, ICipherParameters ckey, int length) => data_in.Read(length);

        public void Encrypt(Stream data_in, Stream data_out, ICipherParameters ckey, int length, int offset = 0, bool seekBack = true)
        {
            byte[] output = Encrypt(data_in, ckey, length);
            data_out.Write(output, offset, length);

            if (seekBack && data_out.CanSeek)
                data_out.Seek(-length, SeekOrigin.Current);
        }
    }

    /// <summary>
    /// Encryption types
    /// </summary>
    public enum EncryptionType
    {
        /// <summary>
        /// Undefined (unknown)
        /// </summary>
        UNDEFINED,
        /// <summary>
        /// No encryption (raw)
        /// </summary>
        NONE,
        /// <summary>
        /// Symmetric encryption and derivatives
        /// </summary>
        SYMMETRIC,
        /// <summary>
        /// Asymmetric encryption and derivatives
        /// </summary>
        ASYMMETRIC
    }
}
