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

**Trust.** Project-declared extensions load in the CLI (`netprints build`, `regen`, `generate`, `migrate`, `catalog`) and in the
build with no trust check, as analyzers and build tasks do: building a project already runs that project's code. The trust
prompt exists only in the editor, where it decides whether the project's extensions are loaded into the editor process and show
their menus, panels and pages. Declining it never changes what a build does. See
[ADR-0030](../adr/0030-extensibility-split-and-editor-facet.md).

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
  "netprintsApi": "0.4",
  "dependsOn": []
}
```

`netprintsApi` is the `major.minor` API version the extension was built for; the host accepts the same major and a minor up to its own (0.4 in NetPrints 0.4.0). An optional `themes` array lists relative paths of `*.nptheme.json` files (data only) and needs `netprintsApi` 0.4 or later. A field the host does not know is ignored and logged once as information.

An extension needs one package reference, to `NetPrints.Extensibility.Abstractions`. It brings
`NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Catalog` and `NetPrints.Serialization` as dependencies, and with
`NetPrints.Core` the NetPrints analyzers (the `NPA` rules below), so the project file stays short. The host assembly,
`NetPrints.Extensibility`, is for applications that load extensions; an extension never references it.

```xml
<PropertyGroup>
  <EnableDynamicLoading>true</EnableDynamicLoading>
</PropertyGroup>

<ItemGroup>
  <!-- The host supplies the NetPrints assemblies at run time; the extension must see the host's own copies. -->
  <PackageReference Include="NetPrints.Extensibility.Abstractions" Version="0.4.0" ExcludeAssets="runtime" />
