using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Honjo.Framework.Util
{
    /// <summary>
    /// Base class for pseudo enum values
    /// Allows the treatment of values very much like an enum but dynamically (you can add custom values at runtime)
    /// </summary>
    /// <remarks>Adisable to use a factory-like register method to emphasize the uniqueness of each object. Also, the id 0 (first register) must be the undefined value (or default value).</remarks>
    public abstract class PseudoEnum
    {
        private static Dictionary<string, List<PseudoEnum>> __REGISTERED = new Dictionary<string, List<PseudoEnum>>();
        private static object __REGISTERED_LOCK = new object();
        private static Dictionary<string, byte> __IDS = new Dictionary<string, byte>();

        /// <summary>
        /// Id of a pseudo enum member
        /// </summary>
        public byte Id { get; protected set; }

        /// <summary>
        /// Constructs a new pseudo enum
        /// </summary>
        public PseudoEnum()
        {
            if (!__IDS.ContainsKey(GetType().AssemblyQualifiedName))
                __IDS.Add(GetType().AssemblyQualifiedName, 0);
            //ensure no id overflow
            Id = checked(__IDS[GetType().AssemblyQualifiedName]++);

            if (!__REGISTERED.ContainsKey(GetType().AssemblyQualifiedName))
                __REGISTERED.Add(GetType().AssemblyQualifiedName, new List<PseudoEnum>());
            __REGISTERED[GetType().AssemblyQualifiedName].Add(this);
        }

        /// <summary>
        /// Constructs a new pseudo enum with a custom registration type
        /// </summary>
        public PseudoEnum(Type type)
        {
            if (!__IDS.ContainsKey(type.AssemblyQualifiedName))
                __IDS.Add(type.AssemblyQualifiedName, 0);
            //ensure no id overflow
            Id = checked(__IDS[type.AssemblyQualifiedName]++);

            if (!__REGISTERED.ContainsKey(type.AssemblyQualifiedName))
                __REGISTERED.Add(type.AssemblyQualifiedName, new List<PseudoEnum>());
            __REGISTERED[type.AssemblyQualifiedName].Add(this);
        }

        /// <summary>
        /// Equals override
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj != null && obj is PseudoEnum pseudoEnum && pseudoEnum.Id == Id;
        }

        /// <summary>
        /// GetHashCode override
        /// </summary>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        /// <summary>
        /// Equality operator overload to check for ids
        /// </summary>
        public static bool operator ==(PseudoEnum pe1, PseudoEnum pe2) => pe1?.Equals(pe2) ?? (object)pe2 == null;

        /// <summary>
        /// Inequality operator overload to check for ids
        /// </summary>
        public static bool operator !=(PseudoEnum pe1, PseudoEnum pe2) => !(pe1 == pe2);

        /// <summary>
        /// Get a pseudo enum value from its id
        /// </summary>
        public static PseudoEnum Get(Type type, byte id)
        {
            lock(__REGISTERED_LOCK)
            {
                if (!__REGISTERED.ContainsKey(type.AssemblyQualifiedName))
                {
                    //try to run static constructor (init class)
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    if (!__REGISTERED.ContainsKey(type.AssemblyQualifiedName))
                        throw new ArgumentException("Type " + type.Name + " could NOT be registered as a pseudo-enum type");
                }
                if (__REGISTERED[type.AssemblyQualifiedName].Count > id)
                    return __REGISTERED[type.AssemblyQualifiedName][id];
                else
                {
                    if (__REGISTERED[type.AssemblyQualifiedName].Count == 0)
                        throw new ArgumentOutOfRangeException("No default value (in fact, no value) has been registered for the given pseudo enum !");
                    else
                        return __REGISTERED[type.AssemblyQualifiedName][0];
                }
            }
        }

        /// <summary>
        /// Is this member null or the default value for the pseudo enum ?
        /// </summary>
        public static bool NullOrDefault(PseudoEnum member)
        {
            return member == null || member.Id == 0;
        }
    }

    /// <summary>
    /// Usage is --> public class XXX : GenPseudoEnum&lt;XXX&gt; (XXX being your pseudo enum type ofc)
    /// </summary>
    /// <typeparam name="T">Value to represent in the pseudo enum</typeparam>
    public abstract class GenPseudoEnum<T> : PseudoEnum where T : PseudoEnum
    {
        /// <summary>
        /// New gen pseudo enum, uses current type as type registration
        /// </summary>
        public GenPseudoEnum() : base() { }

        /// <summary>
        /// New gen pseudo enum, uses given type as type registration
        /// </summary>
        public GenPseudoEnum(Type type) : base(type) { }

        /// <summary>
        /// Get a pseudo enum value from its id
        /// </summary>
        public static T Get(byte id)
        {
            return (T) Get(typeof(T), id);
        }
    }
}
