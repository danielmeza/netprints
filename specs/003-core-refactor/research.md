# Research: Core Refactor and Extension Points (P1)

**Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

> **Revision 2026-09-25 (owner decision): a NetPrints project is an SDK-style `.csproj`.** §5 (R11–R16)
> records the new research. It supersedes R1 (custom reference-pack resolver), the project half of U1
> (`ProjectDocument`), the ProjectCompiler part of the plan, and the project-document parts of R10.
> Everything else below still holds.
>
> **Revision 2026-09-25 (owner-approved graph-format research):** §6 (R17) folds the version-control
> changes of `docs/research/2026-09-25-graph-format/` §5–§6 into schema v1. It amends U2 and R5
> (canonical writer, tolerant reader) and nothing else.
>
> **Revision 2026-09-25 (owner-approved release and docs research):** §7 (R18, R19) folds
> `docs/research/2026-09-25-release-and-docs/` into sub-phase L. It changes the `$schema` URL of R17 and
> drops the version from the generated-file header (project-system.md §3); nothing else above changes.
>
> **Revision 2026-09-25 (owner decision): Snowflake-style ids.** §8 (R20) replaces R17's random-30-bit
> id value and "duplicate → `DocumentFormatException`" rule with a monotonic 63-bit value and load-time
> duplicate repair. R17's alphabet, prefix, graph-key and pin-key decisions are unchanged.
>
> **Revision 2026-09-26 (owner decision): no legacy conversion, strict ids.** §9 (R21) drops the
> user-facing legacy import and conversion, migrates the repository's own legacy files once, and makes
> the id shape strict on read and in the schema. It supersedes U3, R7, K7 and parts of R12, R17, R20
> (marked inline).

Owner rule for this phase: **reuse, don't reinvent**. §1 lists every decision taken from existing
sources. §2 records the research done only for questions those sources left open. Spikes ran on Linux
with SDK 10.0.400 in the session scratchpad (`p1spike/`, `aespike/`), outside the repository. Package
versions were checked against the nuget.org flat-container and registration APIs on 2026-09-25.

Abbreviations: **Plan** = the plan page (`.agent-archive/2026-09-25-session-c18f4e98/netprints-unreal-plan.html`);
**RM** = `.specify/memory/roadmap.md`; **C** = constitution 1.2.1; **P0-R** = `specs/001-modernize-build/research.md`;
**P0-Rev** = PR #1 review follow-ups (archive `reviews.md`, `comments.md`); **UX** = `docs/research/2026-09-25-ux-audit/`;
**Grid** = `specs/002-grid-rendering/research.md`.

## 1. Decisions reused from existing sources

| # | Decision | Source |
|---|---|---|
| U1 | Three serialization layers: versioned cycle-free DTOs (`ClassDocument(SchemaVersion, Header, Graphs, Variables)`), `IDocumentFormat` (Id, Extensions, Read/Write), `IDocumentStore` (OpenRead/OpenWrite/List + `IObservable<DocumentChange> Changes`), `IDocumentMapper` (ToDocument/FromDocument); migrations upgrade old `SchemaVersion`s before mapping | Plan "Serialization"; C VI, VII |
| U2 | JSON via System.Text.Json source-generated `JsonSerializerContext`; polymorphic nodes with `[JsonDerivedType]` or a resolver extensions add to; positions stored separately from logic | Plan "Serialization"; C tech constraints |
| U3 | Legacy DataContract XML becomes import-only *Superseded by R21: no legacy import; the repository's own files are migrated once.* | C tech constraints; RM P1 |
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
nodes round-trip byte-identically and known kinds are unaffected. *Amended by R17: unknown nodes are
re-emitted through the canonical writer (byte-identical for canonical input), and `$kind` may appear
anywhere in the object on read (`AllowOutOfOrderMetadataProperties`).*

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

### R7. Legacy XML importer and mapping — *superseded by R21 (no user-facing import; the importer only serves the one-time migration, then is deleted)*

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
- `src/NetPrints.Serialization` (new): DTOs, `NetPrintsJsonContext`, JSON and legacy XML formats (the
  legacy XML format is removed by R21),
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