</ItemGroup>
```

`EnableDynamicLoading` lets the host load the extension into its own isolated `AssemblyLoadContext`.
`ExcludeAssets="runtime"` keeps the NetPrints assemblies (and anything else the host already loads — Roslyn, Avalonia,
`Microsoft.Extensions.*.Abstractions`) out of the extension's own output folder; everything else the extension references
ships in that folder and is resolved from there. The manifest is copied to the output folder with the assembly
(`CopyToOutputDirectory`), since the loader looks for it next to the assembly.
[`tests/NetPrints.TestExtension`](../../tests/NetPrints.TestExtension) in this repository is a small, complete example; CI
builds it from the packed packages alone (`scripts/verify-packages.sh`).

Package versions follow the API version: `ExtensionApi.Version` is 0.4 for the 0.4.x packages, and the host loads an
extension built for the same major and a minor up to its own.

## The editor facet and the contribution kinds

`IExtensionBuilder` takes what the CLI, the build and the editor all use. `IExtensionBuilder.Editor` is the **editor facet**:
the contributions that only the editor shows. An extension registers both in `Register`, and a host without an editor
(the CLI, the generator, tests) loads the same extension and ignores the facet calls, so a contribution kind never makes an
extension fail to load. The facet and its descriptors are experimental (`NPXE0005`).

| Contribution | Call | What it adds |
| --- | --- | --- |
| Node library, node kinds | `AddNodeLibrary` | Node kinds with their document converter, translator and search suggestions. |
| Node search suggestions | `AddSuggestionProvider` | Entries in the node search, computed from the search context (`NPXE0006`). |
| Node data migrations | `AddDocumentMigration` | Upgrades of a versioned node kind's stored data (`NPXE0013`). |
| Class and member emitters | `AddClassEmitter`, `AddMemberEmitter` | Changes to the generated C# (`NPXE0003`). |
| Type catalogs and profiles | `AddTypeCatalog`, `AddCatalogProfile`, `AddProjectProfile` | Catalogs of types and project profiles. |
| JSON types | `AddJsonTypeInfoResolver` | The source-generated metadata for the extension's document types. |
| Project properties | `AddProjectProperty` | MSBuild properties the editor reads and writes. |
| Host channel | `AddHostChannel` | A link to an external process (`NPXE0001`). |
| Settings | `AddSettings` | A typed settings section stored in the user settings file (`NPXE0002`). |
| String resources | `AddStringResources` | The extension's own localisation resources (`NPXE0011`). |
| Services | `Services` | The extension's own dependency injection container (`NPXE0008`). |
| Project templates | `AddProjectTemplate` | A template for New project, with its files (`NPXE0012`). |
| Commands | `Editor.AddCommand` | A command with a label, icon, default gestures, scope and menu placement. |
| Panels | `Editor.AddPanel` | A dockable tool panel. |
| Start page tiles | `Editor.AddDashboardTile` | A tile on the start page. |
| Context menu items | `Editor.AddContextMenuItem` | An entry in a context menu that runs a command. |
| Status bar items | `Editor.AddStatusBarItem` | A segment of the status bar. |
| Dialogs | `Editor.AddDialog` | A dialog shown by id. |
| Settings pages | `Editor.AddSettingsPage` | A page of the Settings document; without a view model it is generated from the section's schema. |
| Tooltips and go-to | `Editor.AddTooltipProvider`, `Editor.AddGoToProvider` | Tooltip content and go-to-anything results. |
| View resources | `Editor.AddViewResources` | The templates, styles and icon geometries the extension's views use. |

Every id an extension adds starts with its manifest id (`com.example.sample.openPanel`); a contribution without the prefix
is rejected with `NPX013`, and a conflicting one is reported (`NPX004` or `NPX006`) with the first kept. Neither stops
the extension's other contributions.

### Typed contexts

A command handler, a view model factory or an `AppliesTo` predicate receives the context of its own surface:
`ICommandContext`, `IPanelContext`, `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext` or `IDialogContext`.
Each carries the extension's `IServiceProvider` and the part of the session or shell the surface needs (`IProjectSession`,
`IGraphEditor`, `IEditorShell`, `IDialogService`, `IEditorOutput`, `IEditorUndo`). They are interfaces: an extension never
sees an editor view model, and a factory creates its view model from its own services.

Labels are `LocalizedText`. When you give no key, the key comes from the id (`Command.<id>.Label`, `Panel.<id>.Title`,
`Menu.<path>`), so a translation file can replace any label.

### Icons

A descriptor names its icon by `IconId`. A built-in id uses the Material Design Icons set. To add your own, give
`AddViewResources` a `ResourceDictionary` of `StreamGeometry` entries keyed `Icon.<id>` and use `<id>` (starting with your
manifest id) as the `IconId`. Use a single-path vector so the glyph follows the theme; raster images and brand logos are
not accepted ([ADR-0021](../adr/0021-editor-icon-family-and-icon-ids.md)).

### Diagnostics of the host

A problem in an extension is a diagnostic with a stable code, never an exception that stops the host. The `NPX` codes cover
the manifest, the load, a conflicting or unprefixed contribution, an unknown reference and a version mismatch; the list with
causes is in [Diagnostics](diagnostics.md).

## Declaring pins

A node kind declares its pins as properties; the compiler and the NetPrints analyzers (shipped inside the `NetPrints.Core`
package) check them. The rationale is in [ADR-0025](../adr/0025-declarative-node-pins.md).

```csharp
[NodeKind("myCompany.greet")]
public sealed partial class GreetNode : Node
{
    public partial NodeInputExecPin ExecPin { get; }

    public partial NodeInputDataPin<string> NamePin { get; }

    public partial NodeOutputExecPin ThenPin { get; }

