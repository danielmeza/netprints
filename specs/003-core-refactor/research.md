# Research: Core Refactor and Extension Points (P1)

**Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Revision 2026-09-25 (owner decision): a NetPrints project is an SDK-style `.csproj`.** §4 (R11–R16)
> records the new research. It supersedes R1 (custom reference-pack resolver), the project half of U1
> (`ProjectDocument`), the ProjectCompiler part of the plan, and the project-document parts of R10.
> Everything else below still holds.

Owner rule for this phase: **reuse, don't reinvent**. §1 lists every decision taken from existing
sources. §2 records the research done only for questions those sources left open. Spikes ran on Linux
with SDK 10.0.400 in the session scratchpad (`p1spike/`, `aespike/`), outside the repository. Package
versions were checked against the nuget.org flat-container and registration APIs on 2026-09-25.

Abbreviations: **Plan** = the plan page (`.agent-archive/2026-09-25-session-c18f4e98/netprints-unreal-plan.html`);
**RM** = `.specify/memory/roadmap.md`; **C** = constitution 1.2.0; **P0-R** = `specs/001-modernize-build/research.md`;
**P0-Rev** = PR #1 review follow-ups (archive `reviews.md`, `comments.md`); **UX** = `docs/research/2026-09-25-ux-audit/`;
**Grid** = `specs/002-grid-rendering/research.md`.

## 1. Decisions reused from existing sources

| # | Decision | Source |
|---|---|---|
| U1 | Three serialization layers: versioned cycle-free DTOs (`ClassDocument(SchemaVersion, Header, Graphs, Variables)`), `IDocumentFormat` (Id, Extensions, Read/Write), `IDocumentStore` (OpenRead/OpenWrite/List + `IObservable<DocumentChange> Changes`), `IDocumentMapper` (ToDocument/FromDocument); migrations upgrade old `SchemaVersion`s before mapping | Plan "Serialization"; C VI, VII |
| U2 | JSON via System.Text.Json source-generated `JsonSerializerContext`; polymorphic nodes with `[JsonDerivedType]` or a resolver extensions add to; positions stored separately from logic | Plan "Serialization"; C tech constraints |
| U3 | Legacy DataContract XML becomes import-only | C tech constraints; RM P1 |
| U4 | Extension points: `INodeLibrary` (node types, VMs, translators), `IClassEmitter`/`IMemberEmitter` (attributes, partial members, base classes, usings), `ITypeCatalog`, `IProjectProfile` (layout, output dir, base classes, templates, default catalog profile), `IHostChannel`, per-extension settings, plugins via AssemblyLoadContext selected by manifest | Plan "Extension points"; RM P1; C III |
| U5 | Editor-side UI contributions are P3, not P1 (commands, inspector sections, panels, settings pages, `--profile`) | Plan; RM P3 |
| U6 | `CompositeReflectionProvider(precomputed catalog, live Roslyn provider)` behind `IReflectionProvider`; generator/tool flavors are P2 | Plan "Catalog tooling"; RM P1/P2 |
| U7 | Event graphs: one graph, several entry points, each entry its own method; lives in core | Plan "Extension points"; RM P1 |
| U8 | Model INPC moves from PropertyChanged.Fody to CommunityToolkit.Mvvm (source generated, no weaving) | Plan "MVVM split"; RM P1; C "No IL weaving"; P0-R §b |
| U9 | Nullable and analyzer warnings cleaned up in Core and Reflection (RS1024 ×10, Fody warnings, ~100 nullable) | Plan P1; P0-R §a, §f; P0 plan Complexity Tracking |
| U10 | Reference resolution: ref packs or configured paths; replaces the P0 all-or-nothing runtime fallback, which stays as the last resort; `{Name}.runtimeconfig.json` + `dotnet` host for runs *Superseded by R14: MSBuild resolves references; no custom resolver.* | RM P1; P0-R §c, review follow-up |
| U11 | D5 fix: probe `packs/Microsoft.NETCore.App.Ref/<ver>/ref/net*/<name>.xml` *Superseded by R14: docs sit next to MSBuild-resolved pack references (verified).* | UX D5 |
| U12 | Code view: AvaloniaEdit, TextMate C# highlighting, folding, line numbers, live Roslyn diagnostics (squiggles + error list linked to the node); read-only; evaluate RoslynPad.Editor.Avalonia for hover | RM P1; UX D4, H7, L6 |
| U13 | Method-local variables: per `MethodGraph`/`ConstructorGraph`, getter/setter nodes, declared at the top of the method (fits the goto translator), Variables panel groups *Class* and *Method: <name>* | RM P1 (owner idea 2026-09-25) |
| U14 | P0 review follow-ups: narrow child-VM dependencies; Roslyn architecture gate demonstrated to fail on a fixture; Nodify command gestures; replace `SetProperty(model, …)` wrappers; remove the `EditorComposition` test hook (explicit DI, no fallbacks); `[LoggerMessage]` logging | RM P1; P0-Rev "Defer to P1" |
| U15 | Grid `ViewportTransform` binding: **dropped**, superseded by P0.1 D8 (typed property sync, no transform allocation) | Grid D8 |
| U16 | `MetadataReference` caching and translation off the UI thread are P8, not P1 (the review suggested caching "with the reference-pack work"; the roadmap assigns it to P8) | RM P8 |
| U17 | Test stack unchanged: xUnit v3 3.2.2 on MTP, `TestContext.Current.CancellationToken` (xUnit1051 error), Xunit.DependencyInjection only in non-UI tests, Avalonia.Headless.XUnit `[AvaloniaFact(Timeout)]`, page objects + `AutomationIds`, no sleeps, snapshot baselines regenerated with `NETPRINTS_UPDATE_SNAPSHOTS=1` | P0-R review follow-up, §r; archive `p0-automation-round.md` |
| U18 | Composition root stays hand-wired (no DI container) with `EditorContext` bundling services; required members, no defaults | P0-R §o, review follow-up; P0-Rev "explicit-di-no-fallbacks" |
| U19 | CI unchanged: workflow `CI`, Linux only; `e2e` job | `specs/001-modernize-build/contracts/ci-workflow.md` |
| U20 | Sample regeneration through `SampleProjectFactory` with `NETPRINTS_REGENERATE_SAMPLES=1` | P0 tasks T017 |
| U22 | `IDocumentStore` keeps the plan page's read/list/`Changes` members, but writing is one callback-based `WriteAsync(id, write, ct)` instead of `OpenWriteAsync`, so a failed write can never replace a document (atomic temp + move). Deviation recorded here. | Plan "Serialization" (amended) |
| U21 | Code comments sparse, rationale in commits/specs; one PR per phase, sequential work | AGENTS.md; owner memories |

