using System.IO;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace Honjo.Framework.Logging.Loggers
{
    /// <summary>
    /// Log to a single, unique, file
    /// </summary>
    public class FileLogger : Logger
    {
        /// <summary>
        /// The list of lines that have not been logged yet. Used when logging at DTOR time.
        /// </summary>
        protected List<string> lines = new List<string>();
        private readonly object __lines_lock = new object();

        /// <summary>
        /// The default this logger will write (overwrite/append) to
        /// </summary>
        public string Filepath { get; set; }
        /// <summary>
        /// Type of this logger
        /// </summary>
        public FLgType Type { get; protected set; }

        /// <summary>
        /// Constructs a new FileLogger, which will log according to his type to a given file (or multiple files...)
        /// </summary>
        /// <param name="appName">Name of the application using this logger</param>
        /// <param name="filepath">The path of the file to write to when logging</param>
        /// <param name="type">The file logging type. See relative doc</param>
        /// <param name="logStart">Whether to print a standard logging start message. True by default.</param>
        public FileLogger(string appName, string filepath, FLgType type, bool logStart = true) : base(appName, false)
        {
            Filepath = filepath;

            Type = type;

            AppDomain.CurrentDomain.ProcessExit += __DTOR;

            if (logStart)
            {
                foreach (var msg in _LogStartMsg())
                    _Print(LogLevel.ERROR, msg);
            }
        }

        //dtor
        /// <summary>
        /// Blocks this thread whilst an active logging is ongoing
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void __DTOR(object sender, EventArgs args)
        {
            lock(__lines_lock)
            {
                switch (Type)
                {
                    case FLgType.OVERWRITE_DTOR:
                        File.WriteAllLines(Filepath, lines.ToArray());
                        break;
                    case FLgType.APPEND_DTOR:
                        __AppendAllLines(Filepath, lines);
                        break;
                }
            }
        }

        //since it does not exist with framework 3.5
        private void __AppendAllLines(string path, IEnumerable<string> lines)
        {
            using (var writer = new StreamWriter(path, true))
            {
                foreach (var line in lines)
                    writer.WriteLine(line);
            }
        }

        /// <summary>
        /// Will use the file of this logger. Overwrites it.
        /// </summary>
        /// <param name="flush">Whether to flush the logged lines after writing. True by default.</param>
        public void Overwrite(bool flush = true) => Overwrite(Filepath, flush);

        /// <summary>
        /// Will use the file of this logger. Appends to it.
        /// </summary>
        /// <param name="flush">Whether to flush the logger lines after writing. True by default.</param>
        public void Append(bool flush = true) => Append(Filepath, flush);

        /// <summary>
        /// Overwrites the file with logged lines
        /// </summary>
        /// <param name="path">Path of the file to overwrite</param>
        /// <param name="flush">Whether to flush the logged lines after writing. True by default.</param>
        public void Overwrite(string path, bool flush = true)
        {
            lock(__lines_lock)
            {
                File.WriteAllLines(path, lines.ToArray());
                if (flush)
                    lines.Clear();
            }
        }

        /// <summary>
        /// Appends the logged lines
        /// </summary>
        /// <param name="path">Path of the file to append to</param>
        /// <param name="flush">Whether to flush the logged lines after writing. True by default.</param>
        public void Append(string path, bool flush = true)
        {
            lock(__lines_lock)
            {
                __AppendAllLines(path, lines);
                if (flush)
                    lines.Clear();
            }
        }

        /// <summary>
        /// Logs a message. Should apply formatting and stuff.
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="appName">Name of the application to log with</param>
        /// <param name="level">Level to log, check <see cref="LogLevel"/></param>
        public override void Log(string message, string appName, LogLevel level = LogLevel.NORMAL)
        {
            if (!Mode.ShouldLog(level))
                return;

            _Print(level, _Format(level, message));
        }

        /// <summary>
        /// Print a raw message
        /// </summary>
        protected override void _Print(LogLevel level, string message)
        {
            if (String.IsNullOrEmpty(Filepath))
                return;

            switch (Type)
            {
                case FLgType.OVERWRITE_ONREC:
                    File.WriteAllText(Filepath, message);
                    break;
                case FLgType.APPEND_ONREC:
                    File.AppendAllText(Filepath, message);
                    break;
                case FLgType.OVERWRITE_DTOR:
                case FLgType.APPEND_DTOR:
                    lock(__lines_lock)
                        lines.Add(message);
                    break;
            }
        }

        /// <summary>
        /// Log start message
        /// </summary>
        protected override List<string> _LogStartMsg()
        {
            return new List<string>()
            {
                "",
                "-------------------------------------------->",
                "| Logger for " + AppName + " started on " + DateTime.Now.ToString(),
                "| Now logging on file, standard filestream output",
                "| File logging type has been set to : " + Type.ToString(),
                "---------------------------------------"
            };
        }

        /// <summary>
        /// Get a file name composed of the instant's date and time
        /// </summary>
        public static string GetNowFile() => GetNowName() + ".log";

        /// <summary>
        /// Get a name composed of the instant's date and time
        /// </summary>
        public static string GetNowName()
        {
            DateTime dt = DateTime.Now;
            return dt.Year + "-" + dt.Month + "-" + dt.Day + "_" + dt.Hour + "-" + dt.Minute + "-" + dt.Second;
        }

        /// <summary>
        /// Get a name composed of the instant's date and time
        /// </summary>
        public static string GetNowFolder() => GetNowName() + "/";
    }

    /// <summary>
    /// Type of file logging
    /// </summary>
    public enum FLgType
    {
        /// <summary>
        /// Overwrites directly when logging. Useless.
        /// </summary>
        OVERWRITE_ONREC,
        /// <summary>
        /// Overwrites the file with logged lines when application ends/logger is done
        /// </summary>
        OVERWRITE_DTOR,
        /// <summary>
        /// Appends to the file directly when logging.
        /// </summary>
        APPEND_ONREC,
        /// <summary>
        /// Appends to the file with logged lines when application ends/logger is done
        /// </summary>
        APPEND_DTOR,
        /// <summary>
        /// Nothing is automatically written, but everything is added to the lines. For manual use.
        /// <see cref="FileLogger.Append(bool)"/>
        /// <see cref="FileLogger.Append(string, bool)"/>
        /// <see cref="FileLogger.Overwrite(bool)"/>
        /// <see cref="FileLogger.Overwrite(string, bool)"/>
        /// </summary>
        NONE
    }
}
