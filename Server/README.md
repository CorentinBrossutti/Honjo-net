Put config.cfg next to HonjoServer.exe if you wish to use it. See the comments in it to learn how to use it ; basic config keys are already there and there is an SREF example (which allows to set the values of any static variable/property within the server and framework from configuration)

The dll folder contains only necessary libary dlls and server EXE, out contains everything you would ever need.

Put your extensions which MUST be named Extension.(name of your app).dll next to the exe or in a folder named "extensions". Said folder is located next to HonjoServer.exe
  
Do remember that your extension MUST also contain a public class named either ServerInteraction, ServerInteraction2 or ServerInteraction3 in the Honjo.Interaction namespace. This class can contain the following methods:
- public static Dictionary<PEndPoint, Action<Client>> _EXTERN_ServerLoad(Logging) : called when the server is loading. The Logging object passed is the server logger, use it to log informations to the server.
  
    This method must return a Dictionary<PEndPoint, Action<Client>> to bind a PEndPoint (a port and protocol) to a method to call for each client connecting to a port you reserved with your extension.
  
  Example:
  
      public static Dictionary<PEndPoint, Action<Client>> _EXTERN_ServerLoad(Logging logging)
      {
          serverLogger = logging;

          return new Dictionary<PEndPoint, Action<Client>>()
          {
              {new PEndPoint(5001, Protocol.TCP), ReceiveClient},
              {new PEndPoint(5002, Protocol.TCP), ReceiveClient}
          };
      }
- public static void _EXTERN_ServerUnload : called when the server shuts down or disables the extension.

**For more informations**, see [the wiki page on creating your own server extension](https://github.com/Reymmer/honjo-net/wiki/Creating-your-own-server-extension)
