using Honjo.Framework.Crypt;
using Honjo.Framework.Crypt.Hashing;
using Honjo.Framework.Logging;
using Honjo.Framework.Network.Packets;
using Honjo.Framework.Network.Processing;
using Honjo.Framework.Util;
using Honjo.Framework.Util.Concurrent;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Delegate when a given client exchanges data (sends or receives)
    /// </summary>
    /// <param name="client">Client who received the packet</param>
    /// <param name="data">Packet exchanged</param>
    public delegate void PacketExchange(Client client, Packet data);

    /// <summary>
    /// A client. Bear in mind that for client applications (ie not server side), some properties will seem useless or outright desync-ed
    /// </summary>
    public class Client : IDisposable
    {
        /// <summary>
        /// Whether to log all packets by default
        /// </summary>
        public const bool DEFAULT_LOG_PACKETS = true;
        /// <summary>
        /// Whether to allow external errors to stop the server (false by default, can be changed to allow debugging)
        /// </summary>
        public static bool ALLOW_EXTERNAL_EXCEPTIONS = false;
        /// <summary>
        /// Whether to try to send data when client is disconnected to make sure it is not connected
        /// </summary>
        public static bool COMPLETE_CONNECTION_CHECK = false;
        /// <summary>
        /// Whether to warn in the server (debug level) if the server is sending/receiving an already processed packet
        /// </summary>
        public static bool WARN_PACKET_ALREADY_PROCESSED = true;
        /// <summary>
        /// Whether to log the stacktrace in output when an unexpected disconnection happens
        /// </summary>
        public static bool UNEXPECTED_DISCONNECTION_STACKTRACE = false;
        /// <summary>
        /// Thread locker for the clients list
        /// </summary>
        public static readonly object _CLIENTS_LOCK = new object();

        /// <summary>
        /// All clients ids. Has a copy loop and thus more expensive than its collection counterpart
        /// </summary>
        public static int[] Clients_id
        {
            get
            {
                lock (_CLIENTS_LOCK)
                {
                    int[] iout = new int[_CLIENTS.Keys.Count];
                    _CLIENTS.Keys.CopyTo(iout, 0);
                    return iout;
                }
            }
        }

        /// <summary>
        /// All clients ids as a collection of keys
        /// Also, please lock when iterating
        /// </summary>
        public static Dictionary<int, Client>.KeyCollection Clients_id_collection => _CLIENTS.Keys;

        /// <summary>
        /// USE ONLY IF YOU HAVE A REAL UTILITY FOR IT
        /// All clients (objects). Pretty expensive due to the copy loop and the heaviness of objects. Its use is not advised (see collection counterpart)
        /// </summary>
        public static Client[] Clients
        {
            get
            {
                lock (_CLIENTS_LOCK)
                {
                    Client[] cout = new Client[_CLIENTS.Values.Count];
                    _CLIENTS.Values.CopyTo(cout, 0);
                    return cout;
                }
            }
        }

        /// <summary>
        /// All clients (objects). Is simply a collection of values and thus inexpensive.
        /// Please lock when iterating
        /// </summary>
        public static Dictionary<int, Client>.ValueCollection Clients_collection => _CLIENTS.Values;

        /// <summary>
        /// The number of clients connected
        /// </summary>
        public static int ClientsCount => _CLIENTS.Count;

        /// <summary>
        /// Clients by ID. The ID is fixed on client connection
        /// </summary>
        protected static readonly Dictionary<int, Client> _CLIENTS = new Dictionary<int, Client>();

        //local use only. To initialize with unique ids
        private static int __CURID = 0;
        private const byte __AUTH_PKEY_SPEC = 0, __AUTH_SYMKEY_SPEC = 1, __AUTH_EHSHK_SPEC = 2, __AUTH_ADMIN_SIG_SPEC = 3, __AUTH_DISCONNECTION_SPEC = 4, __AUTH_DISPATCH_INFO_SPEC = 5,
            __AUTH_CHANGE_SERIALIZATION_SPEC = 127;
        private static string __ADMIN_HASH_PWD;
        private const Encryption __DEFAULT_ASYM_ENCRYPTION = Encryption.ASYM_RSA2048;
        private const Encryption __DEFAULT_SYM_ENCRYPTION = Encryption.SYM_AES256;

        private AbstractPgpCrypt __pgpCrypt = __DEFAULT_ASYM_ENCRYPTION.GetHelper() as AbstractPgpCrypt;
        private AbstractSymCrypt __symCrypt = __DEFAULT_SYM_ENCRYPTION.GetHelper() as AbstractSymCrypt;
        private bool __pgp_swapping = false;
        //local pgp key pair (public + private)
        private PgpKeyPair __localKeyPair;
        //symmetric key for the transmission
        private KeyParameter __symKey;
        //salt to use for bcrypt
        private string __bcryptSalt;
        private int __packetCount_received = 0, __packetCount_sent = 0;
        //list of packets in queue before connection is setup
        private List<KeyValuePair<Packet, KeyValuePair<Encryption, Encryption>>> __setupQueue = new List<KeyValuePair<Packet, KeyValuePair<Encryption, Encryption>>>();
        /// <summary>
        /// Internal value. Other factors are taken into account
        /// </summary>
        protected bool _connected = true;
        /// <summary>
        /// References all non-static locks for this client
        /// </summary>
        protected readonly Locks _locks = new Locks();
        //async buffers
        private byte[] __buffer = new byte[Packet.RCV_BUFFER_SIZE], __length_header_buf = new byte[Packet.SBYTES_HEADER_SIZE], __data_buf;
        //cursors for buffers
        //__buffer_offset_overlap is used to prevent data erasure when receiving a full packet and there is more in queue
        private int __length_header_cursor = 0, __data_cursor = 0, __expected_length = -1;
        //expected packet serialization
        private Serialization __expected_serialization, __default_serialization = Packet.DEFAULT_SERIALIZATION;
        //expected packet encryption
        private Encryption __expected_encryption;
        //expected packet compression
        private Compression __expected_compression;

        /// <summary>
        /// Native C# socket bound to this client
        /// </summary>
        public Socket Wrapper { get; private set; }
        /// <summary>
        /// Delegate instance. Called when a packet is received.
        /// You can use : OnDataReception += (delegate or method pointer) directly from outside the class
        /// </summary>
        public event PacketExchange OnDataReception = _DefaultPacketReception;
        /// <summary>
        /// Delegate instance. Called when a packet is sent.
        /// You can use : OnDataSent += (delegate/method pointer) directly from outside the class
        /// </summary>
        public event PacketExchange OnDataSent = _DefaultPacketSending;
        /// <summary>
        /// Delegate to call when the connection is unexpectedly severed (not disconnected by request)
        /// </summary>
        public event Action<Client> OnUnexpectedDisconnection = _DefaultUnexpectedDisconnection;
        /// <summary>
        /// Delegate to call when the client is disconnected (either normally or unexpectly). First arg is the client, 2nd is (expected = true, not = false)
        /// </summary>
        /// <seealso cref="OnUnexpectedDisconnection"/>
        public event Action<Client, bool> OnDisconnection = _DefaultDisconnection;
        /// <summary>
        /// Single void method to call when the connection is setup, ready, and in most cases, encrypted
        /// </summary>
        public event Action<Client> OnConnectionSetup = _DefaultConnectionSetup;
        /// <summary>
        /// Event to call when full rights are granted to the client
        /// </summary>
        public event Action<Client> OnFullRightsGranted = _DefaultFullRightsGranted;
        /// <summary>
        /// Event to call when this client (server-side, as client does not need to check for swapping end) has finished swapping keys and crypt helper
        /// </summary>
        public event Action<Client> OnPgpKeySwapped = _DefaultPgpKeySwapped;
        /// <summary>
        /// Process packets received by this client with a given id 
        /// </summary>
        protected Dictionary<byte, PacketExchange> _DataReception_ids { get; set; } = new Dictionary<byte, PacketExchange>();
        /// <summary>
        /// Process packets received by this client with a given id and a given specifier
        /// </summary>
        protected Dictionary<byte, Dictionary<byte, PacketExchange>> _DataReception_ids_specs { get; set; } = new Dictionary<byte, Dictionary<byte, PacketExchange>>();
        /// <summary>
        /// Custom properties of this client
        /// </summary>
        public ClientPropertyManager Properties { get; set; }
        /// <summary>
        /// Local-only (non syncrhonized properties of this client)
        /// </summary>
        public Dictionary<string, object> LocalProperties { get; set; } = new Dictionary<string, object>();
        /// <summary>
        /// Whether this client is connected. Reliable.
        /// </summary>
        public virtual bool Connected
        {
            get
            {
                if (!_connected)
                    return false;

                try
                {
                    if (Wrapper != null && Wrapper.Connected)
                    {
                        if (Wrapper.Poll(0, SelectMode.SelectRead) && Wrapper.Available == 0)
                        {
                            if(COMPLETE_CONNECTION_CHECK)
                                Ping(safe: false);
                            return false;
                        }
                        return true;
                    }
                    else
                        return false;
                }
                catch
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// Whether to bypass connection checks when sending data
        /// </summary>
        public bool IgnoreConnectionStatus { get; set; } = false;
        /// <summary>
        /// The id of this client. For client applications, it should be mirrored with the server
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Remote IP address of this client
        /// </summary>
        public IPAddress Address
        {
            get
            {
                try
                {
                    return ((IPEndPoint)Wrapper.RemoteEndPoint).Address;
                }
                catch (ObjectDisposedException)
                {
                    //unexpected disposed socket here
                    //LogNotNull(LogLevel.ERROR, "prop Client.Address couldn't be accessed : disposed");
                    __FinalDisconnection(false);
                }
                return IPAddress.None;
            }
        }

        /// <summary>
        /// The port to which the client is connected on the server device
        /// </summary>
        public int LocalPort
        {
            get
            {
                try
                {
                    return ((IPEndPoint)Wrapper.LocalEndPoint).Port;
                }
                catch (ObjectDisposedException)
                {
                    //unexpected disposed socket here
                    //LogNotNull(LogLevel.ERROR, "prop Client.LocalPort couldn't be accessed : disposed");
                    __FinalDisconnection(false);
                }
                return int.MinValue;
            }
        }

        /// <summary>
        /// The port which this client uses from his device
        /// </summary>
        public int RemotePort
        {
            get
            {
                try
                {
                    return ((IPEndPoint)Wrapper.RemoteEndPoint).Port;
                }
                catch (ObjectDisposedException)
                {
                    //unexpected disposed socket here
                    //LogNotNull(LogLevel.ERROR, "prop Client.RemotePort couldn't be accessed : disposed");
                    __FinalDisconnection(false);
                }
                return int.MinValue;
            }
        }
        /// <summary>
        /// Whether packets are available
        /// </summary>
        public virtual bool PacketAvailable
        {
            get
            {
                try
                {
                    return Wrapper.Available > 0;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }

            }
        }
        /// <summary>
        /// The number of packets exchanged (sent and received both)
        /// </summary>
        public virtual int PacketCount => ReceivedPacketCount + SentPacketCount;
        /// <summary>
        /// The number of packets received from this side
        /// </summary>
        public virtual int ReceivedPacketCount => __packetCount_received;
        /// <summary>
        /// The number of packets sent from this side
        /// </summary>
        public virtual int SentPacketCount => __packetCount_sent;
        /// <summary>
        /// Whether this client is located on the server
        /// </summary>
        public bool ServerSide => false;
        /// <summary>
        /// The logger for this client. May be null.
        /// </summary>
        public virtual Logging.Logging ClientLogger { get; set; }
        /// <summary>
        /// Whether this client has been established and authentified as administrator
        /// </summary>
        public bool IsAdmin { get; private set; } = false;
        /// <summary>
        /// The time at which the connection with this client was established
        /// </summary>
        public DateTime BeginTime { get; protected set; } = DateTime.Now;
        /// <summary>
        /// A list of all packets exchanged. The bool is whether it was received (true) or sent (false)
        /// </summary>
        public List<PacketLogInfo> ExchangedPackets { get; protected set; } = new List<PacketLogInfo>();
        /// <summary>
        /// Whether this client should log the packets it receives/sends. Set to <see cref="DEFAULT_LOG_PACKETS"/> by default.
        /// </summary>
        public virtual bool LogPackets { get; set; } = DEFAULT_LOG_PACKETS;
        /// <summary>
        /// The BCrypt salt, unique to this exchange
        /// </summary>
        public string BCSalt
        {
            get => __bcryptSalt;

            private set
            {
                if (!String.IsNullOrEmpty(__bcryptSalt) || !ServerSide)
                    return;
                __bcryptSalt = value;
            }
        }
        /// <summary>
        /// The public key of the other end
        /// </summary>
        public CipherKey RemotePKey { get; private set; }
        /// <summary>
        /// The public key of the other end as native bouncy castle instance
        /// </summary>
        public PgpPublicKey RemotePgKey { get; private set; }
        /// <summary>
        /// Is this client fully setup ?
        /// </summary>
        public bool ConnectionSetup { get; private set; } = false;
        /// <summary>
        /// The protocol used by this client
        /// Shortcut for the local end point
        /// </summary>
        public Protocol Protocol => LocalEndPoint.Protocol;
        /// <summary>
        /// Local end point of this client
        /// </summary>
        public PEndPoint LocalEndPoint { get; protected set; }
        /// <summary>
        /// Whether to automatically dispose this client when disconnected
        /// </summary>
        public bool AutoDispose { get; set; } = true;
        /// <summary>
        /// Whether this client is disposed
        /// </summary>
        public bool Disposed { get; private set; }
        /// <summary>
        /// Sym crypt for this client ; changing it client-side may cause disruption
        /// It is unadvised to change it at all without caution as it may cause intermittent packets to be lost
        /// </summary>
        public AbstractSymCrypt SymCrypt
        {
            get => __symCrypt;

            set
            {
                __symCrypt = value;
                _ServerSide_SymKey();
            }
        }
        /// <summary>
        /// Pgp (asymmetric) crypt for this client ; changing it client-side may cause disruption
        /// It is unadvised to change it at all without caution as it may cause intermittent asym-crypted packets/packet data to be lost
        /// </summary>
        public AbstractPgpCrypt PgpCrypt
        {
            get => __pgpCrypt;

            set
            {
                __pgpCrypt = value;
                if (ServerSide)
                {
                    __pgp_swapping = true;
                    _ServerSide_StartExchange_Crypt();
                }
            }
        }
        /// <summary>
        /// Default encryption to use when operating
        /// </summary>
        public Encryption DefaultSymEncryption { get; set; } = __DEFAULT_SYM_ENCRYPTION;
        /// <summary>
        /// Default asym encryption to use when operating
        /// </summary>
        public Encryption DefaultAsymEncryption { get; set; } = __DEFAULT_ASYM_ENCRYPTION;
        /// <summary>
        /// Default compression to use when not specifying any when sending a packet (set to none by framework-default)
        /// </summary>
        public Compression DefaultCompression { get; set; } = Compression.NONE;
        /// <summary>
        /// Default serialization this client will use when not specifying any (also for framework packets). By default is equal to Packet.DEFAULT_SERIALIZATION
        /// </summary>
        /// Send(new Packet(Packet.OK_HEADER, Packet.AUTH_ID, __AUTH_CHANGE_SERIALIZATION_SPEC, value.Id),
        public Serialization DefaultSerialization { get; protected set; } = Packet.DEFAULT_SERIALIZATION;

        /// <summary>
        /// Constructs a client
        /// </summary>
        public Client(IPAddress address, Protocol protocol, int port) : this()
        {
            Wrapper = new Socket(address.AddressFamily, protocol.GetSocketType(), protocol.GetNativeType());
            Wrapper.Connect(address, port);
            LocalEndPoint = new PEndPoint(port, protocol);
            //start receiving...
            Wrapper.BeginReceive(__buffer, 0, __buffer.Length, SocketFlags.None, __DataReceived, Wrapper);

            SetSerialization(Packet.DEFAULT_SERIALIZATION, true);
        }

        /// <summary>
        /// Init constructor
        /// </summary>
        private Client()
        {
            Id = Interlocked.Increment(ref __CURID);
            Properties = new ClientPropertyManager(this);
        }

        /// <summary>
        /// This constructor is only there for servers to create at-a-glance clients
        /// </summary>
        private Client(Socket readySocket) : this()
        {
            Wrapper = readySocket;
            //start receiving...
            try
            {
                Wrapper.BeginReceive(__buffer, 0, __buffer.Length, SocketFlags.None, __DataReceived, Wrapper);
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                LogNotNull(LogLevel.SEVERE, "A client connected but seems to have been instantly disconnected. Follows : ");
                LogNotNull(LogLevel.SEVERE, e.Message);
                return;
            }
        }

        internal void _ServerSide_StartExchange_Crypt()
        {
            //send public key. If not server side, wait to receive
            if (ServerSide)
            {
                //using BCL native serialization as the client may signal serialization change after sending it
                __localKeyPair = PgpCrypt.Generate();
                Send(new PgpPublicKeyPacket(__AUTH_PKEY_SPEC, __localKeyPair.PublicKey)
                {
                    Designation = "Dispatch server PKEY"
                }, Encryption.NONE, Serialization.NATIVE, DefaultCompression);
            }
        }

        /// <summary>
        /// Disconnect this client
        /// </summary>
        /// <param name="message">The disconnection message</param>
        /// <param name="logLevel">The level to log the disconnection with</param>
        public virtual void Disconnect(string message = "No reason specified", LogLevel logLevel = LogLevel.INFO)
        {
            if (!_connected)
                return;

            Send(new StringPacket(Packet.OK_HEADER, Packet.AUTH_ID, __AUTH_DISCONNECTION_SPEC, message)
            {
                Designation = "Sending disconnection status"
            });
            __FinalDisconnection(true);
            LogNotNull(logLevel, "Disconnected : " + message);
        }

        /// <summary>
        /// Sets the symkey of this client to a new one, and sends it PGP-encrypted to the other side.
        /// May cause disruption if wrongly used.
        /// </summary>
        internal void _ServerSide_SymKey()
        {
            if (!ServerSide)
                return;

            if (RemotePKey == null)
                throw new SecurityException("The symkey of a client cannot be sent without a remote pkey");

            __symKey = SymCrypt.Generate();
            Send(new SymKeyPacket(__AUTH_SYMKEY_SPEC, __symKey, PgpCrypt, RemotePgKey)
            {
                Designation = "Server dispatch SYMKEY"
            }, Encryption.NONE, DefaultSerialization, DefaultCompression);
        }

        /// <summary>
        /// Generates a new salt bcrypt salt for this client and returns it
        /// </summary>
        /// <returns>New salt generated</returns>
        /// <remarks>Not sure of its use in hindsight</remarks>
        internal string BcSalt()
        {
            BCSalt = Bcrypt.Salt();
            return BCSalt;
        }

        private async void __FinalDisconnection(bool expected)
        {
            if (!_connected)
                return;

            _connected = false;
            if (OnDisconnection != default(Action<Client, bool>))
            {
                await Task.Factory.StartNew(() =>
                {
                    OnDisconnection(this, expected);
                });
            }

            if (expected)
                Wrapper.Shutdown(SocketShutdown.Both);
            else
            {
                LogNotNull(LogLevel.WARNING, "Unexpected disconnection (socket closed ?). Closing.");
                if (UNEXPECTED_DISCONNECTION_STACKTRACE)
                    LogNotNull(LogLevel.WARNING, "Stacktrace is : ", Environment.StackTrace);
            }
            Wrapper.Close();
            if (AutoDispose)
                Dispose();
        }

        /// <summary>
        /// Destroys this client to free heap space
        /// </summary>
        public void Dispose()
        {
            Disposed = true;

            Properties.Dispose();
            Properties = null;
            LocalProperties = null;

            _DataReception_ids = null;
            _DataReception_ids_specs = null;

            ExchangedPackets = null;

            Wrapper = null;
        }

        /// <summary>
        /// Sets a server property safely, both in public and local, and eventually synchronize it thereafter
        /// </summary>
        /// <param name="property">Property key</param>
        /// <param name="value">Value to set</param>
        /// <param name="overwrite">Overwrite any existing property</param>
        /// <param name="sync">Whether to immediately synchronize</param>
        public void SafeServerProperty(string property, object value, bool overwrite = true, bool sync = false)
        {
            if (!ServerSide)
                return;

            if (LocalProperties.ContainsKey(property) && overwrite)
                LocalProperties[property] = value;
            else
                LocalProperties.Add(property, value);

            Properties.Set(property, new Property(value, PropertyConflictResolution.SERVER_OVERWRITE), overwrite);
            if (sync)
                Properties.Synchronize();
        }

        /// <summary>
        /// Set the serialization property
        /// </summary>
        /// <param name="serialization">Serialization</param>
        /// <param name="signal">Whether to signal change to the other side</param>
        public void SetSerialization(Serialization serialization, bool signal)
        {
            DefaultSerialization = serialization;
            if (signal)
                Send(new Packet(Packet.OK_HEADER, Packet.AUTH_ID, __AUTH_CHANGE_SERIALIZATION_SPEC, (byte)serialization)
                {
                    Designation = "Signaling serialization method change"
                }, Encryption.NONE, serialization, DefaultCompression);
        }

        /// <summary>
        /// Encapsulate securely an object using the crypting informations of this client
        /// </summary>
        /// <typeparam name="T">Generic type of object</typeparam>
        /// <param name="encodeFunc">Function to encode the object into a byte array</param>
        /// <param name="obj">Object to store</param>
        /// <param name="asym">Use asymmetric parameters (if not, symmetric). Default : true</param>
        /// <returns>Encapsulated, securely stored and encrypted object</returns>
        public CryptObj<T> Encapsulate<T>(Func<T, byte[]> encodeFunc, T obj, bool asym = true)
        {
            return asym ? new CryptObj<T>(DefaultAsymEncryption, obj, RemotePKey.Key, encodeFunc) : new CryptObj<T>(DefaultSymEncryption, obj, __symKey, encodeFunc);
        }

        /// <summary>
        /// Encapsulate securely a string using the crypting informations of this client
        /// </summary>
        /// <param name="str">String to store</param>
        /// <param name="asym">Use asymmetric encryption (if not, symmetric). Default : true</param>
        /// <returns>Encapsulated, securely stored and encrypted string</returns>
        public CryptString Encapsulate(string str, bool asym = true)
        {
            return asym ? new CryptString(DefaultAsymEncryption, str, RemotePKey.Key) : new CryptString(DefaultSymEncryption, str, __symKey);
        }

        /// <summary>
        /// Decapsulates an encrypted object using the encrypting informations of this exchange
        /// </summary>
        /// <typeparam name="T">Generic object type to decapsulate</typeparam>
        /// <param name="container">Secure container</param>
        /// <param name="decodeFunc">Custom decode function to use</param>
        /// <param name="asym">Use asymmetric information (if not, symmetric). Default : true</param>
        /// <returns>Decapsulated object</returns>
        public T Decapsulate<T>(CryptObj<T> container, Func<byte[], T> decodeFunc, bool asym = true)
        {
            return asym ? container.Decrypt(__localKeyPair.PrivateKey.Key, decodeFunc) : container.Decrypt(__symKey, decodeFunc);
        }

        /// <summary>
        /// Decapsulates an encrypted string using the encrypted informations of this exchange
        /// </summary>
        /// <param name="container">Secure container</param>
        /// <param name="asym">Use asymmetric information (if not, symmetric). Default : true</param>
        /// <returns>Decapsulated string</returns>
        public string Decapsulate(CryptString container, bool asym = true)
        {
            //Decapsulate<string>
            return asym ? container.Decrypt(__localKeyPair.PrivateKey.Key) : container.Decrypt(__symKey);
        }

        /// <summary>
        /// Pings the server using either default or specified values for the packet
        /// </summary>
        public void Ping(byte id = Packet.SYS_ID, byte specifier = Packet.SYS_DEFAULT_PING_SPEC, bool safe = true)
        {
            if(safe)
                Send(new Packet(Packet.OK_HEADER, id, specifier)
                {
                    Designation = "Sending a ping to the server"
                });
            else
                UnsafeSend(new Packet(Packet.OK_HEADER, id, specifier)
                {
                    Designation = "Sending a ping to the server"
                });
        }

        /// <summary>
        /// Hashes a string using either bcrypt and the salt unique to this session and client (if available) or a default sha-512 1000iter hashing
        /// </summary>
        /// <param name="str">String to hash</param>
        /// <returns>Hashed string</returns>
        public string Hash(string str)
        {
            return String.IsNullOrEmpty(BCSalt) ? Sha512.Hash(str) : Bcrypt.Hash(str, BCSalt);
        }

        /// <summary>
        /// Truly sends data, no check
        /// Uses default symmetric encryption, default serialization, default compression and no encryption as fallback
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        public virtual void UnsafeSend(Packet packet) => UnsafeSend(packet, DefaultSymEncryption, DefaultSerialization, DefaultCompression);

        /// <summary>
        /// Truly sends data, no check
        /// Uses no encryption as default encryption fallback
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        /// <param name="encryption">Default encryption to use</param>
        /// <param name="serialization">Serialization to use. Will use FALLBACK if unsuccessful</param>
        /// <param name="compression">Compression method to use</param>
        public virtual void UnsafeSend(Packet packet, Encryption encryption, Serialization serialization, Compression compression) => 
            UnsafeSend(packet, encryption, Encryption.NONE, serialization, compression);

        /// <summary>
        /// Truly sends data, no check
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        /// <param name="encryption">Default encryption to use</param>
        /// <param name="fallback">Fallback encryption to use if the given one isn't setup. If this one doesn't work either, then no encryption</param>
        /// <param name="serialization">Serialization to use. Will use FALLBACK if unsuccessful</param>
        /// <param name="compression">Compression method to use</param>
        public virtual async void UnsafeSend(Packet packet, Encryption encryption, Encryption fallback, Serialization serialization, Compression compression)
        {
            KeyValuePair<Serialization, byte[]> pready = new KeyValuePair<Serialization, byte[]>();
            //avoid concurrent sending of data
            lock (_locks["OnDataSent"])
            {
                try
                {
                    pready = __FormSendingPacket(encryption, fallback, compression, serialization, packet);
                    Wrapper.Send(new byte[] { (byte)pready.Key, (byte)compression, (byte)encryption });
                    Wrapper.Send(pready.Value.Length.WrapSize());
                    Wrapper.Send(pready.Value);
                }
                catch (ArgumentException e)
                {
                    LogNotNull(LogLevel.ERROR, new string[] { "Cannot send a packet due to invalid size. Follows : ", e.Message });
                }
            }
            await Task.Factory.StartNew(() => packet._PostSetup(pready.Key));
            OnDataSent(this, packet);
        }

        /// <summary>
        /// Tries to send a packet and throws an exception if unsuccessful (client not connected mostly)
        /// Uses default symmetric encryption, default serialization, default compression and no encryption as fallback
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        public virtual void Send(Packet packet) => Send(packet, DefaultSymEncryption, Encryption.NONE, DefaultSerialization, DefaultCompression);

        /// <summary>
        /// Tries to send a packet and throws an exception if unsuccessful (client not connected mostly)
        /// Uses no encryption as fallback
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        /// <param name="encryption">Default encryption to use</param>
        /// /// <param name="serialization">Serialization to use. Will use FALLBACK if unsuccessful</param>
        /// <param name="compression">Default compression method to use</param>
        public virtual void Send(Packet packet, Encryption encryption, Serialization serialization, Compression compression) => 
            Send(packet, encryption, Encryption.NONE, serialization, compression);

        /// <summary>
        /// Tries to send a packet and throws an exception if unsuccessful (client not connected mostly)
        /// </summary>
        /// <param name="packet">Packet to send to this client</param>
        /// <param name="encryption">Default encryption to use</param>
        /// <param name="fallback">Fallback encryption to use if the given one isn't setup. If this one doesn't work either, then no encryption</param>
        /// <param name="serialization">Serialization to use. Will use FALLBACK if unsuccessful</param>
        /// <param name="compression">Compression method to use</param>
        public virtual void Send(Packet packet, Encryption encryption, Encryption fallback, Serialization serialization, Compression compression)
        {
            if (!Connected && !IgnoreConnectionStatus)
            {
                //LogNotNull(LogLevel.ERROR, "Client.Send : client not connected. Stack trace is " + Environment.StackTrace);
                return;
            }

            if ((!ConnectionSetup || __pgp_swapping) && packet.Id != Packet.AUTH_ID)
            {
                lock (_locks["__setupQueue"])
                    __setupQueue.Add(new KeyValuePair<Packet, KeyValuePair<Encryption, Encryption>>(packet, new KeyValuePair<Encryption, Encryption>(encryption, fallback)));
                return;
            }

            if (ServerSide)
            {
                if (packet.ServerInterception != default(DateTime))
                    LogNotNull(LogLevel.DEBUG, "Server sending a packet already marked as server-intercepted");
                else
                    packet.RegisterInterception();
            }

            try
            {
                if ((encryption.GetEncryptionType() == EncryptionType.ASYMMETRIC && RemotePKey == null) || 
                    (encryption.GetEncryptionType() == EncryptionType.SYMMETRIC && __symKey == null))
                {
                    Send(packet, fallback, Encryption.NONE, serialization, compression);
                    return;
                }
                UnsafeSend(packet, encryption, fallback, serialization, compression);
            }
            catch (Exception e)
            {
                if (ALLOW_EXTERNAL_EXCEPTIONS)
                    throw;
                else
                    LogNotNull(LogLevel.ERROR, new string[] { "An exception (" + e.GetType().ToString() + ") occured whilst sending a packet. Follows : ", e.Message, e.StackTrace });
            }
        }

        /// <summary>
        /// Sets a method to be called when this specific client receives a packet with the given id
        /// </summary>
        public void OnReception(byte id, PacketExchange receiver)
        {
            if (!_DataReception_ids.ContainsKey(id))
                _DataReception_ids.Add(id, receiver);
            else
                _DataReception_ids[id] += receiver;
        }

        /// <summary>
        /// Sets a method to be called when this specific client receives a packet with the given id and the given specifier
        /// </summary>
        public void OnReception(byte id, byte specifier, PacketExchange receiver)
        {
            if (!_DataReception_ids_specs.ContainsKey(id))
            {
                _DataReception_ids_specs.Add(id, new Dictionary<byte, PacketExchange>());
                _DataReception_ids_specs[id].Add(specifier, receiver);
            }
            else if (!_DataReception_ids_specs[id].ContainsKey(specifier))
                _DataReception_ids_specs[id].Add(specifier, receiver);
            else
                _DataReception_ids_specs[id][specifier] += receiver;
        }

        /// <summary>
        /// Get the string representation of this client
        /// </summary>
        public override string ToString()
        {
            return "{Address: " + Address + "; Local port: " + LocalPort + "; Remote port: " + RemotePort + "}";
        }

        /// <summary>
        /// Log a message if this client has a logging
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="message">The message to log</param>
        public virtual void LogNotNull(LogLevel level, string message)
        {
            if (ClientLogger == null)
                return;

            lock (_locks["ClientLogger"])
                ClientLogger.Log("[CLIENT ID " + Id + "] " + message, level);
        }

        /// <summary>
        /// Logs a message with a custom app name if this client has a logging
        /// </summary>
        public virtual void LogNotNull(LogLevel level, string appName, string message)
        {
            if (ClientLogger == null)
                return;

            lock (_locks["ClientLogger"])
                ClientLogger.Log("[CLIENT ID " + Id + "] " + message, appName, level);
        }

        /// <summary>
        /// Logs several messages with a custom app name if this client has a logging
        /// </summary>
        public virtual void LogNotNull(LogLevel level, string appName, params string[] messages)
        {
            if (ClientLogger == null)
                return;

            lock (_locks["ClientLogger"])
            {
                //not a foreach for speed
                for (int i = 0; i < messages.Length; i++)
                    LogNotNull(level, appName, messages[i]);
            }
        }

        /// <summary>
        /// Logs several messages with the default app name of this client's logging, if he has one (otherwise nothing is logged)
        /// </summary>
        public virtual void LogNotNull(LogLevel level, params string[] messages)
        {
            if (ClientLogger == null)
                return;

            for (int i = 0; i < messages.Length; i++)
                LogNotNull(level, messages[i]);
        }

        /// <summary>
        /// Resets the methods called when receiving data. USE AT YOUR OWN RISK, MAY OVERRIDE A LOT OF BEHAVIOURS
        /// </summary>
        public void ResetDataReception()
        {
            OnDataReception = _DefaultPacketReception;
        }

        /// <summary>
        /// Resets the methods called when sending data. USE AT YOUR OWN RISK, MAY OVERRIDE A LOT OF BEHAVIOURS
        /// </summary>
        public void ResetDataSending()
        {
            OnDataSent = _DefaultPacketSending;
        }

        private void __DataReceived(IAsyncResult socket)
        {
            try
            {
                Socket sock = (Socket)socket.AsyncState;
                if (!sock.Connected)
                    return;

                int read = sock.EndReceive(socket);
                __ProcessRcv(read);

                if (!Connected)
                {
                    __FinalDisconnection(false);
                    return;
                }

                sock.BeginReceive(__buffer, 0, __buffer.Length, SocketFlags.None, __DataReceived, sock);
            }
            catch (Exception e)
            {
                if (ALLOW_EXTERNAL_EXCEPTIONS)
                    throw;
                if (!(e is ObjectDisposedException) && !(e is SocketException))
                    LogNotNull(LogLevel.SEVERE, new string[] { "Unexpected exception happened: ", e.Message, e.StackTrace });
                //unexpected disconnection
                __FinalDisconnection(false);
                return;
            }
        }

        //todo more modular
        private void __ProcessRcv(int read)
        {
            //it has been an awful mess doing this method due to its nature and to the need for indices
            //I do think it was worth not using list or more expensive objects than arrays because its a method 
            //which is often used and can potentially have a significant impact on performance
            //also, all the array operations (namely Array.Copy) can be heavily optimized by the JIT
            if (read <= 0)
                return;
            int buffer_offset = 0;

            if (__expected_serialization == Serialization.UNKNOWN)
            {
                __expected_serialization = (Serialization)__buffer[buffer_offset];
                buffer_offset++;
            }
            if(__expected_compression == Compression.UNKNOWN && read > buffer_offset)
            {
                __expected_compression = (Compression)__buffer[buffer_offset];
                buffer_offset++;
            }
            if (__expected_encryption == Encryption.UNKNOWN && read > buffer_offset)
            {
                __expected_encryption = (Encryption)__buffer[buffer_offset];
                buffer_offset++;
            }

            read -= buffer_offset;
            if (read <= 0)
                return;
            //reduces buffer size and would preduce a lot of overhead if copied
            //abandoned
            //if (buffer_offset > 0)
            //__buffer = __buffer.Skip(buffer_offset).ToArray();

            //remaning pre-packet length bytes to receive
            int lhrm = __length_header_buf.Length - __length_header_cursor;
            if (lhrm > 0)
            {
                if (read < lhrm)
                {
                    Array.Copy(__buffer, buffer_offset, __length_header_buf, __length_header_cursor, read);
                    __length_header_cursor += read;
                    return;
                }
                else
                {
                    Array.Copy(__buffer, buffer_offset, __length_header_buf, __length_header_cursor, lhrm);
                    __length_header_cursor += lhrm;
                    __expected_length = __length_header_buf.UnwrapSize();
                    __data_buf = new byte[__expected_length];
                    if (read == lhrm)
                        return;
                    else
                    {
                        read -= lhrm;
                        buffer_offset += lhrm;
                    }
                }
            }

            if (__expected_length <= 0 || __expected_serialization == Serialization.UNKNOWN || __expected_compression == Compression.UNKNOWN
                || __expected_encryption == Encryption.UNKNOWN)
            {
                //reading error with pre-packet contents
                LogNotNull(LogLevel.ERROR, "Critical reading error : invalid pre-packet data. Cannot seek, terminating");
                Disconnect("Critical error, invalid pre-packet data ; cannot seek", LogLevel.SEVERE);
                return;
            }

            //remaining data to read
            int drm = __expected_length - __data_cursor;
            if (read < drm)
            {
                Array.Copy(__buffer, buffer_offset, __data_buf, __data_cursor, read);
                __data_cursor += read;
                return;
            }
            else
            {
                Array.Copy(__buffer, buffer_offset, __data_buf, __data_cursor, drm);
                read -= drm;
                buffer_offset += drm;

                try
                {
                    //packet fully received
                    Packet data = __UnpackIncomingPacket();
                    if (data != null)
                    {
                        //process the packet
                        if (data.Id == Packet.AUTH_ID)
                            __AuthPacket(this, data);
                        try
                        {
                            OnDataReception(this, data);
                        }
                        catch (Exception e) when (!ALLOW_EXTERNAL_EXCEPTIONS)
                        {
                            LogNotNull(LogLevel.ERROR, new string[] { "An exception occured during external packet reception treatment (" + e.GetType().ToString() + "). Follows :", e.Message, e.StackTrace });
                        }
                    }
                    else
                        LogNotNull(LogLevel.ERROR, "Reading failed, but no critical error. Skipping...");
                }
                catch (Exception e) when (e is IOException || e is SerializationException || e is SecurityException || e is ObjectDisposedException 
                    || e is InvalidCipherTextException || e is InvalidOperationException)
                {
                    if (e is ObjectDisposedException)
                        LogNotNull(LogLevel.ERROR, new string[] { "ObjectDisposedException. Follows : ", e.Message, e.StackTrace });
                    else if (e is InvalidCipherTextException)
                        LogNotNull(LogLevel.ERROR, new string[] { "Encryption error while receiving data. Follows : ", e.Message, e.StackTrace });
                    else if (e is InvalidOperationException)
                        LogNotNull(LogLevel.ERROR, new string[] { "Invalid operation caught. Follows: ", e.Message });
                    else
                        LogNotNull(LogLevel.SEVERE, new string[] { "Critical reading error. (Several possible causes : connection severed, serialization error...). Follows:", e.Message, e.StackTrace });

                    __FinalDisconnection(false);
                    return;
                }

                __length_header_cursor = 0;
                __data_cursor = 0;
                __expected_length = -1;
                __expected_encryption = Encryption.UNKNOWN;
                __expected_serialization = Serialization.UNKNOWN;
                __expected_compression = Compression.UNKNOWN;
                //another packet is in the buffer
                if (read > 0)
                {
                    Array.Copy(__buffer, buffer_offset, __buffer, 0, read);
                    __ProcessRcv(read);
                }
            }
        }

        //returns also used serialization in case of errors with given one
        private KeyValuePair<Serialization, byte[]> __FormSendingPacket(Encryption encryption, Encryption fallback, Compression compression, 
            Serialization serialization, Packet packet)
        {
            byte[] EncryptMStream(MemoryStream stream)
            {
                if (stream.Length > Packet.HEADER_MAX_VALUE)
                    throw new ArgumentException(stream.Length + " is too large of a size (max : " + Packet.HEADER_MAX_VALUE + "). Packet : " + packet);
                switch (encryption.GetEncryptionType())
                {
                    case EncryptionType.ASYMMETRIC:
                        if (__localKeyPair == null || RemotePKey == null)
                            //in this case we'd rather use no compression to make sure its sent properly
                            return __FormSendingPacket(fallback, Encryption.NONE, Compression.NONE, serialization, packet).Value;
                        return encryption.GetHelper().Encrypt(stream, RemotePKey.Key, (int)stream.Length);
                    case EncryptionType.SYMMETRIC:
                        if (__symKey == null)
                            return __FormSendingPacket(fallback, Encryption.NONE, Compression.NONE, serialization, packet).Value;
                        return encryption.GetHelper().Encrypt(stream, __symKey, (int)stream.Length);
                }
                return stream.ToArray();
            }

            Serialization used = packet._PreSetup(serialization) ? serialization : Serialization.NATIVE;
            using (MemoryStream serialized = serialization.SerializePacket(packet))
            {
                //also bypass when no compression
                if (compression == Compression.NONE)
                    return new KeyValuePair<Serialization, byte[]>(used, EncryptMStream(serialized));
                using (MemoryStream compressed = new MemoryStream())
                {
                    //the compression makes seeking back unreliable, rewind by hand
                    compression.Compress(serialized, compressed, seekBack: false);
                    compressed.Position = 0;

                    return new KeyValuePair<Serialization, byte[]>(used, EncryptMStream(compressed));
                }
            }
        }

        private Packet __UnpackIncomingPacket()
        {
            byte[] decrypted;
            switch (__expected_encryption.GetEncryptionType())
            {
                case EncryptionType.ASYMMETRIC:
                    //is the null delegation really good ? not sure
                    decrypted = __expected_encryption.GetHelper().Decrypt(__data_buf, __localKeyPair?.PrivateKey?.Key);
                    break;
                case EncryptionType.SYMMETRIC:
                    decrypted = __expected_encryption.GetHelper().Decrypt(__data_buf, __symKey);
                    break;
                default:
                    decrypted = __data_buf;
                    break;
            }

            using (MemoryStream ms = new MemoryStream())
            {
                //bypass when no compression for performance's sake
                if (__expected_compression == Compression.NONE)
                {
                    ms.Write(decrypted, 0, decrypted.Length);
                    ms.Position = 0;
                }
                else
                    __expected_compression.Decompress(decrypted, ms, false);
                //compression makes seeking back a bit glitchy
                ms.Position = 0;
                Packet p = __expected_serialization.DeserializePacket(ms);
                p._PostSetup(__expected_serialization);
                return p;
            }
        }

        //process authentication packets
        private static void __AuthPacket(Client client, Packet data)
        {
            switch (data.Specifier)
            {
                case __AUTH_PKEY_SPEC:
                    if (!(data is PgpPublicKeyPacket publicKeyPacket))
                    {
                        client.LogNotNull(LogLevel.WARNING, "AUTH PKEY packet received but type was not expected (?)");
                        break;
                    }
                    if (client.ServerSide && client.RemotePKey != null)
                    {
                        client.LogNotNull(LogLevel.WARNING, "Suspicious AUTH PKEY packet received but remote key was not null");
                        break;
                    }

                    client.RemotePgKey = publicKeyPacket.Get(0);
                    client.RemotePKey = new CipherKey(client.RemotePgKey.GetKey());
                    if (client.ServerSide)
                    {
                        if (client.ConnectionSetup)
                            client.OnPgpKeySwapped(client);
                        else
                            client._ServerSide_SymKey();
                    }
                    else
                    {
                        client.__localKeyPair = client.PgpCrypt.Generate();
                        client.Send(new PgpPublicKeyPacket(__AUTH_PKEY_SPEC, client.__localKeyPair.PublicKey)
                        {
                            Designation = "Client dispatch PKEY"
                        }, Encryption.NONE, client.DefaultSerialization, client.DefaultCompression);
                    }
                    break;
                case __AUTH_SYMKEY_SPEC:
                    if (client.ServerSide)
                    {
                        client.LogNotNull(LogLevel.ERROR, "Suscpicious AUTH SYMKEY packet received on server side");
                        break;
                    }

                    if (!(data is SymKeyPacket symKeyPacket))
                    {
                        client.LogNotNull(LogLevel.WARNING, "AUTH SYMKEY packet received but type was not expected (?)");
                        break;
                    }

                    client.__symKey = symKeyPacket.GetKey(client.PgpCrypt, client.__localKeyPair.PrivateKey);
                    if (!client.ConnectionSetup)
                        client.LogNotNull(LogLevel.INFO, "Handshake successful. Using " + client.SymCrypt.Name + " transmission");

                    //send confirmation handshake
                    client.Send(new Packet(Packet.OK_HEADER, Packet.AUTH_ID, __AUTH_EHSHK_SPEC)
                    {
                        Designation = "Client confirming EHSHK"
                    });
                    //connection has been setup
                    break;
                case __AUTH_EHSHK_SPEC:
                    if (!client.ServerSide)
                        break;

                    if (client.__symKey == null || client.RemotePKey == null)
                    {
                        client.LogNotNull(LogLevel.ERROR, "Suspicious AUTH EHSHK packet received, where symkey and/or pkey are null");
                        break;
                    }

                    client.LogNotNull(LogLevel.INFO, new string[] { "Handshake successful. Using " + client.SymCrypt.Name + " transmission", "Dispatching exchange informations [OK]" });

                    client.Send(new Packet(Packet.OK_HEADER, Packet.AUTH_ID, __AUTH_DISPATCH_INFO_SPEC, client.Id, client.BcSalt())
                    {
                        Designation = "Server dispatch exchange informations"
                    });
                    lock (_CLIENTS_LOCK)
                        _CLIENTS.Add(client.Id, client);

                    if (client.OnConnectionSetup != default(Action<Client>))
                        client.OnConnectionSetup(client);
                    break;
                case __AUTH_DISPATCH_INFO_SPEC:
                    if (client.ServerSide)
                    {
                        client.LogNotNull(LogLevel.ERROR, "Suspicious DPTINF packet received on server side");
                        break;
                    }
                    if (data.OfType(out int clientId))
                    {
                        lock (_CLIENTS_LOCK)
                        {
                            //_CLIENTS.Remove(client.Id);
                            client.Id = clientId;
                            _CLIENTS.Add(clientId, client);
                        }
                    }
                    if (data.ContentLength >= 2 && data.Get(1) is string)
                        client.BCSalt = data.Get(1) as string;

                    if (client.OnConnectionSetup != default(Action<Client>))
                        client.OnConnectionSetup(client);
                    break;
                case __AUTH_ADMIN_SIG_SPEC:
                    if (!client.ServerSide)
                    {
                        client.IsAdmin = data.Header == Packet.ACK_HEADER;
                        client.OnFullRightsGranted(client);
                        break;
                    }
                    else if (!data.OfType(typeof(CryptString)) || String.IsNullOrEmpty(__ADMIN_HASH_PWD))
                        break;
                    if (Bcrypt.Verify(client.Decapsulate(data.Get<CryptString>(0)), __ADMIN_HASH_PWD))
                    {
                        client.Send(new Packet(Packet.ACK_HEADER, Packet.AUTH_ID, __AUTH_ADMIN_SIG_SPEC)
                        {
                            Designation = "Server full rights confirmation"
                        });
                        client.IsAdmin = true;
                        client.LogNotNull(LogLevel.WARNING, "Full rights granted. Client is now administrator");
                        client.OnFullRightsGranted(client);
                        break;
                    }

                    client.Send(new Packet(Packet.AUTH_DENIED_HEADER, Packet.AUTH_ID, __AUTH_ADMIN_SIG_SPEC)
                    {
                        Designation = "Server denies full rights"
                    });
                    client.LogNotNull(LogLevel.SEVERE, "Suspicious client has requested full rights with wrong password. Closing the connection");
                    client.Disconnect("Wrong password for admin rights");
                    break;
                case __AUTH_DISCONNECTION_SPEC:
                    if (!client.Connected)
                        break;
                    if (!data.OfType(out string reason))
                        client.LogNotNull(LogLevel.INFO, "Disconnected : No reason specified");
                    else
                        client.LogNotNull(LogLevel.INFO, "Disconnected : " + reason);
                    client.__FinalDisconnection(true);
                    break;
                case __AUTH_CHANGE_SERIALIZATION_SPEC:
                    if (!data.OfType(out byte sId))
                        break;
                    Serialization s = (Serialization)sId;
                    if (s == Serialization.UNKNOWN || !Enum.IsDefined(typeof(Serialization), s))
                        break;
                    client.SetSerialization(s, false);
                    client.LogNotNull(LogLevel.NORMAL, "Serialization method changed;ID= " + s + " (distant SZCHGSIG)");
                    break;
            }
        }

        /// <summary>
        /// Default reception method for all packets. Calls reception methods and delegates for packets. Logs the packet.
        /// Can be overriden to not call packet reception methods.
        /// </summary>
        /// <param name="client">Client who received the packet, since the method is static</param>
        /// <param name="data">Received packet</param>
        protected static void _DefaultPacketReception(Client client, Packet data)
        {
            if (client.Disposed)
                return;
            Interlocked.Increment(ref client.__packetCount_received);
            if (client.ServerSide)
            {
                if (data.ServerInterception != default(DateTime))
                    client.LogNotNull(LogLevel.DEBUG, "Server received data, but it appears it had already been received before");
                else
                    data.RegisterInterception();
            }
            try
            {
                //sometimes happens
                if (client.LogPackets && !client.Disposed)
                {
                    lock (client._locks["ExchangedPackets"])
                        client.ExchangedPackets.Add(new PacketLogInfo(data, true));
                }
            }
            catch (Exception e)
            {
                client.LogNotNull(LogLevel.ERROR, new string[] { "Error whilst logging a packet (reception). Follows : ", e.Message, e.StackTrace });
            }

            if (client._DataReception_ids.ContainsKey(data.Id))
                client._DataReception_ids[data.Id](client, data);
            if (client._DataReception_ids_specs.ContainsKey(data.Id) && client._DataReception_ids_specs[data.Id].ContainsKey(data.Specifier))
                client._DataReception_ids_specs[data.Id][data.Specifier](client, data);

            ReceptionProc.IdProcesser(data.Id)(client, data);
            ReceptionProc.SpecProcesser(data.Id, data.Specifier)(client, data);
            //client.LogNotNull(LogLevel.INFO, data.ToString());
        }

        /// <summary>
        /// Default sending method for packets. Mainly to store and log them.
        /// </summary>
        /// <param name="client">Client who received the packet, since the method is static</param>
        /// <param name="data">Packet sent</param>
        protected static void _DefaultPacketSending(Client client, Packet data)
        {
            if (client.Disposed)
                return;

            Interlocked.Increment(ref client.__packetCount_sent);

            try
            {
                if (client.LogPackets && !client.Disposed)
                {
                    lock (client._locks["ExchangedPackets"])
                        client.ExchangedPackets.Add(new PacketLogInfo(data, false));
                }
            }
            catch (Exception e)
            {
                client.LogNotNull(LogLevel.ERROR, new string[] { "Error whilst logging a packet (sending). Follows : ", e.Message, e.StackTrace });
            }
            //client.LogNotNull(LogLevel.WARNING, data.ToString());
        }

        /// <summary>
        /// Default full rights granted event handler
        /// </summary>
        protected static void _DefaultFullRightsGranted(Client client)
        {

        }

        /// <summary>
        /// Default unexpected disconnection event handler
        /// </summary>
        /// <param name="client">Disconnected client</param>
        protected static void _DefaultUnexpectedDisconnection(Client client)
        {
            //client.LogNotNull(LogLevel.WARNING, "Unexpected disconnection. Closing");
        }

        /// <summary>
        /// Default disconnection event handler
        /// </summary>
        /// <param name="client">Client disconnected</param>
        /// <param name="expected">Whether the disconnection was expected</param>
        protected static void _DefaultDisconnection(Client client, bool expected)
        {
            if (!expected && client.OnUnexpectedDisconnection != default(Action<Client>))
                _DefaultUnexpectedDisconnection(client);

            //prevent disconnection overlap
            Task.Factory.StartNew(() =>
            {
                lock (_CLIENTS_LOCK)
                    _CLIENTS.Remove(client.Id);
            });
        }

        /// <summary>
        /// Default pgp key swapped event handler
        /// </summary>
        protected static void _DefaultPgpKeySwapped(Client client)
        {
            client.__pgp_swapping = false;
            lock (client._locks["__setupQueue"])
            {
                foreach (var packet in client.__setupQueue)
                    //compression isn't that important, no need to make a big deal about it
                    //serialization also as the other side will have signaled by then any change
                    client.Send(packet.Key, packet.Value.Key, packet.Value.Value, client.DefaultSerialization, client.DefaultCompression);
                client.__setupQueue.Clear();
            }
        }

        /// <summary>
        /// Connection setup event handler to do stuff
        /// </summary>
        /// <param name="client">Client whose connection was setup</param>
        protected static void _DefaultConnectionSetup(Client client)
        {
            lock (client._locks["__setupQueue"])
            {
                client.ConnectionSetup = true;
                foreach (var packet in client.__setupQueue)
                    //see above for compression
                    client.Send(packet.Key, packet.Value.Key, packet.Value.Value, client.DefaultSerialization, client.DefaultCompression);
                client.__setupQueue.Clear();
            }
        }

        /// <summary>
        /// Sets the hashed password to use to accept a client as admin (either local or on a server, albeit local is useless)
        /// Has no effect if the hash is already defined
        /// </summary>
        /// <param name="pwdHash">Hashed password</param>
        public static void SetAdminPwdHash(string pwdHash)
        {
            if (!String.IsNullOrEmpty(__ADMIN_HASH_PWD))
                return;

            __ADMIN_HASH_PWD = pwdHash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">The id</param>
        /// <returns>The client who has the given id</returns>
        public static Client Get(int id)
        {
            lock (_CLIENTS_LOCK)
                return _CLIENTS.ContainsKey(id) ? _CLIENTS[id] : null;
        }
    }
}