## 3. Package version summary (additions to `Directory.Packages.props`; see also §5 R16)

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
| K5 | Scope: ~3.5 w manual estimate, 7 stories (the graph-format changes add ~3 d) | Sub-phases independently green; one PR, opened as a draft after sub-phase E (plan.md) |
| K6 | TextMate native dependency in the future browser build | Plain-text fallback; revisit in P5 (R2) |
| K7 | Compile semantics change for old .NET Framework projects on Windows | *Retired by R21: old projects are not converted.* (Was: converted to net10.0 with `NPM001`) |
| K8 | Rebase onto the reorganization PR | P1 starts after it merges; all paths already use the new layout |
| K9 | Exec'd generator not verified inside Visual Studio (Windows) and Rider in CI (Linux only) | Uses only `Exec` + `DOTNET_HOST_PATH` (set by the .NET SDK in every host); one manual check per IDE recorded in the PR (R12) |
| K10 | In-process MSBuild (Locator) assembly conflicts | `Microsoft.Build*` with `ExcludeAssets=runtime` (verified MSBL001 guard), registration before any MSBuild type loads, MSBuildWorkspace build host is out of process (R14) |
| K11 | Editor now needs the .NET SDK, not only the runtime | Clear message when none is found; documented |
| K12 | Opening a project evaluates MSBuild code from it (as every IDE does); project-referenced extensions run code | Trust prompt before loading project-referenced extensions (FR-019) |
| K13 | **Open item.** Event methods are emitted in `EventGraph.Entries` node order, so `nodes` order stays semantic: two branches that each append an entry conflict at the `nodes` tail, and the resolved order decides method order in the C# | Kept for P1 (graph-format research §5.4, §7); a test pins the current order (T079). Later: translation order independent of storage order, then `nodes` sorted by id |
| K14 | **Resolved (T041).** `JsonSchemaExporter` output for STJ polymorphism (`anyOf`/`const` for `$kind`), for extension kinds, and for `required` was verified and fixed where wrong | See findings in §6 (R17) below |
| K15 | Pin keys come from constructor-assigned pin names, so renaming a built-in pin (or an extension's) breaks existing files | Golden list of every built-in pin reference (DF-T19); a rename requires a schema migration |
| K16 | The custom canonical writer could emit invalid or nondeterministic JSON | Every test re-parses written bytes (DF-T03, DF-T04); the writer only formats a `JsonNode` tree STJ produced |
| K17 | Random ids make fixtures nondeterministic | `IdGeneration.Use(new SeededIdGenerator(seed))` in tests; the one-time migration is deterministic (seeded node and member ids, R21) |


## 5. Revision: the project is an SDK-style `.csproj` (research R11–R16)

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
Extensibility) with a `generate <request.rsp>` command (a `convert <legacy.netpp>` command was dropped by
R21). It is an
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

## 6. Revision: graph format for version control (research R17)

### R17. Canonical JSON with stable identity

**Source**: `docs/research/2026-09-25-graph-format/README.md` (owner-approved 2026-09-25): §5 lists the
rules, §6 the spec changes, §7 the risks. **Decision**: keep System.Text.Json and schema v1, and fix the
four identity and line-layout hazards before any v1 file exists: sequential node ids, index-based pin
references, index-based graph keys, and STJ's one-scalar-per-line indentation. Custom DSL, YAML, TOML and
KDL are rejected (research §1, §5.1). Normative text: document-format.md §1.1, §1.4.1, §1.4.2, §2.3.1,
§2.6, §2.8, §6; data-model.md §2; project-system.md §1.1.

Details the research left open, decided here:

