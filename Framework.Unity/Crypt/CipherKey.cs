using Org.BouncyCastle.Crypto;

namespace Honjo.Framework.Crypt
{
    /// <summary>
    /// Container to avoid inclusion of bouncy castle in other assemblies
    /// </summary>
    public sealed class CipherKey
    {
        /// <summary>
        /// The BouncyCastle cipher key
        /// </summary>
        public ICipherParameters Key { get; private set; }

        /// <summary>
        /// Wraps a new CipherKey
        /// </summary>
        /// <param name="param">BouncyCastle key to wrap</param>
        public CipherKey(ICipherParameters param) => Key = param;
    }
}
