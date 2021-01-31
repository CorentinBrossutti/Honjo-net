using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Abstract class for all symmetric crypting helpers
    /// </summary>
    public abstract class AbstractSymCrypt : StandardCryptHelper
    {
        /// <summary>
        /// The length of the sym key to use in bytes (thus AES256 -> 16)
        /// </summary>
        public abstract int KeyLengthByte { get; }
        /// <summary>
        /// Generates a new symmetric key for encryption/decryption
        /// </summary>
        /// <returns>The newly generated key</returns>
        public abstract KeyParameter Generate();
    }

    /// <summary>
    /// AES, ready to be used, symmetric crypting helper
    /// </summary>
    public sealed class AesCrypt : AbstractSymCrypt
    {
        /// <summary>
        /// Singleton
        /// </summary>
        public static readonly AesCrypt INSTANCE = new AesCrypt();
        private static readonly SecureRandom symRandom = new SecureRandom();

        /// <summary>
        /// The name of the algorithms/methods used by this helper
        /// </summary>
        public override string Name => "AES(256)/CBC/PKCS7";

        private AesCrypt() { }

        /// <summary>
        /// The length of the sym key to use in bytes (thus AES256 -> 16)
        /// </summary>
        public override int KeyLengthByte => 16;

        /// <summary>
        /// Generates a new symmetric key for encryption/decryption
        /// </summary>
        /// <returns>The newly generated key</returns>
        public override KeyParameter Generate()
        {
            byte[] kbs = new byte[KeyLengthByte];
            symRandom.NextBytes(kbs);
            return new KeyParameter(kbs);
        }

        /// <summary>
        /// Process data
        /// </summary>
        /// <param name="encrypting">Whether the goal is to encrypt (or to decrypt, if false)</param>
        /// <param name="data">Input data, as byte array</param>
        /// <param name="key">Encryption/decryption key</param>
        /// <returns>Processed byte array (data)</returns>
        protected override byte[] _Process(bool encrypting, byte[] data, ICipherParameters key)
        {
            PaddedBufferedBlockCipher cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()), new Pkcs7Padding());
            cipher.Init(encrypting, key);

            byte[] output = new byte[cipher.GetOutputSize(data.Length)];
            int len = cipher.ProcessBytes(data, output, 0);
            len += cipher.DoFinal(output, len);

            if(len != output.Length)
                Array.Resize(ref output, len);
            return output;
        }
    }
}
