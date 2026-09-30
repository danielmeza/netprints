# Extensions

An extension is a .NET assembly plus a small manifest (`netprints-extension.json`) that contributes
node kinds, C# emitters, type catalogs, project profiles or a host channel to the editor and the
build. NetPrints itself ships one, the built-in extension; everything else — a game engine's node
library, a company's internal nodes — is loaded the same way.

## Where extensions come from

The editor loads extensions from two places:

- **User-configured directories.** Each immediate subdirectory containing a `netprints-extension.json`
  is loaded. Configured in Settings, or through the `NETPRINTS_EXTENSION_PATH` environment variable
  (a list of directories, separated like `PATH`).
- **The opened project's `NetPrintsExtension` items**, once you trust that project (you are asked
  once; the answer is remembered per project in your user settings):

  ```xml
  <ItemGroup>
    <NetPrintsExtension Include="path/to/netprints.example" />
  </ItemGroup>
  ```

  `NetPrintsExtension` points at the folder containing the extension's `netprints-extension.json`,
  not the assembly itself. It is usually added for you by the extension's NuGet package
  (`build/*.props`), not typed by hand.

A build (`dotnet build`, the generator) always uses the project's `NetPrintsExtension` items — a
build is already trusting the project's contents, so there is no separate prompt. User-configured
extension directories are never used by a build, so builds stay reproducible outside your own
machine.

If an extension a graph depends on is missing or untrusted, its nodes are kept as-is (not deleted)
and reported, so you can fix the reference and get them back.

## Environment variables

| Variable | Effect |
|---|---|
| `NETPRINTS_EXTENSION_PATH` | Extra directories to search for extensions, in addition to the ones in Settings. `PATH`-style separator (`:` on Linux/macOS, `;` on Windows). Editor only. |
| `NETPRINTS_HOST_CHANNEL` | Selects the host channel by id (see below). Unset, or set to an unknown id, runs the editor with no host channel; an unknown id also shows an error and is logged. |
| `NETPRINTS_LOG_LEVEL` | Overrides the desktop editor's minimum log level. Any [`LogLevel`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel) member name, case-insensitive (for example `Debug`); an unset or invalid value keeps the default, `Information`. |

`NETPRINTS_HOST_CHANNEL` is one case of the `NETPRINTS_HOST_*` family: every other `NETPRINTS_HOST_*`
variable is passed to the selected channel factory (with the `NETPRINTS_HOST_` prefix stripped), so a
host channel can take its own settings the same way.

## Writing an extension

An extension project is a class library that implements `INetPrintsExtension` (exactly one public,
non-abstract implementation with a public parameterless constructor per assembly), registering its
contributions with the `IExtensionBuilder` it is handed:

```csharp
public sealed class SampleExtension : INetPrintsExtension
{
    public void Register(IExtensionBuilder builder)
    {
        builder.AddNodeLibrary(new SampleNodeLibrary());
    }
}
```

Next to the built assembly, a `netprints-extension.json` manifest describes it:

```json
{
  "id": "com.example.sample",
  "name": "Sample extension",
  "version": "1.0.0",
  "assembly": "Example.NetPrints.Sample.dll",
  "netprintsApi": "1.0",
  "dependsOn": []
}
```

`NetPrints.Extensibility` (and whatever of `NetPrints.Core`, `NetPrints.Reflection` and
`NetPrints.Serialization` the extension's contributions need) is not published as a standalone NuGet
package in this release, so an in-repo extension references those projects directly, and the project
needs a few settings so the host can load it into its own isolated `AssemblyLoadContext` without
duplicating assemblies the host already provides:

```xml
<PropertyGroup>
  <EnableDynamicLoading>true</EnableDynamicLoading>
</PropertyGroup>

<ItemGroup>
  <!-- The host supplies these at run time; the extension must see the host's own copies. -->
  <ProjectReference Include="path/to/NetPrints.Extensibility.csproj" Private="false" ExcludeAssets="runtime" />
</ItemGroup>
```

`Private="false"` with `ExcludeAssets="runtime"` keeps NetPrints assemblies (and anything else the
host already loads — Roslyn, Avalonia, `Microsoft.Extensions.*.Abstractions`) out of the extension's
own output folder; everything else the extension references ships in that folder and is resolved
from there. The manifest is copied to the output folder with the assembly
(`CopyToOutputDirectory`), since the loader looks for it next to the assembly.
[`tests/NetPrints.TestExtension`](../../tests/NetPrints.TestExtension) in this repository is a small,
complete example of this setup.

## Host channel

A host channel is an optional, extension-provided bidirectional link between the editor and an
external process (for example, a game engine the graphs target). `NETPRINTS_HOST_CHANNEL=<factory
id>` selects it; an extension contributes a channel factory with `IExtensionBuilder`. At most one
channel is active per editor process.

## API stability

Parts of the extension API are still settling. They carry `[Experimental]`, and using one is a compile
error until your project opts in to its id. The opt-in is the acknowledgement that the API can change or
disappear in a minor release.

| Id | Covers |
| --- | --- |
| `NPXE0001` | The host channel: `IHostChannel`, `IHostChannelFactory`, `HostMessage`, `NullHostChannel`, `InMemoryHostChannel`, `IExtensionBuilder.AddHostChannel`, `ExtensionRegistry.HostChannels`, `ExtensionRegistry.FindHostChannel`. |
| `NPXE0002` | Extension settings: `ExtensionSettingsDescriptor` and `ExtensionSettingsDescriptor<T>`, `ISettingsStore`, `JsonFileSettingsStore`, `NetPrintsSettings.Descriptor`, `IExtensionBuilder.AddSettings`, `ExtensionRegistry.Settings`. |
| `NPXE0003` | Class and member emitters: `IClassEmitter`, `IMemberEmitter`, `IExtensionBuilder.AddClassEmitter`, `IExtensionBuilder.AddMemberEmitter`, the emitter lists of `ExtensionRegistry` and `TranslationEnvironment` (including its constructor). |
| `NPXE0004` | The catalog engine and its profiles. |

`IClassEmitter` and `IMemberEmitter` shipped without the attribute in `NetPrints.Core` 0.1.1. Code that
implements or references them, or that builds a `TranslationEnvironment` with emitter lists, now needs
`NPXE0003` when it moves to a newer `NetPrints.Core`.

To opt in, list only the ids your extension uses:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NPXE0001</NoWarn>
</PropertyGroup>
```

A narrow `#pragma warning disable NPXE0001` around the use works too. Projects inside the NetPrints
repository declare `<NetPrintsExperimentalOptIn Include="NPXE0001" />` items instead, and
`Directory.Build.targets` turns them into the one allowed `NoWarn` (ADR-0017). An id disappears when its API
graduates to stable.

The public API of `NetPrints.Core`, `NetPrints.Extensibility`, `NetPrints.Reflection`,
`NetPrints.Serialization` and `NetPrints.Catalog` is tracked in each project's `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`. A change to a public symbol shows up in review as a change to those files.
