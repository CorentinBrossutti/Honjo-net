using Honjo.Framework.Network.Packets;
using MessagePack;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Helper/manager class for properties linked to clients, also handles synchronization
    /// </summary>
    public class ClientPropertyManager : IDisposable
    {
        /// <summary>
        /// Specifier for property sync packets, using Packet.SYNC_ID as ID
        /// </summary>
        public const byte PROP_SYNC_SPEC = 0;
        /// <summary>
        /// The client linked
        /// </summary>
        public Client Client { get; protected set; }
        /// <summary>
        /// The dictionary handle of this manager
        /// </summary>
        public Dictionary<string, Property> Handle { get; protected set; }
        /// <summary>
        /// Called when this manager synchronizes properties
        /// </summary>
        public event Action<ClientPropertyManager> OnSynchronization;
        /// <summary>
        /// Brackets indexer usage
        /// </summary>
        /// <seealso cref="Get(string, bool)"/>
        public virtual Property this[string s, bool except = true]
        {
            get
            {
                return Get(s, except);
            }
            set
            {
                Set(s, value);
            }
        }
        /// <summary>
        /// Whether to automatically synchronize data when a set operation is done
        /// </summary>
        public virtual bool AutoSynchronization { get; set; }

        /// <summary>
        /// Build a property manager for a client
        /// </summary>
        /// <param name="client">Client</param>
        public ClientPropertyManager(Client client)
        {
            Client = client;
            Handle = new Dictionary<string, Property>();

            client.OnDataReception += _SyncRequest;
            OnSynchronization = new Action<ClientPropertyManager>(__DefaultOnSync);
            AutoSynchronization = false;
        }

        private void __DefaultOnSync(ClientPropertyManager manager)
        {
        }

        /// <summary>
        /// Check existence of a property
        /// </summary>
        /// <param name="property">Property to check</param>
        /// <returns>True if the given property is present</returns>
        public virtual bool Has(string property)
        {
            return Handle.ContainsKey(property);
        }

        /// <summary>
        /// Check the existence and type-concordance of a property.
        /// </summary>
        /// <param name="property">Property to check</param>
        /// <param name="type">Type to check</param>
        /// <returns>True if the client has the given property and it is of the given type ; false otherwise</returns>
        public virtual bool HasOfType(string property, Type type)
        {
            return Has(property) && Get(property).Value.GetType() == type;
        }

        /// <summary>
        /// Retrieve the value of a property
        /// </summary>
        /// <param name="property">Property</param>
        /// <param name="except">Whether to throw an exception if the property cannot be found. Default : true</param>
        /// <returns>The value of the given property</returns>
        public virtual Property Get(string property, bool except = true)
        {
            if(!Has(property))
            {
                if (except)
                    throw new ArgumentException("Property " + property + " does not exist. Has it been sync-ed yet ?");
                else
                    return default(Property);
            }
            return Handle[property];
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        /// <param name="property">Property</param>
        /// <param name="value">Value, must be serializable for synchronization</param>
        /// <param name="overwrite">Whether to overwrite the value of the property if already present. Default : true</param>
        public virtual void Set(string property, Property value, bool overwrite = true)
        {
            if(Has(property))
            {
                if (overwrite)
                    Handle[property] = value;
            }
            else
                Handle.Add(property, value);

            if (AutoSynchronization)
                Synchronize();
        }

        /// <summary>
        /// Synchronize properties with the server, and sets conflicts accordingly
        /// </summary>
        public virtual void Synchronize()
        {
            List<object> temp = new List<object>();
            foreach (var entry in Handle)
            {
                if (entry.Value.OnConflict == PropertyConflictResolution.NO_SYNC)
                    continue;

                temp.Add(entry.Key);
                temp.Add(entry.Value);
            }
            Client.Send(new Packet(Packet.OK_HEADER, Packet.SYNC_ID, PROP_SYNC_SPEC, temp.ToArray())
            {
                Designation = "Beginning of property synchronization"
            });
        }

        /// <summary>
        /// A synchronization has been requested
        /// </summary>
        protected virtual void _SyncRequest(Client client, Packet packet)
        {
            if (packet.Id != Packet.SYNC_ID || packet.Specifier != PROP_SYNC_SPEC)
                return;

            if(packet.ContentLength % 2 != 0)
            {
                client.LogNotNull(Logging.LogLevel.ERROR, "PRPSYNC packet received with odd length");
                client.Send(new Packet(Packet.PACKET_MALFORMED_HEADER, packet.Id, packet.Specifier)
                {
                    Designation = "PRPSYNC packet malformed (odd property length)"
                });
                return;
            }

            if (packet.Header == Packet.OK_HEADER)
            {
                var response = new List<object>();
                var toSet = new Dictionary<string, Property>();
                var exclude = new List<string>();

                for (int i = 0; i < packet.ContentLength; i += 2)
                {
                    string temp = packet.Get(i) as string;
                    Property prop = packet.Get(i + 1) as Property;
                    if (String.IsNullOrEmpty(temp) || prop == null)
                    {
                        client.LogNotNull(Logging.LogLevel.ERROR, "Malformed PRPSYNC packet received");
                        client.Send(new Packet(Packet.PACKET_MALFORMED_HEADER, packet.Id, packet.Specifier)
                        {
                            Designation = "PRPSYNC packet malformed. Key null or not a string, or property null or of wrong type"
                        });
                        return;
                    }

                    if (Has(temp))
                    {
                        PropertyConflictResolution pcr = Get(temp).OnConflict;
                        if ((pcr == PropertyConflictResolution.CLIENT_OVERWRITE && client.ServerSide) ||
                            (pcr == PropertyConflictResolution.SERVER_OVERWRITE && !client.ServerSide))
                            toSet.Add(temp, prop);
                        else if (pcr != PropertyConflictResolution.NO_SYNC && pcr != PropertyConflictResolution.NO_SYNC_ON_CONFLICT)
                        {
                            response.Add(temp);
                            response.Add(Get(temp));
                        }
                        else
                            exclude.Add(temp);
                    }
                    else if (prop.OnConflict != PropertyConflictResolution.NO_SYNC)
                        toSet.Add(temp, prop);
                }

                foreach (var ts in toSet)
                    Set(ts.Key, ts.Value, true);

                foreach (var entry in Handle)
                {
                    if (response.Contains(entry.Key) || toSet.ContainsKey(entry.Key) || exclude.Contains(entry.Key))
                        continue;

                    response.Add(entry.Key);
                    response.Add(entry.Value);
                }
                client.Send(new Packet(Packet.CONFIRMATION_AWAIT_HEADER, packet.Id, packet.Specifier, response.ToArray())
                {
                    Designation = "PRPSYNC conflict resolution"
                });
            }
            else if (packet.Header == Packet.CONFIRMATION_AWAIT_HEADER)
            {
                var toSet = new Dictionary<string, Property>();
                for (int i = 0; i < packet.ContentLength; i += 2)
                {
                    string temp = packet.Get(i) as string;
                    Property prop = packet.Get(i + 1) as Property;
                    if (String.IsNullOrEmpty(temp) || prop == null)
                    {
                        client.LogNotNull(Logging.LogLevel.ERROR, "Malformed PRPSYNC packet received");
                        client.Send(new Packet(Packet.PACKET_MALFORMED_HEADER, packet.Id, packet.Specifier)
                        {
                            Designation = "PRPSYNC packet malformed. Key null or not a string, or property null or not of type"
                        });
                        return;
                    }

                    //in case it happens...
                    if (!Has(temp) && prop.OnConflict != PropertyConflictResolution.NO_SYNC)
                    {
                        toSet.Add(temp, prop);
                        continue;
                    }

                    PropertyConflictResolution pcr = prop.OnConflict;
                    if ((pcr == PropertyConflictResolution.CLIENT_OVERWRITE && client.ServerSide) ||
                        (pcr == PropertyConflictResolution.SERVER_OVERWRITE && !client.ServerSide))
                        toSet.Add(temp, prop);
                    else
                    {
                        client.LogNotNull(Logging.LogLevel.WARNING, "PRPSYNC conflict couldn't be resolved, inconsistent behaviour. Aborting");
                        client.Send(new Packet(Packet.AMBIGUITY_HEADER, packet.Id, packet.Specifier)
                        {
                            Designation = "Ambiguous and suspicious conflict resolution packet received"
                        });
                        return;
                    }
                }

                foreach (var ts in toSet)
                    Set(ts.Key, ts.Value, true);
                client.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier)
                {
                    Designation = "ACK ; Synchronization done"
                });
                OnSynchronization(this);
            }
            else if (packet.Header == Packet.ACK_HEADER)
                OnSynchronization(this);
        }

        /// <summary>
        /// Destroys this property manager
        /// </summary>
        public void Dispose()
        {
            Handle = null;
            Client = null;
            OnSynchronization = null;
        }
    }

    /// <summary>
    /// A property and the basic informations it carries
    /// </summary>
    [Serializable, ProtoContract(SkipConstructor = true), MessagePackObject]
    public class Property
    {
        /// <summary>
        /// The value (can be any object) of the property
        /// </summary>
        [ProtoMember(1, DynamicType = true), Key(0)]
        public object Value { get; protected set; }
        /// <summary>
        /// The conflict resolution method
        /// </summary>
        [ProtoMember(2), Key(1)]
        public PropertyConflictResolution OnConflict { get; protected set; }

        /// <summary>
        /// Creates a new property
        /// </summary>
        /// <param name="value">The value of the property</param>
        /// <param name="conflictRes">The conflict resolution method. Default : property is NOT synchronized in case of conflict</param>
        [SerializationConstructor]
        public Property(object value, PropertyConflictResolution conflictRes = PropertyConflictResolution.NO_SYNC_ON_CONFLICT)
        {
            Value = value;
            OnConflict = conflictRes;
        }

        /// <summary>
        /// String representation of this property
        /// </summary>
        public override string ToString()
        {
            return "Property {Value: " + Value.ToString() + " (" + Value.GetType().ToString() + "); OnConflict: " + OnConflict.ToString() + "}";
        }
    }

    /// <summary>
    /// What to do on the presence of a conflict when syncrhonizing a property. Do note the conflict res. method on the server wins
    /// </summary>
    [Serializable, ProtoContract]
    public enum PropertyConflictResolution
    {
        /// <summary>
        /// Client wins on conflict
        /// </summary>
        CLIENT_OVERWRITE,
        /// <summary>
        /// Server wins on conflict
        /// </summary>
        SERVER_OVERWRITE,
        /// <summary>
        /// No sync, two ends retain their own value on conflict
        /// </summary>
        NO_SYNC_ON_CONFLICT,
        /// <summary>
        /// No sync at all
        /// </summary>
        NO_SYNC
    }
}
