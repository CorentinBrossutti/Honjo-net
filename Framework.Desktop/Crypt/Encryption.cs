using Honjo.Framework.Util;
using Org.BouncyCastle.Crypto;
using System;
using System.IO;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Base encryption, does not support generics and thus returns ICryptHelpers
    /// </summary>
    public abstract class EncryptionBase : GenPseudoEnum<EncryptionBase>
    {
        /// <summary>
        /// Framework base values for encryption (unknown or none at all)
        /// </summary>
        public static readonly EncryptionBase UNKNOWN = new Encryption<ICryptHelper>(null, EncryptionType.UNDEFINED),
            NONE = new Encryption<ICryptHelper>(new NoEncryptionHelper(), EncryptionType.NONE);
        /// <summary>
        /// Default framework-supported asymmetric (pgp) encryption methods
        /// </summary>
        public static readonly Encryption<AbstractPgpCrypt> ASYM_RSA2048 = Register<AbstractPgpCrypt>(RsaCrypt.INSTANCE, EncryptionType.ASYMMETRIC);
        /// <summary>
        /// Default framework-supported symmetric encryption methods
        /// </summary>
        public static readonly Encryption<AbstractSymCrypt> SYM_AES256 = Register<AbstractSymCrypt>(AesCrypt.INSTANCE, EncryptionType.SYMMETRIC);

        /// <summary>
        /// The crypt helper of this encryption type
        /// </summary>
        public ICryptHelper Helper { get; protected set; }
        /// <summary>
        /// The encryption type
        /// </summary>
        public EncryptionType Type { get; protected set; } 

        /// <summary>
        /// ...
        /// </summary>
        protected EncryptionBase(ICryptHelper helper, EncryptionType type) : base(typeof(EncryptionBase))
        {
            Helper = helper;
            Type = type;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>The string value of this encryption</returns>
        public override string ToString()
        {
            if (this == UNKNOWN)
                return "Unknown encryption method (ERROR ?)";
            if (this == NONE)
                return "No encryption method (as-is)";
            if (this == ASYM_RSA2048)
                return "Asymmetric RSA 2048 (PGP encapsulation) encryption method";
            if (this == SYM_AES256)
                return "Symmetric AES256/CBC/PKCS7 encryption method";

            return "Custom non-string-referenced encryption method";
        }

        /// <summary>
        /// Registers a new encryption
        /// </summary>
        /// <typeparam name="T">Type param for the helper</typeparam>
        /// <param name="helper">Helper</param>
        /// <param name="type">Encryption type</param>
        /// <param name="setup">Setup method to call before creating the helper</param>
        public static Encryption<T> Register<T>(T helper, EncryptionType type, Action setup = null) where T : ICryptHelper
        {
            setup?.Invoke();
            return new Encryption<T>(helper, type);
        }
    }

    /// <summary>
    /// Defines default supported encryption methods
    /// </summary>
    public sealed class Encryption<THelper> : EncryptionBase where THelper : ICryptHelper
    {
        /// <summary>
        /// Crypting helper
        /// </summary>
        public new THelper Helper
        {
            get => (THelper) base.Helper;

            set => base.Helper = value;
        }

        internal Encryption(ICryptHelper helper, EncryptionType type) : base(helper, type) { }
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