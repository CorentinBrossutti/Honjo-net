using Honjo.Framework.Crypt.Hashing;
using Honjo.Framework.Logging;
using Honjo.Framework.Logging.Loggers;
using Honjo.Framework.Network;
using Honjo.Framework.Network.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace Honjo.Framework.Server
{
    static class ServerData
    {
        public const string LOG_DIRECTORY = "logs/", PWD_DIRECTORY = "pwd/", CONFIRMATION_HASH_FILE = PWD_DIRECTORY + "confirmation.hash", ADMIN_HASH_FILE = PWD_DIRECTORY + "admin.hash",
            PACKET_LOG_DIRECTORY = LOG_DIRECTORY + "packets/";
        public static readonly string CONFIRM_HASH_PWD = File.Exists(CONFIRMATION_HASH_FILE) ? File.ReadAllText(CONFIRMATION_HASH_FILE) : Bcrypt.Hash("aurevoir"),
                ADMIN_HASH_PWD = File.Exists(ADMIN_HASH_FILE) ? File.ReadAllText(ADMIN_HASH_FILE) : Bcrypt.Hash("salut");
        public static readonly string CUR_PACKET_LOG_DIRECTORY = PACKET_LOG_DIRECTORY + FileLogger.GetNowFolder();
        public static readonly Dictionary<string, string> CONFIG = new Dictionary<string, string>(); 
        private static BinaryFormatter logFormatter = new BinaryFormatter();

        public static string ServerAppName => CONFIG["app_name"];

        public static void LogDisconnectedClient(Client client, bool expected)
        {
            ClientLogInfo logInfo = new ClientLogInfo(client, expected);
            string file = CUR_PACKET_LOG_DIRECTORY + client.Id.ToString() + ".clog";
            try
            {
                using (FileStream fs = File.OpenWrite(file))
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    using (GZipStream gzs = new GZipStream(fs, CompressionLevel.Optimal))
                        logFormatter.Serialize(gzs, logInfo);
                }
            }
            catch (IOException) { }
        }

        public static void Init()
        {
            if(!File.Exists("config.cfg"))
            {
                //honjo placeholder server name by default
                CONFIG.Add("app_name", ServerJournal.DEFAULT_LOG_NAME);
                //CONFIG.Add("verbose_mode", VerboseMode.FULL.ToString());
                return;
            }

            foreach (var line in File.ReadAllLines("config.cfg"))
            {
                if (line.StartsWith(";") || String.IsNullOrEmpty(line) || String.IsNullOrWhiteSpace(line))
                    continue;
                string[] val = line.Split('=');
                CONFIG.Add(val[0], val[1]);
            }

            foreach (var cfgElem in CONFIG)
            {
                if (!cfgElem.Key.StartsWith("sref:"))
                    continue;

                string path = cfgElem.Key.Split(':')[1],
                    type = path.Split('-')[0],
                    ivar = path.Split('-')[1];
                Type t = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                         from innerType in assembly.GetTypes()
                         where innerType.FullName.Equals(type)
                         select innerType).First();
                if (t == null)
                {
                    //ServerMain.logging.Alert($"Could not find type {type} (config SREF)");
                    Console.WriteLine($"CONFIG SREF : Could not find type {type}");
                    continue;
                }
                PropertyInfo pi = t.GetProperty(ivar);
                if(pi == null)
                {
                    FieldInfo fi = t.GetField(ivar);
                    if (fi == null)
                    {
                        //ServerMain.logging.Alert($"Could not find static property or field {ivar} in type {type} (config SREF)");
                        Console.WriteLine($"CONFIG SREF : Could not find property or field {ivar} on type {type}");
                        continue;
                    }
                    try
                    {
                        fi.SetValue(null, Convert.ChangeType(cfgElem.Value, fi.FieldType));
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"Could not set static var {ivar} in type {type} : {e.GetType().ToString()}");
                    }
                    continue;
                }

                try
                {
                    pi.SetValue(null, Convert.ChangeType(cfgElem.Value, pi.PropertyType));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Could not set static prop {ivar} in type {type} : {e.GetType().ToString()}");
                }
            }
        }
    }

    sealed class ServerJournal : ConsoleLogger
    {
        //honjo placeholder server name
        //if no name in config
        public const string DEFAULT_LOG_NAME = "HONJO PHSN";

        private const byte SERVER_DISTANT_LOG_SPEC = 128;
        private static List<Tuple<LogLevel, string>> __curLines = new List<Tuple<LogLevel, string>>();
        private static readonly object __curLines_lock = new object(), __adminsToSend_lock = new object();
        private static List<Client> __adminsToSend = new List<Client>();

        public ServerJournal(string appName, bool logStart = true) : base(appName, logStart) => Mode = ServerData.CONFIG.ContainsKey("verbose_mode") ? 
            (VerboseMode)Enum.Parse(typeof(VerboseMode), ServerData.CONFIG["verbose_mode"]) : VerboseMode.FULL;

        protected override void _Print(LogLevel level, string message)
        {
            base._Print(level, message);

            lock (__curLines_lock)
                __curLines.Add(new Tuple<LogLevel, string>(level, message));
            lock (__adminsToSend_lock)
            {
                for (int i = 0; i < __adminsToSend.Count; i++)
                {
                    if (!__adminsToSend[i].Connected)
                    {
                        __adminsToSend.RemoveAt(i);
                        continue;
                    }
                    __adminsToSend[i].Send(new Packet(Packet.OK_HEADER, Packet.ADMIN_ID, SERVER_DISTANT_LOG_SPEC, level, message)
                    {
                        Designation = "Server dispatching logs to clients"
                    });
                }
            }
        }

        protected override List<string> _LogStartMsg()
        {
            return new List<string>()
            {
                "",
                "-------------------------------------------->",
                "Server logger (HONJO version) with " + (AppName == DEFAULT_LOG_NAME ? "default server name" : ("server name " + AppName)) + 
                    " started on " + DateTime.Now.ToString(),
                "Verbose mode set to " + Mode,
                "---------------------------------------",
                ""
            };
        }

        public static void AdminConnected(Client client)
        {
            client.OnDisconnection += AdminDisconnected;

            Thread.Sleep(250);
            lock (__curLines_lock)
            {
                //using a simple for loop. Basically, send can log data to the output which can in turn modify __curLines
                for (int i = 0; i < __curLines.Count; i++)
                {
                    var line = __curLines[i];
                    client.Send(new Packet(Packet.OK_HEADER, Packet.ADMIN_ID, SERVER_DISTANT_LOG_SPEC, line.Item1, line.Item2)
                    {
                        Designation = "Server dispatching opening logs"
                    });
                }
                    
            }
            lock (__adminsToSend_lock)
                __adminsToSend.Add(client);
        }

        public static void AdminDisconnected(Client client, bool expected)
        {
            lock (__adminsToSend_lock)
            {
                if (!__adminsToSend.Contains(client))
                    return;

                __adminsToSend.Remove(client);
            }
        }
    }
}