## 2. Research on open questions

### R1. Reference packs and XML documentation — *superseded by R14 (MSBuild resolves references)*

**Decision**: a `ReferencePackResolver` (behind `IReferenceResolver` in Core) resolves a project's
target framework (`net<major>.<minor>`) and framework references (`Microsoft.NETCore.App`, optionally
`Microsoft.AspNetCore.App`) to `<pack>/<version>/ref/<tfm>/*.dll`. Probe order:

1. configured roots (`ReferenceResolutionOptions.PackRoots`, filled from the user setting
   `netprints.references.packRoots` and the `NETPRINTS_REFERENCE_PACKS` variable, path-separator list);
2. `DOTNET_ROOT`, then the root of the running host (runtime dir `../../..`, the logic already in
   `ReferenceAssemblyResolver.GetDotNetHostPath`), then well-known roots (`/usr/share/dotnet`,
   `/usr/lib/dotnet`, `/usr/local/share/dotnet`, `%ProgramFiles%\dotnet` from the environment, not
   hard-coded), each `<root>/packs/<Pack>.Ref/<version>/ref/<tfm>`;
3. the NuGet global packages folder (`NUGET_PACKAGES` or `~/.nuget/packages`):
   `<pack lowercase>.ref/<version>/ref/<tfm>`;
4. the running runtime's pack major version (warning);
5. the P0 runtime-directory fallback (warning).

