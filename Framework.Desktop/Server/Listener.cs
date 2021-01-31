using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Honjo.Framework.Logging;
using Honjo.Framework.Network;
using Honjo.Framework.Network.Packets;

namespace Honjo.Framework.Server
{

    public abstract class Listener : Socket
    {
        private static bool __ALREADY_SETUP = false;
        public static readonly string[] EXTENSIONS_FOLDERS = new string[]
        {
            "./",
            "./extensions/"
        };
        /// <summary>
        /// Same as below
        /// </summary>
        public static readonly Dictionary<PEndPoint, ServerExtensionInitializer> clientSyncInit = new Dictionary<PEndPoint, ServerExtensionInitializer>()
        {
            {new PEndPoint(5000, Protocol.TCP), new ServerExtensionInitializer(Assembly.GetExecutingAssembly(), __MainServerNewClient)}
        };
        protected static readonly object _clientSyncInit_lock = new object();

        /// <summary>
        /// Do not mess with it. Only iterate
        /// </summary>
        public static readonly Dictionary<PEndPoint, Listener> LISTENERS = new Dictionary<PEndPoint, Listener>();
        protected static readonly object _LISTENERS_LOCK = new object();

        public static readonly Dictionary<Protocol, Type> PROTOCOL_LISTENER_TYPES = new Dictionary<Protocol, Type>()
        {
            {Protocol.TCP, typeof(ListenerTCP)}
        };

        protected PEndPoint _endPoint;
        protected bool _terminated = false;

        /// <summary>
        /// Constructs a new listener. Can be used to manually add them
        /// </summary>
        /// <param name="endPoint">End point of the listener</param>
        public Listener(PEndPoint endPoint) : base(AddressFamily.InterNetwork, endPoint.Protocol.GetSocketType(), endPoint.Protocol.GetNativeType())
        {
            _endPoint = endPoint;
            endPoint.Protocol.SetupListener(this, endPoint.Port);

            lock (_LISTENERS_LOCK)
                LISTENERS.Add(endPoint, this);
        }

        /// <summary>
        /// Terminate this listener
        /// <param name="removeCSyncInit">Default : true. Whether or not to unlink the extension on the port of this listener</param>
        /// </summary>
        public void Terminate(bool removeCSyncInit = true)
        {
            _terminated = true;
            Close();
            lock (_LISTENERS_LOCK)
                LISTENERS.Remove(_endPoint);
            if (removeCSyncInit)
            {
                lock (_clientSyncInit_lock)
                    clientSyncInit.Remove(_endPoint);
            }
            ServerMain.logging.Info("Listener on port " + _endPoint + " has been successfuly terminated");
        }

        /// <summary>
        /// Initializes a new client for the main server
        /// </summary>
        /// <param name="newClient">The client</param>
        private static void __MainServerNewClient(Client newClient)
        {
        }

        /// <summary>
        /// Set up all listeners for all default ports
        /// <seealso cref="Listener(int)"/>
        /// <seealso cref="Link(int, ClientInitialization)"/>
        /// </summary>
        public static void Setup()
        {
            if (__ALREADY_SETUP)
            {
                ServerMain.logging.Alert("static Listener.Setup : Already setup!");
                return;
            }

            ServerMain.logging.Info("Setting up default listeners...");
            lock (_clientSyncInit_lock)
            {
                foreach (var port in clientSyncInit.Keys)
                {
                    Instantiate(port);
                    ServerMain.logging.Log("Default listener set up on port " + port.Port);
                }
            }

            ServerMain.logging.Info("Setting up extension listeners...");
            foreach (var folder in EXTENSIONS_FOLDERS)
                ServerExtensionInitializer.FolderSetup_listeners(folder, clientSyncInit, ServerMain.logging);

            ServerMain.logging.Eyecatch("   DONE...");
            __ALREADY_SETUP = true;
        }

        /// <summary>
        /// Dynamically link a port and an extension on the go
        /// </summary>
        /// <param name="port">Port to set up a listener on</param>
        /// <param name="extension">Extension to link</param>
        public static void Link(PEndPoint port, ServerExtensionInitializer extension)
        {
            if (clientSyncInit.ContainsKey(port))
            {
                ServerMain.logging.Alert("static Listener.Link : port " + port + " is already bounded!");
                return;
            }
            clientSyncInit.Add(port, extension);
            Instantiate(port);
        }

        /// <summary>
        /// Disconnects the clients on a given extension
        /// </summary>
        /// <returns>A list contaning the ids of all clients disconnected</returns>
        public static List<int> DisconnectExtension(string extensionName)
        {
            List<int> disconnections = new List<int>();
            lock (Client._CLIENTS_LOCK)
            {
                foreach (var client in Client.Clients_collection)
                {
                    if (clientSyncInit[client.LocalEndPoint].ToString().Equals(extensionName))
                    {
                        client.Disconnect();
                        disconnections.Add(client.Id);
                    }
                }
            }
            return disconnections;
        }

