using Honjo.Framework.Crypt;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.X509;
using System;
using ProtoBuf;
using MessagePack;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet who has only 1 argument : a pgp public key
    /// </summary>
    [Serializable, ProtoContract, MessagePackObject]
    public sealed class PgpPublicKeyPacket : AuthPacket
    {
        /// <summary>
        /// Pgp crypt method to use
        /// </summary>
        public static readonly AbstractPgpCrypt PGP_CRYPT = RsaCrypt.INSTANCE;

        [NonSerialized, IgnoreMember]
        private PgpPublicKey __key;

        /// <summary>
        /// The public key
        /// </summary>
        [IgnoreMember]
        public new PgpPublicKey Contents
        {
            get
            {
                if(__key == null)
                {
                    if (!PGP_CRYPT.ToPublicKey((byte[])base.Contents[0], (DateTime)base.Contents[1], out PgpPublicKey temp, (byte[])base.Contents[2]))
                        throw new PgpKeyValidationException("Re-assembled public key does not match with original");
                    __key = temp;
                }
                return __key;
            }
        }
        /// <summary>
        /// Always returns the pgp public key contained, no matter the index
        /// </summary>
        [IgnoreMember]
        public new PgpPublicKey this[int _] => Contents;

        /// <summary>
        /// Constructs a new packet for a pgp public key
        /// </summary>
        /// <param name="specifier">Specifier to use with the packet</param>
        /// <param name="key">The key to send</param>
        public PgpPublicKeyPacket(byte specifier, PgpPublicKey key) :
            base(specifier, SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key.GetKey()).GetEncoded(), key.CreationTime, key.GetFingerprint())
        { }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public PgpPublicKeyPacket() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public PgpPublicKeyPacket(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) :
            base(key0, key1, key2, key3, key4, key5, key6)
        { }

        /// <summary>
        /// The return value does not depend on index
        /// </summary>
        /// <returns>Always the public key sent</returns>
        public new PgpPublicKey Get(int _) => Contents;
    }
}