Within a root, the highest patch version whose major.minor matches wins (deterministic, ordinal version
compare). Documentation: the XML sits next to each reference assembly, so the existing "next to the
assembly" probe in `DocumentationUtil` works once compilation uses the pack; in the runtime-directory
fallback it additionally maps `shared/Microsoft.NETCore.App/<v>/X.dll` to the pack XML (the D5 probe).
The Windows `ProgramFilesX86` lookups in `FrameworkAssemblyReference`, `Project` defaults and
`DocumentationUtil` are deleted; legacy `.NETFramework/v4.x/*.dll` references map to the resolved
`Microsoft.NETCore.App` set with one warning.

**Evidence** (this machine): `~/.dotnet/packs/Microsoft.NETCore.App.Ref/{10.0.9,10.0.11}/ref/net10.0/`
holds 173 reference assemblies (per `data/FrameworkList.xml`) and 214 XML files, including
`System.Console.xml`, `mscorlib.dll` and `netstandard.dll` facades. `~/.nuget/packages/microsoft.netcore.app.ref/9.0.18/ref/net9.0/`
holds 104 XML files (NuGet-cache packs also ship docs). GitHub `setup-dotnet` installs the SDK with its
packs, so CI exercises probe 2.

**Alternatives**: MSBuild/`Microsoft.Build.Locator` evaluation of a real project (heavy, needs an SDK,
not WASM-safe); `Basic.Reference.Assemblies` NuGet packages (fixed versions, ~20 MB, no user choice);
downloading packs on demand (network access from the editor; P2 CLI could add it).

### R2. AvaloniaEdit and TextMate for Avalonia 12

**Decision**: `Avalonia.AvaloniaEdit` **12.0.0** + `AvaloniaEdit.TextMate` **12.0.0** (released
2026-04-08; `net8.0`/`net10.0`, depend on Avalonia ≥ 12.0.0 and TextMateSharp/TextMateSharp.Grammars
2.0.3). Pin `TextMateSharp.Grammars` 2.0.3 via its transitive version (no override). Theme: TextMate
`DarkPlus`/`LightPlus` chosen from the Avalonia theme variant. Style include
`avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`. Folding: a `FoldingManager` fed by a folding
strategy built from the Roslyn syntax tree of the generated code (namespace, type, member bodies),
since AvaloniaEdit has no C# folding strategy. Squiggles: a small `IBackgroundRenderer`
(AvaloniaEdit ships no text-marker service). Font: bundle a monospace font as an `AvaloniaResource`
(Cascadia Mono, SIL OFL 1.1), so headless snapshots are deterministic (UX D4 noted that `monospace`
does not resolve headless).

**Evidence**: `aespike/` (Avalonia 12.1.3 headless + Skia, the P0 test setup) created a read-only
`TextEditor` with line numbers, installed TextMate with `GetScopeByLanguageId(".cs")` → `source.cs`,
installed a `FoldingManager` and rendered a 600×300 frame with correct highlighting.

**Risk**: TextMateSharp uses the native Oniguruma wrapper; fine on desktop (bundled for linux/win/osx),
unknown for the P5 browser build. Recorded for P5; the code view keeps a plain-text fallback when the
TextMate installation throws.

### R3. Diagnostics mapped to nodes without changing the C#

**Decision**: `ExecutionGraphTranslator` records, per translated node, the builder offset where its
statements start (an ordered list of `(offset, nodeId)` per method). `TranslatorUtil.FormatCode`
reformats the code, which moves offsets, so the mapping is carried through formatting as Roslyn
`SyntaxAnnotation`s: before formatting, the class translator parses the unformatted text, annotates
the first token at each recorded offset with `("np-node", nodeId)`, formats with annotations kept
(Roslyn formatting preserves annotations), and reads back the annotated token spans from the formatted
tree. The resulting `SourceMap` maps any span to the nearest preceding node in the same member. The
invariant test asserts that code produced with and without the source map is byte-identical (FR-033).

