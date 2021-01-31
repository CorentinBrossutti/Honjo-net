using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Linq;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Abstract asymmetric crypting helper (pgp)
    /// </summary>
    public abstract class AbstractPgpCrypt : StandardCryptHelper
    {
        /// <summary>
        /// Wraps a byte array key under a <code>PgpPublicKey</code>
        /// </summary>
        /// <param name="keyData">The key, raw, as a byte array</param>
        /// <param name="stamp">The creation date for the key</param>
        /// <param name="key">Output public key</param>
        /// <param name="fingerprint">Original fingerprint to check matches</param>
        /// <returns>Whether the key was successfully unwrapped and matches</returns>
        public abstract bool ToPublicKey(byte[] keyData, DateTime stamp, out PgpPublicKey key, byte[] fingerprint = null);

        /// <summary>
        /// Generates a new pgp key pair
        /// </summary>
        /// <returns>Generated pgp key pair</returns>
        public abstract PgpKeyPair Generate();

        /// <summary>
        /// Process asymmetric data always giving back the processed length information
        /// </summary>
        public abstract byte[] ProcessWithLength(bool encrypting, byte[] in_data, ICipherParameters keyParameter, out int length);

        /// <summary>
        /// Process data
        /// </summary>
        /// <param name="encrypting">Whether the goal is to encrypt (or to decrypt, if false)</param>
        /// <param name="in_data">Input data, as byte array</param>
        /// <param name="keyParameter">Encryption/decryption key</param>
        /// <returns>Processed byte array (data)</returns>
        protected override byte[] _Process(bool encrypting, byte[] in_data, ICipherParameters keyParameter) => ProcessWithLength(encrypting, in_data, keyParameter, out _);
    }

    /// <summary>
    /// RSA crypting helper ready to be instancied
    /// </summary>
    public sealed class RsaCrypt : AbstractPgpCrypt
    {
        /// <summary>
        /// Singleton
        /// </summary>
        public static readonly RsaCrypt INSTANCE = new RsaCrypt();
        private static readonly SecureRandom pgpRandom = new SecureRandom();

        /// <summary>
        /// The name of the algorithms/methods used by this helper
        /// </summary>
        public override string Name => "PGP (RSA2048)";

        private RsaCrypt() { }

        /// <summary>
        /// Process asymmetric data using a custom expected length.
        /// The output will ALWAYS be resized to fit expected length
        /// </summary>
        public override byte[] ProcessWithLength(bool encrypting, byte[] in_data, ICipherParameters keyParameter, out int length)
        {
            BufferedAsymmetricBlockCipher bcipher = new BufferedAsymmetricBlockCipher(new RsaEngine());
            bcipher.Init(encrypting, keyParameter);

            byte[] out_data = new byte[bcipher.GetOutputSize(in_data.Length)];
            int len = bcipher.DoFinal(in_data, out_data, 0);

            length = len;
            return out_data;
        }

        /// <summary>
        /// Generates a new pgp key pair
        /// </summary>
        /// <returns>Generated pgp key pair</returns>
        public override PgpKeyPair Generate()
        {
            IAsymmetricCipherKeyPairGenerator generator = GeneratorUtilities.GetKeyPairGenerator("RSA");
            generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), pgpRandom, 2048, 12));
            PgpKeyPair keyPair = new PgpKeyPair(PublicKeyAlgorithmTag.RsaGeneral, generator.GenerateKeyPair(), DateTime.Now);

            return keyPair;
        }

        /// <summary>
        /// Wraps a byte array key under a <code>PgpPublicKey</code>
        /// </summary>
        /// <param name="keyData">The key, raw, as a byte array</param>
        /// <param name="stamp">The creation date for the key</param>
        /// <param name="key">Output public key</param>
        /// <param name="fingerprint">Original fingerprint to check matches</param>
        /// <returns>Whether the key was successfully unwrapped and matches</returns>
        /// <remarks>You have to use <code>SubjectPublicKeyInfo</code> and <code>SubjectPublicKeyInfoFactory</code> to have a proper array when decomposing a key</remarks>
        /// <see cref="Org.BouncyCastle.Asn1.X509.SubjectPublicKeyInfo"/>
        /// <see cref="Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory"/>
        public override bool ToPublicKey(byte[] keyData, DateTime stamp, out PgpPublicKey key, byte[] fingerprint = null)
        {
            key = new PgpPublicKey(PublicKeyAlgorithmTag.RsaGeneral, PublicKeyFactory.CreateKey(keyData), stamp);
            //shouldn't happen but I'm paranoid
            return Enumerable.SequenceEqual(fingerprint, key.GetFingerprint());
        }
    }
}
