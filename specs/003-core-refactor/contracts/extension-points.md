# Contract: Extension points, manifest and loading

Consumers: extension authors (NetPrintsUnreal U1–U3, the P3 sample extension), the editor, the CLI.
API version of this contract: **`netprintsApi` 1.0** (`ExtensionApi.Version`). A breaking change bumps
the major. Test obligations (`EX-Txx`) at the end.

| Interface | Project / file | Consumed by |
|---|---|---|
| `INodeTranslator`, `IExecutionTranslationContext`, `NodeTranslatorRegistry`, `TranslationEnvironment` | `src/NetPrints.Core/Translator/Extensibility/*.cs` | `ExecutionGraphTranslator`, `ClassTranslator` |
| `IClassEmitter`, `IMemberEmitter`, contexts | `src/NetPrints.Core/Translator/Extensibility/Emitters.cs` | `ClassTranslator` |
| `IProjectProfile`, `ClassTemplate`, `DefaultProjectProfile` | `src/NetPrints.Core/Profiles/*.cs` | `IProjectSystem.CreateAsync`, editor (new project/class) |
| `ITypeCatalog`, `CatalogInfo`, `CompositeReflectionProvider`, `InMemoryTypeCatalog` | `src/NetPrints.Reflection/Catalogs/*.cs` | `ReflectionHost` |
| `INodeDocumentConverter` | `src/NetPrints.Serialization/Mapping/` (document-format.md §2.6) | `DocumentMapper` |
| `INetPrintsExtension`, `IExtensionBuilder`, `INodeLibrary`, `NodeKindDescriptor`, manifest, loader, registry, `IHostChannel`, settings | `src/NetPrints.Extensibility/**` | Desktop, Editor, CLI |

## 1. Entry point and builder — `src/NetPrints.Extensibility/INetPrintsExtension.cs`, `IExtensionBuilder.cs`

```csharp
namespace NetPrints.Extensibility;

public static class ExtensionApi
{
    public static Version Version { get; } = new(1, 0);
}

/// Exactly one public non-abstract implementation with a public parameterless constructor per extension assembly.
public interface INetPrintsExtension
{
    void Register(IExtensionBuilder builder);
}

public interface IExtensionBuilder
{
    ExtensionManifest Manifest { get; }
    ILoggerFactory LoggerFactory { get; }
    IExtensionBuilder AddNodeLibrary(INodeLibrary library);
    IExtensionBuilder AddClassEmitter(IClassEmitter emitter);
    IExtensionBuilder AddMemberEmitter(IMemberEmitter emitter);
    IExtensionBuilder AddTypeCatalog(ITypeCatalog catalog);
    IExtensionBuilder AddProjectProfile(IProjectProfile profile);
    IExtensionBuilder AddHostChannel(IHostChannelFactory factory);
    IExtensionBuilder AddSettings(ExtensionSettingsDescriptor descriptor);
    IExtensionBuilder AddJsonTypeInfoResolver(IJsonTypeInfoResolver resolver);
    IExtensionBuilder AddProjectProperty(string msbuildPropertyName);   // captured into ProjectSnapshot.Properties
}
```

| Rule | Contract |
|---|---|
| `Register` | Called once, on the loading thread, before the registry is built. Contributions are buffered and committed only if `Register` returns normally; an exception discards all of that extension's contributions (`NPX005`). |
| Builder calls | Null arguments → `ArgumentNullException`. Calls after `Register` returned → `InvalidOperationException`. |
| Lifetime | Contributed objects are singletons owned by the registry for the process lifetime; `IDisposable`/`IAsyncDisposable` contributions are disposed when the registry is disposed. |
| Threading | Contributions must be thread-safe for reads after registration (translator and reflection run on background threads). |

## 2. Node libraries — `src/NetPrints.Extensibility/Nodes/*.cs`