        /// <summary>
        /// Unloads the listeners of an extension
        /// </summary>
        /// <param name="extName">Extension name</param>
        /// <param name="removeAsm">Whether to unload the assembly as well. Default : true</param>
        /// <param name="keepPath">Cf ServerExtensionInitializer. Whether to keep the path when unloading the assembly as well</param>
        public static void Unload(string extName, bool removeAsm = true, bool keepPath = false)
        {
            ServerMain.logging.Info("Unloading extension " + extName);
            List<PEndPoint> endPoints = new List<PEndPoint>();
            lock (Client._CLIENTS_LOCK)
            {
                foreach (var client in Client.Clients_collection)
                {
                    if (clientSyncInit[client.LocalEndPoint].ToString().Equals(extName))
                    {
                        if (!endPoints.Contains(client.LocalEndPoint))
                            endPoints.Add(client.LocalEndPoint);
                        client.Disconnect();
                    }
                }
            }
            foreach (var endPoint in endPoints)
                clientSyncInit.Remove(endPoint);

            foreach (var endPoint in endPoints)
                LISTENERS[endPoint].Terminate();

            ServerMain.logging.Log("All listeners of " + extName + " terminated");

            if (removeAsm)
                ServerExtensionInitializer.RemoveAssembly(extName, keepPath);
        }

        /// <summary>
        /// Instantiates a new listener with a type fit for the given protocol and the given port
        /// </summary>
        public static Listener Instantiate(Protocol protocol, int port) => Instantiate(new PEndPoint(port, protocol));

        /// <summary>
        /// Instantiates a new listener with a type fit for the given end point
        /// </summary>
        public static Listener Instantiate(PEndPoint endPoint)
        {
            return (Listener)PROTOCOL_LISTENER_TYPES[endPoint.Protocol].GetConstructor(new Type[] { typeof(PEndPoint) }).Invoke(new object[] { endPoint });
        }
    }

    public class ListenerTCP : Listener
    {
        public ListenerTCP(PEndPoint endPoint) : base(endPoint) => BeginAccept(_OnConnection, this);

        protected static async void _OnConnection(IAsyncResult listener)
        {
            ListenerTCP tli = (ListenerTCP)listener.AsyncState;
            try
            {
                Socket socket = tli.EndAccept(listener);
                if (tli._terminated)
                    return;
                //the method has no state and is thread-safe, let the clients connect as fast as they can
                tli.BeginAccept(_OnConnection, tli);

                Client c = await Task.Run(() => Client._Server_New(socket, tli._endPoint, ServerMain.logging));

                clientSyncInit[tli._endPoint].NewClient(c);

                c.Properties.Set("Server.Extension", new Property(clientSyncInit[tli._endPoint].ToString()));
                c.OnDisconnection += ServerData.LogDisconnectedClient;
                c.OnFullRightsGranted += ServerJournal.AdminConnected;
                //c.Properties.Synchronize();

                if (socket == null)
                    throw new SocketException();

                ServerMain.logging.Info("Client connected on port " + ((IPEndPoint)tli.LocalEndPoint).Port + " with id " + c.Id + ". Remote address: " + ((IPEndPoint)socket.RemoteEndPoint).Address.ToString());
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                ServerMain.logging.Log("A client connected but seems to have been instantly disconnected. Follows : ", LogLevel.SEVERE);
                ServerMain.logging.Log(e.Message, LogLevel.SEVERE);
                return;
            }
        }
    }

    [Obsolete("UDP support has been abandoned due to the structure of the exchanges")]
    public class ListenerUDP : Listener
    {
        protected byte[] _buffer = new byte[Packet.RCV_BUFFER_SIZE], _length_header_buf = new byte[Packet.SBYTES_HEADER_SIZE],
            _data_buf;
        protected int _length_header_cursor = 0, _data_cursor = 0;

        public ListenerUDP(PEndPoint endPoint) : base(endPoint)
        {
            BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, OnReception, this);
        }

        protected void OnReception(IAsyncResult listener)
        {
            //ListenerUDP tli = (ListenerUDP)listener.AsyncState;
            //int read = tli.EndReceive(listener);
            //if (read == 0)
            //{
            //    //unexpected disconnection
            //}
            //if(_length_header_cursor + 1 < _length_header_buf.Length)
            //{
            //    if (read < _length_header_buf.Length - (_length_header_cursor + 1))
            //    {
            //        _buffer.NCopy(_length_header_buf, _length_header_cursor, read);
            //        _length_header_cursor = read - 1;
            //    }
            //    else if(read >= _length_header_buf.Length - (_length_header_cursor + 1))
            //    {
            //        _buffer.NCopy(_length_header_buf, _length_header_cursor, _length_header_buf.Length - (_length_header_cursor + 1));
            //        _length_header_cursor = _length_header_buf.Length - 1;
            //        _data_buf = new byte[_length_header_buf.UnwrapSize()];
            //        if(read > _length_header_buf.Length - (_length_header_cursor + 1))
            //        {
            //            _buffer = _buffer.Skip(_length_header_buf.Length - (_length_header_cursor + 1)).ToArray();
            //        }
            //    }
            //}

            //    _buffer.Take(_length_header_buf.Length).ToArray().CopyTo(_length_header_buf, 0);
        }
    }
}
