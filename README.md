# MusicModder
## Requirements<br>
* [XWBTool](https://github.com/microsoft/DirectXTK/wiki/XWBTool)
* [ffmpeg](https://www.gyan.dev/ffmpeg/builds/)
* [.NET SDK 8](https://dotnet.microsoft.com/en-us/download)
## Setup
Obtain the .exe files for ffmpeg and XWBTool above, and drag them inside the MusicModder folder, and make sure .NET SDK is installed

## Building and running the project
Open a terminal and navigate to the project director 
```sh
cd path/to/MusicModder
```
[Build](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build) the project with
```csharp
dotnet build
dotnet run
```
If no issues arises [release](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) the build with
```csharp
dotnet publish -c Release -r win-x64 --self-contained true
```
After running the command above, your created .exe should be located in bin\Release\netX.X\win-x64\publish\