```csharp
namespace NetPrints.Extensibility.Nodes;

[Flags]
public enum GraphKinds { None = 0, Method = 1, Constructor = 2, Class = 4, Type = 8, Event = 16, Execution = Method | Constructor | Event }

public interface INodeLibrary
{
    string Id { get; }                                  // "<extension id>" or "<extension id>/<library>"
    IReadOnlyList<NodeKindDescriptor> NodeKinds { get; }
}

public sealed record NodeKindDescriptor(
    string Kind,                                        // == Converter.Kind
    Type NodeType,                                      // : NetPrints.Graph.Node; == Converter.NodeType
    INodeDocumentConverter Converter,
    INodeTranslator Translator,
    GraphKinds AllowedIn,
    IReadOnlyList<NodeSuggestion> Suggestions);

/// A node-search entry. Create receives the target graph and must add the node to it (Node constructors do).
public sealed record NodeSuggestion(string Category, string DisplayName, string? IconKey, Func<NodeGraph, Node> Create);

public static class BuiltInNodeLibrary
{
    public const string Id = "netprints";
    public static INodeLibrary Instance { get; }        // 24 built-in kinds (document-format.md §1.5)
}
```

| Rule | Contract |
|---|---|
| Kind ids | Built-in: no `/`. Extension: `"<manifest id>/<Name>"`; a kind whose prefix is not the registering extension's id → `NPX006`, rejected. |
| Conflicts | Duplicate `Kind` or `NodeType` across all libraries → the later registration (load order) is rejected, `NPX006`, logged `NodeKindConflict` (2005). |
| Consistency | `Kind != Converter.Kind` or `NodeType != Converter.NodeType` or `NodeType` not assignable to `Node` → rejected, `NPX006`. |
| Search | Editor shows `Suggestions` of kinds whose `AllowedIn` contains the open graph's kind, grouped by `Category`, after the built-in categories (PAR-52 order unchanged). |
| Rendering | Extension nodes use the generic `NodeVM`/`NodeView` (`NodeVisualKind.Default`); custom visuals are P3. |

### 2.1 Translation — `src/NetPrints.Core/Translator/Extensibility/INodeTranslator.cs`

```csharp
namespace NetPrints.Translator;

public interface INodeTranslator
{
    /// Exec nodes: called once per input exec pin (inputExecPinIndex = 0..InputExecPins.Count-1).
    /// Pure nodes: called once with inputExecPinIndex = 0 and must assign every output data pin it produces.
    void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex);
}

public interface IExecutionTranslationContext
{
    NodeGraph Graph { get; }                 // MethodGraph, ConstructorGraph or EventGraph being translated
    ClassGraph Class { get; }
    void Append(string code);
    void AppendLine(string code = "");
    string GetOrCreatePinName(NodeOutputDataPin pin);       // stable local variable name for an output
    string GetOrCreateTypedPinName(NodeOutputDataPin pin);  // "<Type> <name>"
    string? GetPinIncomingValue(NodeInputDataPin pin);      // expression; null when the parameter default applies; InvalidOperationException when unconnected without value
    void TranslateDependentPureNodes(Node node);            // emits pure dependencies in order
    void WriteGotoOutputPin(NodeOutputExecPin pin);
    void WriteGotoOutputPinIfNecessary(NodeOutputExecPin pin, NodeInputExecPin fromPin);
    void WritePushJumpStack(NodeInputExecPin pin);
    void WriteGotoJumpStack();
    string CreateTemporaryVariableName();                   // deterministic (seeded) like today
}

public sealed class NodeTranslatorRegistry
{
    public NodeTranslatorRegistry(IReadOnlyDictionary<Type, INodeTranslator> translators); // exact runtime types
    public static NodeTranslatorRegistry BuiltIn { get; }  // the handlers of today's static table
    public INodeTranslator? Find(Type nodeType);
    public NodeTranslatorRegistry With(Type nodeType, INodeTranslator translator); // ArgumentException if present
}

public sealed record TranslationEnvironment(
    NodeTranslatorRegistry Nodes,
    IReadOnlyList<IClassEmitter> ClassEmitters,
    IReadOnlyList<IMemberEmitter> MemberEmitters)
{
    public static TranslationEnvironment BuiltIn { get; }  // BuiltIn nodes, no emitters
}
```

