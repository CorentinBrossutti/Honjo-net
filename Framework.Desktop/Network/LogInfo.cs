using Honjo.Framework.Network.Packets;
using MessagePack;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Wrapper class when a client leaves and is logged
    /// Contains various informations
    /// </summary>
    [Serializable, ProtoContract(SkipConstructor = true), MessagePackObject]
    public sealed class ClientLogInfo
    {
        /// <summary>
        /// Whether the disconnection preceeding the disconnection was expected
        /// </summary>
        [ProtoMember(1), Key(0)]
        public bool ExpectedDisconnection { get; private set; }
        /// <summary>
        /// All the properties this client had prior to disconnection
        /// </summary>
        [ProtoMember(2), Key(1)]
        public Dictionary<string, Property> Properties { get; private set; }
        /// <summary>
        /// The number of properties which couldn't be logged (non serializable) and are instead using a string representation
        /// </summary>
        [ProtoMember(3), Key(2)]
        public int NonSerializedProperties { get; private set; }
        /// <summary>
        /// The packets this client exchanged
        /// </summary>
        [ProtoMember(4), Key(3)]
        public List<PacketLogInfo> PacketsLog { get; private set; }

        /// <summary>
        /// Constructs new log informations about a client
        /// </summary>
        /// <param name="client">Client to log</param>
        /// <param name="expected">Whether the disconnection (source of the log) was expected</param>
        public ClientLogInfo(Client client, bool expected)
        {
            NonSerializedProperties = 0;
            Properties = new Dictionary<string, Property>();

            ExpectedDisconnection = expected;
            PacketsLog = client.ExchangedPackets;

            foreach (var prop in client.Properties.Handle)
            {
                if (prop.Value.Value.GetType().IsSerializable)
                    Properties.Add(prop.Key, prop.Value);
                else
                {
                    Properties.Add(prop.Key, new Property(prop.Value.Value.ToString(), prop.Value.OnConflict));
                    NonSerializedProperties++;
                }
            }
        }

        /// <summary>
        /// MessagePack-C# serialization constructor
        /// </summary>
        [SerializationConstructor]
        public ClientLogInfo(bool key0, Dictionary<string, Property> key1, int key3, List<PacketLogInfo> key4)
        {
            ExpectedDisconnection = key0;
            Properties = key1;
            NonSerializedProperties = key3;
            PacketsLog = key4;
        }
    }

    /// <summary>
    /// Wrapper class to log informations about a packet exchanged.
    /// Values may differ from side-to-side (server/client)
    /// </summary>
    [Serializable, ProtoContract(SkipConstructor = true), MessagePackObject]
    public sealed class PacketLogInfo
    {
        /// <summary>
        /// The packet
        /// </summary>
        [ProtoMember(1), Key(0)]
        public Packet Packet { get; private set; }
        /// <summary>
        /// Whether this packet was received on this side
        /// </summary>
        [ProtoMember(2), Key(1)]
        public bool Received { get; private set; }

        /// <summary>
        /// Constructs a new PacketLogInfo
        /// </summary>
        [SerializationConstructor]
        public PacketLogInfo(Packet packet, bool received)
        {
            Packet = packet;
            Received = received;
        }
    }
}