| Question | Decision |
|---|---|
| Id alphabet and length | `n`/`m` + 6 characters of lowercase Crockford base32 (`0123456789abcdefghjkmnpqrstvwxyz`), about 30 bits; uniqueness checked in the graph (nodes) or class (members) (revised by R19: Snowflake ids, no allocation-time check) |
| How node constructors get a generator | Ambient `IdGeneration.Current` (`AsyncLocal`, default `Random.Shared`), scoped with `IdGeneration.Use` in tests; no constructor changes |
| Legacy member ids | Deterministic: `SeededIdGenerator(FNV-1a-32(class full name))`, variables, then methods, then constructors. Nodes stay `n<index>` (revised by R21: used only by the one-time migration, which also rewrites node ids to seeded Snowflake ids; then removed) |
| Accessor graphs | No id of their own; keys `<variableId>/get`, `/set`, `/type` |
| Pin key for user-renamable pins | Positional name `Input<i>` / `Output<i>` (entry argument and return pins), the pin `Name` for all others; `~n` for duplicates; implemented by `Node.GetPinKeyName`, overridable by extensions |
| Old index form `in.data.0` on read | Rejected (treated as an unknown key, `NPD002`/`NPD003`): no v1 files exist, and one grammar avoids ambiguity |
| Layout value type | `int[2]`; model doubles rounded `MidpointRounding.AwayFromZero` on write; a non-integer or wrong-length position → `DocumentFormatException` |
| Auto-placement | Next to the first placed neighbour (upstream +300 px, downstream −300 px), else a column right of the graph; collisions push down by 120 px (data-model.md §2) |
| Inline records | Chosen by property name in the `JsonNode` tree (document-format.md §2.3.1), not by CLR type, so the writer needs no type information and extension documents follow the same rule; `Utf8JsonWriter.WriteRawValue` was not used because its interaction with indentation is unverified |
| String escaping | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (readable generic names and non-ASCII text) |
| `$schema` value | `https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json` (revised by R18: the GitHub Pages site publishes `schemas/` under `/schemas/`; previously a `raw.githubusercontent.com` URL); ignored on read |
| Unknown properties on read | Ignored; dropped on the next save of that class |
| Missing or duplicate ids on read | `DocumentFormatException` (nodes and members alike) (revised by R19: missing still throws; duplicate is repaired — the later occurrence gets a fresh id and an `NPD007` warning — instead of failing the whole load; revised by R21: a present id that does not match `IdFormat` is replaced the same way and reported as `NPD009`) |
| Dirty tracking | Explicit `ClassGraph.IsDirty`, set by the editor on every undoable command, node move and inspector edit; `SaveAsync` maps and writes only dirty classes, so no ripple saves (research §5.2 rule 12) |
| Missing layout / NPD codes | New issue codes `NPD003` (pin state dropped) and `NPD004` (layout entry ignored) |
| `.gitattributes` | Per project folder, two `eol=lf` lines, created or appended by `CreateAsync` (and `ProjectConverter`, dropped by R21); no `linguist-generated`, so `.netpc.g.cs` diffs stay visible in PRs |
| Stale checks | Tests (run by CI): committed schema = generated (DF-T24); committed sample graphs canonical and `.netpc.g.cs` = regenerated (DF-T26). CLI `format --check` / `regen --check` are P2 |
| Merge driver | `netprints merge` / `git-install` / `textconv` diff: follow-up after P1 (research §3, §5.4) |