`ExecutionGraphTranslator` (`src/NetPrints.Core/Translator/ExecutionGraphTranslator.cs`) becomes
`public sealed class ExecutionGraphTranslator : IExecutionTranslationContext` with
`ExecutionGraphTranslator(TranslationEnvironment environment)`, `string Translate(ExecutionGraph graph, bool withSignature)`
(output unchanged), `string TranslateEventEntry(EventGraph graph, EventEntryNode entry)` (US4) and
`IReadOnlyList<NodeCodeOffset> NodeOffsets { get; }` (`readonly record struct NodeCodeOffset(int Offset, string NodeId)`,
research R3). A node type without translator → `TranslationException(NPT006, "No translator for <type>")`
(today: silently skipped via `Debug.WriteLine`; golden tests prove no built-in hits this).
Not thread-safe; one instance per translation (as today).

## 3. Emitters — `src/NetPrints.Core/Translator/Extensibility/Emitters.cs`

```csharp
namespace NetPrints.Translator;

public interface IClassEmitter
{
    string Id { get; }
    void EmitClass(ClassEmitContext context);
}

public interface IMemberEmitter
{
    string Id { get; }
    void EmitMember(MemberEmitContext context);
}

public sealed class ClassEmitContext
{
    public ClassGraph Class { get; }
    public ISet<string> Usings { get; }          // namespaces; emitted as "using X;" before the namespace, ordinal-sorted
    public IList<string> Attributes { get; }     // attribute bodies without brackets, e.g. "UClass(ClassFlags.Abstract)"
    public ISet<string> ExtraModifiers { get; }  // subset of { "partial", "sealed", "abstract", "static", "unsafe" }
    public IList<string> BaseTypes { get; }      // prefilled with ClassGraph.AllBaseTypes (code names); may be edited
}

public enum EmittedMemberKind { Field, Property, Method, Constructor, EventMethod }

public sealed class MemberEmitContext
{
    public ClassGraph Class { get; }
    public EmittedMemberKind Kind { get; }
    public string Name { get; }
    public object Model { get; }                 // Variable | MethodGraph | ConstructorGraph | EventEntryNode
    public IList<string> Attributes { get; }
    public ISet<string> ExtraModifiers { get; }  // e.g. "partial", "virtual", "override", "new"
    public bool DeclarePartial { get; set; }     // properties only: emit "partial T X { get; set; }" without bodies/backing field
}
```

| Rule | Contract |
|---|---|
| Order | Class emitters, then member emitters, each in registry order (extension load order, then `Add*` call order). Attributes are emitted in list order, one `[...]` per line. |
| No emitters | Output byte-identical to today (golden tests, DF-T01). |
| Validation | `ExtraModifiers` outside the allowed set → `TranslationException(NPT007)`. `DeclarePartial` on a non-property → `NPT007`. |
| Exceptions | An emitter exception becomes `TranslationException(NPT005, "<emitter id>: <message>")` for that class; other classes still translate. |

## 4. Type catalogs — `src/NetPrints.Reflection/Catalogs/*.cs`

```csharp
namespace NetPrints.Reflection;

public sealed record CatalogInfo(string Id, string Version, IReadOnlyList<string> CoveredAssemblyNames); // simple names, e.g. "UnrealSharp"

public interface ITypeCatalog : IReflectionProvider
{
    CatalogInfo Info { get; }
}

public sealed class CompositeReflectionProvider : IReflectionProvider
{
    public CompositeReflectionProvider(IReadOnlyList<IReflectionProvider> providers); // ArgumentException if empty
    public IReadOnlyList<IReflectionProvider> Providers { get; }
}

public sealed class InMemoryTypeCatalog : ITypeCatalog
{
    public InMemoryTypeCatalog(CatalogInfo info, IReadOnlyList<TypeSpecifier> nonStaticTypes, IReadOnlyList<MethodSpecifier> methods,
        IReadOnlyList<VariableSpecifier> variables, IReadOnlyList<ConstructorSpecifier> constructors,
        IReadOnlyDictionary<TypeSpecifier, IReadOnlyList<string>> enumNames, IReadOnlyDictionary<MethodSpecifier, string> documentation);
}
```