**Alternatives**: marker comments stripped after formatting (fragile, formatting of trivia may
differ); `#line` directives (change the C#; reserved for P6 debugging).

### R4. RoslynPad.Editor.Avalonia

**Decision**: **not adopted in P1.** Hover quick info is implemented directly: the diagnostics
compilation (R3) already has a `SemanticModel`; on hover, `SymbolFinder`-free lookup
(`semanticModel.GetSymbolInfo(node)`) gives the symbol, displayed with
`ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)` and its XML summary through the same
`DocumentationProvider` that P1 attaches to the pack references.

**Evidence**: `RoslynPad.Editor.Avalonia` 5.0.0 (2026-05-21) depends on Avalonia 12.0.3,
AvaloniaEdit 12.0.0 and, through `RoslynPad.Roslyn` 5.0.0, on `Microsoft.CodeAnalysis.CSharp.Features`
and `.Scripting` **5.3.0**, `Microsoft.VisualStudio.Threading` 17.14 and `System.Composition`. NetPrints
pins Roslyn 5.9.0; with transitive pinning this forces Features/Scripting to 5.9 under a library built
against 5.3, and RoslynPad's host model is an editable script workspace. The cost (several large
assemblies, a Roslyn version skew, MEF host) buys completion/editing that P1 does not need (read-only).
Revisit for editable "code nodes" (roadmap: later idea).

### R5. System.Text.Json polymorphism with extension-registered node kinds