    public GreetNode(NodeGraph graph)
        : base(graph)
    {
        DeclarePins();
    }
}
```

- **Typed properties.** The property type says what the pin is: `NodeInputExecPin`, `NodeOutputExecPin`,
  `NodeInputDataPin<T>`, `NodeOutputDataPin<T>`, `NodeInputTypePin`. The property is `partial` and get-only; the generator
  implements it. An unconnected input starts at `default(T)`, or at the value of a `[DefaultValue]` attribute.
- **The convention.** The pin's key, the name stored in the graph file, is the property name without the `Pin` suffix:
  `NamePin` stores `Name`. Pins are numbered in declaration order inside their kind (`InputExecPins[0]`, ...). The
  constructor calls `DeclarePins()` once, before it touches a pin.
- **`[Pin("key")]`.** Sets the key when the property name cannot (a name that does not end in `Pin`, or one that would clash
  with another member). It also keeps a key stable when you rename the property later.
- **`[PinType(nameof(member))]`.** For a data pin whose type is decided at run time, such as a generic result: the member is
  an `ObservableValue<BaseType>` field of the node, and the pin follows it.
- **`[NodeBase]`.** Marks an abstract base class whose derived kinds continue the pin lists after the base's pins. A
  base class never adds pins by hand to a collection it also declares; NPA005 reports it.
- **Kinds that stay imperative.** A node whose pins depend on constructor arguments can still call `AddInputExecPin(...)` and
  friends. Declared pins come first: add imperative pins only after `DeclarePins()`. NPA003 and NPA005 find the calls
  by name inside the constructor (`AddInputDataPin`, `AddOutputDataPin` and the exec and type variants, and `Add` on the
  pin collections); a pin added by a helper method the constructor calls is not seen.

### The `NodePins` files

An extension project that declares pins lists their keys, one per line, in two additional files next to its project file:

```xml
<ItemGroup>
  <AdditionalFiles Include="NodePins.Shipped.txt" />
  <AdditionalFiles Include="NodePins.Unshipped.txt" />