Composite semantics (providers in order: catalogs in registry order, then the live provider):

| `IReflectionProvider` member | Composite result |
|---|---|
| `TypeSpecifierIsSubclassOf`, `HasImplicitCast` | `true` if any provider returns `true` |
| `GetNonStaticTypes`, `GetOverridableMethodsForType`, `GetPublicMethodOverloads`, `GetConstructors`, `GetEnumNames`, `GetMethods`, `GetVariables` | concatenation in provider order, distinct by `Equals`, first occurrence kept |
| `GetMethodDocumentation`, `GetMethodParameterDocumentation`, `GetMethodReturnDocumentation` | first non-null |

Live provider change: `ReflectionProvider(IReadOnlyList<ResolvedAssembly> assemblies, IEnumerable<string> sourcePaths, IEnumerable<string> sources, IReadOnlySet<string> excludedAssemblyNames)`;
types whose containing assembly name is in `excludedAssemblyNames` (union of `CoveredAssemblyNames`)
are skipped in every enumeration; the assemblies stay referenced so user sources still bind.
Thread-safety: catalogs are immutable; the composite adds no state.

## 5. Project profiles — `src/NetPrints.Core/Profiles/*.cs`

```csharp
namespace NetPrints.Core;

public sealed record ClassTemplate(string Id, string DisplayName, Func<Project, string, ClassGraph> Create); // (project, className)

public interface IProjectProfile
{
    string Id { get; }                                  // reverse-DNS, e.g. "netprints.default", "netprints.unreal"
    string DisplayName { get; }
    string DefaultTargetFramework { get; }              // "net10.0"
    string ProjectTemplate { get; }                     // .csproj text; placeholders {ProjectName} {RootNamespace} {TargetFramework} {NetPrintsSdkVersion} {ProfileId}
    IReadOnlyList<TypeSpecifier> BaseTypes { get; }     // offered for new classes; first = default
    IReadOnlyList<ClassTemplate> ClassTemplates { get; }
    string? CatalogProfileId { get; }                   // used by P2 catalog tooling; unused in P1
}

public sealed class DefaultProjectProfile : IProjectProfile
{
    public const string ProfileId = "netprints.default";
    public static DefaultProjectProfile Instance { get; }
    // net10.0, ProjectTemplate = project-system.md §1 (OutputType Exe),
    // BaseTypes [System.Object], ClassTemplates [ "netprints.empty-class" ], CatalogProfileId null
}
```

| Rule | Contract |
|---|---|
| Lookup | `ExtensionRegistry.FindProfile(id)` with `id = $(NetPrintsProfile)`; unknown id on load → `DefaultProjectProfile` for this session, issue `NPD005`, the `.csproj` is not rewritten. |
| Conflicts | Duplicate `Id` → later rejected (`NPX006`). `netprints.default` cannot be replaced. |
| Use in P1 | `IProjectSystem.CreateAsync(dir, name, profile, ns)` writes `ProjectTemplate`; the editor's New Class uses the first `ClassTemplates` entry and `BaseTypes[0]` (choice UI is P6). Output layout is standard MSBuild (`bin/`, `obj/`). |

## 6. Host channel — `src/NetPrints.Extensibility/Hosting/*.cs`

```csharp
namespace NetPrints.Extensibility.Hosting;

public enum HostChannelState { Connecting, Open, Closed }

public sealed record HostMessage(string Type, JsonElement Payload);

public static class HostMessageTypes
{
    public const string TypesChanged = "netprints.types-changed";     // payload: {} ; editor reloads reflection
    public const string FocusDocument = "netprints.focus-document";   // payload: { "path": "<project-relative or absolute>", "nodeId"?: "n3" }
}

public interface IHostChannel : IAsyncDisposable
{
    string Id { get; }
    HostChannelState State { get; }
    IObservable<HostMessage> Messages { get; }                        // completes when Closed; never OnError
    ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken); // InvalidOperationException when Closed
}

public sealed record HostLaunchContext(IReadOnlyDictionary<string, string> Settings); // from NETPRINTS_HOST_* env vars in P1

public interface IHostChannelFactory
{
    string Id { get; }
    IHostChannel Create(HostLaunchContext context);
}

public sealed class NullHostChannel : IHostChannel { public static NullHostChannel Instance { get; } } // State Open, no messages, SendAsync no-op
public sealed class InMemoryHostChannel : IHostChannel
{
    public static (InMemoryHostChannel Editor, InMemoryHostChannel Host) CreatePair(string id);
}
```

