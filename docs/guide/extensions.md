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
| `NPXE0004` | The catalog engine and its profiles (see [Type catalogs](catalogs.md)). |

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

## Coexistence rules

When multiple extensions load, each one runs in its own isolated `AssemblyLoadContext`, so versions of
shared dependencies can be kept separate. However, assemblies the host already provides should not be
duplicated.

**What the host provides** and therefore every extension must use the host's copy:

- The trusted platform assemblies (the host application's whole dependency closure, and it differs per host:
  Desktop includes Avalonia and CommunityToolkit.Mvvm, the CLI includes Spectre.Console, the Generator includes neither).
  An extension that ships one of these libraries gets the host's copy in one host and its own copy in another.
- Any assembly already loaded in the default `AssemblyLoadContext` at the time the extension's context is created
- Assemblies named `Microsoft.Build` or `Microsoft.Build.*` (the MSBuild runtime, which the host ensures is registered)

**What loads privately** and therefore each extension can ship its own copy:

- Everything else: your extension's own dependencies and third-party packages, so you can use different
  versions without conflicts.

**Shadowing warnings**: if you ship a copy of a host-provided assembly, or a copy of an assembly one of your
dependencies (listed in `dependsOn`) provides, it is ignored. NetPrints logs a warning for each case:

- Event 2010 (`HostAssemblyShadowed`): your extension folder contains a copy of an assembly the host provides.
- Event 2011 (`DependencyAssemblyShadowed`): your extension folder contains a copy of an assembly one of your
  dependencies provides.

These are not errors; the host's or dependency's copy is used, and your copy is simply not loaded.

**JSON type-info resolvers**: when multiple extensions contribute JSON resolvers, each extension's own document types
are resolved by that extension's resolvers first. If another extension's resolver would also handle those types, the
other extension receives NPX006 ("JSON resolver"), and its resolver is ignored for that type, so the owning extension's
type serialization contract is preserved.

## Depending on another extension

An extension can depend on the contributions of another extension by listing it in the manifest's
`dependsOn` array (a list of extension ids). This is how you compose functionality: extension B can use
types and emitters from extension A, and can contribute nodes that call A's methods.

When you depend on another extension, you must reference its assembly with `Private="false"` in your
project file, so it is not copied into your extension folder. Here's an example, from a consumer that
uses types from a provider extension:

```xml
<ItemGroup>
  <!-- The provider owns the assembly: the consumer must not ship its own copy. -->
  <ProjectReference Include="..\Fx.TypesProvider\Fx.TypesProvider.csproj" Private="false" />
</ItemGroup>
```

And in your manifest, declare the dependency:

```json
{
  "dependsOn": ["fx.types-provider"]
}
```

NetPrints loads extensions in dependency order, so the provider is always loaded before the consumer. When
resolving assemblies, the consumer can see everything the provider loads, can resolve through the provider's contexts,
then the provider's dependencies (depth-first in declared order), so `typeof(ProviderType)`
in the consumer will have the same identity as it does in the provider.

**Type identity in dependencies**: each extension (or version of an extension) loads in its own context,
but when you depend on another extension, you both see the same assembly and the same types from it. If
extension B and extension C both depend on extension A, they both get A's types with a single identity.

**Diamond dependencies**: when B depends on both A and C, and C also depends on A, the search is depth-first in B's
declared `dependsOn` order. NetPrints loads A first and adds it to its own context. When B resolves its dependencies,
it finds A in that context and reuses A's copy. When C resolves its dependencies (C is visited after B), it also finds
A already loaded from A's context, so both B and C see the same A. Each extension keeps its own copy of any assembly
it provides: C's `Describe()` method (if it defines one) stays in C's copy and is not shadowed by A's copy.

**Assembly version mismatch**: when your extension is compiled against a specific version of a dependency, NetPrints
requires that the version provided by a `dependsOn` extension matches or is newer than what your extension expects.
If the dependency provides an older version, the extension fails to load with error NPX008, naming the dependency, the
version your extension expects, and the version the dependency provides. This is an important safety check: if NetPrints
silently loaded an older version, your extension would crash later with a `MissingMethodException` when it calls a
method that did not exist in the older version. NetPrints does not yet check versions from host-provided assemblies
(such as `NetPrints.Core` or standard library packages); that is a planned extension.

**Assembly name resolution**: all assembly resolution is by simple name (no version); NetPrints matches by name only
and relies on the version check above to catch direct-dependency mismatches. This is the same lookup that the .NET
runtime uses: when your extension loads an assembly by name, it will get whichever version is already loaded (from a
dependency or the host), and if that version is too old, the version check will catch it.

For more on how extensions resolve and share assemblies, see [ADR-0010](../adr/0010-extension-testing-and-coexistence.md).
