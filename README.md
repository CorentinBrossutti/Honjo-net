Honjo-net is a .NET/C# framework primarily intended to enhanced, clarify and optimize the building of client/server based network solutions.

**PLEASE SEE THE WIKIS FOR TECHNICAL INFORMATIONS. VIDEO TUTORIALS IN PROGRESS.**


It has three distinct versions so far, and the first two are available as a single unified NuGet package :

- Framework.Desktop : Targets the latest (4.6.1 initially) .NET Framework versions. 
Contains both server and client libraries, intended for use in a .NET Framework environment (windows...)
		
- Framework.NSTD (client only) : Targets the latest (2.0 initially) .NET Standard versions. 
Contains client libraries only, as the server should only be located in a Desktop environment.
This version is very useful for cross-platform projects, namely mono-based solutions and mobile frameworks  (Xamarin)
		
- Framework.Unity (client only) : A lightweight version of Framework.Desktop with several advanced features removed (PseudoEnums and such).
This version targets .NET Framework 3.5 (it may evolve as Unity supports more recent versions) and is less flexible and more suited to the needs of a Unity scripting environment. It only contains client libraries.
	- If you have a serialization error, use _client.SetSerialization(Serialization.NATIVE, true)_ on client side (Unity) right after your instanciation (constructor call).
  - **TO USE IT, download the Unity.zip file in the UNITY CLIENT folder and unpack it into your Assets/Plugins**
  - The server should be a separate project (or solution, whatever) using latest .NET **Framework**


Everything except the Server and Unity (client) binaries is available as a nuget package (see nuget.org). The Server, since it's a standalone application, can be download along with all needed dlls in Server\dll or Server\out which also contains PDB, XML and CONFIG files and honjo-net libraries.
- **DO NOT USE OLD NUGET VERSIONS FOR UNITY CLIENT, INSTEAD: See above point**

***
				
The included client libraries are :

- Crypt : Contains crypting, hashing and security helpers. Used by the Network module for encrypted transmissions.
    - Depends on BouncyCastle, BCrypt for cryptography and on MessagePack, protobuf-net to register encryptables as serializable
	
- Logging : Provides foundation for logging functionalities.
	
- Network : The most useful library of the framework. Provides transmission support, structured payload-oriented (packets) objects exchanges, synchronized properties and more.
    - Depends on other framework libs only although it has indirect dependencies (namely from Crypt).
	
- Util : Provides miscellaneous utilitaries. Contains (except for Unity version) definition for pseudo-enums, concurrent operations, compression, streams, etc.
	
	
The included server libraries are :

- Database : Contains very basic yet quite useful database helpers, namely the possibility to unpack query results as arrays.
	
- Server : Another very important part of the framework as it provides a very robust foundation for the server-based part of an architecture. See wiki.

***


TODO:
- MessagePack-C# for Unity.



README and wikis in progress.


See LICENSE.md for more legal information.



CREDITS AND SPECIAL THANKS TO

- [Marc Gravell for his wonderful protobuf-net implementation](https://github.com/mgravell/protobuf-net)

- [Neuecc for his incredible MessagePack-C#](https://github.com/neuecc/MessagePack-CSharp)

- [The bouncy castle team for their tool on which rests all the crypting foundation of the framework](https://www.bouncycastle.org/fr/)