| Rule | Contract |
|---|---|
| Selection | Desktop: `NETPRINTS_HOST_CHANNEL=<factory id>` → that factory's channel; unset → `NullHostChannel.Instance`; set but unknown → `NullHostChannel` + error dialog + log `HostChannelUnknown` (1021). Exactly one channel per editor process. (`--profile`/launch arguments: P3.) |
| Threading | `Messages` may emit on any thread; the editor observes on `EditorContext.Scheduler` via `IUiDispatcher`. |
| Unknown message types | Ignored, logged at Debug (`HostMessageIgnored`, 1022). |
| Lifetime | The composition root owns the channel and disposes it on exit. |

## 7. Settings — `src/NetPrints.Extensibility/Settings/*.cs`

```csharp
namespace NetPrints.Extensibility.Settings;

public abstract record ExtensionSettingsDescriptor(string ExtensionId, Type ValueType);
public sealed record ExtensionSettingsDescriptor<T>(string ExtensionId, JsonTypeInfo<T> TypeInfo, T Default)
    : ExtensionSettingsDescriptor(ExtensionId, typeof(T));

public interface ISettingsStore
{
    T Get<T>(ExtensionSettingsDescriptor<T> descriptor);
    ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken);
}

public sealed class JsonFileSettingsStore : ISettingsStore
{
    public JsonFileSettingsStore(string filePath, ILogger<JsonFileSettingsStore> logger);
    public static string DefaultFilePath();   // Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "NetPrints", "settings.json") ($XDG_CONFIG_HOME or ~/.config on Linux/macOS, %APPDATA% on Windows)
}

// Project-scope extension settings are MSBuild properties: extensions list the property names they need in
// ExtensionSettingsDescriptor-independent form through IExtensionBuilder.AddProjectProperty(name), and read them with
// ProjectSnapshot.GetProperty(name) (project-system.md §4). No NetPrints-specific project settings file exists.
```

File layout: `{ "schemaVersion": 1, "netprints": { "extensionPaths": [], "trustedProjects": [] }, "extensions": { "<id>": { … } } }`
(ordinal-sorted, canonical writing rules of document-format.md §1.1). `netprints` is the built-in
section (`NetPrintsSettings` record: `ExtensionPaths`, `TrustedProjects` = full `.csproj` paths whose
`NetPrintsExtension` items the user allowed).

| Rule | Contract |
|---|---|
| `Get` | Missing file/section → `Default`. Invalid JSON in a section → `Default` + log `SettingsSectionInvalid` (2006); the invalid section is kept on disk. Reads the file once and caches; `SetAsync` updates the cache. |
| `SetAsync` | Rewrites the whole file atomically (temp + move); unknown sections preserved verbatim. |
| Registration | One descriptor per extension id; duplicates → `NPX006`. |
| Thread-safety | `Get` concurrent-safe; `SetAsync` serialized by a `SemaphoreSlim`. |

## 8. Manifest and loading — `src/NetPrints.Extensibility/Loading/*.cs`

Manifest file `netprints-extension.json`, next to the extension assembly:

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