</ItemGroup>
```

`NodePins.Shipped.txt` holds the keys of the released versions; `NodePins.Unshipped.txt` holds new ones. Before a release, move
the unshipped lines to the shipped file, in the same sorted order. A project without the files is not tracked.

### Rules

| Code | Severity | Meaning |
|---|---|---|
| NPA001-NPA009 | error | The declaration is wrong: not `partial`, a duplicate or invalid key, a missing `[PinType]` member, pin properties outside a `[NodeKind]` or `[NodeBase]` class. The code fixes make the class partial, add `[Pin]` or add the marker. |
| NPA010 | warning | A declared key is not in either file. The fix adds it to `NodePins.Unshipped.txt`. |
| NPA011 | error | A shipped key is no longer declared; the message names the old and the new key. Keep the key with `[Pin("old")]` on the replacing property, or remove it on purpose with a `*REMOVED*<line>` in the unshipped file. |

| NPA040 | warning | A `Node.<kind>.Title` or `Node.<kind>.Pin.<in\|out>.<exec\|data\|type>.<key>` key that a `[NodeKind]` class implies is not in the neutral resx. Add the resx as an `AdditionalFiles` item; a project without one is not checked. |
| NPA041 | error | A satellite resx (`Name.es.resx`) has a key its neutral file lacks, or a `{n}` placeholder count that differs. |
| NPA042 | info | A `Node.<kind>.*` key of a `[NodeKind]` class in the project that no declaration uses. |

The full list is in [Diagnostics](diagnostics.md#npa-authoring).

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
| `NPXE0002` | Extension settings: `ExtensionSettingsDescriptor` and `ExtensionSettingsDescriptor<T>`, `ISettingsStore`, `JsonFileSettingsStore`, `NetPrintsSettings.Descriptor`, `IExtensionBuilder.AddSettings`, `ExtensionRegistry.Settings`, `ExtensionLoaderOptions.SettingsStore`. |
| `NPXE0003` | Class and member emitters: `IClassEmitter`, `IMemberEmitter`, `IExtensionBuilder.AddClassEmitter`, `IExtensionBuilder.AddMemberEmitter`, the emitter lists of `ExtensionRegistry` and `TranslationEnvironment` (including its constructor). |
| `NPXE0004` | The catalog engine and its profiles (see [Type catalogs](catalogs.md)). |
| `NPXE0005` | Editor contributions: the typed contexts (`ICommandContext`, `IPanelContext`, `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext`, `IDialogContext`), `IProjectSession`, `IGraphEditor`, `IEditorShell`, `IDialogService`, `IEditorOutput`, `IEditorUndo`, `ICommandHandler`, `CommandScope`, `CommandSelection`, the facet `IExtensionBuilder.Editor` and `IEditorContributions`, the descriptors (`CommandDescriptor`, `PanelDescriptor`, `DashboardTileDescriptor`, `ContextMenuItemDescriptor`, `StatusBarItemDescriptor`, `DialogDescriptor`, `SettingsPageDescriptor`), `ITooltipProvider`, `IGoToProvider`, `ConventionKeys`, `DocumentId`, `NavigationTarget`, `ExtensionLoaderOptions.EditorContributions`. |
| `NPXE0006` | Node search providers: `INodeSuggestionProvider`, `NodeSearchContext`, `NodeSearchEntry`, `INodeCreationContext`, `IExtensionBuilder.AddSuggestionProvider`, `ExtensionRegistry.SuggestionProviders`. |
| `NPXE0007` | C# features and requirements: `CSharpFeature`, `CSharpFeatureId`, `Availability`, `NodeSearchEntry.Requires`. |
| `NPXE0008` | Extension services: `IExtensionBuilder.Services`, `HostExports`, `ExtensionRegistry.ServicesOf`, `ExtensionRegistry.OpenSession`. |
| `NPXE0009` | Settings metadata: `PathKind`, `[RequiresRestart]`, `[SettingPath]`, `[SettingChoices]`. The settings page descriptor, its custom view model factory and `AddSettingsPage` are part of `NPXE0005`. |
| `NPXE0010` | Retired: never allocated and never reused. |
| `NPXE0011` | Extension string resources: `IExtensionBuilder.AddStringResources`, `ExtensionRegistry.StringResources`. |
| `NPXE0012` | Project templates with files: `TemplateFile`, `ProjectTemplateDescriptor`, `IExtensionBuilder.AddProjectTemplate`, `ExtensionRegistry.ProjectTemplates`. |
| `NPXE0013` | Node-data migrations: `INodeDataMigration`, `NodeKindDescriptor.DataVersion`, `IExtensionBuilder.AddDocumentMigration`, `ExtensionRegistry.NodeMigrator`, `NodeDataMigrator`, the three-argument `JsonDocumentFormat` constructor. |

Stable since 0.4.0, with no opt-in: the typed pins and the pin attributes (`[NodeKind]`, `[NodeBase]`, `[Pin]`,
`[PinType]`), `PinRole`, `CSharpLanguageVersion`, the minimal `CSharpTarget` with `CSharpTarget.Resolve`, `CSharpFrameworkInput` and
`CSharpTargetResolution`, `INodeHintSource` and `NodeHint`, `LocalizedText`, `GraphPoint`, `Node.TitleIsFixed` and the surface that already shipped. Ids for the surfaces that land later in the 0.4.0 work
(inspector sections, graph rules, translation context additions, node kinds by convention and package reference edits) are
added to this table when their API ships. From the publish gate of 0.4.0 a public shape changes only under an `NPXE` id or
with a recorded `*REMOVED*` entry in the API files.

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

The public API of `NetPrints.Core`, `NetPrints.Extensibility.Abstractions`, `NetPrints.Extensibility`,
`NetPrints.Reflection`, `NetPrints.Serialization` and `NetPrints.Catalog` is tracked in each project's `PublicAPI.Shipped.txt` and
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
method that did not exist in the older version. The same check covers the assemblies the host provides (such as
`NetPrints.Core` or standard library packages) and the references of the private libraries your extension ships in its
own folder: a library built against a newer version than the host or the dependency provides fails the load with
NPX008, naming the library that asked for it.

**Assembly name resolution**: all assembly resolution is by simple name (no version); NetPrints matches by name only
and relies on the version check above to catch direct-dependency mismatches. This is the same lookup that the .NET
runtime uses: when your extension loads an assembly by name, it will get whichever version is already loaded (from a
dependency or the host), and if that version is too old, the version check will catch it.

For more on how extensions resolve and share assemblies, see [ADR-0010](../adr/0010-extension-testing-and-coexistence.md).
