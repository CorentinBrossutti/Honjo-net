using Honjo.Framework.Crypt;
using Honjo.Framework.Logging;
using MessagePack;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Honjo.Framework.Network.Packets
{
    /// <summary>
    /// A packet. Organizes groups of data properly.
    /// </summary>
    [Serializable, ProtoContract(ImplicitFields = ImplicitFields.None), MessagePackObject]
    [ProtoInclude(1, typeof(TPacket<byte>))]
    [ProtoInclude(2, typeof(TPacket<double>))]
    [ProtoInclude(3, typeof(TPacket<float>))]
    [ProtoInclude(4, typeof(TPacket<int>))]
    [ProtoInclude(5, typeof(TPacket<string>))]
    [ProtoInclude(6, typeof(AuthPacket))]
    [ProtoInclude(7, typeof(UtilPacket))]
    public class Packet
    {
        /// <summary>
        /// Receiving buffer size
        /// </summary>
        public static int RCV_BUFFER_SIZE = 1024;
        /// <summary>
        /// The number of bytes in a pre-packet header. Internal only, no other real use.
        /// </summary>
        public static int SBYTES_HEADER_SIZE = 3;
        /// <summary>
        /// To update along with SBYTES_HEADER_SIZE. Avoids a calculation.
        /// Calculated by doing : sum(for i from SBYTES_HEADER_SIZE to 0) of 255^i
        /// It is then of approx. 16 Mb by default and can be expanded but it increases exponentionally, meaning the next step is directly 4 Gb...
        /// </summary>
        public static long HEADER_MAX_VALUE = 16646655;
        /// <summary>
        /// ID for admin packets
        /// </summary>
        public const byte ADMIN_ID = 0;
        /// <summary>
        /// ID for authentication packets
        /// </summary>
        public const byte AUTH_ID = 1;
        /// <summary>
        /// ID for synchronization packets
        /// </summary>
        public const byte SYNC_ID = 2;
        /// <summary>
        /// ID for system-wise packets
        /// </summary>
        public const byte SYS_ID = byte.MaxValue;
        /// <summary>
        /// First ID value that is not reserved. Do note that the SYS_ID will usually be set to 255
        /// </summary>
        public const byte FIRST_AVAILABLE_ID = 3;
        /// <summary>
        /// Only for interal transliteration mismatch errors
        /// </summary>
        public const byte UNKNOWN_ID_VALUE = 255;
        /// <summary>
        /// Default system ping specifier
        /// </summary>
        public const byte SYS_DEFAULT_PING_SPEC = 0;
        /// <summary>
        /// Default header values (OK, ACK, MALFORMED...), as per convention, headers not signaling an error (OK, ACK, YES, NO...) are positive, and inversely.
        /// Doxygen might not show them all
        /// </summary>
        public const sbyte OK_HEADER = 0, ACK_HEADER = 1, YES_HEADER = 2, NO_HEADER = 3, CONFIRMATION_AWAIT_HEADER = 4, CONFIRMATION_HEADER = 5, FORWARD_HEADER = 6,
            PACKET_MALFORMED_HEADER = -1, AUTH_DENIED_HEADER = -2, NO_REQUEST_MATCH_HEADER = -3, AMBIGUITY_HEADER = -4, UQC_BROKEN_HEADER = -5, ERROR_HEADER = -6, UNKNOWN_HEADER_VALUE = -128;
        /// <summary>
        /// First available header values (negative, positive). Note that UNKNOWN_HEADER is usually set to -128
        /// </summary>
        public const sbyte FIRST_AVAILABLE_NEGATIVE_HEADER = -7, FIRST_AVAILABLE_POSITIVE_HEADER = 7;

        private static Dictionary<string, sbyte> __headers = null;
        private static Dictionary<string, byte> _ids = null;
        /// <summary>
        /// Default asymmetric pgp crypt instance
        /// </summary>
        protected static readonly AbstractPgpCrypt defPgpCrypt = RsaCrypt.INSTANCE;
        /// <summary>
        /// Default symmetric crypt instance
        /// </summary>
        protected static readonly AbstractSymCrypt defSymCrypt = AesCrypt.INSTANCE;
        /// <summary>
        /// Binary formatter to send packets
        /// </summary>
        protected static readonly BinaryFormatter formatter = new BinaryFormatter();
        /// <summary>
        /// Default serialization to use (also for general packets)
        /// </summary>
        public static Serialization DEFAULT_SERIALIZATION = Serialization.MSGPACK_CSHARP;
        /// <summary>
        /// Contents of this packet. Any object is good.
        /// </summary>
        [ProtoMember(8, DynamicType = true), Key(0)]
        public object[] Contents { get; protected set; }
        /// <summary>
        /// Header signed byte (-128 to +127). Handling of errors and ACK packets
        /// </summary>
        //starting at 8 to avoid conflicting with subtypes tags
        [ProtoMember(9), Key(1)]
        public sbyte Header { get; private set; }
        /// <summary>
        /// Identifier byte of the packet (0 to 255)
        /// </summary>
        [ProtoMember(10), Key(2)]
        public byte Id { get; private set; }
        /// <summary>
        /// Specifier byte of the packet (0 to 255)
        /// </summary>
        [ProtoMember(11), Key(3)]
        public byte Specifier { get; private set; }
        /// <summary>
        /// The number of arguments in this packet
        /// </summary>
        [IgnoreMember]
        public int ContentLength => Contents == null ? 0 : Contents.Length;
        /// <summary>
        /// The date this packet was created
        /// </summary>
        [ProtoMember(12), Key(4)]
        public DateTime CreationTime { get; protected set; } = DateTime.Now;
        /// <summary>
        /// Designation of this packet, somewhat optional
        /// </summary>
        [ProtoMember(13), Key(5)]
        public string Designation { get; set; }
        /// <summary>
        /// The time at which the server either sent or received this packet
        /// </summary>
        [ProtoMember(14), Key(6)]
        public DateTime ServerInterception { get; private set; }
        /// <summary>
        /// Square brackets indexer
        /// </summary>
        [IgnoreMember]
        public virtual object this[int i] => Get(i);

        /// <summary>
        /// Constructs a packet. Will use default serialization
        /// </summary>
        /// <param name="header">Header signed byte</param>
        /// <param name="id">Identifier byte</param>
        /// <param name="specifier">Specifier byte</param>
        /// <param name="contents">Contents (arguments of this packet)</param>
        public Packet(sbyte header, byte id, byte specifier, params object[] contents)
        {
            Header = header;
            Id = id;
            Specifier = specifier;

            Contents = contents;
        }

        /// <summary>
        /// Protobuf-net serialization constructor
        /// </summary>
        public Packet() { }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public Packet(object[] key0, sbyte key1, byte key2, byte key3, DateTime key4, string key5, DateTime key6) : this(key1, key2, key3, key0)
        {
            CreationTime = key4;
            Designation = key5;
            ServerInterception = key6;
        }

        /// <summary>
        /// Sets this packet up for a given serialization
        /// </summary>
        /// <param name="serialization">Serialization to use</param>
        /// <returns>True whether the setup was successful. If false, then fallback serialization was used (usually BCL native)</returns>
        public virtual bool _PreSetup(Serialization serialization)
        {
            //TODO optimization
            object[] temp = Contents;
            bool b = serialization.SetupPacket_PreSerialization(ref temp);
            Contents = temp;
            return b;
        }

        /// <summary>
        /// Sets this packet up
        /// ONLY FOR POST DESERIALIZATION
        /// </summary>
        public virtual void _PostSetup(Serialization serialization)
        {
            if(Contents != null && Contents.Length > 0)
            {
                object[] temp = Contents;
                serialization.SetupPacket_PostDeserialization(ref temp);
                Contents = temp;
            }
        }

        /// <summary>
        /// Retrieve the content at the given index
        /// </summary>
        /// <param name="index">Index of the content (argument)</param>
        /// <returns>The content at the given index</returns>
        public virtual object Get(int index) => Contents[index];

        /// <summary>
        /// Retrieve the (true) content at the given index, always by reference.
        /// Modifying it has NO effect once the packet is setup
        /// </summary>
        /// <seealso cref="GetRefInner(int)"/>
        public virtual ref object GetRef(int index) => ref Contents[index];

        /// <summary>
        /// Retrieve the (inner) content at the given index, always by reference.
        /// Modifying it has an effect but be careful of content wrappers
        /// </summary>
        public virtual ref object GetRefInner(int index) => ref Contents[index];

        /// <summary>
        /// Retrieve the content at the given index using generic casting
        /// </summary>
        /// <typeparam name="T">Generic for expected type</typeparam>
        /// <param name="index">Index of the content (argument)</param>
        /// <returns>The content at the given index</returns>
        public virtual T Get<T>(int index) => (T) Get(index);

        /// <summary>
        /// Tries to send this packet
        /// </summary>
        /// <param name="client">Client to send the packet to</param>
        public virtual void TrySendTo(Client client) => client.Send(this);

        /// <summary>
        /// Set the contents after initialization. The array is decomposed
        /// </summary>
        public virtual void SetContentsArray(params object[] array) => Contents = array;

        /// <summary>
        /// Registers this packet as intercepted now
        /// </summary>
        public void RegisterInterception()
        {
            if (ServerInterception == default(DateTime))
                ServerInterception = DateTime.Now;
        }

        /// <summary>
        /// String representation of this packet
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "{Designation : " + (Designation ?? "(...)") + "; Header: " + Header + "; Id: " + Id + 
            "; Specifier: " + Specifier + "; Contents: " + ContentsToString() + "}";

        /// <summary>
        /// Retrieve a string representation of the contents of this packet
        /// </summary>
        public string ContentsToString()
        {
            StringBuilder builder = new StringBuilder("{");
            for (int i = 0; i < ContentLength; i++)
            {
                if (Get(i) == null)
                    continue;

                builder.Append(Get(i).ToString()).Append("; ");
            }

            return builder.Append("}").ToString();
        }

        /// <summary>
        /// For subclasses only. Simple method to delegate explicit object conversion
        /// </summary>
        /// <typeparam name="T">Placeholder generic</typeparam>
        /// <param name="from">Object to convert</param>
        /// <returns>The object as a true object instance</returns>
        protected static object _OCVRT<T>(T from) => from;

        /// <summary>
        /// Transliterates a header value
        /// </summary>
        public static string HeaderTransliteration(sbyte header)
        {
            Headers();
            foreach (var entry in __headers)
            {
                if (entry.Value == header)
                    return entry.Key;
            }
            return "??";
        }

        /// <summary>
        /// Transliterates an id value
        /// </summary>
        public static string IdTransliteration(byte id)
        {
            Ids();
            foreach (var entry in _ids)
            {
                if (entry.Value == id)
                    return entry.Key;
            }
            return "??";
        }

        /// <summary>
        /// Retrieve the value of a default header using its string representation
        /// </summary>
        /// <param name="header">The string representation of the header</param>
        public static sbyte HeaderValue(string header)
        {
            Headers();
            return __headers.ContainsKey(header) ? __headers[header] : UNKNOWN_HEADER_VALUE;
        }

        /// <summary>
        /// Retrieve the value of a default id using its string representation
        /// </summary>
        /// <param name="id">The string representation of the id</param>
        public static byte IdValue(string id)
        {
            Ids();
            return _ids.ContainsKey(id) ? _ids[id] : UNKNOWN_ID_VALUE;
        }

        /// <summary>
        /// Returns a dictionary containing all headers referenced by string key
        /// </summary>
        public static Dictionary<string, sbyte> Headers()
        {
            if(__headers == null)
            {
                __headers = new Dictionary<string, sbyte>();
                foreach (var constant in typeof(Packet).GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    //is it const, does it match the value ?
                    if (!constant.IsLiteral || constant.IsInitOnly || !constant.Name.EndsWith("HEADER") ||
                        constant.FieldType != typeof(sbyte))
                        continue;
                    __headers.Add(constant.Name.Substring(0, constant.Name.LastIndexOf('_')), (sbyte) constant.GetValue(null));
                }
            }
            return __headers;
        }

        /// <summary>
        /// Returns a dictionary containing all ids referenced by string key
        /// </summary>
        public static Dictionary<string, byte> Ids()
        {
            if(_ids == null)
            {
                _ids = new Dictionary<string, byte>();
                foreach (var constant in typeof(Packet).GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    //is it const, does it match the value ?
                    if (!constant.IsLiteral || constant.IsInitOnly || !constant.Name.EndsWith("ID") ||
                        constant.FieldType != typeof(byte))
                        continue;

                    _ids.Add(constant.Name.Substring(0, constant.Name.LastIndexOf('_')), (byte)constant.GetValue(null));
                }
            }
            return _ids;
        }
    }

    /// <summary>
    /// Base type to check non-generic equality
    /// </summary>
    public abstract class ContentWrapperBase
    {
        /// <summary>
        /// Object property-backing field for content
        /// </summary>
        protected object _content;
        /// <summary>
        /// The content wrapped
        /// </summary>
        public object Content
        {
            get => _content;

            set => _content = value;
        }


        /// <summary>
        /// Add delegates here which return a content wrapper for a given type.
        /// Doing it here will allow protobuf serialization for packets which have the given type as a content
        /// </summary>
        private static readonly Dictionary<string, Func<object, ContentWrapperBase>> CUSTOM_WRAPPERS = new Dictionary<string, Func<object, ContentWrapperBase>>();

        /// <summary>
        /// Adds a custom wrapper for a given type.
        /// If it already contains a definition the overwrite bool will define its behaviour. False by default.
        /// </summary>
        public static void AddCustomWrapper(Type type, Func<object, ContentWrapperBase> wrapper, bool overwrite = false)
        {
            if (!CUSTOM_WRAPPERS.ContainsKey(type.AssemblyQualifiedName))
                CUSTOM_WRAPPERS.Add(type.AssemblyQualifiedName, wrapper);
            else if (overwrite)
                CUSTOM_WRAPPERS[type.AssemblyQualifiedName] = wrapper;
        }

        /// <summary>
        /// Returns the given object wrapped as a generic ContentWrapper (used as a ContentWrapperBase)
        /// If the given object is NOT primtive, it returns null
        /// </summary>
        public static ContentWrapperBase Wrap(object obj) => Wrap(obj, out _);

        /// <summary>
        /// Returns the given object wrapped as a generic ContentWrapper (used as a ContentWrapperBase)
        /// If the given object is NOT primtive, it returns null
        /// </summary>
        /// <param name="obj">Object to wrap</param>
        /// <param name="invalidType">Whether the type was invalid (if not and null, then serializable object)</param>
        public static ContentWrapperBase Wrap(object obj, out bool invalidType) => Wrap(obj.GetType(), obj, out invalidType);

        /// <summary>
        /// Returns the given object wrapped as a generic ContentWrapper (used as a ContentWrapperBase)
        /// If the given object is NOT primtive or a list/dictionary, it returns null
        /// </summary>
        /// <param name="type">Object type to wrap</param>
        /// <param name="obj">Object to wrap</param>
        /// <param name="invalidType">Whether the type was invalid (if not and null, then serializable object)</param>
        public static ContentWrapperBase Wrap(Type type, object obj, out bool invalidType)
        {
            if (obj == null || type == null)
            {
                invalidType = true;
                return null;
            }

            invalidType = false;
            //I HATE MYSELF
            //the only way...
            if (type == typeof(byte))
                return new ContentWrapper<byte>((byte)obj);
            else if (type == typeof(byte[]))
                return new ContentWrapper<byte[]>((byte[])obj);
            else if (type == typeof(bool))
                return new ContentWrapper<bool>((bool)obj);
            else if (type == typeof(bool[]))
                return new ContentWrapper<bool[]>((bool[])obj);
            else if (type == typeof(sbyte))
                return new ContentWrapper<sbyte>((sbyte)obj);
            else if (type == typeof(sbyte[]))
                return new ContentWrapper<sbyte[]>((sbyte[])obj);
            else if (type == typeof(char))
                return new ContentWrapper<char>((char)obj);
            else if (type == typeof(char[]))
                return new ContentWrapper<char[]>((char[])obj);
            else if (type == typeof(double))
                return new ContentWrapper<double>((double)obj);
            else if (type == typeof(double[]))
                return new ContentWrapper<double[]>((double[])obj);
            else if (type == typeof(float))
                return new ContentWrapper<float>((float)obj);
            else if (type == typeof(float[]))
                return new ContentWrapper<float[]>((float[])obj);
            else if (type == typeof(int))
                return new ContentWrapper<int>((int)obj);
            else if (type == typeof(int[]))
                return new ContentWrapper<int[]>((int[])obj);
            else if (type == typeof(uint))
                return new ContentWrapper<uint>((uint)obj);
            else if (type == typeof(uint[]))
                return new ContentWrapper<uint[]>((uint[])obj);
            else if (type == typeof(long))
                return new ContentWrapper<long>((long)obj);
            else if (type == typeof(long[]))
                return new ContentWrapper<long[]>((long[])obj);
            else if (type == typeof(ulong))
                return new ContentWrapper<ulong>((ulong)obj);
            else if (type == typeof(ulong[]))
                return new ContentWrapper<ulong[]>((ulong[])obj);
            else if (type == typeof(short))
                return new ContentWrapper<short>((short)obj);
            else if (type == typeof(short[]))
                return new ContentWrapper<short[]>((short[])obj);
            else if (type == typeof(ushort))
                return new ContentWrapper<ushort>((ushort)obj);
            else if (type == typeof(ushort[]))
                return new ContentWrapper<ushort[]>((ushort[])obj);
            else if (type == typeof(DateTime))
                return new ContentWrapper<DateTime>((DateTime)obj);
            else if (type == typeof(DateTime[]))
                return new ContentWrapper<DateTime[]>((DateTime[])obj);
            else if (type == typeof(LogLevel))
                return new ContentWrapper<LogLevel>((LogLevel)obj);
            else if (type == typeof(LogLevel[]))
                return new ContentWrapper<LogLevel[]>((LogLevel[])obj);
            else if(CUSTOM_WRAPPERS.ContainsKey(type.AssemblyQualifiedName))
                return CUSTOM_WRAPPERS[type.AssemblyQualifiedName](obj);
            //unrecognized struct
            else if (obj is ValueType)
                invalidType = true;
            else if (type.IsGenericType)
            {
                //unserializable collection
                if ((obj is IList && type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>))) ||
                    (obj is IDictionary && type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>))))
                    invalidType = true;
            }

            //is a serializable object (not a list, not a dic, not a primitive, not a supported struct)
            //or cannot be serialized
            return null;
        }
    }

    /// <summary>
    /// Wraps a type in a serializable object
    /// </summary>
    /// <typeparam name="T">Type to wrap</typeparam>
    [ProtoContract(SkipConstructor = true)]
    public class ContentWrapper<T> : ContentWrapperBase
    {
        /// <summary>
        /// Property-backing field
        /// </summary>
        protected T _tContent;

        /// <summary>
        /// The content wrapped
        /// </summary>
        [ProtoMember(1, IsRequired = true)]
        public new T Content
        {
            get => _tContent;
            set
            {
                base.Content = value;
                _tContent = value;
            }
        }

        /// <summary>
        /// Constructs a new content wrapper
        /// </summary>
        /// <param name="obj"></param>
        public ContentWrapper(T obj) => Content = obj;
    }
}
