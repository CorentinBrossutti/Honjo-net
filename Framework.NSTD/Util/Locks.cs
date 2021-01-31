using System.Collections.Generic;

namespace Honjo.Framework.Util.Concurrent
{
    /// <summary>
    /// A handy class to reference immutable, readonly object instances to lock upon
    /// </summary>
    public class Locks
    {
        /// <summary>
        /// Internal dictionary for locks
        /// </summary>
        protected readonly Dictionary<string, object> _internLocks = new Dictionary<string, object>();

        /// <summary>
        /// Get the lock object linked to a given key
        /// </summary>
        public virtual object this[string s]
        {
            get
            {
                if (!_internLocks.ContainsKey(s))
                {
                    object o = new object();
                    _internLocks.Add(s, o);
                    return o;
                }
                return _internLocks[s];
            }
        }

        /// <summary>
        /// Constructs a new lock container
        /// </summary>
        /// <param name="baseLocks">Base locks to register</param>
        public Locks(params string[] baseLocks)
        {
            foreach (var baseLock in baseLocks)
                _internLocks.Add(baseLock, new object());
        }
    }
}
