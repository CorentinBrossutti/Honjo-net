using System;
using System.Collections.Generic;
using Honjo.Framework.Network.Packets;

namespace Honjo.Framework.Network.Processing
{
    /// <summary>
    /// A delegate to call when a given client receives a given packet
    /// </summary>
    public delegate void PProcess(Client receiver, Packet packet);
    /// <summary>
    /// Handles processing of received packets at a global scale (all clients), sorted by id and specifier
    /// </summary>
    public static class ReceptionProc
    {
        private static readonly Dictionary<byte, PProcess> __ID_PROCESSERS = new Dictionary<byte, PProcess>();
        private static readonly Dictionary<byte, Dictionary<byte, PProcess>> __SPEC_PROCESSERS = new Dictionary<byte, Dictionary<byte, PProcess>>();
        private static readonly object __ID_PROCESSERS_LOCK = new object(), __SPEC_PROCESSERS_LOCK = new object();

        /// <summary>
        /// An event (delegate) to call for ALL packets
        /// </summary>
        public static PProcess ALL_PACKETS_RECEPTION;

        private static void __CheckDelegateExist(byte id)
        {
            lock(__ID_PROCESSERS_LOCK)
            {
                if (!__ID_PROCESSERS.ContainsKey(id))
                    __ID_PROCESSERS.Add(id, __DefaultPacketProcessing_id);
            }
        }

        private static void __CheckDelegateExist(byte id, byte specifier)
        {
            lock(__SPEC_PROCESSERS_LOCK)
            {
                if (!__SPEC_PROCESSERS.ContainsKey(id))
                {
                    var sdpa = new Dictionary<byte, PProcess>
                {
                    { specifier, __DefaultPacketProcessing_spec }
                };
                    __SPEC_PROCESSERS.Add(id, sdpa);
                }
                else
                {
                    if (!__SPEC_PROCESSERS[id].ContainsKey(specifier))
                        __SPEC_PROCESSERS[id].Add(specifier, __DefaultPacketProcessing_spec);
                }
            }
        }

        /// <summary>
        /// Used solely to call it. Use <see cref="Put(byte, byte, PProcess)"/> to add new ones.
        /// </summary>
        /// <param name="id">Identifier byte</param>
        /// <returns>The delegate attached to the given id</returns>
        public static PProcess IdProcesser(byte id)
        {
            __CheckDelegateExist(id);

            return __ID_PROCESSERS[id];
        }

        /// <summary>
        /// Used solely to call it. Use <see cref="Put(byte, byte, PProcess)"/> to add new ones.
        /// </summary>
        /// <param name="id">Identifier byte</param>
        /// <param name="specifier">Specifier byte</param>
        /// <returns>The delegate attached to the given id and specifier</returns>
        public static PProcess SpecProcesser(byte id, byte specifier)
        {
            __CheckDelegateExist(id, specifier);

            return __SPEC_PROCESSERS[id][specifier];
        }

        /// <summary>
        /// Adds a new method to run for all packets of the given identifier
        /// </summary>
        /// <param name="id">Identifier byte</param>
        /// <param name="method">Method to add to the list of callables</param>
        public static void Put(byte id, PProcess method)
        {
            __CheckDelegateExist(id);

            __ID_PROCESSERS[id] += method;
        }

        /// <summary>
        /// Adds a new method to run for all packets of the given identifier AND specifier
        /// </summary>
        /// <param name="id">Identifier byte</param>
        /// <param name="specifier">Specifier byte</param>
        /// <param name="method">Method to add to the list of callables</param>
        public static void Put(byte id, byte specifier, PProcess method)
        {
            __CheckDelegateExist(id, specifier);

            __SPEC_PROCESSERS[id][specifier] += method;
        }

        /// <summary>
        /// Default placeholder method for packet reception. Called for every packet, as long as client reception has not been overriden
        /// </summary>
        /// <param name="receiver">Client receiver</param>
        /// <param name="packet">Packet received</param>
        private static void __DefaultPacketProcessing_id(Client receiver, Packet packet)
        {
            if (ALL_PACKETS_RECEPTION != default(PProcess))
                ALL_PACKETS_RECEPTION(receiver, packet);
        }

        /// <summary>
        /// Cf above. Differentiation between the two reception to avoid double packet processing.
        /// </summary>
        private static void __DefaultPacketProcessing_spec(Client receiver, Packet packet) { }

