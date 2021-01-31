using Honjo.Framework.Network.Packets;
using ProtoBuf;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Serialization enum
    /// </summary>
    public enum Serialization
    {
        /// <summary>
        /// Unknown serialization methods (error parsing ?)
        /// </summary>
        UNKNOWN,
        /// <summary>
        /// Native BCL binary serialization
        /// </summary>
        NATIVE,
        /// <summary>
        /// Protobuf-net serialization
        /// </summary>
        PROTOBUF
    }

    /// <summary>
    /// Extension methods for serialization
    /// </summary>
    public static class SerializationExtensions
    {
        /// <summary>
        /// Sets a packet up before serialization. Mainly for serialization methods light on type metadata (protobuf-net...)
        /// Designed to be used solely by the packet class
        /// </summary>
        internal static bool SetupPacket_PreSerialization(this Serialization serialization, ref object[] contents)
        {
            if (contents == null)
                return true;

            switch (serialization)
            {
                case Serialization.NATIVE:
                    return true;
                case Serialization.PROTOBUF:
                    return ProtobufSerialization.SetupPacket_PreSerialization(ref contents);
            }
            throw new ArgumentException("Unrecognized serialization method: " + serialization);
        }

        /// <summary>
        /// Sets a packet up after serialization. Mainly for serialization methods light on type metadata (protobuf-net...)
        /// Designed to be used solely by the packet class
        /// </summary>
        /// <returns></returns>
        internal static void SetupPacket_PostDeserialization(this Serialization serialization, ref object[] contents)
        {
            if (contents == null)
                return;

            switch (serialization)
            {
                case Serialization.NATIVE:
                    return;
                case Serialization.PROTOBUF:
                    ProtobufSerialization.SetupPacket_PostDeserialization(ref contents);
                    return;
            }
            throw new ArgumentException("Unrecognized serialization method: " + serialization);
        }

        /// <summary>
        /// Serializes a packet and returns a memory stream containing streamed bytes (position is start of serialization)
        /// </summary>
        public static MemoryStream SerializePacket(this Serialization serialization, Packet p)
        {
            switch (serialization)
            {
                case Serialization.NATIVE:
                    return NativeSerialization.SerializePacket(p);
                case Serialization.PROTOBUF:
                    return ProtobufSerialization.SerializePacket(p);
            }
            throw new ArgumentException("Unrecognized serialization method: " + serialization);
        }

        /// <summary>
        /// Deserializes a packet from a stream
        /// </summary>
        public static Packet DeserializePacket(this Serialization serialization, Stream from)
        {
            switch (serialization)
            {
                case Serialization.NATIVE:
                    return NativeSerialization.DeserializePacket(from);
                case Serialization.PROTOBUF:
                    return ProtobufSerialization.DeserializePacket(from);
            }
            throw new ArgumentException("Unrecognized serialization method: " + serialization);
        }
    }

    /// <summary>
    /// Protobuf-net serialization
    /// </summary>
    public static class ProtobufSerialization
    {
        static ProtobufSerialization() => Serializer.PrepareSerializer<Packet>();

        /// <summary>
        /// Setup protobuf-net content wrappers and surrogates before serialization
        /// </summary>
        public static bool SetupPacket_PreSerialization(ref object[] contents)
        {
            object[] temp = new object[contents.Length];
            for (int i = 0; i < temp.Length; i++)
            {
                //no risk taken here, in case of inner packet
                if (contents[i] is Packet)
                {
                    Serialization.NATIVE.SetupPacket_PreSerialization(ref contents);
                    return false;
                }

                ContentWrapperBase wrapper = ContentWrapperBase.Wrap(contents[i], out bool b);
                if (wrapper == null && !b)
                    temp[i] = contents[i];
                else if (wrapper == null)
                {
                    Serialization.NATIVE.SetupPacket_PreSerialization(ref contents);
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
        public static void SetupPacket_PostDeserialization(ref object[] contents)
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
        public static MemoryStream SerializePacket(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            try
            {
                Serializer.Serialize(ms, p);
            }
            catch (InvalidOperationException)
            {
                p._PostSetup(Serialization.PROTOBUF);
                p._PreSetup(Serialization.NATIVE);
                return Serialization.NATIVE.SerializePacket(p);
            }
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet
        /// Remember to unpack the packet after that
        /// </summary>
        public static Packet DeserializePacket(Stream from) => Serializer.Deserialize<Packet>(from);
    }

    /// <summary>
    /// Native serialization
    /// </summary>
    public class NativeSerialization
    {
        private static BinaryFormatter formatter = new BinaryFormatter();
        /// <summary>
        /// Serializes a packet using BCL native
        /// </summary>
        public static MemoryStream SerializePacket(Packet p)
        {
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, p);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Deserializes a packet serialized using BCL native
        /// </summary>
        public static Packet DeserializePacket(Stream from) => formatter.Deserialize(from) as Packet;
    }
}