**Findings recorded during implementation (T041, K14).** `JsonSchemaExporter.GetJsonSchemaAsNode` over
`ClassDocument`, given `NetPrintsJsonContext.Default.Options` (built-in kinds only), already produces the
right *shape* for polymorphism with no help: the `NodeDocument` schema is `{"type":"object","required":
["$kind"],"anyOf":[...]}`, one branch per `[JsonDerivedType]` (24), each an inline object with its own
`properties`/`required` (id/name/pins duplicated into every branch rather than shared via `$ref`/`allOf`
with a base) and `"$kind":{"const":"<kind>"}`. `GraphDocument.Nodes`'s custom
`[JsonConverter(typeof(NodeListConverter))]` does not change this: the exporter never asks a converter
how it reads/writes and simply describes `NodeDocument`'s own declared contract, so the schema for
`nodes[]` is exactly as if no custom converter were registered. The one addition `TransformSchemaNode`
makes is the extension branch: when `context.TypeInfo.Type == typeof(NodeDocument)`, append `{"type":
"object","required":["$kind","id"],"properties":{"$kind":{"type":"string","pattern":"/"}}}` to that
`anyOf` array (document-format.md §6's exact literal) — a generic "`$kind` contains `/`" branch, since
loaded extensions never contribute to this committed file (only `NetPrintsJsonContext`'s built-in types
are exported).

`required` needed a real fix. The exporter marks a member required purely from whether its record's
positional constructor parameter has a C# default value — independent of nullability and of
`[JsonIgnore(Condition=…)]`. This over-includes two families: every `ClassDocument`/`MethodDocument`/
`ConstructorDocument`/`VariableDocument`/`EventGraphDocument`/`NodeDocument`-common member that has no
C# default (which is all of them, since this codebase relies on the class-wide `DefaultIgnoreCondition =
WhenWritingDefault` plus a per-member `[JsonIgnore(Condition = Never)]` opt-in for Req. = yes, document-
format.md §1.1 — not on C# default parameter values), so e.g. `namespace`, `modifiers`, `pins` and
`layout` all came out required; and several §1.6 DTO members that are nullable but were never given an
explicit `= null` (`VariableRef.DeclaringType`, `MethodRef.Parameters`/`ReturnTypes`/`GenericArgs`,
`ConstructorRef.Parameters`, `PinStateDocument.Name`/`Value`), which came out required even though the
model omits them whenever unset. Fix, in `NetPrintsJsonSchema.FixRequired`: for a type where at least one
member carries `[JsonIgnore(Condition = Never)]` (every graph/member/node-common type above), `required`
is recomputed from exactly that attribute, discarding the exporter's own list entirely; for a type with
no such member anywhere (the §1.6 reference/value DTOs, which rely on nullability and real C# defaults
instead), the exporter's own list is kept minus any member that is nullable — checked with
`NullabilityInfoContext` against the member's underlying `PropertyInfo` (`JsonPropertyInfo.AttributeProvider`),
**not** by inspecting the already-generated child schema node for a `"null"` in its `"type"`: a shape that
repeats (e.g. `TypeRef`, `MethodRef.parameters`) is rendered once and every later occurrence is a bare
`{"$ref": "..."}` with no `type` keyword at all, so a schema-based nullability check silently failed to
fire for exactly the repeated, over-included members it needed to fix. Verified against
`MethodDocument` (DF-T24: `required` = `["id","name","visibility","graph"]`, `modifiers` excluded) and,
manually, against every §1.6 DTO (`TypeRef` → `["name"]`, `MethodRef` → `["name","declaringType",
"modifiers","visibility"]`, `ParameterRef` → `["name","type"]`, `ConstructorRef` → `["declaringType"]`,
`VariableRef` → `["name","type","getterVisibility","setterVisibility","visibility","modifiers"]`,
`ConnectionDocument` → `["from","to"]`, `PinStateDocument` → `["pin"]`, `LocalVariableDocument` →
`["name","type"]`).

Two accepted, documented limitations of this fix, neither exercised by a test: (1) because only
`NodeDocument.Id` carries `[Never]`, every node-kind branch falls into the "recompute exhaustively from
`[Never]`" case and ends up with `required: ["id"]` only — a kind-specific field that is, in practice,
always present (`callMethod.method`, `eventEntry.eventName`) is not marked required, since no individual
kind field opts into `[Never]`. This underclaims rather than overclaims (a conservative schema, not a
wrong one); marking these `[Never]` later, if a validator needs the stronger guarantee, would also force
them to always be written (bigger file diffs) and is left as a follow-up, not done here. (2)
`TypedValue.Value` (`string?`, no `[Never]`) is excluded from `required` by the same nullability rule,
even though document-format.md §1.6 describes it as "string or JSON null" (implying always-present); in
fact the model's own `DefaultIgnoreCondition = WhenWritingDefault` would omit the `value` property
entirely when it is null (no override forces it), so the generated schema matches the model's actual
runtime behavior — the prose's "always present" framing is the part that is arguably imprecise, not the
generated schema.

**Findings recorded during T054b (id patterns, research R21).** `TransformSchemaNode` distinguishes a
node id from a member id purely by `context.PropertyInfo.DeclaringType`: every concrete node-document
record (`MethodEntryNodeDocument`, `CallMethodNodeDocument`, …) passes its `Id` constructor parameter
straight to the shared base (`NodeDocument(Id, Name, Pins)`) rather than redeclaring the property, so
`DeclaringType` is `typeof(NodeDocument)` for all 24 of them plus `UnknownNodeDocument` — one check
covers every built-in kind, no per-kind branch needed. The four member-document types
(`VariableDocument`, `MethodDocument`, `ConstructorDocument`, `EventGraphDocument`) each declare their own
`Id`, so their `DeclaringType` is the type itself; a small lookup table (`MemberDocumentTypes`) covers all
four. `ConnectionDocument.From`/`.To` are matched by `Name is "from" or "to"` plus `DeclaringType ==
typeof(ConnectionDocument)` (the only type with properties of those names). The extension `anyOf` branch
(added in T041) had no `id` property schema at all before this task — only `required: ["$kind","id"]` —
because nothing needed one until ids became strict; T054b adds `"id": {"type":"string","pattern":
IdFormat.PatternFor('n')}` to it. `layout`'s own two nesting levels are matched by `context.TypeInfo.Type`
alone (`SortedDictionary<string, SortedDictionary<string, int[]>>` for the root, `SortedDictionary<string,
int[]>` for a per-graph position map): each type is used in exactly one place in the whole document model,
so no property-name check is needed to disambiguate. One real bug caught by inspecting the regenerated
file, not by a failing test: a first attempt built the connection-endpoint pattern with a helper that
stripped *both* anchors from `IdFormat.PatternFor('n')` before splicing in the pin-reference suffix,
silently dropping the leading `^` (`"n[0-9a-hjkmnp-tv-z]{13}/(in|out)\.(exec|data|type)\..+$"` instead of
`"^n…"`) — a JSON Schema `pattern` is an unanchored substring search, so the missing `^` would still have
passed every real id but is a real, if subtle, weakening of the constraint; fixed with a second helper
(`StripTrailingAnchor`, one character off the end only) so the leading anchor survives. Separately, the
default `JavaScriptEncoder` escapes `+` as `+` (the connection pattern's own "one or more" quantifier
is the first `+` this file has ever contained); `GenerateV1`'s writer options now set
`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, matching the canonical document writer's own relaxed
escaping (document-format.md §2.3) and keeping the committed schema's diff readable.

## 7. Revision: release, packages and docs (research R18–R19)

### R18. Release and docs stack (reused from the owner-approved research)

**Source**: `docs/research/2026-09-25-release-and-docs/README.md` (approved 2026-09-25), §10 summary; the
owner's `kicad-sharp` repository for the concrete shapes (release jobs, `NuGet.config` + git-ignored
`local-packages/` + `.gitkeep`, a pack script with a `0.1.0-local.<timestamp>` version, README packed from
`Directory.Build.targets`). Normative text: contracts/release-and-docs.md.

