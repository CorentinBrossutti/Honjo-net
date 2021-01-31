@ECHO OFF
del /q .\honjo-net.crypt\*.nupkg
del /q .\honjo-net.database\*.nupkg
del /q .\honjo-net.logging\*.nupkg
del /q .\honjo-net.network\*.nupkg
del /q .\honjo-net.util\*.nupkg

cd .\honjo-net.crypt\
..\nuget.exe pack .\honjo-net.crypt.nuspec
cd ..\honjo-net.database\
..\nuget.exe pack .\honjo-net.database.nuspec
cd ..\honjo-net.logging\
..\nuget.exe pack .\honjo-net.logging.nuspec
cd ..\honjo-net.network\
..\nuget.exe pack .\honjo-net.network.nuspec
cd ..\honjo-net.util\
..\nuget.exe pack .\honjo-net.util.nuspec
pause