You just downloaded the network library of the honjo-net framework, which is probably its biggest component.
It allows you to easily and with a robust foundation exchange data between client/server.

YOU WILL NEED TO SETUP A SERVER to exchange data, which can be done on your own using listeners and the framework, but I strongly advise 
you to use my already existing server foundation and to setup your own extension(s).
The server binaries and informations on its use can be downloaded from https://github.com/Reymmer/honjo-net
I strongly advise to check the wikis.

See https://github.com/Reymmer/honjo-net for more information and namely for the wikis, which also contains UNITY binaries.

Most useful classes:
  - Client
  - Packet
  - PacketProc
  
Packets are identified with a header (OK, ACK, WAUTH...) which signals errors and packet types, an ID and a Specifier.
For example the server dispatching the PGP public key gives its packet the following informations, respectively: OK (0), AUTH_ID (1), SERVER_DISPATCH_PKEY (0)
Create a client with a distant ip, a port and a protocol to connect it to a server. Send packets using Client.Send and bind packet reception using events like
  client.OnDataReception += ... (Client c, Packet p) => to bind something to this client for every packet or even better using client.OnReception(id, specifier, method)
  to bind packets with a given id or specifier to a certain method.
Once again see https://github.com/Reymmer/honjo-net and the wikis for detailed information.