| Topic | Decision (runner-up in the research) | Detail decided here |
|---|---|---|
| Versioning | MinVer 8.0.0, `v` prefix, minimum 0.1, `GlobalPackageReference` (tag → `-p:Version`) | local packs use `MinVerVersionOverride` because a global `Version` does not stop MinVer from setting `PackageVersion`; `MINVER1001` (no `.git`, source archives) is not an error; the version is read with `-t:MinVer -getProperty:MinVerVersion` |
| Generated header | — | no NetPrints version in `.netpc.g.cs` headers: every commit has its own MinVer version, and committed generated files would otherwise change on each update and make DF-T26 depend on git height |
| Pipeline | plain GitHub Actions + `scripts/*.sh` (Cake.Sdk `build.cs`) | same scripts in CI and locally: `pack-local.sh`, `verify-packages.sh`, `smoke-desktop.sh`, `archive-desktop.sh`, `build-docs.sh` |
| Packages | Core, Reflection, Sdk, Cli (`netprints` tool) | id `NetPrints.Cli` for the tool (kicad-sharp: `KiCadSharp.Cli` → `kicadsharp`), all under one `NetPrints.` prefix; the SDK package has no symbol package (NU5017 without build output); package validation on the two libraries only |
| Package README | root README, packed | generated at pack time from the root README (HTML → Markdown, relative links pinned to the commit; readme-craft `PackageReadme.targets`), because nuget.org drops HTML and relative links |
| Publishing | Trusted Publishing, environment `release` | kicad-sharp job shape; a first step names the missing `NUGET_USER` secret |
| Desktop | self-contained folder archives, SHA256SUMS, `actions/attest` (Velopack next) | `osx-arm64` on `macos-latest` for the ad-hoc signed apphost; win-x64 cross-published on Linux and not smoke-tested (no Windows runner in P1) |
| Dry runs | `workflow_dispatch` | plus a path-filtered `pull_request` trigger, because `workflow_dispatch` works only once the file is on the default branch, and the P1 PR must show a green dry run |
| Docs | Docusaurus 3 in `website/` reading `../docs` (DocFX only) | `numberPrefixParser: false` so `adr/0002-…` and `research/2026-09-25-…` keep their names; a small remark plugin rewrites links that leave `docs/` to GitHub URLs; `specs/` is not published |
| API reference | DocFX `modern` at `/api/` (DefaultDocumentation) | DocFX 2.81.0 as a local tool (`.config/dotnet-tools.json`); packable libraries only |
| Research notes | — | published as a "Research notes" category with a generated index that says they are dated and not updated; prototypes excluded |
| Wiki | two-page pointer via github-wiki-action (curated mirror) | `strategy: init`, gated by `vars.PUBLISH_WIKI` and a `git ls-remote` check; no PR trigger |
| Publishing gates | — | Pages deploy gated by `vars.PUBLISH_DOCS`, wiki by `vars.PUBLISH_WIKI`, nuget.org and the GitHub Release by `v*` tags; nothing publishes from P1 |
| `$schema` URL | — | `https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json`, copied from `schemas/` by `build-docs.sh`; replaces the `raw.githubusercontent.com` URL of R17 (a Pages URL survives a repository layout change and serves `application/json`) |