```csharp
namespace NetPrints.Extensibility.Loading;

public sealed record ExtensionManifest(string Id, string Name, string Version, string Assembly, string NetprintsApi, IReadOnlyList<string> DependsOn)
{
    public static ExtensionManifest Parse(Stream json, string manifestPath); // ExtensionManifestException (NPX001)
}

public abstract record ExtensionLoadResult(string Id, string? ManifestPath)
{
    public sealed record Loaded(string Id, string? ManifestPath, ExtensionManifest Manifest) : ExtensionLoadResult(Id, ManifestPath);
    public sealed record Failed(string Id, string? ManifestPath, string Code, string Reason, Exception? Exception) : ExtensionLoadResult(Id, ManifestPath);
}

public sealed record ExtensionLoaderOptions(
    IReadOnlyList<string> SearchDirectories,                 // settings "netprints.extensionPaths" then NETPRINTS_EXTENSION_PATH entries (editor); empty in the generator
    IReadOnlyList<string> ExtensionFolders,                  // explicit folders containing netprints-extension.json: trusted NetPrintsExtension items (editor) / request "extension=" lines (generator)
    IReadOnlyList<(ExtensionManifest Manifest, INetPrintsExtension Extension)> InProcess); // built-in + tests; loaded without an ALC

/// Editor-side holder: rebuilds the registry when a trusted project adds extension folders. Load contexts
/// are cached by manifest path (non-collectible), so re-loading never loads an assembly twice.
public interface IExtensionHost
{
    ExtensionRegistry Current { get; }
    ExtensionRegistry LoadForProject(IReadOnlyList<string> projectExtensionFolders, CancellationToken cancellationToken);
    event EventHandler<ExtensionRegistry>? RegistryChanged;
}

public sealed class ExtensionLoader
{
    public ExtensionLoader(ExtensionLoaderOptions options, ILoggerFactory loggerFactory);
    public ExtensionRegistry Load(CancellationToken cancellationToken);
}

internal sealed class ExtensionLoadContext : AssemblyLoadContext
{
    public ExtensionLoadContext(string extensionAssemblyPath); // name = manifest id, isCollectible: false
}

public sealed class ExtensionRegistry : IDisposable
{
    public IReadOnlyList<ExtensionLoadResult> Results { get; }          // every manifest found, in load order, then failures
    public IReadOnlyList<ExtensionManifest> Loaded { get; }
    public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; }
    public IReadOnlyList<IClassEmitter> ClassEmitters { get; }
    public IReadOnlyList<IMemberEmitter> MemberEmitters { get; }
    public IReadOnlyList<ITypeCatalog> TypeCatalogs { get; }
    public IReadOnlyList<IProjectProfile> Profiles { get; }             // DefaultProjectProfile first
    public IReadOnlyList<IHostChannelFactory> HostChannels { get; }
    public IReadOnlyList<ExtensionSettingsDescriptor> Settings { get; }
    public TranslationEnvironment Translation { get; }
    public NodeDocumentConverterRegistry NodeConverters { get; }
    public IProjectProfile? FindProfile(string id);
    public IHostChannelFactory? FindHostChannel(string id);
}
```

### 8.1 Discovery and order

1. In-process extensions (`BuiltInExtension` with id `netprints`, then test/in-process ones) in list order.
2. `ExtensionFolders` in order (each must contain `netprints-extension.json`, else `NPX001`), then for each
   search directory in order: each **immediate** subdirectory (ordinal order) containing
   `netprints-extension.json`. Non-existent directories are skipped (Debug log).
3. Duplicate id → first wins; later → `Failed(NPX004)`.
4. Validation: manifest schema (`NPX001`); `netprintsApi` major ≠ `ExtensionApi.Version.Major` or
   minor > host minor → `NPX002`; `dependsOn` id missing or failed → `NPX003`; cycle → all members `NPX003`.
5. Topological sort by `dependsOn`; ties by id (ordinal). This order is the **registry order** used
   everywhere (emitters, catalogs, JSON resolvers, search categories).
6. For each: load assembly in its `ExtensionLoadContext` (failure → `NPX007`), find the single
   `INetPrintsExtension` type (none/many → `NPX001`), instantiate, call `Register` (throws → `NPX005`),
   commit contributions (conflicts → per-contribution `NPX006`, extension still loaded).
7. Build the registry. `Load` never throws for extension problems; it throws only
   `OperationCanceledException`.

