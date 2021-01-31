using Honjo.Framework.Crypt;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using ProtoBuf;
using MessagePack;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet which contains only 1 argument : a symmetric encryption key, itself crypted with a pgp public key
    /// </summary>
    [Serializable, ProtoContract, MessagePackObject]
    public sealed class SymKeyPacket : AuthPacket
    {
        /// <summary>
        /// Symmetric crypting helper instance
        /// </summary>
        public static readonly AbstractSymCrypt SYM_CRYPT = AesCrypt.INSTANCE;

        /// <summary>
        /// The symmetric key as a byte array
        /// </summary>
        /// <seealso cref="GetKey(AbstractPgpCrypt, PgpPrivateKey)"/>
        [IgnoreMember]
        public new byte[] Contents => (byte[])base.Contents[0];
        /// <summary>
        /// Always returns the symmetric key contained, no matter the index
        /// </summary>
        [IgnoreMember]
        public new byte[] this[int _] => Contents;

        /// <summary>
        /// Constructs a new symmetric key packet
        /// </summary>
        public SymKeyPacket(byte specifier, KeyParameter key, AbstractPgpCrypt pgpCrypt, PgpPublicKey pgpPublicKey) : 
            base(specifier, pgpCrypt.Encrypt(key.GetKey(), pgpPublicKey.GetKey()))
        { }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public SymKeyPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public SymKeyPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }

        /// <summary>
        /// The returned value doesn't depend on index
        /// </summary>
        /// <returns>Always the symmetric key as a byte array pgp-encrypted</returns>
        /// <seealso cref="GetKey(AbstractPgpCrypt, PgpPrivateKey)"/>
        public new byte[] Get(int _) => Contents;

        /// <summary>
        /// Decrypts the symmetric key
        /// </summary>
        /// <param name="pgpCrypt">PGP asymmetric crypting helper instance</param>
        /// <param name="key">The private key to decrypt the symmetric key with</param>
        /// <returns>The decrypted, wrapped, symmetric key contained in this packet</returns>
        public KeyParameter GetKey(AbstractPgpCrypt pgpCrypt, PgpPrivateKey key)
        {
            byte[] decrypted = pgpCrypt.ProcessWithLength(false, Get(0), key.Key, out int length);
            Array.Resize(ref decrypted, SYM_CRYPT.KeyLengthByte);
            if (length > SYM_CRYPT.KeyLengthByte)
                throw new PgpKeyValidationException("Decryption yielded a result too large for an AES key");
            else if (length < SYM_CRYPT.KeyLengthByte)
            {
                //an error happened with leading zeros where they were put at the last place and the processed length was 15
                Array.Copy(decrypted, 0, decrypted, 1, length);
                decrypted[0] = 0;
            }

            return new KeyParameter(decrypted);
        }
    }
}