**Constitution note**: the release workflow uses a macOS runner for the `osx-arm64` archive. The main CI
workflow (`CI`) stays Linux-only as the constitution requires; its two new jobs (`packages`,
`desktop-publish`) run on `ubuntu-latest`. Proposed PATCH clarification for the coordinator: "release
packaging workflows may use macOS or Windows runners; they are not the main CI".

### R19. Reference assemblies in a self-contained editor

**Question** (owner): `ReferenceAssemblyResolver.FindRuntimeAssemblyPaths()` enumerates `*.dll` in
`RuntimeEnvironment.GetRuntimeDirectory()`, which is empty in a single-file bundle and is the editor's own
folder in a self-contained publish. Should the editor use `Basic.Reference.Assemblies.Net100` or the
reference-pack approach already in the spec?

**Decision**: the approach already in the spec, R14: references are what MSBuild resolves for the open
project, which for the framework means the installed SDK's `packs/Microsoft.NETCore.App.Ref/<ver>/ref/net10.0/`.
`Basic.Reference.Assemblies.Net100` is not added. Release archives are self-contained folders, never
single-file or trimmed.

**Rationale**:
- The old fallback is gone after P1: T063 deletes `ReferenceAssemblyResolver` and `CodeCompiler`; live
  analysis (`CodeAnalysisSession`) takes `ProjectSnapshot.References` and builds run `dotnet build`. Neither
  depends on the editor's install layout, so the self-contained publish does not change what a graph compiles
  against. RL-T07 keeps it that way.
- The editor needs the .NET 10 SDK anyway (FR-011, FR-015, K11): to evaluate, restore and build the `.csproj`.
  A bundled reference set would only help a "no project" case that no longer exists.
- It would make analysis disagree with the build: `Basic.Reference.Assemblies.Net100` 1.8.12 is one fixed
  `net10.0` BCL set (a 2.2 MB package with one 6.3 MB assembly that embeds the reference DLLs as resources;
  checked 2026-09-25), with no packages, project references, other shared frameworks or future TFMs. FR-011
  and FR-032 require the same references as `dotnet build`.
- It ships no XML documentation (the package holds the assembly only), so tooltips (FR-014, D5) would need a
  second source; the pack references come with their `.xml` files (R14 evidence).

