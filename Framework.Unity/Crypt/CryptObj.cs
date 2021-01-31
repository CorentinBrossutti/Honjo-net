using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using ProtoBuf;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// A wrapper for an encrypted object. Data is NOT stored as clear, but instead as an encrypted byte array
    /// </summary>
    /// <typeparam name="T">Type of the object to store</typeparam>
    [Serializable, ProtoContract(SkipConstructor = true)]
    [ProtoInclude(1, typeof(CryptString))]
    public class CryptObj<T>
    {
        /// <summary>
        /// Helper to use (interfaced with encryption method)
        /// </summary>
        [NonSerialized]
        protected ICryptHelper _helper;
        /// <summary>
        /// Encryption method to use (id)
        /// </summary>
        [ProtoMember(2)]
        public byte _Encryption { get; private set; }
        /// <summary>
        /// The stored data, encrypted
        /// </summary>
        [ProtoMember(3)]
        public byte[] _TData { get; private set; }

        /// <summary>
        /// Helper property
        /// </summary>
        protected ICryptHelper _Helper
        {
            get
            {
                if (_helper == null)
                    _helper = ((Encryption)_Encryption).GetHelper();
                return _helper;
            }
            private set => _helper = value;
        }

        /// <summary>
        /// Constructs a new encrypted object for secure storing/transmission
        /// </summary>
        /// <param name="method">Encryption/decryption method to use</param>
        /// <param name="obj">Object to securely store</param>
        /// <param name="ckey">Key to use to encrypt the object</param>
        /// <param name="encodeFunc">Function to encode the object into a proper byte array</param>
        /// <remarks>A binary formatter may prove useful to serialize object into bytes</remarks>
        public CryptObj(Encryption method, T obj, ICipherParameters ckey, Func<T, byte[]> encodeFunc)
        {
            _Encryption = (byte)method;
            _TData = _Helper.Encrypt(encodeFunc(obj), ckey);
        }

        /// <summary>
        /// MessagePack-C# constructor
        /// </summary>
        public CryptObj(byte key0, byte[] key1)
        {
            _Encryption = key0;
            _TData = key1;
        }

        /// <summary>
        /// Decrypts the object securely stored
        /// </summary>
        /// <param name="dkey">Decipher key</param>
        /// <param name="decodeFunc">Function to use to transform the decrypted bytes into a proper object</param>
        /// <returns>The deciphered object</returns>
        public T Decrypt(ICipherParameters dkey, Func<byte[], T> decodeFunc) => decodeFunc(_Helper.Decrypt(_TData, dkey));
    }

    /// <summary>
    /// An object to securely store a string
    /// </summary>
    [Serializable, ProtoContract(SkipConstructor = true)]
    public class CryptString : CryptObj<string>
    {
        /// <summary>
        /// Constructs a new encrypted string container
        /// </summary>
        /// <param name="method">Encryption/decryption method to use</param>
        /// <param name="obj">String to store</param>
        /// <param name="ckey">Encrypting key</param>
        /// <param name="encoding">Encoding to use to encode/decode the string</param>
        public CryptString(Encryption method, string obj, ICipherParameters ckey, Encoding encoding) : base(method, obj, ckey, encoding.GetBytes)
        {}

        /// <summary>
        /// Constructs a new encrypted string container, using default utf8 encoding
        /// </summary>
        /// <param name="method">Encryption/decryption method to use</param>
        /// <param name="obj">String to store</param>
        /// <param name="ckey">Encrypting key</param>
        public CryptString(Encryption method, string obj, ICipherParameters ckey) : this(method, obj, ckey, Encoding.UTF8)
        {}

        /// <summary>
        /// MessagePack-C# constructor
        /// </summary>
        public CryptString(byte key0, byte[] key1) : base(key0, key1) { }

        /// <summary>
        /// Decrypts a securely stored string
        /// </summary>
        /// <param name="dkey">Decryption key</param>
        /// <param name="encoding">Encoding to use</param>
        public string Decrypt(ICipherParameters dkey, Encoding encoding)
        {
            //remaining \0s after decryption
            return Decrypt(dkey, encoding.GetString).Replace("\0", string.Empty);
        }

        /// <summary>
        /// Decrypts a securely stored string
        /// Will use utf-8 as a default encoding
        /// </summary>
        /// <param name="dkey">Decryption key</param>
        public string Decrypt(ICipherParameters dkey) => Decrypt(dkey, Encoding.UTF8);
    }
}
