using Honjo.Framework.Crypt;
using Honjo.Framework.Crypt.Hashing;
using Honjo.Framework.Logging;
using Honjo.Framework.Network;
using Honjo.Framework.Network.Packets;
using Honjo.Framework.Network.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Honjo.Framework.Server
{
    static class PacketOperations
    {
        public const byte SHUTDOWN_SPEC = 0, REQUEST_CLIENTS_SPEC = 1, LOG_TO_SERVER_CONSOLE_SPEC = 2, DISCONNECT_CLIENT_SPEC = 3, FORWARD_PACKET_SPEC = 4,
            REQUEST_CLIENT_PROPERTY_SPEC = 5, REQUEST_CLIENT_SYNC_PROP_SPEC = 6, REQUEST_CLIENT_LOCAL_PROP_SPEC = 7, REQUEST_ALL_SYNC_PROPS_SPEC = 8,
            EXEC_COMMAND_SPEC = 9, REQUEST_LOG_LIST_SPEC = 10, REQUEST_LOG_CONTENTS_SPEC = 11, RESTART_SPEC = 12, REQUEST_ALL_LISTENERS_SPEC = 13, REQUEST_CLIENTS_ON_EXTENSION = 14,
            FORCE_CLIENT_PROPERTY_SYNC = 15, DISCONNECT_CLIENTS_ON_EXTENSION_SPEC = 16, UNLOAD_EXTENSION_SPEC = 17, UPDATE_EXTENSION_SPEC = 18, RELOAD_EXTENSION_SPEC = 19,
            DELETE_EXTENSION_SPEC = 20, ADD_EXTENSION_SPEC = 21, CLIENT_LOGS_DATES_SPEC = 22, CLIENT_LOGS_ON_DATE_SPEC = 23, CLIENT_LOG_CONTENT_SPEC = 24;

        public static bool LOG_ALL_PACKETS = false;

        #region Packet processing (delegates)
        private static void _Ping(Client requester, Packet packet)
        {
            ServerMain.CLog(LogLevel.DEBUG, "Ping received, responding", requester);
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, packet.Contents)
            {
                Designation = "Ping response ; ACK"
            });
        }

        private static void _RequestClientLogsContents(Client requester, Packet packet)
        {
            if (!packet.OfType(out string date, out int clientId))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client log content requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client log content requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (!File.Exists(ServerData.PACKET_LOG_DIRECTORY + date + "/" + clientId + ".clog"))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client log contents on date " + date + " and id " + clientId + "requested, but no data matches", requester);
                RespondNoMatch(requester, packet);
                return;
            }

            ServerMain.CLog(LogLevel.DEBUG, $"Dispatching client log contents;ID={clientId},DATESTR={date}", requester);
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, date, packet.Get(1), File.ReadAllBytes(ServerData.PACKET_LOG_DIRECTORY + date + "/" + clientId + ".clog"))
            {
                Designation = "Server dispatch log contents"
            });
        }
        private static void _RequestClientLogOnDate(Client requester, Packet packet)
        {
            if (!packet.OfType(out string date))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client logs on date requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client logs on date requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (!Directory.Exists(ServerData.PACKET_LOG_DIRECTORY + date + "/"))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client logs on date " + date + " requested, but no date matches", requester);
                RespondNoMatch(requester, packet);
                return;
            }

            string[] ss = Directory.GetFiles(ServerData.PACKET_LOG_DIRECTORY + date + "/");
            string[] ssout = new string[ss.Length + 1];
            ssout[0] = date;
            for (int i = 0; i < ss.Length; i++)
                ss[i] = Path.GetFileNameWithoutExtension(ss[i]);
            ss.CopyTo(ssout, 1);

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching available client logs;DATESTR=" + date, requester);
            requester.Send(new StringPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, ssout)
            {
                Designation = "Server dispatching list of client id logs for given date"
            });
        }

        private static void _RequestClientLogsDates(Client requester, Packet packet)
        {
            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client logs dates list requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            //using linq query to avoid having to use toList or to iterate several times
            var list = from dir in Directory.GetDirectories(ServerData.PACKET_LOG_DIRECTORY)
                       select new DirectoryInfo(dir).Name;

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching all available date logs", requester);
            requester.Send(new StringPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, list.ToArray())
            {
                Designation = "Server dispatching client logs dates"
            });
        }

        private static void _AddExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extNPath, out byte[] contents))
            {
                ServerMain.CLog(LogLevel.WARNING, "Extension addition requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Addition of " + extNPath + " requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (extNPath.Equals("Server") || extNPath.Equals("HonjoServer") || ServerExtensionInitializer.EXTENSIONS_PATH.ContainsKey(extNPath))
            {
                ServerMain.CLog(LogLevel.ERROR, "Addition of " + extNPath + " requested, but this extension already exists !", requester);
                requester.Send(new Packet(Packet.NO_REQUEST_MATCH_HEADER, packet.Id, packet.Specifier, extNPath)
                {
                    Designation = "Extension already exists"
                });
                return;
            }

            try
            {
                ServerMain.CLog(LogLevel.INFO, "Distant extension adding", requester);
                File.WriteAllBytes(extNPath, contents);
                ServerExtensionInitializer.LoadAssembly(extNPath, Listener.clientSyncInit, ServerMain.logging);
                requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, extNPath)
                {
                    Designation = "ACK ; Extension added"
                });
            }
            catch (Exception)
            {
                ServerMain.CLog(LogLevel.WARNING, "Distant extension adding failed due to server exception", requester);
                requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, extNPath)
                {
                    Designation = "An error occured (exception thrown)"
                });
            }
        }

        private static void _DeleteExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Extension deleting requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Deleting of " + extName + " requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (extName.Equals("Server") || extName.Equals("HonjoServer") || !ServerExtensionInitializer.EXTENSIONS_PATH.ContainsKey(extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Deleting of " + extName + " requested, but no path matches", requester);
                RespondNoMatch(requester, packet, "Extension not referenced on server");
                return;
            }

            string path = ServerExtensionInitializer.EXTENSIONS_PATH[extName];
            ServerMain.CLog(LogLevel.INFO, "Deleting extension " + extName, requester);
            Listener.Unload(extName);
            File.Delete(path);

            RespondACK(requester, packet, "ACK ; Extension deleted");
        }

        private static void _ReloadExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Extension reloading requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Reloading of " + extName + " requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }


            if (typeof(PacketOperations).Assembly.GetName().Name.Equals(extName) || !ServerExtensionInitializer.EXTENSIONS_PATH.ContainsKey(extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Reloading of " + extName + " requested, but no path matches", requester);
                RespondNoMatch(requester, packet, "Extension not referenced on the server");
                return;
            }

            string path = ServerExtensionInitializer.EXTENSIONS_PATH[extName];
            ServerMain.CLog(LogLevel.INFO, "Reloading extension " + extName, requester);
            Listener.Unload(extName);
            ServerExtensionInitializer.LoadAssembly(path, Listener.clientSyncInit, ServerMain.logging);
            RespondACK(requester, packet, "ACK ; Extension reloaded");
        }

        private static void _UpdateExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName, out byte[] newContents))
            {
                ServerMain.CLog(LogLevel.WARNING, "Updating of extension requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Updating of " + extName + " requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (extName.Equals("Server") || extName.Equals("HonjoServer") || !ServerExtensionInitializer.EXTENSIONS_PATH.ContainsKey(extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Updating of " + extName + " requested, but no path matches", requester);
                requester.Send(new Packet(Packet.NO_REQUEST_MATCH_HEADER, packet.Id, packet.Specifier, extName)
                {
                    Designation = "Extension not referenced on the server (add instead ?)"
                });
                return;
            }
            string path = ServerExtensionInitializer.EXTENSIONS_PATH[extName];
            Listener.Unload(extName);
            ServerMain.CLog(LogLevel.INFO, "Updating extension " + extName, requester);
            try
            {
                File.Delete(path);
                File.WriteAllBytes(path, newContents);
                ServerExtensionInitializer.LoadAssembly(path, Listener.clientSyncInit, ServerMain.logging);
                requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, extName)
                {
                    Designation = "ACK ; extension updated"
                });
            }
            catch (Exception)
            {
                ServerMain.CLog(LogLevel.WARNING, "Extension update failed due to internal exception", requester);
                requester.Send(new Packet(Packet.ERROR_HEADER, packet.Id, packet.Specifier, extName)
                {
                    Designation = "An error occured (exception thrown)"
                });
            }
        }

        private static void _UnloadExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Unloading of extension requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Unloading of extension " + packet.Get(0) as string + " without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (extName.Equals("Server") || extName.Equals("HonjoServer") || !ServerExtensionInitializer.EXTENSIONS_PATH.ContainsKey(extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Unloading of " + extName + " requested, but no path matches", requester);
                RespondNoMatch(requester, packet, "No match for unloading target");
                return;
            }

            ServerMain.CLog(LogLevel.NORMAL, "Extension " + extName + " unloading", requester);
            Listener.Unload(extName);
            RespondACK(requester, packet, "ACK ; Extension unloaded");
        }

        private static void _DisconnectClientsOnExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Disconnection of clients on extension requested, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Disconnection of all clients on " + extName + " requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            ServerMain.CLog(LogLevel.NORMAL, "Disconnecting all clients on extension " + extName, requester);
            requester.Send(new IntPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, Listener.DisconnectExtension(extName).ToArray())
            {
                Designation = "ACK ; all clients on extension disconnected"
            });
        }

        private static void _ForcePropSync(Client requester, Packet packet)
        {
            if (!packet.OfType(out int clientId))
            {
                RespondMalformed(requester, packet);
                ServerMain.CLog(LogLevel.WARNING, "Force synchronization on another client requested as invalid request", requester);
                return;
            }
            if (!requester.IsAdmin)
            {
                RespondWAuth(requester, packet);
                ServerMain.CLog(LogLevel.ERROR, "Force synchronization on another client requested without admin rights", requester);
                return;
            }

            Client c = Client.Get(clientId);
            if (c == null)
            {
                ServerMain.CLog(LogLevel.NORMAL, "Client id " + clientId + " not found to force propsync", requester);
                RespondNoMatch(requester, packet, "Client not found with given id");
                return;
            }
            ServerMain.CLog(LogLevel.DEBUG, "Properties of client id " + clientId + " forcefully syncing", requester);
            c.Properties.Synchronize();
            RespondACK(requester, packet, "ACK ; client synchronized");
        }

        private static void _ReqClientsOnExtension(Client requester, Packet packet)
        {
            if (!packet.OfType(out string extName))
            {
                RespondMalformed(requester, packet);
                ServerMain.CLog(LogLevel.WARNING, "Clients on extension requested, but the request was invalid", requester);
                return;
            }

            if (!requester.IsAdmin)
            {
                RespondWAuth(requester, packet);
                ServerMain.CLog(LogLevel.ERROR, "Client on extension requested without admin rights", requester);
                return;
            }

            //somewhat expensive, in fact
            List<int> clients = new List<int>();
            lock (Client._CLIENTS_LOCK)
            {
                foreach (var client in Client.Clients_collection)
                {
                    if (!Listener.clientSyncInit.ContainsKey(client.LocalEndPoint))
                        continue;

                    if (Listener.clientSyncInit[client.LocalEndPoint].ToString().Equals(extName))
                        clients.Add(client.Id);
                }
            }

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching all clients on extension " + extName, requester);
            //keeping it as one argument to avoid having to loop to get the count
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, packet.Get(0) as string, clients.ToArray())
            {
                Designation = "Dispatching a list of all clients on extension"
            });
        }

        private static void _ReqListeners(Client requester, Packet packet)
        {
            if (!requester.IsAdmin)
            {
                RespondWAuth(requester, packet);
                ServerMain.CLog(LogLevel.ERROR, "A list of all listeners has been requested without admin rights", requester);
                return;
            }

            var dout = new Dictionary<int, string>();
            foreach (var entry in Listener.clientSyncInit)
                dout.Add(entry.Key.Port, entry.Value.ToString());
            int i = 0;
            foreach (var item in ServerExtensionInitializer.LOADED_ASSEMBLYS)
                dout.Add(--i, item.Key);

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching all listeners and relative extension", requester);
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, dout)
            {
                Designation = "Dispatching all listeners and relative extension informations"
            });
        }

        private static void _Restart(Client requester, Packet packet)
        {
            if (!packet.OfType(out CryptString confirmation))
            {
                ServerMain.CLog(LogLevel.ERROR, "Server restart requested, but request was invalid", requester);
                RespondMalformed(requester, packet, "Missing confirmation password");
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.SEVERE, "Server restart requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (!Bcrypt.Verify(requester.Decapsulate(confirmation), ServerData.CONFIRM_HASH_PWD))
            {
                ServerMain.CLog(LogLevel.SEVERE, "Server restart requested with admin rights, but confirmation password invalid", requester);
                RespondWAuth(requester, packet, "WAUTH confirmation invalid");
                return;
            }

            ServerMain.CLog(LogLevel.WARNING, "Server restarting due to external request...", requester);
            try
            {
                Process.Start("restart.bat");
                RespondACK(requester, packet, "ACK, server restarting", false);
            }
            catch (Exception)
            {
                requester.Send(new Packet(Packet.ERROR_HEADER, packet.Id, packet.Specifier));
                ServerMain.CLog(LogLevel.ERROR, "Restart script not found, aborting", requester);
                return;
            }
            ServerMain.Stop();
        }

        private static void _LogList(Client requester, Packet packet)
        {
            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client requested log list without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            List<string> logList = new List<string>();
            foreach (var file in Directory.GetFiles(ServerData.LOG_DIRECTORY))
            {
                if (!file.EndsWith(".log"))
                    continue;
                logList.Add(Path.GetFileName(file));
            }

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching a list of all server logs", requester);
            requester.Send(new StringPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, logList.ToArray())
            {
                Designation = "Dispatching log list"
            });
        }

        private static void _LogContents(Client requester, Packet packet)
        {
            if (!packet.OfType(out string logFileDate))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client requested log contents, but request was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client request log contents without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            try
            {
                ServerMain.CLog(LogLevel.DEBUG, "Dispatching server log contents;DATESTR=" + logFileDate, requester);
                requester.Send(new StringPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, File.ReadAllLines(ServerData.LOG_DIRECTORY + "/" + logFileDate)));
            }
            catch (Exception)
            {
                ServerMain.CLog(LogLevel.WARNING, "Couldn't dispatch server log contents due to internal exception", requester);
                requester.Send(new Packet(Packet.ERROR_HEADER, packet.Id, packet.Specifier));
            }
        }

        private static void _ExecCommand(Client requester, Packet packet)
        {
            if (!(packet.Get(0) is string))
            {
                RespondMalformed(requester, packet, "Packet malformed", true);
                return;
            }
            DistantCommands.Exec(requester, packet);
        }

        private static void _RequestAllSyncProperties(Client requester, Packet packet)
        {
            if (!packet.OfType(out int clientId))
            {
                ServerMain.CLog(LogLevel.WARNING, "All client sync property requested, but request packet was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Al client sync property requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            Client target = Client.Get(clientId);
            if (target == null)
            {
                ServerMain.CLog(LogLevel.WARNING, "All client sync property requested, but no client matches given id", requester);
                RespondNoMatch(requester, packet, "No client matching");
                return;
            }

            var values = new Dictionary<string, string[]>();

            foreach (var entry in target.Properties.Handle)
            {
                values[entry.Key] = new string[2];
                values[entry.Key][0] = entry.Value.Value == null ? "NULL" : entry.Value.Value.ToString();
                values[entry.Key][1] = entry.Value.OnConflict.ToString();
            }

            ServerMain.CLog(LogLevel.DEBUG, "Dispatching all sync properties of client id " + clientId, requester);
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, target.Id, values)
            {
                Designation = "Server dispatch all sync properties for client"
            });
        }

        private static void _RequestClientSyncProperty(Client requester, Packet packet)
        {
            if (!packet.OfType(out int clientId, out string propName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client sync property requested, but request packet was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client sync property requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            Client target = Client.Get(clientId);
            if (target == null)
            {
                ServerMain.CLog(LogLevel.WARNING, "Client sync property requested, but no client matches given id", requester);
                RespondNoMatch(requester, packet, "No client matching");
                return;
            }

            if (!target.Properties.Has(propName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client sync property requested, but target client does not have the requested property", requester);
                RespondNoMatch(requester, packet, "No matching property on client");
                return;
            }

            Property prop = target.Properties.Get(propName);
            ServerMain.CLog(LogLevel.DEBUG, $"Dispatching sync property informations;ID={clientId},PROP={propName}", requester);
            if (packet.ContentLength >= 3 && packet.Get(2) is bool stringRep && stringRep)
                requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, target.Id, propName, prop.Value.ToString(), prop.OnConflict.ToString())
                {
                    Designation = "Server dispatch " + propName + " sync property for client ID " + target.Id.ToString()
                });
            else
                requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, target.Id, propName, prop)
                {
                    Designation = "Server dispatch " + propName + " sync property for client ID " + target.Id.ToString()
                });
        }

        private static void _RequestClientProperty(Client requester, Packet packet)
        {
            if (!packet.OfType(out int clientId, out string propName))
            {
                ServerMain.CLog(LogLevel.WARNING, "Client info requested, but request packet was invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client info requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            Client target = Client.Get(clientId);
            if (target == null)
            {
                ServerMain.CLog(LogLevel.WARNING, "Client info requested, but no client matches given id", requester);
                RespondNoMatch(requester, packet, "No client matching");
                return;
            }

            PropertyInfo pinfo;
            try
            {
                pinfo = target.GetType().GetProperty(propName);
            }
            catch (AmbiguousMatchException)
            {
                ServerMain.CLog(LogLevel.WARNING, "Client info requested with ambiguous property name", requester);
                requester.Send(new Packet(Packet.AMBIGUITY_HEADER, packet.Id, packet.Specifier, clientId, propName)
                {
                    Designation = "Ambiguous property name, multiple matches"
                });
                return;
            }

            if (pinfo == null)
                return;
            ServerMain.CLog(LogLevel.DEBUG, $"Dispatching reflect property;ID={clientId},PROP={propName}", requester);
            //1st arg : property requested
            //2nd : client target id
            //3rd : property, as requested as raw form or to string
            //should i send it always native ?
            requester.Send(new Packet(Packet.ACK_HEADER, packet.Id, packet.Specifier, propName, clientId,
                (packet.ContentLength > 2 && packet.Get(2) is bool stringRep && stringRep) ? pinfo.GetValue(target).ToString() : pinfo.GetValue(target))
            {
                Designation = "Server dispatch " + propName + " property for client ID " + clientId.ToString()
            }, requester.DefaultSymEncryption, Serialization.NATIVE, requester.DefaultCompression);
        }

        private static void _Shutdown(Client requester, Packet packet)
        {
            if (!packet.OfType(out CryptString confirmation))
            {
                ServerMain.CLog(LogLevel.ERROR, "Server shutdown requested, but request was invalid", requester);
                RespondMalformed(requester, packet, "Missing confirmation password");
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.SEVERE, "Server shutdown requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            if (!Bcrypt.Verify(requester.Decapsulate(confirmation), ServerData.CONFIRM_HASH_PWD))
            {
                ServerMain.CLog(LogLevel.SEVERE, "Server shutdown requested with admin rights, but confirmation password invalid", requester);
                RespondWAuth(requester, packet, "Confirmation password invalid");
                return;
            }

            ServerMain.CLog(LogLevel.WARNING, "Server shutdowning due to external request...", requester);
            RespondACK(requester, packet, "ACK ; server shutdowning");
            ServerMain.Stop();
        }

        private static void _RequestClientList(Client requester, Packet packet)
        {
            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Client list requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }
            ServerMain.CLog(LogLevel.DEBUG, "Dispatching client list", requester);

            requester.Send(new IntPacket(Packet.ACK_HEADER, packet.Id, packet.Specifier, Client.Clients_id)
            {
                Designation = "Server dispatch client list"
            });
        }

        private static void _LogServerConsole(Client requester, Packet packet)
        {
            if (!packet.OfType(out int logLevel, out string message))
            {
                ServerMain.CLog(LogLevel.WARNING, "Distant logging requested, but the request is invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }
            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Distant logging requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            ServerMain.logging.Log("[DISTANT] " + message, (LogLevel)logLevel);
            RespondACK(requester, packet);
        }

        private static void _DisconnectClient(Client requester, Packet packet)
        {
            if (!packet.OfType(out int targetId))
            {
                ServerMain.CLog(LogLevel.WARNING, "Disconnection requested, but the request is invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            Client c = Client.Get(targetId);
            if (c == null)
            {
                ServerMain.CLog(LogLevel.WARNING, "Disconnection requested, but no client matches id " + (int)packet.Get(0), requester);
                RespondNoMatch(requester, packet, "No client matching");
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Disconnection requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            ServerMain.CLog(LogLevel.DEBUG, "Disconnecting client id " + c.Id, requester);
            c.Disconnect("Disconnected by admin");
            //if not the requester disconnected himself...
            if (requester.Id != c.Id)
                RespondACK(requester, packet, "ACK ; client disconnected");
        }

        private static void _SendPacketToClient(Client requester, Packet packet)
        {
            if (!packet.OfType(out int targetId, out Packet toForward))
            {
                ServerMain.CLog(LogLevel.WARNING, "Packet forwarding requested, but the request is invalid", requester);
                RespondMalformed(requester, packet);
                return;
            }

            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "Packet forwarding requested without admin rights", requester);
                RespondWAuth(requester, packet);
                return;
            }

            Client c = Client.Get(targetId);
            if (c == null)
            {
                ServerMain.CLog(LogLevel.WARNING, "Packet forwarding requested, but no client matches id " + targetId, requester);
                RespondNoMatch(requester, packet, "No matching client");
                return;
            }

            ServerMain.CLog(LogLevel.DEBUG, "Forwarding packet to client id " + c.Id, requester);
            c.Send(toForward);
            RespondACK(requester, packet, "ACK ; packet forward successful");
        }
        #endregion

        public static void Setup()
        {
            if (ReceptionProc.ALL_PACKETS_RECEPTION == default(PProcess))
                ReceptionProc.ALL_PACKETS_RECEPTION = _LogPacket;
            else
                ReceptionProc.ALL_PACKETS_RECEPTION += _LogPacket;

            //admin
            ReceptionProc.Put(Packet.ADMIN_ID, SHUTDOWN_SPEC, _Shutdown);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_CLIENTS_SPEC, _RequestClientList);
            ReceptionProc.Put(Packet.ADMIN_ID, LOG_TO_SERVER_CONSOLE_SPEC, _LogServerConsole);
            ReceptionProc.Put(Packet.ADMIN_ID, DISCONNECT_CLIENT_SPEC, _DisconnectClient);
            ReceptionProc.Put(Packet.ADMIN_ID, FORWARD_PACKET_SPEC, _SendPacketToClient);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_CLIENT_PROPERTY_SPEC, _RequestClientProperty);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_CLIENT_SYNC_PROP_SPEC, _RequestClientSyncProperty);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_ALL_SYNC_PROPS_SPEC, _RequestAllSyncProperties);
            ReceptionProc.Put(Packet.ADMIN_ID, EXEC_COMMAND_SPEC, _ExecCommand);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_LOG_LIST_SPEC, _LogList);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_LOG_CONTENTS_SPEC, _LogContents);
            ReceptionProc.Put(Packet.ADMIN_ID, RESTART_SPEC, _Restart);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_ALL_LISTENERS_SPEC, _ReqListeners);
            ReceptionProc.Put(Packet.ADMIN_ID, REQUEST_CLIENTS_ON_EXTENSION, _ReqClientsOnExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, FORCE_CLIENT_PROPERTY_SYNC, _ForcePropSync);
            ReceptionProc.Put(Packet.ADMIN_ID, DISCONNECT_CLIENTS_ON_EXTENSION_SPEC, _DisconnectClientsOnExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, UNLOAD_EXTENSION_SPEC, _UnloadExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, UPDATE_EXTENSION_SPEC, _UpdateExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, RELOAD_EXTENSION_SPEC, _ReloadExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, DELETE_EXTENSION_SPEC, _DeleteExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, ADD_EXTENSION_SPEC, _AddExtension);
            ReceptionProc.Put(Packet.ADMIN_ID, CLIENT_LOGS_DATES_SPEC, _RequestClientLogsDates);
            ReceptionProc.Put(Packet.ADMIN_ID, CLIENT_LOGS_ON_DATE_SPEC, _RequestClientLogOnDate);
            ReceptionProc.Put(Packet.ADMIN_ID, CLIENT_LOG_CONTENT_SPEC, _RequestClientLogsContents);

            //sys
            ReceptionProc.Put(Packet.SYS_ID, Packet.SYS_DEFAULT_PING_SPEC, _Ping);

            //put custom protobuf wrapper types
            //
            //listeners information
            ContentWrapperBase.AddCustomWrapper(typeof(Dictionary<int, string>), (object obj) => new ContentWrapper<Dictionary<int, string>>((Dictionary<int, string>)obj));
        }

        private static void _LogPacket(Client client, Packet packet)
        {
            if (!LOG_ALL_PACKETS)
                return;

            ServerMain.logging.Debug("Packet received. Client : " + client.ToString() + "\n           " + packet.ToString());
        }

        #region Quick handy methods to respond to requests
        public static void RespondHeader(Client client, Packet packet, sbyte header, bool keepContents, string designation)
        {
            Packet p = new Packet(header, packet.Id, packet.Specifier)
            {
                Designation = designation
            };
            if (keepContents)
                p.SetContentsArray(packet.Contents);
            client.Send(p);
        }

        public static void RespondMalformed(Client client, Packet packet, string designation = "Packet malformed", bool keepContents = false)
        {
            RespondHeader(client, packet, Packet.PACKET_MALFORMED_HEADER, keepContents, designation);
        }

        public static void RespondError(Client client, Packet packet, string designation = "Unspecified error", bool keepContents = true)
        {
            RespondHeader(client, packet, Packet.ERROR_HEADER, keepContents, designation);
        }

        public static void RespondWAuth(Client client, Packet packet, string designation = "WAUTH response (denied)", bool keepContents = false)
        {
            RespondHeader(client, packet, Packet.AUTH_DENIED_HEADER, keepContents, designation);
        }

        public static void RespondACK(Client client, Packet packet, string designation = "ACK", bool keepContents = true)
        {
            RespondHeader(client, packet, Packet.ACK_HEADER, keepContents, designation);
        }

        public static void RespondNoMatch(Client client, Packet packet, string designation = "No match for target", bool keepContents = true)
        {
            RespondHeader(client, packet, Packet.NO_REQUEST_MATCH_HEADER, keepContents, designation);
        }
        #endregion
    }

    delegate void Command(Client requester, Packet command);
    /// <summary>
    /// Handle distant commands (terminal)
    /// </summary>
    static class DistantCommands
    {
        public static readonly Dictionary<string, CommandWrapper> commands = new Dictionary<string, CommandWrapper>()
        {
            //{"shutdown", new CommandWrapper("Eteint le serveur ; requiert un mot de passe de confirmation", __ShutdownCommand)},
            //{"echo", new CommandWrapper("Affiche un message sur le serveur. Syntaxe : echo <level> <message>", __EchoCommand)},
            //{"count", new CommandWrapper("Affiche le nombre de clients connectés", __CountCommand)},
            //{"restart", new CommandWrapper("Redémarre le serveur ; requiert un mot de passe de confirmation", __RestartCommand)},
            //{"reboot", new CommandWrapper("Alias pour restart", __RestartCommand)},
            //{"list", new CommandWrapper("Affiche la liste des clients connectés sur le serveur", __ListCommand)},
            //{"sprop", new CommandWrapper("Recupère une propriété synchro. Syntaxe : sprop <id client> <prop 1> <prop 2> <...>", __SyncPropCommand)},
            //{"syncprop", new CommandWrapper("Alias pour sprop", __SyncPropCommand)},
            //{"prop", new CommandWrapper("Recupère une propriété de classe. Syntaxe : prop <id client> <prop 1> <prop 2> <...>", __ReflectPropCommand)},
            //{"fsync", new CommandWrapper("Force la synchronisation d'un client. Syntaxe : fsync <id client>", __SyncCommand)},
            //{"help", new CommandWrapper("Affiche l'aide et la liste des commandes traitées par le serveur", __HelpCommand)},
            //{"teso", new CommandWrapper("Try it", __SkyrimEasterEgg)}
            {"shutdown", new CommandWrapper("Shuts the server down ; requires password confirmation", __ShutdownCommand)},
            {"echo", new CommandWrapper("Shows a message on the server. Syntax : echo <level> <message>", __EchoCommand)},
            {"count", new CommandWrapper("Prints the number of clients connected", __CountCommand)},
            {"restart", new CommandWrapper("Restarts the server ; requires password confirmation", __RestartCommand)},
            {"reboot", new CommandWrapper("Alias for restart", __RestartCommand)},
            {"list", new CommandWrapper("Prints the list of clients connected on the server", __ListCommand)},
            {"sprop", new CommandWrapper("Retrives a client sync property. Syntax : sprop <client id> <prop 1> <prop 2> <...>", __SyncPropCommand)},
            {"syncprop", new CommandWrapper("Alias for sprop", __SyncPropCommand)},
            {"prop", new CommandWrapper("Retrieves a client class property. Syntax : prop <client id> <prop 1> <prop 2> <...>", __ReflectPropCommand)},
            {"fsync", new CommandWrapper("Forces a client to synchronize. Syntax : fsync <client id>", __SyncCommand)},
            {"help", new CommandWrapper("Prints the help message for all server commands", __HelpCommand)},
            {"teso", new CommandWrapper("Try it", __SkyrimEasterEgg)}
        };

        #region Command processors (delegates)
        private static void __SkyrimEasterEgg(Client requester, Packet command)
        {
            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, "DOVAHKIN !!!")
            {
                Designation = "HE IS THE DRAGONBORN !"
            });
        }

        private static void __SyncCommand(Client requester, Packet command)
        {
            if (!command.OfType(out string _, out string clientId))
            {
                PacketOperations.RespondMalformed(requester, command);
                return;
            }

            if (!requester.IsAdmin)
            {
                PacketOperations.RespondWAuth(requester, command);
                return;
            }

            if (!int.TryParse(clientId, out int cid))
            {
                PacketOperations.RespondMalformed(requester, command, "Client id could not be resolved");
                return;
            }

            Client c = Client.Get(cid);
            if (c == null)
            {
                PacketOperations.RespondNoMatch(requester, command, "No client matching");
                return;
            }
            c.Properties.Synchronize();
            PacketOperations.RespondACK(requester, command, "ACK ; client synchronized");
        }

        private static void __ShutdownCommand(Client requester, Packet command)
        {
            switch (command.Header)
            {
                case Packet.OK_HEADER:
                    if (!requester.IsAdmin)
                    {
                        PacketOperations.RespondWAuth(requester, command);
                        return;
                    }
                    requester.Send(new Packet(Packet.CONFIRMATION_AWAIT_HEADER, command.Id, command.Specifier, "Entrez le mot de passe de confirmation: ", true)
                    {
                        Designation = "Shutdown command ; await confirmation signal"
                    });
                    break;
                case Packet.CONFIRMATION_HEADER:
                    if (!command.OfType(out string _, out CryptString confirmation))
                    {
                        PacketOperations.RespondMalformed(requester, command);
                        ServerMain.CLog(LogLevel.ERROR, "Shutdown command received, but request was invalid", requester);
                        break;
                    }
                    if (!requester.IsAdmin)
                    {
                        PacketOperations.RespondWAuth(requester, command);
                        ServerMain.CLog(LogLevel.SEVERE, "Suspicious client sent shutdown command without admin rights", requester);
                        return;
                    }
                    if (!Bcrypt.Verify(requester.Decapsulate(confirmation), ServerData.CONFIRM_HASH_PWD))
                    {
                        ServerMain.CLog(LogLevel.SEVERE, "Suspicious shutdown command received and confirmed with wrong confirmation password", requester);
                        PacketOperations.RespondWAuth(requester, command);
                        break;
                    }
                    ServerMain.CLog(LogLevel.WARNING, "Server shutdowning due to external request...", requester);
                    PacketOperations.RespondACK(requester, command, "ACK ; server shutdowning", true);
                    ServerMain.Stop();
                    break;
            }
        }

        private static void __EchoCommand(Client requester, Packet command)
        {
            if (!command.OfType(out string _, out string logLevel, out string _))
            {
                PacketOperations.RespondMalformed(requester, command);
                return;
            }
            if (!requester.IsAdmin)
            {
                PacketOperations.RespondWAuth(requester, command);
                return;
            }

            string s = "";
            for (int i = 2; i < command.ContentLength; i++)
            {
                s += command.Get(i) + " ";
            }

            try
            {
                ServerMain.CLog((LogLevel)Enum.Parse(typeof(LogLevel), logLevel), "[DISTANT] " + s, requester);
            }
            catch (Exception e) when (e is ArgumentException || e is OverflowException)
            {
                PacketOperations.RespondMalformed(requester, command);
                return;
            }

            PacketOperations.RespondACK(requester, command);
        }

        private static void __CountCommand(Client requester, Packet command)
        {
            if (!requester.IsAdmin)
            {
                PacketOperations.RespondWAuth(requester, command);
                return;
            }

            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, Client.ClientsCount.ToString() + " client(s) connecté(s)"));
        }

        private static void __RestartCommand(Client requester, Packet command)
        {
            switch (command.Header)
            {
                case Packet.OK_HEADER:
                    if (!requester.IsAdmin)
                    {
                        PacketOperations.RespondWAuth(requester, command);
                        return;
                    }
                    requester.Send(new Packet(Packet.CONFIRMATION_AWAIT_HEADER, command.Id, command.Specifier, "Entrez le mot de passe de confirmation: ", true)
                    {
                        Designation = "Restart command ; await confirmation signal"
                    });
                    break;
                case Packet.CONFIRMATION_HEADER:
                    if (!command.OfType(out string _, out CryptString confirmation))
                    {
                        PacketOperations.RespondMalformed(requester, command);
                        ServerMain.CLog(LogLevel.ERROR, "Restart command received, but request was invalid", requester);
                        break;
                    }
                    if (!requester.IsAdmin)
                    {
                        PacketOperations.RespondWAuth(requester, command);
                        ServerMain.CLog(LogLevel.SEVERE, "Suspicious client sent restart command without admin rights", requester);
                        return;
                    }
                    if (!Bcrypt.Verify(requester.Decapsulate(confirmation), ServerData.CONFIRM_HASH_PWD))
                    {
                        ServerMain.CLog(LogLevel.SEVERE, "Suspicious restart command received and confirmed with wrong confirmation password", requester);
                        PacketOperations.RespondWAuth(requester, command);
                        break;
                    }

                    ServerMain.CLog(LogLevel.WARNING, "Server restarting due to external request...", requester);
                    try
                    {
                        Process.Start("restart.bat");
                        PacketOperations.RespondACK(requester, command, "ACK ; server restarting", false);
                    }
                    catch (Exception)
                    {
                        PacketOperations.RespondError(requester, command, "Restart script not found", false);
                        ServerMain.CLog(LogLevel.ERROR, "Restart script not found, aborting", requester);
                        return;
                    }
                    ServerMain.Stop();
                    break;
            }
        }

        private static void __ListCommand(Client requester, Packet command)
        {
            if (!requester.IsAdmin)
            {
                ServerMain.CLog(LogLevel.ERROR, "List command received, but requester does not have admin rights", requester);
                PacketOperations.RespondWAuth(requester, command);
                return;
            }

            string[] s = new string[Client.ClientsCount];
            int i = 0;
            lock (Client._CLIENTS_LOCK)
            {
                foreach (var client in Client.Clients_collection)
                    s[i++] = "Client " + client.Id + " -> " + client.ToString();
            }

            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, s)
            {
                Designation = "Dispatching a list of connected clients (command)"
            });
        }

        private static void __SyncPropCommand(Client requester, Packet command)
        {
            if (!command.OfType(out string _, out string clientId) || !int.TryParse(clientId, out int arg))
            {
                PacketOperations.RespondMalformed(requester, command);
                return;
            }
            Client client = Client.Get(arg);
            if (client == null)
            {
                requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, "Le client n'a pas été trouvé ; pas de correspondance pour l'ID"));
                return;
            }

            string[] sa;
            if (client.Properties.Handle.Count > 0)
            {
                sa = new string[client.Properties.Handle.Count];
                int i = 0;
                foreach (var entry in client.Properties.Handle)
                    sa[i++] = entry.Key + " -> " + entry.Value.ToString();
            }
            else
                sa = new string[] { "Pas de propriété pour le client " + client.Id };

            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, sa));
        }

        private static void __ReflectPropCommand(Client requester, Packet command)
        {
            if (!command.OfType(out string _, out string clientId, out string _) || !int.TryParse(clientId, out int arg))
            {
                PacketOperations.RespondMalformed(requester, command);
                return;
            }
            Client client = Client.Get(arg);
            if (client == null)
            {
                requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, "Le client n'a pas été trouvé ; pas de correspondance pour l'ID")
                {
                    Designation = "Client not found"
                });
                return;
            }
            string[] sa = new string[command.ContentLength - 2];
            for (int i = 0; i < sa.Length; i++)
            {
                if (!(command.Get(i + 2) is string))
                    continue;
                PropertyInfo pinfo = client.GetType().GetProperty(command.Get(i + 2) as string);
                if (pinfo == null)
                {
                    sa[i] = command.Get(i + 2) + " -> (non réferencé)";
                    continue;
                }
                sa[i] = command.Get(i + 2) + " -> " + pinfo.GetValue(client);
            }
            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, sa)
            {
                Designation = "Dispatching property of client"
            });
        }

        private static void __HelpCommand(Client requester, Packet command)
        {
            List<string> lines = new List<string>();
            foreach (var cmd in commands)
                lines.Add(cmd.Key + " -> " + cmd.Value.Label);
            requester.Send(new StringPacket(Packet.CONFIRMATION_HEADER, command.Id, command.Specifier, lines.ToArray())
            {
                Designation = "Dispatching help"
            });
        }
        #endregion

        public static void Exec(Client requester, Packet packetCmd)
        {
            string cmd = ((string)packetCmd.Get(0)).ToLower();
            if (!commands.ContainsKey(cmd))
            {
                ServerMain.CLog(LogLevel.DEBUG, "Unknown command: " + cmd, requester);
                PacketOperations.RespondNoMatch(requester, packetCmd, "Unknown command");
                return;
            }

            ServerMain.CLog(LogLevel.DEBUG, "Executing command: " + cmd, requester);
            commands[cmd].Command(requester, packetCmd);
        }
    }

    /// <summary>
    /// Wrapper for commands
    /// </summary>
    sealed class CommandWrapper
    {
        /// <summary>
        /// Label, short description of the command
        /// </summary>
        public string Label { get; private set; }
        /// <summary>
        /// Command delegate to call
        /// </summary>
        public Command Command { get; private set; }

        public CommandWrapper(string label, Command command)
        {
            Label = label;
            Command = command;
        }
    }
}