        /// <summary>
        /// Checks the types of packet contents. Also checks for length. For example:
        /// types contains three values and packet has four arguments (contents).
        /// The first is string, second is int, third is float
        /// It will return true if the first packet argument is string, the second is int, and the third is float
        /// </summary>
        /// <param name="packet">The packet whose contents are to check</param>
        /// <param name="types">Types to check</param>
        /// <returns>True if packet arguments match types</returns>
        public static bool OfType(this Packet packet, params Type[] types)
        {
            return OfType(packet, false, types);
        }

        /// <summary>
        /// Checks the types of packet contents. Also checks for length. For example:
        /// types contains three values and packet has four arguments (contents).
        /// The first is string, second is int, third is float
        /// It will return true if the first packet argument is string, the second is int, and the third is float
        /// </summary>
        /// <param name="packet">The packet whose contents are to check</param>
        /// <param name="skipNull">Whether to skip (as if valid) null values</param>
        /// <param name="types">Types to check</param>
        /// <returns>True if packet arguments match types</returns>
        public static bool OfType(this Packet packet, bool skipNull, params Type[] types)
        {
            if (packet.ContentLength < types.Length)
                return false;

            //use LINQ for better readability ?
            for (int i = 0; i < types.Length; i++)
            {
                object o = packet.Get(i);
                if (o == null)
                {
                    if (!skipNull)
                        return false;
                    continue;
                }
                if (packet.Get(i).GetType() != types[i])
                    return false;
            }

            return true;
        }

        #region Of type generics region
        //Could I make it more generic somehow since every of type calls its predecessor ?
        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T>(this Packet packet, out T out1, bool skipNull = false)
        {
            if (packet.ContentLength >= 1 && packet.Get(0) is T tout)
            {
                out1 = tout;
                return true;
            }

            out1 = default(T);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2>(this Packet packet, out T1 out1, out T2 out2, bool skipNull = false)
        {
            if (OfType(packet, out out1, skipNull) && packet.ContentLength >= 2 && packet.Get(1) is T2 t2out)
            {
                out2 = t2out;
                return true;
            }

            out2 = default(T2);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, skipNull) && packet.ContentLength >= 3 && packet.Get(2) is T3 t3out)
            {
                out3 = t3out;
                return true;
            }

            out3 = default(T3);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, skipNull) && packet.ContentLength >= 4 && packet.Get(3) is T4 t4out)
            {
                out4 = t4out;
                return true;
            }

            out4 = default(T4);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, skipNull) && packet.ContentLength >= 5 && packet.Get(4) is T5 t5out)
            {
                out5 = t5out;
                return true;
            }

            out5 = default(T5);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5, T6>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, out T6 out6, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, out out5, skipNull) && packet.ContentLength >= 6 && packet.Get(5) is T6 t6out)
            {
                out6 = t6out;
                return true;
            }

            out6 = default(T6);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5, T6, T7>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, out T6 out6, out T7 out7, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, out out5, out out6, skipNull) && packet.ContentLength >= 7 && packet.Get(6) is T7 t7out)
            {
                out7 = t7out;
                return true;
            }

            out7 = default(T7);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5, T6, T7, T8>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, out T6 out6, out T7 out7, 
            out T8 out8, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, out out5, out out6, out out7, skipNull) && packet.ContentLength >= 8 && packet.Get(7) is T8 t8out)
            {
                out8 = t8out;
                return true;
            }

            out8 = default(T8);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, out T6 out6, out T7 out7,
            out T8 out8, out T9 out9, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, out out5, out out6, out out7, out out8, skipNull) && packet.ContentLength >= 9 && packet.Get(8) is T9 t9out)
            {
                out9 = t9out;
                return true;
            }

            out9 = default(T9);
            return false;
        }

        /// <summary>
        /// Generic version with out argument
        /// </summary>
        /// <seealso cref="OfType(Packet, bool, Type[])"/>
        public static bool OfType<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Packet packet, out T1 out1, out T2 out2, out T3 out3, out T4 out4, out T5 out5, out T6 out6, out T7 out7,
            out T8 out8, out T9 out9, out T10 out10, bool skipNull = false)
        {
            if (OfType(packet, out out1, out out2, out out3, out out4, out out5, out out6, out out7, out out8, out out9, skipNull) && packet.ContentLength >= 10 && packet.Get(9) is T10 t10out)
            {
                out10 = t10out;
                return true;
            }

            out10 = default(T10);
            return false;
        }
        //do it by hand now...
        #endregion
    }
}