### 8.2 AssemblyLoadContext rules (research R8)

| Assembly requested by an extension | Resolved from |
|---|---|
| Name starts with `NetPrints` | Default context (host copy) |
| `Microsoft.Build*` | Default context (Locator-registered SDK copy) |
| `Microsoft.CodeAnalysis*`, `CommunityToolkit.Mvvm`, `System.Reactive`, `DynamicData`, `Avalonia*`, `Microsoft.Extensions.*.Abstractions` | Default context |
| Framework assemblies (in `TRUSTED_PLATFORM_ASSEMBLIES`) | Default context |
| Anything else | `AssemblyDependencyResolver` of the extension (its folder / `.deps.json`); unresolved → `null` (runtime fails → `NPX007`) |
| Native libraries | `LoadUnmanagedDll` via the same resolver |

Extension project requirements (documented for authors, verified by `tests/NetPrints.TestExtension`):
`<EnableDynamicLoading>true</EnableDynamicLoading>`; NetPrints references with
`<Private>false</Private><ExcludeAssets>runtime</ExcludeAssets>`; output = extension folder with the
manifest copied (`CopyToOutputDirectory`).

### 8.3 Security

Editor: directories from user settings and `NETPRINTS_EXTENSION_PATH`, plus a project's `NetPrintsExtension`
folders **only** if the `.csproj` full path is in `netprints.trustedProjects`. Otherwise the editor asks once
(`IEditorDialogs.ConfirmTrustAsync(projectPath, folders)`); "Trust" appends to `trustedProjects` and loads,
"Don't load" opens the project with those extension nodes preserved but inactive and issue `NPD006`.
Generator (build): exactly the request's `extension=` folders (building a project already trusts it);
user directories are not used, so builds are reproducible.

## 9. Test obligations

| ID | Case |
|---|---|
| EX-T01 | Test extension loads from a temp search dir via ALC; `typeof(Node)` seen by the extension == host `typeof(Node)`; its `Assembly` is in its own `AssemblyLoadContext` |
| EX-T02 | Extension node kind: appears in `NodeKinds` and editor search for allowed graph kinds only; saved, reloaded and translated with the extension's C# (build path: PS-T14) |
| EX-T03 | Missing extension: document with its node loads without it (DF-T08) and translation reports `NPT003` |
| EX-T04 | Class/member emitters: attribute, `partial` modifier, using and `DeclarePartial` property appear in order; with no emitters output equals golden |
| EX-T05 | Emitter throws → `NPT005` for that class only |
| EX-T06 | Catalog: composite returns catalog types first, distinct; `CoveredAssemblyNames` excluded from live enumeration; `HasImplicitCast` any-true |
| EX-T07 | Profile: `IProjectSystem.CreateAsync` writes the profile's template; unknown `NetPrintsProfile` → default profile + `NPD005`, `.csproj` untouched |
| EX-T08 | Host channel: `TypesChanged` via `InMemoryHostChannel` triggers `IReflectionHost.ReloadAsync` once; unknown type ignored; `SendAsync` after dispose → `InvalidOperationException` |
| EX-T09 | Settings: set/get round trip; unknown section preserved byte-identically; invalid section → default + log 2006 |
| EX-T10 | Failures, each with the editor/registry still usable and the other extension loaded: invalid manifest (`NPX001`), API 2.0 (`NPX002`), missing dependency (`NPX003`), duplicate id (`NPX004`), `Register` throws (`NPX005`), kind conflict (`NPX006`), missing assembly (`NPX007`) |
| EX-T11 | Order: two extensions with `dependsOn` load dependency first; ties by id; emitters applied in that order |
| EX-T12 | Built-in node kinds are registered through `BuiltInNodeLibrary` (registry contains 24 kinds; removing it from `InProcess` makes built-in nodes unknown) |
| EX-T13 | Security: a project with a `NetPrintsExtension` item is not loaded until trusted (dialog fake answers "Don't load" → `NPD006`, nodes preserved; "Trust" → loaded and recorded in settings); a stray `netprints-extension.json` in the project folder without an item is never loaded |
