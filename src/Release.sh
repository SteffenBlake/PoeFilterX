dotnet publish -c Release -r "linux-x64" /property:Version=3.26.1 /p:DebugType=None /p:DebugSymbols=false --self-contained true -p:PublishSingleFile=true ./PoeFilterX/
dotnet publish -c Release -r "win-x64" /property:Version=3.26.1 /p:DebugType=None /p:DebugSymbols=false --self-contained true -p:PublishSingleFile=true ./PoeFilterX/
