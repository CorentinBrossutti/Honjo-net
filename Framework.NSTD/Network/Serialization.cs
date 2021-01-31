using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Honjo.Framework.Network.Packets;
using Honjo.Framework.Util;
using MessagePack;
using MessagePack.Formatters;
using ProtoBuf;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Interface-class for serialization helpers and pseudo enums
    /// </summary>
    public abstract class SerializationMethod
    {
        /// <summary>
        /// Setups a packet before serialization (mainly for surrogates and such)
        /// </summary>
        /// <param name="contents">Contents array of the packet</param>
        /// <returns>Whether it was successful (true) or the fallback setup was used (false). If false you probably want to use Serialization.FALLBACK</returns>
        public virtual bool SetupPacket_PreSerialization(ref object[] contents) => true;

        /// <summary>
        /// Setups a packet after serialization (unpack surrogates, etc.)
        /// </summary>
        /// <param name="contents">Contents array of the packet</param>
        public virtual void SetupPacket_PostDeserialization(ref object[] contents) { }

        /// <summary>
        /// Serializes a packet. Returns a stream from which to read the serialized data
        /// </summary>
        public abstract MemoryStream Serialize(Packet packet);

        /// <summary>
        /// Deserializes a packet from a given stream
        /// </summary>
        public abstract Packet Deserialize(Stream from);
    }

    /// <summary>
    /// Enum type for serialization. Must be mirrored on both sides
    /// </summary>
    public sealed class Serialization : GenPseudoEnum<Serialization>
    {
        /// <summary>
        /// Resolvers for custom packet types
        /// Can contain any string as name, as long as the serialization recognizes it (namespace name for MsgPack, AQN for protobuf-net...)
        /// </summary>
        public static Dictionary<string, Type> RESOLVERS = new Dictionary<string, Type>()
        {
            {"Honjo.Framework.Network.Packets.Packet", typeof(Packet)},
            {"Honjo.Framework.Network.Packets.AuthPacket", typeof(AuthPacket)},
            {"Honjo.Framework.Network.Packets.PgpPublicKeyPacket", typeof(PgpPublicKeyPacket)},
            {"Honjo.Framework.Network.Packets.SymKeyPacket", typeof(SymKeyPacket)},
            {"Honjo.Framework.Network.Packets.TPacket", typeof(TPacket<>)},
            {"Honjo.Framework.Network.Packets.BytePacket", typeof(BytePacket)},
            {"Honjo.Framework.Network.Packets.DoublePacket", typeof(DoublePacket)},
            {"Honjo.Framework.Network.Packets.IntPacket", typeof(IntPacket)},
            {"Honjo.Framework.Network.Packets.StringPacket", typeof(StringPacket)},
            {"Honjo.Fraemwork.Network.Packets.UtilPacket", typeof(UtilPacket)},
            {"Honjo.Framework.Network.Packets.FilePacket", typeof(FilePacket)},
            {"Honjo.Framework.Network.Packets.ImagePacket", typeof(ImagePacket)}
        };
        /// <summary>
        /// Default framework-supported serializations
        /// FALLBACK is the serialization to use in case of failure (native serialization right now)
        /// </summary>
        public static readonly Serialization
            UNKNOWN = Register(UnknownSerialization.INSTANCE),
            NATIVE = Register(NativeSerialization.INSTANCE),
            PROTOBUF = Register(ProtobufSerialization.INSTANCE),
            MSGPACK_CSHARP = Register(MsgPackCsharpSerialization.INSTANCE),
            MSGPACK_CSHARP_LZ4 = Register(MsgPackCsharpLz4Serialization.INSTANCE),
            FALLBACK = NATIVE;

        /// <summary>
        /// Serialization method (helper)
        /// </summary>
        public SerializationMethod Method { get; private set; }

        private Serialization() { }

        /// <summary>
        /// Setups a packet before serialization (mainly for surrogates and such)
        /// </summary>
        /// <param name="contents">Contents array of the packet</param>
        /// <returns>Whether it was successful (true) or the fallback setup was used (false). If false you probably want to use Serialization.FALLBACK</returns>
        public bool SetupPacket_PreSerialization(ref object[] contents) => Method.SetupPacket_PreSerialization(ref contents);

        /// <summary>
        /// Setups a packet after serialization (unpack surrogates, etc.)
        /// </summary>
        /// <param name="contents">Contents array of the packet</param>
        public void SetupPacket_PostDeserialization(ref object[] contents) => Method.SetupPacket_PostDeserialization(ref contents);

        /// <summary>
        /// Serializes a packet. Returns a stream from which to read the serialized data
        /// Remember to setup the packet before doing it
        /// </summary>
        public MemoryStream Serialize(Packet packet) => Method.Serialize(packet);

        /// <summary>
        /// Setups a packet and serializes it
        /// </summary>
        public MemoryStream SSerialize(Packet packet)
        {
            packet._PreSetup(this);
            return Serialize(packet);
        }

        /// <summary>
        /// Deserializes a packet from a given stream
        /// Remember to unpack the packet after doing it
        /// </summary>
        public Packet Deserialize(Stream from) => Method.Deserialize(from);

        /// <summary>
        /// Deserializes a packet and unpacks it
        /// </summary>
        public Packet UDeserialize(Stream from)
        {
            Packet p = Deserialize(from);
            if (p == null)
                return null;
            p._PostSetup(this);
            return p;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>String rep of this serialization</returns>
        public override string ToString()
        {
            if (this == UNKNOWN)
                return "Unknown serialization method (ERROR ?)";
            if (this == NATIVE)
                return "Native BCL serialization method";
            if (this == PROTOBUF)
                return "Protobuf-net serialization method";
            if (this == MSGPACK_CSHARP)
                return "MessagePack-C# serialization method";
            if (this == MSGPACK_CSHARP_LZ4)
                return "MessagePack-C# with LZ4 stream-compress serialization method";

            return "Custom non-string-referenced serialization method";
        }

        /// <summary>
        /// Register a new serialization method
        /// </summary>
        public static Serialization Register(SerializationMethod method)
        {
            return new Serialization()
            {
                Method = method
            };
        }
    }

    internal class UnknownSerialization : SerializationMethod
    {
        public static readonly UnknownSerialization INSTANCE = new UnknownSerialization();

        private UnknownSerialization() { }

        public override bool SetupPacket_PreSerialization(ref object[] contents) => throw new InvalidOperationException();

        public override void SetupPacket_PostDeserialization(ref object[] contents) => throw new InvalidOperationException();

        public override Packet Deserialize(Stream from) => throw new InvalidOperationException();

        public override MemoryStream Serialize(Packet packet) => throw new InvalidOperationException();
    }

    /// <summary>
    /// Protobuf-net serialization
    /// </summary>
    public class ProtobufSerialization : SerializationMethod
    {
        /// <summary>
        /// Singleton class handler
        /// </summary>
        public static readonly ProtobufSerialization INSTANCE = new ProtobufSerialization();

        private ProtobufSerialization() => Serializer.PrepareSerializer<Packet>();

        /// <summary>
        /// Setup protobuf-net content wrappers and surrogates before serialization
        /// </summary>
        public override bool SetupPacket_PreSerialization(ref object[] contents)
        {
            object[] temp = new object[contents.Length];
            for (int i = 0; i < temp.Length; i++)
            {
                //no risk taken here, in case of inner packet
                if (contents[i] is Packet)
                {
                    Serialization.FALLBACK.SetupPacket_PreSerialization(ref contents);
                    return false;
                }

                ContentWrapperBase wrapper = ContentWrapperBase.Wrap(contents[i], out bool b);
                if (wrapper == null && !b)
                    temp[i] = contents[i];
                else if (wrapper == null)
                {
                    Serialization.FALLBACK.SetupPacket_PreSerialization(ref contents);
                    return false;
                }
                else
                    temp[i] = wrapper;
            }
            contents = temp;
            return true;
        }

        /// <summary>
        /// Unpacks protobuf-net serialized packet contents
        /// </summary>
        public override void SetupPacket_PostDeserialization(ref object[] contents)
        {
            for (int i = 0; i < contents.Length; i++)
            {
                object o = contents[i];
                if (o is ContentWrapperBase wrapper)
                    contents[i] = wrapper.Content;
                else
                    contents[i] = o;
            }
        }

        /// <summary>
        /// Serialize a packet
        /// Remember to setup the packet before doing it
        /// </summary>
        public override MemoryStream Serialize(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            try
            {
                Serializer.Serialize(ms, p);
            }
            catch (InvalidOperationException)
            {
                if (Serialization.FALLBACK == Serialization.UNKNOWN)
                    throw;
                else
                {
                    p._PostSetup(Serialization.PROTOBUF);
                    p._PreSetup(Serialization.FALLBACK);
                    return Serialization.FALLBACK.Serialize(p);
                }
            }
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet
        /// Remember to unpack the packet after that
        /// </summary>
        public override Packet Deserialize(Stream from) => Serializer.Deserialize<Packet>(from);
    }

    /// <summary>
    /// Native serialization
    /// </summary>
    public class NativeSerialization : SerializationMethod
    {
        /// <summary>
        /// Singleton class handler
        /// </summary>
        public static readonly NativeSerialization INSTANCE = new NativeSerialization();
        private static readonly BinaryFormatter formatter = new BinaryFormatter();

        private NativeSerialization() { }

        /// <summary>
        /// Serializes a packet using BCL native
        /// </summary>
        public override MemoryStream Serialize(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, p);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet serialized using BCL native
        /// </summary>
        public override Packet Deserialize(Stream from) => formatter.Deserialize(from) as Packet;
    }

    /// <summary>
    /// MessagePack-C# serialization
    /// </summary>
    public class MsgPackCsharpSerialization : SerializationMethod
    {
        /// <summary>
        /// Singleton class handler
        /// </summary>
        public static readonly MsgPackCsharpSerialization INSTANCE = new MsgPackCsharpSerialization();

        private MsgPackCsharpSerialization()
        {
            TypelessFormatter.BindToType = typeName =>
            {
                if (Serialization.RESOLVERS.ContainsKey(typeName))
                    return Serialization.RESOLVERS[typeName];
                return Type.GetType(typeName);
            };
        }

        /// <summary>
        /// Serializes a packet
        /// </summary>
        public override MemoryStream Serialize(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            try
            {
                MessagePackSerializer.Typeless.Serialize(ms, p);
            }
            //circular references might happen too
            catch (Exception e) when (e is InvalidOperationException || e is StackOverflowException)
            {
                if (Serialization.FALLBACK == Serialization.UNKNOWN)
                    throw;
                else
                {
                    p._PostSetup(Serialization.MSGPACK_CSHARP);
                    p._PreSetup(Serialization.FALLBACK);
                    return Serialization.FALLBACK.Serialize(p);
                }
            }
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet
        /// </summary>
        public override Packet Deserialize(Stream from) => MessagePackSerializer.Typeless.Deserialize(from) as Packet;
    }

    /// <summary>
    /// MessagePack-C# LZ4 serialization
    /// </summary>
    public class MsgPackCsharpLz4Serialization : SerializationMethod
    {
        /// <summary>
        /// Singleton class handler
        /// </summary>
        public static readonly MsgPackCsharpLz4Serialization INSTANCE = new MsgPackCsharpLz4Serialization();

        private MsgPackCsharpLz4Serialization() { }

        /// <summary>
        /// Serializes a packet
        /// </summary>
        public override MemoryStream Serialize(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            try
            {
                LZ4MessagePackSerializer.Typeless.Serialize(ms, p);
            }
            //circular references might happen too
            catch (Exception e) when (e is InvalidOperationException || e is StackOverflowException)
            {
                if (Serialization.FALLBACK == Serialization.UNKNOWN)
                    throw;
                else
                {
                    p._PostSetup(Serialization.MSGPACK_CSHARP_LZ4);
                    p._PreSetup(Serialization.FALLBACK);
                    return Serialization.FALLBACK.Serialize(p);
                }
            }
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet
        /// </summary>
        public override Packet Deserialize(Stream from) => LZ4MessagePackSerializer.Typeless.Deserialize(from) as Packet;
    }
}