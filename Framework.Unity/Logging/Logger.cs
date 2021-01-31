using System;
using System.Collections.Generic;

namespace Honjo.Framework.Logging
{
    /// <summary>
    /// A single logger
    /// </summary>
    public abstract class Logger
    {
        /// <summary>
        /// Name of the application using this logger
        /// </summary>
        public string AppName { get; protected set; }
        /// <summary>
        /// Verbose mode of this logger, full by default
        /// </summary>
        public VerboseMode Mode { get; set; } = VerboseMode.FULL;

        /// <summary>
        /// Constructs a new logger
        /// </summary>
        /// <param name="appName">Name of the application (or sub...) using this logger</param>
        /// <param name="logStart">Whether to log a message when the logger starts. True by default.</param>
        public Logger(string appName, bool logStart = true)
        {
            AppName = appName;

            if(logStart)
            {
                foreach (var msg in _LogStartMsg())
                    _Print(LogLevel.EYECATCHER, msg);
            }
        }

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="level">Level to log, check <see cref="LogLevel"/>. Default is normal</param>
        /// <param name="message">Message to log</param>
        public void Log(string message, LogLevel level = LogLevel.NORMAL) => Log(message, AppName, level);

        /// <summary>
        /// Logs several messages with a custom app name
        /// </summary>
        public void Log(LogLevel level, string appName, params string[] messages)
        {
            foreach (var message in messages)
                Log(message, appName, level);
        }

        /// <summary>
        /// Logs several messages
        /// </summary>
        public void Log(LogLevel level, params string[] messages) => Log(level, AppName, messages);

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="level">Level to log, check <see cref="LogLevel"/>. Default is normal</param>
        /// <param name="appName">Name of the application to log</param>
        /// <param name="message">Message to log</param>
        public abstract void Log(string message, string appName, LogLevel level = LogLevel.NORMAL);

        /// <summary>
        /// Print system-wise message (logger start...)
        /// </summary>
        /// <param name="level">Level, mainly used for color and formatting and does not hold any real information</param>
        /// <param name="message">The message to print</param>
        protected abstract void _Print(LogLevel level, string message);

        /// <summary>
        /// Will NOT be formatted
        /// </summary>
        protected virtual List<string> _LogStartMsg()
        {
            return new List<string>()
            {
                "",
                "-------------------------------------------->",
                "| Logger for " + AppName + " started on " + DateTime.Now.ToString(),
                "---------------------------------------"
            };
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Message to log as debug</param>
        public void Debug(string message) => Debug(message, AppName);

        /// <summary>
        /// Logs a debug message
        /// </summary>
        /// <param name="message">Message to log as debug</param>
        /// <param name="appName">Name of the application to log with</param>
        public void Debug(string message, string appName) => Log(message, appName, LogLevel.DEBUG);

        /// <summary>
        /// Log an information
        /// </summary>
        /// <param name="message">Information to log</param>
        public void Info(string message) => Info(message, AppName);

        /// <summary>
        /// Log an information
        /// </summary>
        /// <param name="message">Information to log</param>
        /// <param name="appName">Name of the application to log</param>
        public void Info(string message, string appName) => Log(message, appName, LogLevel.INFO);

        /// <summary>
        /// Log an important information (eyecatcher)
        /// </summary>
        /// <param name="message">Message to log</param>
        public void Eyecatch(string message) => Eyecatch(message, AppName);

        /// <summary>
        /// Log an important information (eyecatcher)
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="appName">Name of the application to log with</param>
        public void Eyecatch(string message, string appName) => Log(message, appName, LogLevel.EYECATCHER);

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">Warning to log</param>
        public void Warn(string message) => Warn(message, AppName);

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">Warning to log</param>
        /// <param name="appName">Name of the application to log</param>
        public void Warn(string message, string appName) => Log(message, appName, LogLevel.WARNING);

        /// <summary>
        /// Logs an error. More severe errors (SEVERE, FATAL levels mostly) must be logged manually using <see cref="Log(string, LogLevel)"/>
        /// </summary>
        /// <param name="message">Error to log</param>
        public void Alert(string message) => Alert(message, AppName);

        /// <summary>
        /// Logs an error. More severe errors (SEVERE, FATAL levels mostly) must be logged manually using <see cref="Log(string, LogLevel)"/>
        /// </summary>
        /// <param name="message">Error to log</param>
        /// <param name="appName">Name of the application to log</param>
        public void Alert(string message, string appName) => Log(message, appName, LogLevel.ERROR);

        /// <summary>
        /// Returns a formatted, ready to log, version of a string
        /// </summary>
        /// <param name="level">Level of severity of the message</param>
        /// <param name="message">Message (string) to format</param>
        /// <returns>The formatted string</returns>
        protected virtual string _Format(LogLevel level, string message) => _Format(level, message, AppName);

        /// <summary>
        /// Returns a formatted, ready to log, version of a string
        /// </summary>
        /// <param name="level">Level of severity of the message</param>
        /// <param name="message">Message (string) to format</param>
        /// <param name="appName">Name of the application to log with</param>
        /// <returns>The formatted string</returns>
        protected virtual string _Format(LogLevel level, string message, string appName) => "[" + DateTime.Now.ToString() + 
            "]--[" + appName + "]-[" + (level == LogLevel.EYECATCHER ? "INFO" : level.ToString()) + "] " + message;
    }

    /// <summary>
    /// Defines the level (severity) of a log
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Additional information (debug)
        /// </summary>
        DEBUG,
        /// <summary>
        /// Everything is normal (execution informations)
        /// </summary>
        NORMAL,
        /// <summary>
        /// This is just an information you may want to see
        /// </summary>
        INFO,
        /// <summary>
        /// Equals to information level, except it is synthethic and is differenciated from less important informations
        /// </summary>
        EYECATCHER,
        /// <summary>
        /// Nothing wrong, but you may want to check this
        /// </summary>
        WARNING,
        /// <summary>
        /// Something went wrong
        /// </summary>
        ERROR,
        /// <summary>
        /// An error that could possibly cause big trouble
        /// </summary>
        SEVERE,
        /// <summary>
        /// The application cannot continue
        /// </summary>
        FATAL,
    }
}
