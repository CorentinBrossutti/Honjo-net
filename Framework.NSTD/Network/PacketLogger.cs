using Honjo.Framework.Logging;
using Honjo.Framework.Network.Packets;
using System.Collections.Generic;
using System;

namespace Honjo.Framework.Network
{
    /// <summary>
    /// Sends a packet every time something is logged
    /// </summary>
    public class PacketLogger : Logger
    {
        /// <summary>
        /// Identifier byte of packets to send when logging
        /// </summary>
        public byte Id { get; set; }
        /// <summary>
        /// Specifier byte to use for packets to send when logging
        /// </summary>
        public byte Specifier { get; set; }
        /// <summary>
        /// Client to send packets to when logging
        /// </summary>
        public Client Client { get; set; }
        /// <summary>
        /// Whether to format strings before sending them
        /// </summary>
        public bool Format { get; set; } = true;

        /// <summary>
        /// Constructs a packet logger
        /// </summary>
        /// <param name="appName">Name of the app using this logger</param>
        /// <param name="id">Identifier byte for packets</param>
        /// <param name="specifier">Specifier byte for packets</param>
        /// <param name="client">Client to send packets to</param>
        /// <param name="logStart">Whether to send packets to print a standard starting message. False by default for this one.</param>
        public PacketLogger(string appName, byte id, byte specifier, Client client, bool logStart = false) : base(appName, logStart)
        {
            Id = id;
            Specifier = specifier;
            Client = client;
        }

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="appName">Name of the application to log with</param>
        /// <param name="level">Level to log, check <see cref="Logging.LogLevel"/></param>
        /// <param name="message">Message to log</param>
        public override void Log(string appName, string message, LogLevel level = LogLevel.NORMAL) => _Print(level, Format ? _Format(level, message) : message);

        /// <summary>
        /// Print something as raw. Acutally sends a log packet
        /// </summary>
        /// <param name="level">Level of the message</param>
        /// <param name="message">Message to log</param>
        protected override void _Print(LogLevel level, string message)
        {
            StringPacket spck = new StringPacket(0, Id, Specifier, message);
            Client.Send(spck);
        }
    }

    /// <summary>
    /// A logger which sends packets when logging, and which has a specifier byte for packets varying depending on the log level
    /// </summary>
    public class MultiPLogger : Logger
    {
        /// <summary>
        /// Dictionary to link logging levels to the specifier of packets. Intended for modification.
        /// </summary>
        public Dictionary<LogLevel, byte> LEVEL_SPECIFIERS = new Dictionary<LogLevel, byte>();

        /// <summary>
        /// Identifier byte of packets to send when logging
        /// </summary>
        public byte Id { get; set; }
        /// <summary>
        /// Client to send packets to when logging
        /// </summary>
        public Client Client { get; set; }
        /// <summary>
        /// Whether to format strings before logging them
        /// </summary>
        public bool Format { get; set; } = true;

        /// <summary>
        /// Constructs a new multi-packet logger
        /// </summary>
        /// <param name="appName">Name of application using this logger</param>
        /// <param name="id">Identifier byte for ALL packets. Does not change.</param>
        /// <param name="client">Client to send logging packets to</param>
        /// <param name="logStart">Whether to print out a standard logger start message. False by default for this one, too.</param>
        public MultiPLogger(string appName, byte id, Client client, bool logStart = false) : base(appName, logStart)
        {
            Id = id;
            Client = client;
        }

        /// <summary>
        /// Constructs a new multi-packet logger
        /// </summary>
        /// <param name="appName">Name of application using this logger</param>
        /// <param name="id">Identifier byte for ALL packets. Does not change.</param>
        /// <param name="client">Client to send logging packets to.</param>
        /// <param name="specifier_start">The starting specifier. It will be the specifier for the least severe level (eg: NORMAL or such). For each increasing level, the specifier is incremented.</param>
        /// <param name="logStart">Whether to print out a standard logger start message. False by default for this one.</param>
        public MultiPLogger(string appName, byte id, Client client, byte specifier_start, bool logStart = false) : this(appName, id, client, logStart)
        {
            foreach (var level in Enum.GetValues(typeof(LogLevel)))
                LEVEL_SPECIFIERS.Add((LogLevel)level, specifier_start++);
        }

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="level">Level to log, check <see cref="Logging.LogLevel"/></param>
        /// <param name="message">Message to log</param>
        /// <param name="appName">Name of the application to log with</param>
        public override void Log(string message, string appName, LogLevel level = LogLevel.NORMAL) => _Print(level, Format ? _Format(level, message) : message);

        /// <summary>
        /// Print something as raw. Acutally sends a log packet
        /// </summary>
        /// <param name="level">Level of the message</param>
        /// <param name="message">Message to log</param>
        protected override void _Print(LogLevel level, string message)
        {
            StringPacket spck = new StringPacket(0, Id, LEVEL_SPECIFIERS[level], message);
            Client.Send(spck);
        }
    }
}
