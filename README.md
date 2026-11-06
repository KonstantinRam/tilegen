Procedural tile generator for Roguelike type games.

Build for Linux:
dotnet publish TileGen.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/linux

Build for Win:
dotnet publish TileGen.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64-app
