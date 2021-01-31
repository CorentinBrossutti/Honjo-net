using Honjo.Framework.Logging;
using Honjo.Framework.Logging.Loggers;
using Honjo.Framework.Network;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Honjo.Framework.Server
{
    internal delegate bool CtrlEventHandler(CtrlTypes sig);

    public sealed class ServerMain
    {
        public static Logging.Logging logging;

        private static EventWaitHandle __hanger = new ManualResetEvent(false);
        private static int __exitCode = 0;
        private static bool __exited = false;
        //avoid GC collecting
        private static CtrlEventHandler __ctrl_eventHandler = __EXIT_HANDLER;

        public static void Main(string[] args)
        {
            ServerData.Init();
            logging = Logging.Logging.GetLogging(ServerData.ServerAppName, new ServerJournal(ServerData.ServerAppName),
            new FileLogger(ServerData.ServerAppName, ServerData.LOG_DIRECTORY + FileLogger.GetNowFile(), FLgType.OVERWRITE_DTOR));

            Console.Title = ServerData.ServerAppName + " - SERVER";
            Console.SetWindowSize((int)Math.Floor(Console.WindowWidth * 1.2), (int)Math.Floor(Console.WindowHeight * 1.2));

            if (!Directory.Exists(ServerData.LOG_DIRECTORY))
                Directory.CreateDirectory(ServerData.LOG_DIRECTORY);
            if (!Directory.Exists(ServerData.CUR_PACKET_LOG_DIRECTORY))
                Directory.CreateDirectory(ServerData.CUR_PACKET_LOG_DIRECTORY);

            logging.Eyecatch("Server starting...");

            Listener.Setup();
            PacketOperations.Setup();
            Client.SetAdminPwdHash(ServerData.ADMIN_HASH_PWD);
            SetConsoleCtrlHandler(__ctrl_eventHandler, true);

            //The current (main server) thread will hang until the signal is set using Stop
            __hanger.WaitOne();

            __INTERN_EXIT();
        }

        /// <summary>
        /// Stop the server
        /// </summary>
        public static void Stop() => __hanger.Set();

        /// <summary>
        /// Log information relative to a client on the main server logger
        /// </summary>
        /// <param name="level">Level to log</param>
        /// <param name="message">Message to log</param>
        /// <param name="client">Client whose the information is relative to</param>
        public static void CLog(LogLevel level, string message, Client client)
        {
            logging.Log("[CLIENT ID " + client.Id + "] " + message, level);
        }

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(CtrlEventHandler handler, bool add);

        private static bool __EXIT_HANDLER(CtrlTypes ctrlType)
        {
            __INTERN_EXIT();
            return true;
        }

        private static void __INTERN_EXIT()
        {
            if (__exited)
                return;
            __exited = true;

            logging.Eyecatch("Stopping...");
            //unloading all extensions
            foreach (var asm in ServerExtensionInitializer.EXTENSIONS.Keys)
            {
                if (asm == null)
                    continue;

                try
                {
                    foreach (var tstr in ServerExtensionInitializer.INTERACTION_CLASSES)
                    {
                        Type t = asm.GetType(tstr);
                        if (t == null)
                            continue;
                        MethodInfo minfo = t.GetMethod(ServerExtensionInitializer.UNLOAD_METHOD);
                        if (minfo == null)
                            continue;
                        minfo.Invoke(null, null);
                    }
                }
                catch (Exception) { }
            }

            //disconnecting all clients (also for logging)
            lock(Client._CLIENTS_LOCK)
            {
                foreach (var client in Client.Clients_collection)
                    client.Disconnect("Server is stopping");
            }

            logging.Eyecatch("Server has been successfully stopped");
            Environment.Exit(__exitCode);
        }
    }

    internal enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }
}