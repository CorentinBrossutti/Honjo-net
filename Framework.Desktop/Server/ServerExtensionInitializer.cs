using Honjo.Framework.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Honjo.Framework.Server
{
    /// <summary>
    /// Represents an external method (action/delegate) to process new clients on a server extension
    /// </summary>
    public class ServerExtensionInitializer : IDisposable
    {
        public const string LOAD_METHOD = "_EXTERN_ServerLoad", UNLOAD_METHOD = "_EXTERN_ServerUnload";
        /// <summary>
        /// Has no real technical value, merely for information
        /// </summary>
        public static readonly Dictionary<Assembly, List<ServerExtensionInitializer>> EXTENSIONS = new Dictionary<Assembly, List<ServerExtensionInitializer>>();
        public static readonly Dictionary<string, Assembly> LOADED_ASSEMBLYS = new Dictionary<string, Assembly>();
        public static readonly Dictionary<string, string> EXTENSIONS_PATH = new Dictionary<string, string>();
        public static readonly string[] INTERACTION_CLASSES = new string[] { "Honjo.Interaction.ServerInteraction", "Honjo.Interaction.ServerInteraction2",
            "Honjo.Interaction.ServerInteraction3"};

        //REFLECTION METHOD STRING : public static Dictionary<int, Action<Client>> _EXTERN_ServerLoad() |||| where int is the port and Action<Client> the client init method
        protected Action<Client> _clientInitialization;

        public Assembly Extension { get; private set; }
        public string FullPath { get; private set; }

        /// <summary>
        /// Constructs a new server extension. Do note it does not contain the port necessarily contain the port it is linked to
        /// </summary>
        /// <param name="assembly">The assembly (itself an extension) containing this server call extension</param>
        /// <param name="clientInit">The action to call when processing a new client</param>
        public ServerExtensionInitializer(Assembly assembly, Action<Client> clientInit, string fullPath = "./")
        {
            Extension = assembly;
            FullPath = fullPath;
            _clientInitialization = clientInit;

            string s = assembly.GetName().Name;

            if (!EXTENSIONS.ContainsKey(assembly))
                EXTENSIONS.Add(assembly, new List<ServerExtensionInitializer>());
            EXTENSIONS[assembly].Add(this);
        }

        /// <summary>
        /// Informs this extensions to process a new client
        /// </summary>
        /// <param name="client">Client to process</param>
        public void NewClient(Client client) => _clientInitialization(client);

        public override string ToString() => Extension.GetName().Name;

        #region Mainly for listeners
        /// <summary>
        /// Merely to be called when setting up listeners. Will link listeners and extensions.
        /// </summary>
        /// <param name="folderPath">The path of the folder containing extensions</param>
        /// <param name="output">The dictionary to put the link between ports and extensions</param>
        /// <param name="logOutput">Default : null. Logger to log information (ex : listener set up)</param>
        public static void FolderSetup_listeners(string folderPath, Dictionary<PEndPoint, ServerExtensionInitializer> output, Logging.Logging logOutput = null)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                if (!file.EndsWith(".dll") || !Path.GetFileName(file).StartsWith("Extension."))
                    continue;
                LoadAssembly(file, output, logOutput);
            }
        }

        /// <summary>
        /// Load a single extension. Has the same usage value as <see cref="FolderSetup_listeners(string, Dictionary{int, ServerExtensionInitializer}, Logging.Logging)"/>
        /// </summary>
        /// <param name="asmPath">Path to the assembly extension</param>
        /// <param name="output">The dictionary to put the link between ports and extensions</param>
        /// <param name="logOutput">Default : null. Logger to log information (ex : listener set up)</param>
        public static void LoadAssembly(string asmPath, Dictionary<PEndPoint, ServerExtensionInitializer> output, Logging.Logging logOutput = null)
        {
            var asm = Assembly.Load(File.ReadAllBytes(asmPath));
            foreach (var tstr in INTERACTION_CLASSES)
            {
                Type t = asm.GetType(tstr);
                if (t == null)
                    continue;
                MethodInfo minfo = t.GetMethod(LOAD_METHOD);
                if (minfo == null)
                    continue;

                var dic = (Dictionary<PEndPoint, Action<Client>>)minfo.Invoke(null, new object[] { logOutput });
                foreach (var entry in dic)
                {
                    if (output.ContainsKey(entry.Key))
                    {
                        if (logOutput != null)
                            logOutput.Alert("Assembly " + Path.GetFileNameWithoutExtension(asmPath) + " tried to bind port " + entry.Key.Port + ". That port is already bound");
                        continue;
                    }

                    output.Add(entry.Key, new ServerExtensionInitializer(asm, entry.Value, asmPath));
                    Listener.Instantiate(entry.Key);
                    if (logOutput != null)
                        logOutput.Log("Listener set up on port " + entry.Key.Port + " for assembly " + Path.GetFileNameWithoutExtension(asmPath));
                }
            }

            LOADED_ASSEMBLYS.Add(asm.GetName().Name, asm);
            EXTENSIONS_PATH.Add(asm.GetName().Name, asmPath);

            if(logOutput != null)
                logOutput.Info("Extension assembly " + asm.GetName().Name + " loaded");
        }

        /// <summary>
        /// Unload the assembly of an extension
        /// </summary>
        /// <param name="extName">Extension name</param>
        /// <param name="keepPath">Whether to keep the path stored</param>
        public static void RemoveAssembly(string extName, bool keepPath = false)
        {
            Assembly temp = LOADED_ASSEMBLYS[extName];
            foreach (var entry in EXTENSIONS)
            {
                string s = entry.Key.GetName().Name;
                if (!entry.Key.GetName().Name.Equals(extName))
                    continue;

                foreach (var sei in entry.Value)
                    sei.Dispose();
            }
            if (temp == null)
                return;

            EXTENSIONS.Remove(temp);
            LOADED_ASSEMBLYS.Remove(extName);
            if (!keepPath)
                EXTENSIONS_PATH.Remove(extName);
            temp = null;
            GC.Collect();
            ServerMain.logging.Warn("Assembly of extension " + extName + " unloaded [DONE]");
        }
        #endregion

        public void Dispose() => Extension = null;
    }
}