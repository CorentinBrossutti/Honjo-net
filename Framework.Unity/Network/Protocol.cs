using System;
using System.Net;
using System.Net.Sockets;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Available protocols for clients
    /// </summary>
    public enum Protocol
    {
        /// <summary>
        /// TCP protocol
        /// </summary>
        TCP,
        /// <summary>
        /// UDP protocol
        /// </summary>
        [Obsolete("Support has been dropped for UDP (structure issues with this protocol)")]
        UDP
    }

    /// <summary>
    /// Extension methods for protocols
    /// </summary>
    public static class ProtocolMethods
    {
    #pragma warning disable CS0618 // Le type ou le membre est obsolète
        /// <summary>
        /// The preferable socket type to use for a given protocol
        /// </summary>
        public static SocketType GetSocketType(this Protocol protocol)
        {
            switch (protocol)
            {
                case Protocol.TCP:
                    return SocketType.Stream;
                case Protocol.UDP:
                    return SocketType.Dgram;
            }
            return SocketType.Unknown;
        }

        /// <summary>
        /// The native C# protocol type linked to a protocol
        /// </summary>
        public static ProtocolType GetNativeType(this Protocol protocol)
        {
            switch (protocol)
            {
                case Protocol.TCP:
                    return ProtocolType.Tcp;
                case Protocol.UDP:
                    return ProtocolType.Udp;
            }
            return ProtocolType.Unknown;
        }

        /// <summary>
        /// Sets up a listener for a socket and a given protocol
        /// </summary>
        public static void SetupListener(this Protocol protocol, Socket listener, int port)
        {
            switch(protocol)
            {
                case Protocol.TCP:
                    listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    listener.Listen(port);
                    break;
                case Protocol.UDP:
                    listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    break;
            }
        }
    #pragma warning restore CS0618 // Le type ou le membre est obsolète
    }

    /// <summary>
    /// Stands for protocol end point. A port and a protocol (which defines an end point)
    /// </summary>
    public sealed class PEndPoint
    {
        /// <summary>
        /// The port number
        /// </summary>
        public int Port { get; private set; }

        /// <summary>
        /// The protocol used
        /// </summary>
        public Protocol Protocol { get; private set; }

        /// <summary>
        /// Constructs a new protocol end point
        /// </summary>
        public PEndPoint(int port, Protocol protocol)
        {
            Port = port;
            Protocol = protocol;
        }

        /// <summary>
        /// Simple equality check for the port and the protocol
        /// </summary>
        public override bool Equals(object obj) => obj != null && obj is PEndPoint && ((PEndPoint)obj).Port == Port && ((PEndPoint)obj).Protocol == Protocol;

        /// <summary>
        /// Hash code
        /// </summary>
        public override int GetHashCode() => Port.GetHashCode() * Protocol.GetHashCode();
    }
}