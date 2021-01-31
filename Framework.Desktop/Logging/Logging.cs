using System.Collections.Generic;

namespace Honjo.Framework.Logging
{
    /// <summary>
    /// Stored using application name, and thus has to be unique for a given application
    /// A set of multiple loggers
    /// </summary>
    public sealed class Logging
    {
        private static readonly Dictionary<string, Logging> __LOGGERS = new Dictionary<string, Logging>();
        private static readonly object __LOGGERS_LOCK = new object();

        /// <summary>
        /// All loggers contained
        /// </summary>
        public List<Logger> Loggers { get; private set; }
        /// <summary>
        /// Name of the application using this set of loggers
        /// </summary>
        public string AppName { get; private set; }

        private Logging(string appName)
        {
            Loggers = new List<Logger>();
            AppName = appName;
        }

        /// <summary>
        /// Adds a new logger to the set
        /// </summary>
        /// <param name="logger">The logger to add</param>
        public void Put(Logger logger) => Loggers.Add(logger);

        /// <summary>
        /// Log a message to all the loggers of this set
        /// </summary>
        /// <param name="level">Level of severity, check <see cref="LogLevel"/></param>
        /// <param name="message">Message to log</param>
        public void Log(string message, LogLevel level = LogLevel.NORMAL) => Log(message, AppName, level);

        /// <summary>
        /// Log a message to all the loggers of this set
        /// </summary>
        /// <param name="level">Level of severity, check <see cref="LogLevel"/></param>
        /// <param name="appName">Name of the application log with</param>
        /// <param name="message">Message to log</param>
        public void Log(string message, string appName, LogLevel level = LogLevel.NORMAL)
        {
            foreach (var logger in Loggers)
                logger.Log(message, level);
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
        /// Gets a new logging instance
        /// </summary>
        /// <param name="appName">Name of the application using this set of loggers</param>
        /// <returns>The set of loggers for the given application</returns>
        public static Logging GetLogging(string appName)
        {
            lock (__LOGGERS_LOCK)
            {
                if (__LOGGERS.ContainsKey(appName))
                    return __LOGGERS[appName];
                Logging l = new Logging(appName);
                __LOGGERS.Add(appName, l);

                return l;
            }
        }

        /// <summary>
        /// If the set of loggers already exists, the given loggers are added
        /// If it does not exist, they are also added
        /// </summary>
        /// <param name="appName">Name of the application using this set of loggers</param>
        /// <param name="loggers">A list of all loggers to add to the set. Added on top of the others if the set already exists for the given application.</param>
        /// <returns>The set of loggers for the given application, which has been added the given loggers</returns>
        public static Logging GetLogging(string appName, params Logger[] loggers)
        {
            Logging l = GetLogging(appName);
            foreach (var logger in loggers)
            {
                l.Put(logger);
            }

            return l;
        }
    }

    /// <summary>
    /// Verbosity enum when logging
    /// </summary>
    public enum VerboseMode
    {
        /// <summary>
        /// Nothing is logged
        /// </summary>
        NOTHING,
        /// <summary>
        /// Fatal, severe and errors are logged
        /// </summary>
        MINIMAL,
        /// <summary>
        /// Fatal, severe, errors, warnings, and eyecatchers (important infos)
        /// </summary>
        STANDARD,
        /// <summary>
        /// Fatal, severe, errors, warnings, infos
        /// </summary>
        FULL,
        /// <summary>
        /// Everything except debug
        /// </summary>
        EXTENDED,
        /// <summary>
        /// Everything including debug
        /// </summary>
        DEBUG
    }

    /// <summary>
    /// Extensions for enum VerboseMode
    /// </summary>
    public static class VerboseModeExtensions
    {
        /// <summary>
        /// Whether a message with a given level should be logged according to a verbosity mode (level)
        /// </summary>
        public static bool ShouldLog(this VerboseMode mode, LogLevel level)
        {
            switch(mode)
            {
                case VerboseMode.DEBUG:
                    return true;
                case VerboseMode.EXTENDED:
                    return level > LogLevel.DEBUG;
                case VerboseMode.FULL:
                    return level > LogLevel.NORMAL;
                case VerboseMode.STANDARD:
                    return level > LogLevel.INFO;
                case VerboseMode.MINIMAL:
                    return level > LogLevel.WARNING;
                case VerboseMode.NOTHING:
                    return false;
            }
            return true;
        }
    }
}
