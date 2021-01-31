using System;
using System.Collections.Generic;

namespace Honjo.Framework.Logging.Loggers
{
    /// <summary>
    /// Logs to the console (WriteLine)
    /// </summary>
    public class ConsoleLogger : Logger
    {
        private static readonly Dictionary<LogLevel, ConsoleColor> DEFAULT_COLORS = new Dictionary<LogLevel, ConsoleColor>()
        {
            {LogLevel.DEBUG, ConsoleColor.Gray},
            {LogLevel.NORMAL, ConsoleColor.White},
            {LogLevel.INFO, ConsoleColor.White},
            {LogLevel.EYECATCHER, ConsoleColor.Green},
            {LogLevel.WARNING, ConsoleColor.Yellow},
            {LogLevel.ERROR, ConsoleColor.Red},
            {LogLevel.SEVERE, ConsoleColor.DarkRed},
            {LogLevel.FATAL, ConsoleColor.DarkRed}
        };

        private LogLevel current;
        /// <summary>
        /// Dictionary to link log levels to different console colors. Can be freely modified.
        /// </summary>
        public Dictionary<LogLevel, ConsoleColor> colors = new Dictionary<LogLevel, ConsoleColor>()
        {
            {LogLevel.DEBUG, ConsoleColor.Gray},
            {LogLevel.NORMAL, ConsoleColor.White},
            {LogLevel.INFO, ConsoleColor.White},
            {LogLevel.EYECATCHER, ConsoleColor.Green},
            {LogLevel.WARNING, ConsoleColor.Yellow},
            {LogLevel.ERROR, ConsoleColor.Red},
            {LogLevel.SEVERE, ConsoleColor.DarkRed},
            {LogLevel.FATAL, ConsoleColor.DarkRed}
        };

        /// <summary>
        /// Constructs a new logger, which will log directly to console (stdout)
        /// </summary>
        /// <param name="appName">Name of the appliation using this logger</param>
        /// <param name="logStart">Whether to print a standard message when this logger starts. True by default.</param>
        public ConsoleLogger(string appName, bool logStart = true) : base(appName, logStart)
        {
        }

        /// <summary>
        /// Set the console color
        /// </summary>
        /// <param name="level">Level to set the color</param>
        protected void _SetConsoleColor(LogLevel level)
        {
            if(current != level)
            {
                current = level;
                _SetConsoleColor(colors[level]);
            }
        }

        /// <summary>
        /// Set the console color
        /// </summary>
        /// <param name="color">Native console color</param>
        protected void _SetConsoleColor(ConsoleColor color)
        {
            Console.ResetColor();
            Console.ForegroundColor = color;
        }

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="level">Level to log, check <see cref="LogLevel"/>. Default is normal</param>
        /// <param name="appName">Name of the application to log</param>
        /// <param name="message">Message to log</param>
        public override void Log(string message, string appName, LogLevel level = LogLevel.NORMAL)
        {
            if (!Mode.ShouldLog(level))
                return;

            _Print(level, _Format(level, message));
        }

        /// <summary>
        /// Print system-wise message (logger start...)
        /// </summary>
        /// <param name="level">Level, mainly used for color and formatting and does not hold any real information</param>
        /// <param name="message">The message to print</param>
        protected override void _Print(LogLevel level, string message)
        {
            _SetConsoleColor(level);

            Console.WriteLine(message);
        }

        /// <summary>
        /// The log start message
        /// </summary>
        protected override List<string> _LogStartMsg()
        {
            return new List<string>()
            {
                "",
                "-------------------------------------------->",
                "| Logger for " + AppName + " started on " + DateTime.Now.ToString(),
                "| Now logging on console, standard output",
                "---------------------------------------"
            };
        }

        /// <summary>
        /// Gets the default color for a given logging level
        /// </summary>
        public static ConsoleColor DefaultColor(LogLevel level) => DEFAULT_COLORS[level];
    }
}
