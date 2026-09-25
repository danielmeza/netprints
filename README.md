![](https://raw.githubusercontent.com/RobinKa/RobinKa.github.io/master/NetPrintsBanner.png)

[![CI](https://github.com/danielmeza/netprints/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/danielmeza/netprints/actions/workflows/ci.yml)

NetPrints is a visual programming language inspired by Unreal Engine 4's Blueprints which compiles into .NET binaries or alternatively C# source code. These can be used from any other .NET language (eg. C#) or used as standalone programs. Furthermore any .NET binaries (both .NET Framework and .NET Core, and ideally .NET Standard) can be referenced and used. Its goal is to support using anything that is made in C#. 
[Overview](https://github.com/RobinKa/netprints/wiki/Overview)

[Use cases](https://github.com/RobinKa/netprints/wiki/Use-cases)

[Hello world (video)](https://youtu.be/s4M-WOlGEFk)

[Unity tutorial](https://github.com/RobinKa/NetPrintsUnityTutorial)

# Download
Version 0.0.7 of the original WPF editor can be found [here](https://github.com/RobinKa/netprints/releases/tag/0.0.7). The editor has since been rebuilt on [Avalonia](https://avaloniaui.net/) and runs on Linux, Windows and macOS; build it from source as described below.

# Build and Test
Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.100 or later; `global.json` rolls forward to newer feature bands). Two commands build and test everything, on Linux too, without a display server:

```bash
dotnet build NetPrints.sln -c Release
dotnet test --solution NetPrints.sln -c Release --no-build
```

Package versions live in `Directory.Packages.props` (Central Package Management) and shared build settings in `Directory.Build.props`. The [CI](.github/workflows/ci.yml) workflow runs the same commands on every push and pull request.

Command line:

```bash
dotnet run --project NetPrintsCLI -c Release -- --version
dotnet run --project NetPrintsCLI -c Release -- -p samples/HelloWorld/HelloWorld.netpp -r   # prints "Hello, World!"
```

Editor:

```bash
dotnet run --project NetPrints.Desktop -c Release -- samples/HelloWorld/HelloWorld.netpp
```

# Target Frameworks
Every project targets .NET 10.

| Project | Target | Notes |
|--|--|--|
| NetPrints (core) | net10.0 | Node graphs, C# translation, compilation (Roslyn) |
| NetPrints.Reflection | net10.0 | UI-free type reflection used by the editor |
| NetPrints.Editor | net10.0 | Avalonia 12 editor library (views, view models, services) |
| NetPrints.Desktop | net10.0 | Desktop application hosting the editor |
| NetPrintsCLI | net10.0 | Command line compiler |
| NetPrintsUnitTests, NetPrints.Editor.Tests, NetPrints.Editor.UITests | net10.0 | xUnit v3 on Microsoft.Testing.Platform; headless Avalonia UI tests (Avalonia.Headless.XUnit) |
| NetPrintsVSIX | .NET Framework 4.6.1 | Legacy, not built (see below) |

# Visual Studio Extension
The legacy Visual Studio extension (`NetPrintsVSIX`) is kept in the repository for reference but is not part of `NetPrints.sln`, does not build and is not tested in CI; Visual Studio integration is out of scope for now (see `NetPrintsVSIX/README.md`). The published extension on the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=NawTora.NetPrints) is the old version.

# Standalone Editor Guide
The editor runs on Linux (X11 or Wayland), Windows and macOS with the .NET 10 runtime. Any .NET binaries can be used with this editor. The recommended way to add new assembly references is installing them with NuGet and referencing their reference assemblies in the NuGet package folder (`~/.nuget/packages` or `%UserProfile%/.nuget/packages`). The hints for the included references should then appear within the editor. You can also add C# source directories which can either be used for reflection only (useful when you want to use NetPrints within Unity to access your existing scripts) or compiled into the output.

Projects created by the old editor reference the .NET Framework 4.5 reference assemblies. When those are not installed (for example on Linux), NetPrints falls back to the assemblies of the running .NET runtime for reflection and compilation, and compiled executables are started through the `dotnet` host. Proper reference-pack and target selection is planned.

# Contributions
Any contributions are welcome. If you notice bugs or have feature suggestions just create an issue for it. You can also contact me by email at `tora@warlock.ai`.

# Screenshots
| | |
|:-------------------------:|:-------------------------:|
|<img src="https://i.imgur.com/ld32kuo.png" />|<img src="https://i.imgur.com/qHF1cmq.png" />|
|<img src="https://i.imgur.com/NahX6AM.png" />|<img src="https://i.imgur.com/wekGSFs.png" />|
|<img src="https://i.imgur.com/qdYBLni.png" />|<img src="https://i.imgur.com/bq0vECa.png" />|