**Decision**: `NodeDocument` is an abstract record with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]`
and `[JsonDerivedType]` for the built-in kinds in `NetPrintsJsonContext`. Extension node DTOs come from
the extension's own `JsonSerializerContext`; the host combines resolvers
(`JsonTypeInfoResolver.Combine(NetPrintsJsonContext.Default, ext1, ext2 …)`) and adds each registered
`(discriminator, type)` with `WithAddedModifier` on `PolymorphismOptions.DerivedTypes` of `NodeDocument`.
Graph node lists use a `NodeListConverter` that looks up `$kind`: known kinds deserialize through the
polymorphic type info; unknown kinds become `UnknownNodeDocument(Id, Kind, JsonElement Raw)` and are
written back verbatim (FR-006). Discriminators of extension kinds are namespaced
(`<extension id>/<name>`); built-ins use short names.

**Evidence** (`p1spike/`): (1) a derived type from a second source-generated context registered at
runtime round-trips; (2) without a fallback, an unknown discriminator throws `JsonException: Read
unrecognized type discriminator id` (so the converter is required); (3) with the converter, unknown
nodes round-trip byte-identically and known kinds are unaffected.

**Alternatives**: an envelope `{kind, data: JsonElement}` per node (always two-phase, less readable
files); a single hand-written converter for all nodes (loses source-gen metadata).

### R6. Removing Fody from the core model

**Decision**: one abstract base `ModelObject` in Core, marked `[DataContract]` and CommunityToolkit
`[INotifyPropertyChanged]`; `Node`, `NodePin`, `Variable` and `Project` (the four
`[AddINotifyPropertyChangedInterface]` roots) derive from it. Notifying auto-properties become
`[ObservableProperty]` partial properties (already used in the editor with CTK 8.4.2 and C# 14);
computed properties get `[NotifyPropertyChangedFor]` or explicit `OnPropertyChanged`. `MVVMTK0032`
("use ObservableObject instead") is suppressed at that one declaration with a justification.
Which notifications Fody produced is captured *before* the change by a characterization test that sets
every public settable property of every model type and records the raised property names into a
golden file; the same test must pass after the change (FR-036). `OnInputTypeChanged` is renamed
(`HandleInputTypeChanged`) so no convention hook remains.

**Evidence** (`p1spike/`): deriving a `[DataContract]` model from `ObservableObject` throws
`InvalidDataContractException: Type 'ModelB' cannot inherit from a type that is not marked with
DataContractAttribute` — this would break the legacy XML importer (R7). A `[DataContract, INotifyPropertyChanged]`
base works: an XML written by a type without the base deserializes into the new type (the empty base
contributes no members), and `PropertyChanged` fires after deserialization.

The editor subscribes to model INPC in `NodeVM`, `NodePinVM`, `MemberVariableVM`, `ClassEditorVM`,
`MainEditorVM` and `GraphEditorView` (casts to `INotifyPropertyChanged`); these keep working unchanged.

### R7. Legacy XML importer and mapping

**Decision**: `LegacyXmlDocumentFormat` (read-only) keeps the existing `DataContractSerializer` path
(`PreserveObjectReferences`, known types) to build the model, then `ClassDocumentMapper.ToDocument`
produces the DTO. The `[DataContract]`/`[DataMember]` attributes therefore stay on the model, used
only by the importer. The type-inference relaxation in `MethodGraph.OnDeserialized` moves to a shared
`GraphTypeInference.Relax(graph)` that both the importer and `FromDocument` call. `FromDocument`
builds nodes through per-kind converters (constructor + kind data, then pin state: dynamic pin
counts, editable names, unconnected values), then connects the edge list, then relaxes types.

**Alternatives**: mirror DTO classes for the XML (large, duplicates the `z:Id/z:Ref` graph handling).

### R8. AssemblyLoadContext plugin isolation

**Decision**: a small in-repo loader (~150 lines), no package. Each extension gets a non-collectible
`ExtensionLoadContext : AssemblyLoadContext` with an `AssemblyDependencyResolver` on the extension
assembly. `Load(AssemblyName)` returns `null` (defer to Default) for **shared assemblies**: any
assembly already present in the Default context's trusted platform list or loaded there whose name is
`NetPrints*`, `Microsoft.CodeAnalysis*`, `CommunityToolkit.Mvvm`, `System.Reactive`, `DynamicData`,
`Microsoft.Extensions.*.Abstractions` or `Avalonia*`; everything else resolves from the extension
folder. This keeps type identity for `Node`, `ITypeSymbol`, `ILogger` etc. Extension projects reference
NetPrints with `Private=false`/`ExcludeAssets=runtime` and set `EnableDynamicLoading=true`. Unloading
is out of scope (hot reload of extensions is not a P1 goal; non-collectible avoids unload leaks).
Manifest `netprints-extension.json`; API compatibility = equal major of `netprintsApi`.

**Evidence**: the pattern of the .NET docs tutorial "Create a .NET application with plugins"
(learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support) and of UnrealSharp's
`PluginLoader.cs` (Plan "Findings: UnrealSharp"). `McMaster.NETCore.Plugins` 2.0.0 (2025-01-05; betas
since) solves the same problem with more features (hot reload, shared-type lists) but adds a dependency
for ~150 lines (C VIII). The test extension (`tests/NetPrints.TestExtension`) proves type identity in
tests (`typeof(Node)` from the extension equals the host's).

### R9. Logging

**Decision**: `Microsoft.Extensions.Logging.Abstractions` **10.0.12** (ships the `[LoggerMessage]`
generator) in Editor, Extensibility and Serialization (Core and Reflection stay logging-free and
return problems as data); `Microsoft.Extensions.Logging` + `.Console` 10.0.12 only in Desktop (and the CLI).
Avalonia's own log output (e.g. the P0.1 grid shader warning) is forwarded by an `ILogSink` adapter. `EditorContext` gains a required `ILoggerFactory`; tests pass
`NullLoggerFactory` or a collecting fake. `CA1848`/`CA2254` are errors in projects that log. Log
methods live in one `static partial class Log` per feature folder; event ids in contracts/editor-services.md §6.

### R10. Project layout for new code

**Decision** (paths per the reorganization PR that lands before P1, see plan.md):
- `src/NetPrints.Serialization` (new): DTOs, `NetPrintsJsonContext`, JSON and legacy XML formats,
  mappers, migrations, stores, `ProjectPersistence`. One project instead of the Plan's three
  (`.Serialization`, `.Json`, `.LegacyXml`): JSON and DataContract are both in the BCL, so separate
  packages would add projects without isolating any dependency (C VIII). A format with a third-party
  dependency (e.g. YAML) gets its own project later.
- `src/NetPrints.Extensibility` (new): extension entry point and builder, registries, manifest,
  loader, `IHostChannel`, settings store.
- Extension-point interfaces live where their consumer is: node translation and emitters in Core
  (the translator calls them), `ITypeCatalog`/composite in Reflection, node DTO converters in
  Serialization; `INodeLibrary` in Extensibility bundles them for authors.
- `tests/NetPrints.TestExtension` (new, test asset, not packed).

## 3. Package version summary (additions to `Directory.Packages.props`; see also §4 R16)

| Package | Version | Used by |
|---|---|---|
| Avalonia.AvaloniaEdit | 12.0.0 | Editor |
| AvaloniaEdit.TextMate | 12.0.0 | Editor |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | Editor, Extensibility, Serialization |
| Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Console | 10.0.12 | Desktop, Cli |
| ~~Fody, PropertyChanged.Fody~~ | removed | — |

CommunityToolkit.Mvvm 8.4.2 (now also in Core) and Roslyn 5.9.0 unchanged. Rejected:
RoslynPad.Editor.Avalonia 5.0.0 (R4), McMaster.NETCore.Plugins 2.0.0 (R8).

## 4. Risks

| ID | Risk | Mitigation |
|---|---|---|
| K1 | Mapper misses node state → C# drift | Golden C# recorded from pre-P1 code on an all-node-kinds fixture (FR-008), before any model change |
| K2 | Fody wove notifications the editor relies on implicitly | Notification-map characterization test recorded before removal (R6) |
| K3 | Formatting changes spans; source map wrong | Annotation-based mapping + byte-identity invariant test (R3) |
| K4 | Plugin type identity broken by duplicated contract assemblies | Shared-assembly rule + identity test (R8) |
| K5 | Scope: ~3.5 w manual estimate, 7 stories | Sub-phases independently green; split recommendation in plan.md |
| K6 | TextMate native dependency in the future browser build | Plain-text fallback; revisit in P5 (R2) |
| K7 | Compile semantics change for old .NET Framework projects on Windows | Converted to net10.0 with `NPM001`; documented (spec clarification) |
| K8 | Rebase onto the reorganization PR | P1 starts after it merges; all paths already use the new layout |
| K9 | Exec'd generator not verified inside Visual Studio (Windows) and Rider in CI (Linux only) | Uses only `Exec` + `DOTNET_HOST_PATH` (set by the .NET SDK in every host); one manual check per IDE recorded in the PR (R12) |
| K10 | In-process MSBuild (Locator) assembly conflicts | `Microsoft.Build*` with `ExcludeAssets=runtime` (verified MSBL001 guard), registration before any MSBuild type loads, MSBuildWorkspace build host is out of process (R14) |
| K11 | Editor now needs the .NET SDK, not only the runtime | Clear message when none is found; documented |
| K12 | Opening a project evaluates MSBuild code from it (as every IDE does); project-referenced extensions run code | Trust prompt before loading project-referenced extensions (FR-019) |


## 4. Revision: the project is an SDK-style `.csproj` (research R11–R16)

Spikes in the session scratchpad: `sdkspike/` (props/targets + an `Exec`'d net10.0 generator, built
with SDK 10.0.400) and `wsspike/` (Microsoft.Build.Locator + MSBuildWorkspace opening that project).

### R11. Package shape: PackageReference with `build/` props+targets

**Decision**: `NetPrints.Sdk` is a normal NuGet package consumed as
`<PackageReference Include="NetPrints.Sdk" Version="…" PrivateAssets="all" />`, with
`build/NetPrints.Sdk.props`, `build/NetPrints.Sdk.targets` and the generator under `tools/net10.0/`
(`DevelopmentDependency=true`, `IncludeBuildOutput=false`). Only `build/` (not `buildTransitive/`),
so consuming a NetPrints-built library does not pull code generation into its consumers.

**Rationale**: works with Central Package Management, `dotnet restore`, Dependabot/Renovate and the
`dotnet add package` flow; it is the one-line change NetPrintsUnreal needs in UnrealSharp's existing
Script `.csproj`. An additive MSBuild SDK (`<Sdk Name="NetPrints.Sdk" Version="…" />`) is not managed
by CPM (versions go inline or into `global.json` `msbuild-sdks`) and resolves before restore, which
gains nothing here (no property must be set before `Microsoft.NET.Sdk`). The same package could add an
`Sdk/` folder later if ever needed.

### R12. How the build runs the net10.0 translator: `Exec` a bundled generator

**Decision**: the `NetPrintsGenerate` target runs `"$(DOTNET_HOST_PATH)" exec "<package>/tools/net10.0/NetPrints.Generator.dll" generate "<request.rsp>"`
(fallbacks: `$(NetCoreRoot)dotnet`, then `dotnet` on `PATH`) once per build with the out-of-date graphs
only, `BeforeTargets="CoreCompile"`. No MSBuild task assembly.

**Evidence**:
- `TaskHostFactory` with `Runtime="NET"` (a .NET task hosted out of process by .NET Framework
  MSBuild) exists only from **MSBuild 18.0 / .NET SDK 10 / Visual Studio 2026**, and only for
  `Microsoft.NET.Sdk` projects (learn.microsoft.com "What's new in the SDK and tooling for .NET 10";
  "Configure targets and tasks": `Runtime="NET"` starting in MSBuild 18.0). VS 2022 (MSBuild 17.x) would
  fail to load it. dotnet/msbuild#12514 (open): the .NET task host fails for tasks with non-trivial
  dependencies (MSB4062 while MSBuild.exe inspects the task assembly); our task would carry Roslyn
  and extension assemblies.
- `Exec` only spawns a process, so it behaves the same in `dotnet build`, `msbuild.exe` (VS 2022/2026)
  and Rider. `DOTNET_HOST_PATH` is set by the .NET SDK for every SDK-style build (verified:
  `/home/…/.dotnet/dotnet` from `dotnet msbuild -getProperty:DOTNET_HOST_PATH`).
- Spike results (`sdkspike/`): first build generates both graphs and compiles them; second build logs
  *Skipping target "NetPrintsGenerate" because all output files are up-to-date*; touching one graph logs
  *Building target "NetPrintsGenerate" partially* and regenerates only that file. Found during the spike:
  the generator skips rewriting identical content (no churn in git/IDEs), so the target must `Touch`
  the outputs afterwards, otherwise unchanged-content outputs stay older than their inputs and the
  target never becomes up to date.
- Cost: one process start (~0.1 s) plus Roslyn formatting, only when a graph changed.

**Revisit** when VS 2022 support can be dropped and #12514 is fixed: the same library can then be
wrapped in a `Runtime="NET"` task without changing the targets' contract.

**Entry point**: `src/NetPrints.Generator` (Exe, net10.0; references Core, Serialization,
Extensibility) with `generate <request.rsp>` and `convert <legacy.netpp>` commands. It is an
internal build tool, not the P2 user CLI; P2's `netprints generate` calls the same
`GraphCodeGenerator` library class.

### R13. No source-generator mode (owner decision)

A Roslyn generator would need a `netstandard2.0` build of the translator (Core targets net10.0 only,
constitution IV) and cannot feed UnrealSharp's generator: generators don't see each other's output and
cannot be ordered (dotnet/roslyn#57239 "Allow for a way to ensure some source generators run before /
after others", open; #85239 "Sharing pipeline values between incremental generators", open). This
matches the plan page ("Why NetPrints can't be a source generator"). Future note only.

### R14. Project model and references: MSBuild via Microsoft.Build.Locator + MSBuildWorkspace

**Decision**: mirror UnrealSharp (`UnrealSharp.Plugins/Main.cs`: `MSBuildLocator.QueryVisualStudioInstances()`
ordered by version, `RegisterInstance(highest)`, else `RegisterDefaults()`; `UnrealSharp.Editor/SolutionManager.cs`:
`MSBuildWorkspace.Create()` + `OpenProjectAsync`). A new `src/NetPrints.Workspace` project implements
`IProjectSystem` (interface in Core) with:
- **Evaluation** (properties, `NetPrintsGraph` and `NetPrintsExtension` items): in-process
  `Microsoft.Build.Evaluation.Project` after Locator registration;
- **References, compilation options and other sources**: `MSBuildWorkspace.OpenProjectAsync`
  (Roslyn 5.9.0; its design-time build runs in Roslyn's out-of-process build host);
- **Edits** (`OutputType`, `NetPrintsProfile`, `Reference`, source directories):
  `Microsoft.Build.Construction.ProjectRootElement` (preserves formatting);
- **Restore**: `dotnet restore <csproj>` out of process when `obj/project.assets.json` is missing or
  older than the project file (MSBuildWorkspace does not restore);
- **Build/Run**: `dotnet build <csproj> -nologo -tl:off -v:quiet` and `dotnet run --project <csproj> --no-build`
  out of process; messages parsed from MSBuild's canonical format (`file(line,col): error ID: text [project]`).

**Evidence** (`wsspike/`, SDK 10.0.400, Microsoft.Build.Locator 1.11.2, Workspaces.MSBuild 5.9.0):
Locator registered ".NET Core SDK 10.0.400"; `OpenProjectAsync` took **0.8 s** and returned 167
metadata references with `System.Console.dll` from `packs/Microsoft.NETCore.App.Ref/10.0.11/ref/net10.0/`;
`GetDocumentationCommentXml()` for `Console.WriteLine(string)` returned the pack summary (**D5 solved
without custom probing**); the generated `A.netpc.g.cs`/`B.netpc.g.cs` were project documents and the
compilation had 0 errors; in-process evaluation read `OutputType=Exe` and 2 `NetPrintsGraph` items.
Gotcha: Workspaces.MSBuild brings `Microsoft.Build.Framework` transitively; Locator's MSBL001 check
fails the build unless `Microsoft.Build` **and** `Microsoft.Build.Framework` are referenced with
`ExcludeAssets="runtime" PrivateAssets="all"`.

**Consequences**: `ReferencePackResolver`, `DotNetEnvironment` probing, `ProjectCompiler`, runtimeconfig
writing and `ProjectDocument` are dropped (R1 superseded). The editor needs the .NET SDK (K11).
Locator registers one MSBuild per process, before any `Microsoft.Build` type is touched (Desktop `Main`,
test module initializers). Not WASM-safe: behind `IProjectSystem`, hosted by the P5 sidecar.

### R15. Incremental generation, nesting and "never compile twice"

Verified in `sdkspike/` (`dotnet msbuild -getItem:Compile`): the targets file (evaluated after the
SDK's default `Compile` glob) does `<Compile Remove="**/*.netpc.g.cs" />` and then
`<Compile Include="@(NetPrintsGraph->'%(GeneratedFile)')" />`, so each generated file is in `Compile`
exactly once whether or not it existed at evaluation time. `GeneratedFile` and `DependentUpon`
metadata are set with `<NetPrintsGraph Update="@(NetPrintsGraph)" GeneratedFile="%(RootDir)%(Directory)%(Filename).g.cs" DependentUpon="%(Filename)%(Extension)" />`
(an `ItemDefinitionGroup` does **not** expand `%(Filename)` — verified, it stays literal); the item
transform copies `DependentUpon` onto the `Compile` items (`B.netpc.g.cs → B.netpc.json`). Inputs =
graphs + generator + `@(NetPrintsExtension)` assemblies + the project file; Outputs = the transform, so
MSBuild's partial builds pass only out-of-date graphs. VS Code nesting:
`"explorer.fileNesting.patterns": { "*.netpc.json": "${capture}.netpc.g.cs" }`.

### R16. Packages added by the revision

| Package | Version | Used by |
|---|---|---|
| Microsoft.Build.Locator | 1.11.2 | Workspace, Desktop, test projects (registration) |
| Microsoft.CodeAnalysis.Workspaces.MSBuild | 5.9.0 | Workspace |
| Microsoft.Build, Microsoft.Build.Framework | 18.0.2 (`ExcludeAssets=runtime`, `PrivateAssets=all`) | Workspace (compile-time only; runtime comes from the SDK via Locator) |

UnrealSharp pins Locator 1.9.1 and Workspaces.MSBuild 5.6.0; NetPrints uses the latest stable (C IV).