**Constraints found**:
- Microsoft.Build.Locator skips SDKs whose major.minor is newer than the running runtime
  (`DotNetSdkLocationHelper`; microsoft/MSBuildLocator#320, maintainer answer). The self-contained editor runs
  on its bundled 10.0 runtime, so it needs a **10.0** SDK; with only a newer SDK installed it shows NPW001.
  Documented on the install page; a framework-dependent build with roll-forward is a later option.
- `MSBuildWorkspace` starts its build host from `BuildHost-netcore/` next to the application; the folder is
  copied only for a direct `Microsoft.CodeAnalysis.Workspaces.MSBuild` reference (dotnet/roslyn#80127, open).
  Desktop and Cli reference the package directly; RL-T03 and RL-T06 check the folder.
- Running a compiled graph needs a .NET runtime; the required SDK provides it. Stated in the README, the
  install page, the release notes and the archive's `README.txt` (FR-058).

**Evidence in CI**: the `desktop-publish` job publishes the editor self-contained for linux-x64 and runs
`NetPrints.Desktop --check-project samples/HelloWorld/HelloWorld.csproj --run` without a display: MSBuild
registration, evaluation, `MSBuildWorkspace` references, in-process Roslyn analysis of the translated graphs,
`dotnet build` and the run (RL-T06). The release workflow repeats it for linux-x64 and osx-arm64.

**Packages** (additions to `Directory.Packages.props`): `MinVer` 8.0.0 (`GlobalPackageReference`). Tools:
`docfx` 2.81.0 (local tool). Node packages: `website/package.json` (Docusaurus 3.10.2, React 19.3).

## 8. Revision: Snowflake ids (research R20)

### R20. Node/member id format: Snowflake-style ids replace random Crockford32 (owner decision 2026-09-25)

**Decision**: the id *value* is a 63-bit non-negative `long` — 41 bits of milliseconds since
`2026-01-01T00:00:00Z`, 16 bits of session id, 6 bits of per-millisecond sequence — generated
monotonically per `IIdGenerator` instance (ULID-style: a sequence overflow or a clock that reads at or
before the last-used time advances a logical millisecond instead of reusing or going back). The text
form is unchanged in shape (a one-character prefix, `IdFormat.Alphabet`) but longer: 13 digits instead
of 6, zero-padded so string order equals numeric order — 14 characters total, `IdFormat.Pattern`
(`^[nm][0-9a-hjkmnp-tv-z]{13}$`). Normative text: data-model.md §2 (full rationale), contracts/document-format.md
§1.4.1, §2.6, §6. Implemented in tasks.md T040a–T040c, between T040 and T041.

**Rationale**: a random 30-bit id (the original R17 choice) already made an allocation-time collision
astronomically unlikely, so this is not a correctness fix; it is a size/ordering upgrade the owner asked
for once ids might also need to work as a database key: sortable (string order and numeric order agree,
so ids sort correctly in a directory listing, `git log`, or a SQL `ORDER BY` without a separate sequence
column), fits a bigint column directly (no encoding round trip), and remains unique by construction (a
real clock plus a per-instance session make a same-millisecond, same-sequence collision across two
generator instances need both to share a session, which does not happen by default). Because it stays
unique by construction, `NodeGraph.AllocateNodeId` drops the retry loop entirely (T040b) — the loop
existed only to guard against the vanishingly small chance a 30-bit random draw repeats, and a
monotonic 63-bit value removes even that.

**Alternatives considered**: keeping the 30-bit random id (rejected — the owner specifically wants
sortability and a bigint-sized value, neither of which a random id gives); a UUID/GUID (rejected — 128
bits is more than the owner asked for and is not sortable without a variant like UUIDv7, which is
strictly more complex than a purpose-built Snowflake for a single-process, single-repository id space);
a plain incrementing counter (rejected — the entire point of R17 was to remove sequential/positional
ids as a merge hazard, §5.2 hazard 1; a counter reintroduces exactly that).

**Constraint found** (*superseded by R21: ids are strict on read and in the schema; no legacy ids remain*): the id format's own "accepted on read" rule (document-format.md §1.4.1) — any
non-empty string without `/` or whitespace — already permits ids that don't match the *creation* shape,
which legacy-imported node ids (`AssignLegacyNodeIds`'s `n0`, `n1`, …) rely on and keep forever once
converted to v1 JSON. `IdFormat.Pattern` describes only what a freshly generated id looks like; T041's
JSON Schema must not turn it into a read-time `pattern` constraint on `id` fields, or it would reject
already-shipped, legitimately-imported documents (document-format.md §6, flagged there for T041).

**Load-time duplicate ids**: R17's original "duplicate → `DocumentFormatException`" (research.md §6
table) is also revised: a merge or a hand-edited/copy-pasted file can still produce two nodes (in one
graph) or two members (in one class) sharing an id even with Snowflake ids. Failing the whole load over
one duplicate is worse than necessary now that repairing it is cheap and safe: the later occurrence
(document order) is reassigned a fresh id (`StableIds.AllocateUnique`, a bounded retry against the ids
already seen in *this* document — the one place a search is still correct, since the generator cannot
know about ids it did not itself allocate) and reported as a `DocumentIssue.DuplicateIdReassigned`
(`NPD007`) warning instead. A duplicate's own connections and layout entries that reference the
now-stale (shared) id text resolve to whichever occurrence still holds that text (the first one seen,
deterministically) — an accepted, documented limitation (implementation-notes.md, this task group's
entry) rather than an attempt to reconstruct ambiguous merge intent from id text alone.

## 9. Revision: no legacy conversion, strict ids (research R21)

### R21. Legacy `.netpp`/`.netpc` conversion is not a feature; ids are always Snowflake ids (owner decision 2026-09-26)

**Decision**: NetPrints never had enough users for real projects in the old format to exist, so P1 ships
no user-facing way to read or convert DataContract XML projects: no `ProjectConverter`, no generator or CLI
`convert` command, no "open legacy project" in the editor, no `LegacyXmlDocumentFormat` in any
`DocumentFormatRegistry` a user reaches, no `NPM` codes. Opening a `.netpp` is rejected like any other
file that is not a `.csproj`. The repository's own legacy files (`samples/HelloWorld`, the characterization
fixtures `AllNodes` and `HelloWorld` under `tests/NetPrints.Core.Tests/Fixtures/Legacy/`) are migrated
**once**, inside P1 (tasks.md T054a), by a throwaway test-scoped converter built on the code that already
exists (`LegacyXmlDocumentFormat` + `DocumentMapper` + `JsonDocumentFormat`); the results are committed and
every test and golden reads them. Then the legacy code goes (T062a: `LegacyXmlDocumentFormat`, the
`Legacy/` DataContract copies, `AssignLegacyNodeIds`/`AssignLegacyMemberIds`, `StableIds.SeedFor`, the
legacy fixtures and their tests; T063: the DataContract persistence of the model itself).

Ids become strict (T054b): every node and member id in a document must match `IdFormat` for its prefix
(`^n[0-9a-hjkmnp-tv-z]{13}$` for nodes, `^m…` for members). The generated JSON Schema carries these
patterns on `id` fields and on the places that reference ids (connection endpoints, `layout` keys). The
reader does not accept other shapes any more: a present id that does not match is replaced by a fresh id,
references to it in the same document (connections, `layout`) follow the replacement, and the load reports
`NPD009` (Warning), the same repair-and-report approach as duplicate ids (`NPD007`, R20). A missing id is
still a `DocumentFormatException`.

**Rationale**: the legacy path cost more than it bought: a second read pipeline (DataContract with
reference preservation, `[OnDeserialized]` hooks, `[OnDeserializing]` initializers for every new member),
DataContract copies of the old project classes, positional legacy ids (`n0`, `n1`, …) that forced the
reader to accept any non-slash, non-whitespace id forever, a converter with rollback, four `NPM` codes, two
log events, an editor dialog and a CLI command, all for files nobody has. Without it, ids have one shape,
the schema can check it, and the model can drop DataContract entirely.

**Migration details (T054a)**: the characterization intent of sub-phase A holds: the C# generated from
the migrated JSON must equal the goldens recorded from the pre-P1 code byte for byte; the golden files are
not regenerated during the migration (a golden diff means the migration is wrong). Legacy node ids are
rewritten to Snowflake ids from `SeededIdGenerator(StableIds.SeedFor(<class full name> + "/nodes"))` in
graph order (a seed distinct from the member-id seed),
so re-running the converter gives the same bytes; member ids already come from the seeded generator
(`AssignLegacyMemberIds`) and match `IdFormat`. Project files are written by hand from the `.netpp`
settings (they are two small `.csproj` files, not worth converter code). `AllNodes.csproj` drops the
reference to `ExternalLibrary.dll` (the file never existed; the item only exercised reference conversion)
and the `.NETFramework` references, like the dropped conversion would have.

**Alternatives considered**: keep the importer but hide it (still forces loose ids and DataContract on
the model); keep `ProjectConverter` as an internal tool (same cost, no user); regenerate the fixtures from
the factories straight to JSON (loses the "same files the pre-P1 code read" link that makes the goldens a
proof; `AllNodesFixtureFactory` stays only as an in-memory model builder for tests).

**Superseded by this revision**: U3 (import-only legacy XML), R7 (legacy importer), K7, K17's legacy half,
the R17 rows "Legacy member ids" and the `.gitattributes` writer list, the `convert` command of R12, and
R20's "Constraint found" paragraph (the looser "accepted on read" id rule it protected is gone).
