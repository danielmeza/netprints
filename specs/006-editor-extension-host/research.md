# Research: Editor Extension Host (P3)

Phase 0 of the plan. Each item records the decision, the reason and the alternatives. Most of P3's design was
researched, audited and decided by the owner before the spec; this file reuses that work and cites it instead of
repeating it. New research was done only where the spec left a real unknown; those items say so.

The design records live in the workspace's `.agent-archive/` folder, one level above this repository, and are cited
by path as text:

| Record | What P3 takes from it |
|---|---|
| `.agent-archive/2026-10-08-aop-plan/report.md`, `followup.md`, `decisions.md` | S1 infrastructure, NPS/NPA/NPH families, declarative pins by convention, pin-key tracking, the 0.3.0 renames, L1, S5, S6, S7, S10; owner decisions 1–13 |
| `.agent-archive/2026-10-08-solid-plan/report.md`, `report-2.md`, `decisions.md` | SUG1–SUG5, R2, R4, R5, R6, R7, N1–N9, the M1–M5 measurement plan; owner decisions 1–15 and the DI reopening |
| `.agent-archive/2026-10-13-theme-presets/decisions.md` (with `report.md`, `audit.md`) | B0, B1, C; preset list; Semi pass-through; contrast policy |
| `.agent-archive/2026-10-13-details-panel/decisions.md` (with `report.md`, `audit.md`) | R6 widened into the value-editor contract; the grid's P3-tail-or-P3b placement; inspector tokens |
| `.agent-archive/2026-10-14-attributes-langversion/report.md`, `audit.md`, `sources/` | `CSharpTarget`, `CSharpFeature`, `Requires`, filtering and lowering policy |
| `.agent-archive/2026-10-14-run-profile-kinds/report.md`, `audit.md`, `sources/` | the run profile kinds and their prior art (ADR-0037) |
| `docs/research/2026-09-29-extension-testing/README.md` (in the repository) | the twelve conformance checks, the noisy neighbour, the verifier |
| `docs/research/2026-10-14-engine-integration/` (in the repository) | the Godot project shape the sample extension's template uses |

The ADRs these records produced are in the repository: ADR-0003, ADR-0010, ADR-0017, ADR-0020, ADR-0021, ADR-0023,
ADR-0031, ADR-0033 to ADR-0040.

## R1. Scope

- **Decision**: P3 is the spec's 12 active stories (US1–US11, US15). US12–US14 and FR-089–FR-106, SC-012–SC-014
  moved to P3c on 2026-10-16 and are not planned. The tail (US16, US17, FR-109–FR-115: R6, N2, N5, S7, SUG5, B1 and C)
  moved to P3b on the same day, so the Checkpoint F scope rule is gone; P3b decides whether any of it goes to P6. The
  release preparation of the old sub-phase G stays in P3. No other cut is allowed (Clarifications answer 1, narrowed
  on 2026-10-16). R6, R14, R16 and R24 below describe the designs P3b picks up; P3 builds only the parts they name for
  A–F.
- **Rationale**: the owner placed these items in P3 and named the only fallbacks.
- **Alternatives rejected**: splitting P3 into more phases now (the owner confirmed one spec and one PR on
  2026-10-16); planning the P3c items "in case they fit" (that would re-scope silently).

## R2. Analyzer and generator projects (S1, ADR-0024)

- **Decision**: four new `netstandard2.0` Roslyn projects (the constitution IV exception), following the AOP plan §3.1:
  - `src/NetPrints.Authoring` (**shipped** inside the `NetPrints.Core` package under `analyzers/dotnet/cs`): the pin
    convention generator, the NPA analyzers, and later the S7 node-kind generator. It sees the same Core it was
    shipped with, so the attributes and the generator never skew (the ADR-0022 lesson).
  - `src/NetPrints.Authoring.CodeFixes` (shipped next to it): the NPA code fixes. RS1038 keeps the Workspaces
    reference out of the assembly the compiler loads.
  - `src/NetPrints.RepoAnalyzers` (**internal**, `IsPackable=false`): the NPS style rules, the NPH hygiene rules, the
    `[SmartEnum]` generator and the DI registration generator with its NPI analyzers. Its attributes are emitted as
    `internal` post-initialization sources, so no runtime attribute assembly is added.
  - `src/NetPrints.RepoAnalyzers.CodeFixes` (internal): the NPS fixes that the sweep runs through
    `dotnet format analyzers`.
  - `src/Shared/Roslyn/` holds `EquatableArray` and the diagnostic helpers, linked into `NetPrints.Annotations` and the
    new projects (Annotations already links Catalog sources this way).
  - Wiring: `src/Directory.Build.props` and `tests/Directory.Build.props` add the RepoAnalyzers projects with
    `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`; `NetPrints.Core.csproj` adds Authoring the same way,
    and `tests/Directory.Build.targets` adds it to every project that references Core (analyzers do not flow through
    `ProjectReference`).
  - Tests: one new project, `tests/NetPrints.Analyzers.Tests`, for every analyzer and generator NetPrints owns. The
    `GeneratorTestHost` now inside `NetPrints.Catalog.Tests` moves to `tests/NetPrints.Testing/Roslyn/` and serves
    both. Each generator has snapshot, compile and incrementality tests; each rule has a positive and a negative test
    at exact spans through `CompilationWithAnalyzers`; code fixes are tested through an `AdhocWorkspace`
    (the in-house pattern; `Microsoft.CodeAnalysis.Testing` is not added).
  - The build-time cost is measured with `-p:ReportAnalyzer=true` before and after S1 and S2a; the target is under 2%.
- **Rationale**: owner decision 4 (the infrastructure is the first P3 batch); the AOP plan's layout; one test project
  avoids an extern-alias host in every test project.
- **Alternatives rejected**: one project for everything (shipped and internal rules would ship together); putting the
  generators in `NetPrints.Sdk` (the SDK never reaches extension authors); StyleCop for the layout rules (ADR-0003
  note: it cannot express the decided blank-line scope); `Microsoft.CodeAnalysis.Testing` (a second test style for
  the same job).

## R3. Style rules NPS001–NPS003 and the sweep

- **Decision**: NPS001 (an attribute shares a line with its member or another attribute) and NPS002 (no blank line
  between members that need one) at `error` in `src/` and `tests/`; NPS003 (grouped non-public fields) at `suggestion`,
  never failing a build. NPS002 applies when either adjacent member is a property, indexer, method, constructor,
  operator or event, any public member (consts and fields included), or a member with braces, a comment or
  attributes. Non-public fields and consts on one line with no comment and no attributes may stay grouped. Enum
  members and record parameters are out of scope. The sweep (about 274 sites in `src/`, 177 in `tests/`) is one
  mechanical commit through `dotnet format analyzers --diagnostics NPS001 NPS002 --severity error`.
- **Rationale**: owner decision 3 and FR-002; the follow-up's §5 scope.
- **Alternatives rejected**: counting `internal` and `protected` fields as public (follow-up question 8): it doubles
  the sweep for no readability gain; the spec's "consecutive private fields MAY stay grouped" reads as "fields that
  are not public". This is listed for the owner because it is the owner's own style rule.

## R4. Editor composition with MS.DI (ADR-0031, ADR-0040)

- **Decision**: the ADR-0031 migration path, one step per batch, each with its gate:
  1. **R5-0 (sub-phase A)**: M1 (`ShellStartupPerformanceTests`: composition, shell host and a ready shell view
     model, headless) and M2 (a generated graph of 1000 nodes and about 4000 pins: time and allocated bytes, with
     counting fakes) baselines in the perf job; M4 already runs (`trim-readiness`). The M5 spike composes a throwaway
     copy of `EditorServices` three ways (hand-wired, MS.DI with our generator, Pure.DI) with BenchmarkDotNet and
     records the result in the implementation notes. If MS.DI with the generator loses on the ADR-0031 terms (the
     generated file passes about 600 lines, or the registration generator cannot express a needed shape), the
     migration stops and the coordinator rewrites the C1 composition tasks before they start, as P3a did after the
     Dock spike.
  2. **Attributes and generator (A)**: `[Singleton<TService>]` and `[SessionScoped<TService>]` on implementations; the
     generator emits `AddEditorServices()` with factory lambdas that call constructors (no reflection activation);
     class and graph factories are `partial` classes marked `[ScopeFactory]` whose `Create` methods the generator
     implements. NPI001 reports a singleton that depends on a session-scoped service; the other NPI rules are in
     [contracts/analyzer-diagnostics.md](contracts/analyzer-diagnostics.md).
  3. **App and shell (C1)**: `EditorServices` builds a `ServiceCollection` from `AddEditorServices()` plus the host
     instances; `EditorContext` stays as a facade; `TestComposition` swaps doubles with `services.Replace`. Gate M1:
     composition at most 150 ms including the provider build and first resolves.
  4. **Project session (C1)**: `ProjectSessionScope` extracted from `ProjectLoader`; backups, recovery and templates
     become session-scoped. Gate: a test resolves every registration in both scopes.
  5. **Class and graph (C1)**: the generated `ClassContextFactory` and `GraphScopeFactory`. Gate M2 with counting
     fakes: zero per-pin resolutions, allocations per node at most the baseline plus 10%.
  6. **Extensions (C2)**: R15 below.
  - The composition calls the options validators itself at start (no generic host), on every provider build
    including the extensions'.
  - SC-005 is checked at Checkpoints C1 and C: M1 and M2 at most 10% over their R5-0 baselines, in the perf job.
- **Rationale**: ADR-0031 is decided; the gates are its own.
- **Alternatives rejected**: everything in one batch (no gate could stop a bad step); a generic host (a second
  lifetime model for one validator call).

## R5. Smart enums, `CommandKey` and gestures (ADR-0034)

- **Decision**: as ADR-0034 decisions 1–6, in sub-phase A: the internal `[SmartEnum]` generator emits only the
  `CommandKeyId` enum and `All`; `CommandKey` is an abstract class with private nested sealed members; `CommandKey` and
  `CommandGesture` implement `ISpanParsable<T>` and `ISpanFormattable` with a `FrozenDictionary` alternate-span lookup;
  `CommandDescriptor.DefaultGestures` becomes `IReadOnlyList<CommandGesture>?`; strings are parsed only at the
  boundaries (the `keyboard` settings section, manifests, display). Tests: the 1:1 id mapping, the Avalonia key map
  completeness, a generator snapshot, and a test that touches the static constructor (the `TypeInitializationException`
  the audit reproduced).
- **Rationale**: decided; it must land before the command descriptors publish in C2.
- **Alternatives rejected**: as ADR-0034.

## R6. Settings sections, validators and the schema (ADR-0040, S10)

- **Decision**:
  - Each section is a sealed options class with `init` members, read through the source-generated JSON contexts.
  - Validation uses the `[OptionsValidator]` source generator of `Microsoft.Extensions.Options` over standard
    DataAnnotations (`[Range]`, `[Required]`, `[RegularExpression]`, `[Length]`); `ValidateDataAnnotations` is not used.
  - The same attributes feed the exported schema: `JsonSchemaExporter.GetJsonSchemaAsNode(JsonTypeInfo, options)` with
    a `TransformSchemaNode` callback that reads the property's attribute provider and writes `minimum`, `maximum`,
    `pattern`, `minLength`, `maxLength`, `description` (from `[Description]`) and three NetPrints keywords:
    `x-netprints-restart` (`[RequiresRestart]`), `x-netprints-path` (`[SettingPath(PathKind.File|Folder)]`) and
    `x-netprints-choices` (a choices provider id for open string sets such as preset and language ids).
  - The built-in schema is published as `schemas/settings.v1.schema.json`, golden-tested, validated by the existing
    `jsonschema` CI job, and the docs' settings table is generated from it with a staleness test.
  - Open sets (preset ids, profile ids, language) are strings with a choices provider, never enums (ADR-0040 decision
    6).
- **Rationale**: one set of attributes drives the validator, the schema and the generated page; nothing reflects over
  user types; the schema exporter works on source-generated metadata, so it is trim-safe.
- **Alternatives rejected**: a hand-written schema (drifts); our own validator generator (Microsoft's ships with the
  package already referenced); reading `JsonTypeInfo.Properties` directly for pages (N9's first idea) without the
  schema (the schema is needed anyway for S10 and gives extensions one model).

## R7. Unknown document members (FR-007, ADR-0038 decision 10)

- **Decision**: every document record gets a `[JsonExtensionData]` member of type
  `IReadOnlyDictionary<string, JsonElement>?` (init-only, excluded from equality noise by keeping it last). On save
  the canonical writer writes known members in their canonical order, then the unknown ones in ordinal key order.
  `format`, `show` and the merge driver treat each unknown member as an opaque value compared by JSON equality; the
  merge driver reports a conflict when both sides changed the same unknown member. It lands in sub-phase A, before
  any new document field ships (the pin roles and `out`/`ref` pins of B, the `kindVersion` of R18).
- **Rationale**: a newer editor's or an extension's fields survive an older editor's save; ADR-0038 and ADR-0039 make
  it a prerequisite.
- **Alternatives rejected**: preserving unknown members in their original position (the canonical writer would need
  the source order; canonical order is the format's own rule).

## R8. Declarative pins, pin-key tracking and the 0.3.0 renames (S2a, S2b, S4, S3; ADR-0025)

- **Decision**: the follow-up's "recommended combined design", in the order S2a → S2b → S4 → S3:
  - **S2a**: `NodeInputDataPin<T>` and `NodeOutputDataPin<T>` (`default(T)`, no generic-math constraint); `[NodeKind]`,
    `[NodeBase]` (abstract bases), `[Pin("key")]` and `[PinType(nameof(member))]` in Core; `partial` pin properties
    implemented by the convention generator (key = property name without `Pin`), with base offsets,
    `[NodePinLayout]` metadata and BCL `[DefaultValue]`. A pack spike confirms the analyzer ships inside the Core
    package and reaches a consumer of the packed package.
  - **S2b**: NPA001–NPA009 and `NetPrints.Authoring.CodeFixes` (make partial, add the marker, keep the key).
  - **S4**: `NodePins.Shipped.txt` and `NodePins.Unshipped.txt` per project, seeded from the S0 golden; NPA010 (a
    shipped key disappeared) and NPA011 (an unshipped key not recorded), with the "keep key" and "remove on purpose"
    fixes; the release step moves Unshipped to Shipped.
  - **S3**: the 13 renames and the 2 overrides as a hard break: `*REMOVED*` entries, release notes, no `[Obsolete]`.
    The S0 golden, every fixture and every sample graph stay byte-identical.
  - Resource keys use the full pin reference (`Node.<kind>.Pin.in.data.InitialIndex`; follow-up question 10).
- **Rationale**: owner decisions 5–7; FR-009–FR-012.
- **Alternatives rejected**: bare pin names (46 renames and member clashes, follow-up question 1); `[PinType<int>]`
  instead of typed pins (question 2); `[NodeKind]` without an id for abstract bases (question 11: a missing id would
  be a silent file-contract hole).

## R9. Pin roles, `out` and `ref` output pins, purity (owner decisions 11–13)

- **Decision**: a `PinRole` (`Target`, `Argument`, `Result`, `OutParam`, `Exception`; `OutParam` covers `out` and `ref`)
  set by the generator from the pin's declaration (`[Pin(Role = …)]` where the convention cannot tell) and by
  `CallMethodNode` for its dynamic pins. `Node.CanSetPure` becomes one base rule: at least one output data pin with
  the `Result` or `OutParam` role; the `Exception` pin never counts. Every `out` and `ref` parameter gets an output data
  pin carrying its value after the call, with the key `out.data.<parameterName>`; an `out` or `ref` input not
  connected to a variable is a validation error (the S0 check, now from the pin role). Schema v1 stays additive. The
  changed goldens (graphs with `out`/`ref` calls gain the new pins; the generated C# of a pure call with `out`
  parameters) are listed in the implementation notes with the reason.
- **Rationale**: decided by the owner; the roles also serve R2's capabilities and search offers.
- **Alternatives rejected**: deciding purity from the method signature (decision 13 replaced it).

## R10. Node search providers, creation and ranking (SUG1–SUG4; ADR-0028)

- **Decision**: the SOLID plan's migration steps 2–8 with decision 8:
  - **SUG1**: `SuggestionRows.Build` extracted; about 12 internal providers in a fixed order, grouped by the node kind
    that owns them (CallMethod → methods, Variable get/set → variables, Constructor → constructors, Type → types);
    `NodeSearchEntry` records replace `object Value`; `SuggestionListViewModel` → `NodeSearchViewModel`,
    `SuggestionItem` → `SuggestionRowViewModel`; catalog metadata feeds titles and keywords.
  - **SUG2**: one `INodeCreationService` (clamp, connect, undo in one step), a prompts adapter over the dialogs,
    `Func<NodeGraph, Node>` factories instead of `Activator`/`GetConstructor` (FR-117: the trim ratchet drops by two).
  - **SUG3**: `ISuggestionRanker` with the default ranker equal to the goldens.
  - **SUG4** (C1, after R7 and with L1): the contracts move to Abstractions under NPXE0006, `AddSuggestionProvider`,
    an NPX code for id-prefix and duplicate problems, a conformance check, and `Requires` on entries (NPXE0007, R19).
  - Opening the search creates no node; providers are built once per registry, never per open (M3 counting
    provider); the 30,000-row budget holds.
- **Rationale**: owner decisions 1, 4, 5 and 8 of the SOLID plan.
- **Alternatives rejected**: as the SOLID report.

## R11. Node capabilities and `PrimitiveKinds` (R2, ADR-0029; ADR-0034)

- **Decision**: R2 in the 0.3.0 window after S3: `IPinListEditor`, `IOverloadableNode`, editable pin names and
  `IMemberReferencingNode` as role interfaces that search, navigation and the inspector ask for; the header role falls
  back by convention. `PrimitiveKinds` (Core, one `INumberBase<T>` row per primitive) replaces the five scattered
  lists and the IL2057 at `TypedValueConverter`; `sbyte` and `decimal` become full primitives, pinned by a golden test.
- **Rationale**: decided (ADR-0029 and ADR-0034 are planned or written); both change public shapes, so they land
  before the gate.
- **Alternatives rejected**: none open.

## R12. The package set, the Abstractions split and analyzer delivery (R7, ADR-0030)

- **Decision**:
  - `NetPrints.Extensibility.Abstractions` (contracts only; references Core, Serialization, Reflection, Catalog,
    `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`) is split
    from `NetPrints.Extensibility` (host only) with the SOLID report's type split. Namespaces do not change; no type
    forwards (nothing has shipped). An architecture test pins the allowed references and the absence of
    `System.Runtime.Loader` in Abstractions.
  - Packages at 0.3.0: `NetPrints.Core` (with the Authoring analyzers), `NetPrints.Reflection`, `NetPrints.Catalog`,
    `NetPrints.Serialization`, `NetPrints.Extensibility.Abstractions`, `NetPrints.Extensibility`,
    `NetPrints.Extensibility.Testing`, `NetPrints.Extensibility.Testing.Xunit`, `NetPrints.Templates`, plus the
    existing `NetPrints.Annotations`, `NetPrints.Sdk` and the `NetPrints.Cli` tool.
  - Every packable library enables `EnablePackageValidation`. Packages already on nuget.org get
    `PackageValidationBaselineVersion` set to their last published version, with a `CompatibilitySuppressions.xml` that
    holds exactly the intended 0.3.0 breaks (a test cross-checks it against the `*REMOVED*` lines); the new packages
    get their baseline in the release that first publishes them (a `releasing.md` step, FR-020).
  - A test ties `ExtensionApi.Version` to the Abstractions and Extensibility API files: a recorded hash of the
    `PublicAPI.*.txt` files changes only together with the version.
  - **How the analyzers reach authors**: NuGet excludes analyzers of transitive package dependencies by default. The
    Abstractions package declares its Core dependency with no excluded assets (`PrivateAssets="none"` on that
    reference), so an author who references only Abstractions or Extensibility still gets the Authoring analyzers
    once. The S2a pack spike and the sample Godot extension (built from packed packages only) verify it. If the spike
    shows a duplicate or missing analyzer, the fallback is the AOP plan's: the `netprints-extension` template
    references `NetPrints.Core` directly and the guide says why.
- **Rationale**: SOLID decision 3; ADR-0010 decision 3; FR-019, FR-020. The `PrivateAssets` route is new research: it
  keeps the author's project file to one reference.
- **Alternatives rejected**: packing the analyzer into several packages (duplicate generator runs and duplicate
  hint names); a separate `NetPrints.Authoring` package (version skew with Core, the ADR-0022 problem); a package
  validation baseline for packages never published (there is nothing to compare with).

## R13. Localization infrastructure (L1, ADR-0027)

- **Decision**: the follow-up's mechanism:
  - convention keys (`Node.<kind>.Title`, `Node.<kind>.Pin.<pinRef>`, `Category.<id>`, `Command.<id>.Label`,
    `Menu.<menuId>`, `Panel.<panelId>.Title`, `Settings.<section>.<key>.Label` and `.Description`);
  - a public `LocalizedText` value (key plus default text) on every contribution descriptor label; the host resolves
    it through the owner's resources and falls back to the default text (FR-027);
  - neutral resx files `NetPrints.Core/Graph/NodeStrings.resx` and `NetPrints.Editor/Strings.resx` with the MSBuild
    strongly typed generator (`PublicClass=true`), and `{x:Static}` in XAML;
  - extension resources through `IExtensionBuilder.AddStringResources` (NPXE0011);
  - `netprints.language` (a string; empty = the OS culture) with restart to apply; Semi and Ursa `Locale` follow the
    app culture (ADR-0023 amendment);
  - NPA040–NPA042 for missing, extra and placeholder-mismatched keys; NPH006 for literals passed to known UI sinks;
    the XAML literal ratchet (E7) does not grow; a per-pin automation id;
  - pin labels fall back to the raw key, as today, and the built-in node category stays the single "NetPrints" one
    (follow-up questions 4 and 9; both are listed for the owner because they change what users see).
- **Rationale**: owner decision 8 (L1 in P3 before the packages publish; L2 in P6).
- **Alternatives rejected**: live language switching through a binding service (question 5: compiled `x:Static` stays,
  and restart matches the "Restart to apply" bar); moving existing strings now (L2, P6).

## R14. Typed contexts, the editor facet and contribution groups (R5a, R5b, N1, N8, R4; ADR-0020 amendment, ADR-0030)

- **Decision**:
  - **Typed contexts (C1)**: `ICommandContext` (shell services subset, `IProjectSession?`, `IGraphEditor?`, selection,
    parameter, scope), `IPanelContext`, `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext` and
    `IDialogContext`. `IProjectSession` carries the project, its `CSharpTarget`, the reflection provider, the active
    graph, the output and the extension's session services. `IGraphEditor` is the R16 graph abstraction shared with
    `INodeCreationContext` (`Place`, selection, `IEditorUndo`). `GraphPoint` moves to Core (`NetPrints.Graph`), because
    Abstractions depends on Core and a public contract uses it (SOLID question 6). Descriptor factories take typed
    contexts; `NoServices`, `StartPageServices` and the `?? new Default()` fallbacks go.
  - **The facet (C2)**: `IExtensionBuilder.Editor` returns `IEditorContributions` (NPXE0005); hosts without an editor
    (the CLI, the generator) return a Null Object that accepts and ignores every call. The built-in editor registers
    all its contributions through the same facet as the in-process `netprints.editor` extension; its DataTemplates
    move to `BuiltInViews.axaml`, added with `AddViewResources`.
  - **Kinds on the facet**: commands, panels, dashboard tiles, context-menu items (with `AppliesTo`), tooltip
    providers, go-to providers, status-bar items (C-15), settings pages (NPXE0009, custom views NPXE0010), dialogs
    (R4: a registered dialog view model shown by id through `IDialogService`), view resources and icon ids (N8:
    `Icon.<extension id>.<name>`; an unknown id draws the fallback glyph and records an issue); inspector sections and
    value editors (NPXE0014) are P3b's. Model-side kinds stay flat on the builder because the CLI uses
    them: project templates (NPXE0012), node search providers (NPXE0006), document migrations (NPXE0013); graph rules
    (NPXE0015) are P3b's.
  - **Groups (D1)**: the registry keeps the app group (built-ins first, then per-user extensions in load order) and
    one session group per open project; it is no longer frozen. Adding or removing a group raises `Changed` with a new
    immutable snapshot; menus, the command bar, key bindings, the palette, the shortcuts sheet, context menus, the
    start page, templates, the status bar and the Settings document rebuild from the snapshot.
  - **The built-in status-bar segments** (error and warning counts, build state, run state, zoom, selection count)
    become status-bar items in C2.
- **Rationale**: SOLID decisions 3, 11 and 15 and report-2 §3; ADR-0020 R16; Clarifications answer 2.
- **Alternatives rejected**: flat editor methods on the builder (about 20 members, and the CLI would need stubs);
  reopening the shell when a project changes (loses the shell state); a frozen registry plus a second dynamic one
  (two code paths for every surface).

## R15. Per-extension services and host exports (ADR-0031 decision 3)

- **Decision**: `IExtensionBuilder.Services` (`IServiceCollection`, NPXE0008); `ExtensionRegistry` builds and owns one
  provider per extension from its collection plus the `HostExports` allow-list. The P3 allow-list is `ILoggerFactory`,
  `ILogger<T>` for the extension's own types, `TimeProvider`, and `IOptionsMonitor<T>` for the extension's own settings
  sections; host singletons are bridged as instances. Session services reach an extension through a per-extension
  scoped accessor on `IProjectSession`. The seam filter rejects closed generics over host types and any
  `IConfigureOptions`, `IPostConfigureOptions` or `IValidateOptions` for host options. Disposal order: sessions, then
  the provider, each extension wrapped so one throwing `Dispose` does not stop the rest. A test pins the allow-list.
- **Rationale**: ADR-0031 decisions 3–5; FR-024.
- **Alternatives rejected**: one shared provider (extensions would see each other's registrations).

## R16. Experimental ids (FR-030, ADR-0017)

- **Decision**: new ids in `NetPrints.Core/ExperimentalApiIds.cs`, each listed in the extension guide's "API stability"
  section:

  | Id | Surface | Lands in |
  |---|---|---|
  | NPXE0005 | Editor contributions: `IExtensionBuilder.Editor`, `IEditorContributions`, the editor descriptors and their typed contexts, `IProjectSession`, `IGraphEditor` | C1 (contexts), C2 (facet) |
  | NPXE0006 | Node search providers (SUG4) | C1 |
  | NPXE0007 | C# features and requirements: `CSharpFeature`, `CSharpFeatureId`, `Requires`, `Availability` | C1 |
  | NPXE0008 | Extension services and host exports | C2 |
  | NPXE0009 | Settings page contributions and settings metadata attributes | C2 |
  | NPXE0010 | Custom settings page views | C2 |
  | NPXE0011 | Extension string resources (`AddStringResources`) | C1 |
  | NPXE0012 | Project templates with files | C2 |
  | NPXE0013 | Extension node-data migrations | C2 |
  | NPXE0014 | Inspector sections and value editors (R6) | P3b (was G) |
  | NPXE0015 | Graph rules (N2) | P3b (was G) |
  | NPXE0016 | Translation context additions (N5) | P3b (was G) |
  | NPXE0017 | Node kinds by convention (S7, SUG5) | P3b (was G) |

  Stable from 0.3.0: the typed pins and pin attributes (`[NodeKind]`, `[NodeBase]`, `[Pin]`, `[PinType]`),
  `PinRole`, `CSharpLanguageVersion`, the minimal `CSharpTarget`, `LocalizedText`, `GraphPoint`, the manifest's
  `themes` field and the existing stable surface. NPXE0001–NPXE0004 keep their meaning.
- **Rationale**: one id per surface an author opts into, so a settings-only extension does not opt into editor
  contributions; ADR-0040 asked for separate ids for pages and custom views.
- **Alternatives rejected**: one P3 id for everything (an opt-in would mean nothing); stable descriptors (they are the
  newest and least proven API).

## R17. Templates with files, `netprints new` and `dotnet new` (N3, B15)

- **Decision**: `ProjectTemplateDescriptor` gains `Files` (`TemplateFile`: a relative path and content, or an embedded
  resource) with the `dotnet new` replacement model: a `sourceName` literal (`NetPrintsProject`) replaced in file
  names and contents, plus the symbols `RootNamespace` and `ProfileId`, written as the literal tokens
  `NetPrintsRootNamespace` and `NetPrintsProfileId`. The built-in `console` and `library` templates are file trees
  embedded in `NetPrints.Workspace` (the CLI and the editor both reference it); an optional
  `Properties/launchSettings.json` is a template file like any other. `NetPrints.Templates` (a `dotnet new` template
  package) is built from the same trees: its project copies them and adds `.template.config/template.json` with the
  same `sourceName` and symbols, so the editor, `netprints new` and `dotnet new` cannot drift; a CLI test runs
  `dotnet new` in a temporary hive (`--debug:custom-hive`) and compares the trees byte for byte with `netprints new`.
- **Rationale**: FR-025, FR-107, FR-108; the `dotnet new` model is the one users already know, and a literal
  `sourceName` keeps the template files valid, buildable projects.
- **Alternatives rejected**: a template language (Scriban, Handlebars: a new dependency and template files that do not
  build as they are); separate files for `dotnet new` (they would drift).

## R18. Extension node-data migrations (N4) — new research

- **Unknown**: how a migration knows that a node was saved "with the older shape" (US3 scenario 7), since node
  documents carry no version of their own (SOLID question 11).
- **Decision**: a per-kind data version. `NodeKindDescriptor` gains `DataVersion` (default 0). The serializer writes
  an optional `kindVersion` member on a node document only when its kind declares a version above 0, so every
  built-in node and every existing file stays byte-identical. `IExtensionBuilder.AddDocumentMigration` (NPXE0013)
  registers an `INodeDataMigration` (`NodeKind`, `FromVersion`, `Migrate(JsonObject node)`); on load, before the node is
  created, the migrations of that kind run in `FromVersion` order from the stored version (absent = 0) up to the
  current one. A migration whose kind does not start with `<manifest id>/` is rejected with an NPX code; the
  conformance kit checks that a migration touches only its node's JSON. It depends on R7 (an older editor keeps the
  member).
- **Rationale**: explicit versions are how the document migrator already works (`IDocumentMigration.FromVersion`);
  the member costs nothing for kinds that never version.
- **Alternatives rejected**: shape-sniffing migrations with no version (each author reinvents detection, and a
  migration can run twice); a per-document map of extension versions (a class document would change when any
  extension's version changes); per-kind schema files (heavier than the problem).

## R19. C# target (ADR-0038) — one new decision

- **Decision**: ADR-0038 as written, with the split the spec makes: the stable `CSharpLanguageVersion` and minimal
  `CSharpTarget` (Core, `NetPrints.Projects`, next to `ProjectSnapshot`), the evaluation capture of `LangVersion`,
  `MaxSupportedLangVersion` and `TargetFramework(s)` in `NetPrints.Workspace`, the generate request lines in the SDK
  target, the translator's default-implemented `Target` and version-aware literal formatting, and the six workspace
  fixtures with the three-writer byte-identity test all land in C1, before the gate; the node-search filtering, "Show
  unavailable" and the quick fixes land in F. NPT010, NPT011, the probes and the style matrix stay in P3b.
  - **New**: the cap for `latest`, `preview` and `default` is NetPrints' own known maximum,
    `CSharpLanguageVersion.MaxKnown` (a constant in Core raised with each Roslyn bump), not the running Roslyn's
    maximum. The editor, `netprints regen` and the SDK's generator could run different Roslyn builds; a shared
    constant makes them resolve the same version, which SC-010 requires.
  - Info messages: NPW006 (`latest`/`preview`/`default` resolved, pin `<LangVersion>`), NPW007 (newer than the
    framework's default), NPW008 (unknown or newer than `MaxKnown`; `MaxKnown` used).
- **Rationale**: FR-070–FR-075 and SC-010.
- **Alternatives rejected**: the running Roslyn's maximum (three writers, three answers); `Requires` on every
  descriptor now (the research's "contract work" list): only node-search entries get it in P3 (ADR-0038 decision 9);
  the other experimental descriptors can add it later without a break.

## R20. Settings pages and the Settings document (ADR-0040)

- **Decision**: `openSettings` (Ctrl+,) opens one Settings document with a page list: General, Appearance, Keyboard
  shortcuts, Run, Extensions, and under Extensions one page per loaded extension section. Pages come from the
  settings page kind; a section with no custom page gets a generated page from its schema: `boolean` → toggle,
  `integer`/`number` with range → numeric box, `string` → text box, `string` with `enum` or `x-netprints-choices` →
  drop-down, `x-netprints-path` → path picker, `array` of strings → string list; `description` → help text. A field
  validates on focus loss or Enter through the section's validator and, if valid, is saved at once; an
  `x-netprints-restart` field shows "Requires restart" and raises the "Restart to apply" bar. Search filters fields
  by label and description across pages. The store writes through the existing `JsonFileSettingsStore`, preserving
  unknown sections and keys; an unreadable file is copied to `settings.json.unreadable-<timestamp>` before the next
  write, and the editor says so once.
- **Rationale**: ADR-0040; FR-039–FR-044.
- **Alternatives rejected**: as ADR-0040.

## R21. User key bindings (ADR-0040 decision 5)

- **Decision**: the `keyboard` section holds `bindings`, a list of overrides `{ command, scope, add[], remove[] }` with
  gestures in canonical form; "Reset" removes the command's entry, "Reset all" removes the list. The effective gesture
  set is computed once per registry snapshot: user overrides, then built-in defaults, then extension defaults in load
  order; an extension default that a user binding takes is marked "overridden". Recording a gesture checks ADR-0020's
  scope rules and finds conflicts in overlapping scopes before anything changes; accepting writes a `remove` for the
  other command. Entries for commands that are not loaded stay in the file and are listed under "Not loaded"; an
  unparseable gesture is reported on the page and ignored. The macOS Ctrl-to-Cmd mapping stays a view-layer concern,
  as for defaults. Every surface that shows gestures reads the effective set.
- **Rationale**: FR-045–FR-050.
- **Alternatives rejected**: a separate `keybindings.json` (ADR-0040); storing the full binding table (a default that
  changes in a later version would never reach the user).

## R22. Run profiles: placement and process model (ADR-0037) — one new decision

- **Decision**:
  - The launch settings reader, the internal kinds table (`Project`, `Executable`), the resolver (expansion, the
    `CommandLineToArgvW` splitter and its joiner, path rules, problems) live in `NetPrints.Workspace/Run/`, which both
    the editor and the CLI reference, so both build the identical process request (SC-008).
  - `ProcessStartRequest` (Core) gains an argument list, environment removals, input redirection, output capture and an
    external-terminal flag; `IProcessLauncher` (editor) and `ProgramRunner` (CLI) honour them.
  - **New**: the MSBuild variable scrub needs to know which variables NetPrints set. `MsBuildRegistration` records the
    names it set and whether each existed before; the resolver removes only those that did not, so user-set values
    pass (FR-057).
  - Stop: `CloseMainWindow()` on Windows and SIGTERM through a `libc` `kill` import on Linux and macOS, then
    `Kill(entireProcessTree: true)` after `run.stopTimeoutSeconds`.
  - The Run section of Project settings edits `launchSettings.json` through `JsonNode`, preserving unknown members and
    property order; a file with comments is shown read-only with "Open file", because `JsonNode` would drop them.
  - Problems carry NPR codes ([contracts/run-profiles.md](contracts/run-profiles.md)).
- **Rationale**: FR-051–FR-065; one resolver for two front ends.
- **Alternatives rejected**: delegating to `dotnet run` for `Executable` (SDK-band dependent, ADR-0037); rewriting the
  file with the serializer (loses unknown members and order).

## R23. Console input, external terminal, configuration selector (B5, B23)

- **Decision**: with output captured, Output shows an input line that writes a line to the program's standard input
  and a control that closes it (end of file); without capture there is no input line. "Run in external terminal"
  starts the program in a new terminal window: on Linux and macOS NetPrints writes a POSIX `sh` script in the session's
  temp folder (single-quoted arguments, `cd`, exported environment, a final "Press Enter to close" read) and starts
  it through the terminal command (`run.linuxTerminal`, default: the first of `x-terminal-emulator -e`,
  `gnome-terminal --`, `konsole -e`, `xfce4-terminal -x`, `xterm -e` found on `PATH`; macOS: `open -a Terminal`); on
  Windows the program is started with a new console window and no redirection, and the window closes when the program
  exits. The configuration selector (Debug, Release) sits next to the run profile button; build and run use it; it is
  remembered per user and per project; the CLI keeps `-c`.
- **Rationale**: FR-063, FR-064; the script keeps the argument list exact without a shell parse on our side.
- **Alternatives rejected**: `cmd /k` on Windows (re-parses the arguments, the FR-056 reason); a pseudo-terminal in
  Output (a much larger feature). The Windows window closing at exit and the macOS path are listed for the owner's
  manual check.

## R24. Theme presets (B0) and the tail themes (B1, C) (ADR-0035)

- **Decision**: B0 in D1: the token catalogue and the contrast math in `NetPrints.Editor/Theming/` (the tests call it);
  the four built-in presets as embedded `*.nptheme.json` files in the ADR-0035 format, read by the same loader B1 will
  use for user files (embedded presets are trusted and gated by tests); a per-variant overlay writer for
  `Application.Resources.ThemeDictionaries[Dark|Light]`; High contrast selects Semi's high-contrast variant for its
  slot (Aquatic for Dark, Desert for Light) plus its tuned tokens; `darkPreset` and `lightPreset` strings with a
  choices provider; the Appearance page; wire opacity and thickness and the ADR-0036 inspector tokens. B1 and C moved
  to P3b on 2026-10-16 (the ADR-0035 amendment in F records it).
- **Rationale**: ADR-0035 decisions 1–9; one format for presets and user files means B1 adds only validation messages
  and the Reload and Duplicate commands.
- **Alternatives rejected**: presets as C# dictionaries (a second format for B1 to parse).

## R25. Conformance kit, extension package, `verify`, author workflow (ADR-0010; new ADR-0041) — new research

- **Decision**:
  - `NetPrints.Extensibility.Testing` (framework-agnostic): `ExtensionTest<TExtension>` with a declarative `TestState`
    and `RunAsync`, the public `ExtensionHarness` (moved from `tests/NetPrints.Testing`), and a conformance suite with
    checks NPK001–NPK016: the roadmap's twelve plus service providers, graph rules, editor contributions and the
    contributed-view literal scan. `NetPrints.Extensibility.Testing.Xunit` adds a `ConformanceTests<TExtension>` base
    class with one test per check (xunit.v3).
  - **Unknown resolved**: the view literal scan (NPK016) cannot rely on the compiled assembly holding the `.axaml`
    text, so it scans the extension's `.axaml` sources (the kit finds the folder holding `netprints-extension.json`
    above the test assembly, or takes it from `ConformanceOptions.SourceDirectory`) with the same rules as the
    repository's E2/E8 XAML hygiene tests. `verify` does not run it.
  - **Extension package**: a `.nupkg` with package type `NetPrintsExtension` holding the loader's folder
    (`netprints-extension.json`, assemblies, resources) under `extension/`; the `netprints-extension` template's project
    sets this up inline, so authors see it; P3c may change it after its feed spike.
  - **`netprints extension verify <folder|package>`** runs NPK001 (manifest), NPK002 (packaging), NPK003 (type identity)
    and NPK017 (API compatibility): it reads the extension's member and type references with
    `System.Reflection.Metadata` and resolves each against the host's assemblies (the CLI's own by default,
    `--host <folder>` for another host), without loading anything. It writes text or `--format json`, exits 0, 1 or 2
    (ADR-0015).
  - The reusable author workflow `.github/workflows/netprints-extension.yml` (`workflow_call`): restore, build, kit
    tests, pack, `verify`; inputs for the extension folder and an optional packages artifact used as a local feed.
  - ADR-0041 records the package format, `verify` and the workflow.
- **Rationale**: FR-076–FR-081; IntelliJ Plugin Verifier is the model the research named; metadata reading needs no
  load context and works on a package that targets a newer host.
- **Alternatives rejected**: loading the extension to find missing members (fails at the first one, and runs code);
  a scan of compiled XAML (Avalonia compiles views to IL, so literals are no longer visible as text).

## R26. The sample Godot extension (FR-037) — one new decision

- **Decision**: it lives in `extensions/NetPrints.Godot/` (`src/` and `tests/`), outside `NetPrints.slnx`, with its own
  `Directory.Build.props` (which stops the walk to the repository's props), `Directory.Packages.props` and
  `NuGet.config` pointing at `local-packages/`. It contributes the `godot` project profile, a Godot template (with
  `Executable` profiles for the game and the Godot editor, `%GODOT4%` and `--path .`, labelled with the stale-assembly
  note for Release), a command, a settings section with its generated page, and a status-bar item. CI builds it in the
  `packages` job's follow-up through the reusable workflow, after the packages are packed; its tests run the kit.
- **Rationale**: "build only against the packed packages" needs a folder the solution build does not reach; `samples/`
  is reserved for graph projects and its props would apply.
- **Alternatives rejected**: `samples/extensions/` (inherits the samples' props and the "never leave changes under
  samples/" rule); a separate repository (the owner asked for it in this one).

## R27. Failure isolation (FR-034, FR-035)

- **Decision**: every call into extension code at a boundary goes through one guard that catches `Exception`, logs it
  with the extension id, and converts it: a `Register` failure drops the extension's whole group and adds NPX005 to
  Errors; a handler failure shows the error dialog naming the extension and the command; a provider failure drops
  that provider's results for that call and logs once per provider and session (NPX011); a view factory or template
  failure shows `ContributionErrorView` naming the extension. Registry issues map to extension diagnostics in Errors:
  duplicate id (NPX004), gesture conflict (NPX009), unknown referenced id (NPX010).
- **Rationale**: FR-034, FR-035, SC-004; AGENTS.md allows catching `Exception` only at such boundaries.
- **Alternatives rejected**: isolating extensions in processes (out of scope; the host channel exists for that need).

## R28. Test locations, CI and time budgets (FR-118, SC-016)

- **Decision**:
  - Tests go in the existing projects, with three justified exceptions: `tests/NetPrints.Analyzers.Tests` (Roslyn
    hosts for netstandard2.0 analyzers, R2), `tests/NetPrints.Extensibility.Testing.Tests` (the kit's own tests and
    the failing fixtures, which the spec exempts), and the Godot sample's tests (outside the solution, R26).
  - Conformance failing fixtures live in `tests/Fixtures/Extensions/Conformance/` (one `Fx.Fail.<Check>` per check).
  - CI: one new matrix leg (Analyzers; the kit's tests join the Core leg), the Godot job after `packages`, and the
    `packages` job also validates and packs the new packages. `CiWorkflowTests` keeps the legs and the UI shard
    filters complementary.
  - New Desktop E2E scenarios follow ADR-0006 and run in the existing E2E job; new headless UI tests land in the four
    shards. If a shard passes 12 minutes, a fifth shard is added in the same batch, so no job passes 15 minutes
    (SC-016).
- **Rationale**: FR-118, SC-016; P3a's infrastructure is reused as is (Assumptions).
- **Alternatives rejected**: one test project per new src project (more legs for little isolation).

## R29. Carry-overs (FR-082–FR-088)

- **Decision**: red-then-green tests for each code item in sub-phase A (FR-082 NPX008 over host-provided and
  transitive references; FR-083 the reflection provider skips an unreadable reference with NPW009; FR-085 no generated
  file on load or `--check-project`; FR-086 `.gitattributes` `*.npcat.json text eol=lf` and a byte-compare that ignores
  line endings, with a Windows CLI test; FR-087 a `git-install --command` path quoted for `sh`, with a Windows CLI
  test); FR-084 triage: each info-level rule in `src/` is fixed in code or recorded in ADR-0003 with its reason, ending
  at 0 unrecorded hits. The P3a editor items (FR-088) land in D1, and the Dock bump is checked at every checkpoint.
- **Rationale**: they touch code A and D1 rewrite; fixing them first keeps the later diffs verifiable.
- **Alternatives rejected**: none.

## R30. Sizing

- **Decision**: units of about half a day, as in the AOP and SOLID plans. P3 is about 160 units of implementation
  plus about 10 for the five checkpoint reviews and fix batches; the sub-phase table in [plan.md](plan.md) has the
  split. The units are human half-days and overstate calendar time by about 5 times, so the roadmap states P3 as about
  4 weeks in agent time (owner-approved 2026-10-16). The scope rule this section once described is gone with the tail.
- **Rationale**: the spec's Sizing section asks the plan to re-estimate.
- **Alternatives rejected**: none.

## R31. Open points taken by default

- AOP follow-up questions 4–11 (owner decision 9: the recommendations apply by default): pin labels fall back to the
  raw key; restart to apply a language change; C# modifier labels, type and member names, CLI output, logs and codes
  are never translated, and diagnostic messages are not translated until L3; L2 is P6; non-public fields may group;
  one "NetPrints" category; full pin references as keys; `[NodeBase]` for abstract bases. Questions 4, 8 and 9 change
  what users see and are listed for the owner.
- SOLID report-2 §4.3: the facet with a Null Object; per-session groups; R7 before SUG4; `GraphPoint` in Core; graph
  rules also in `build` and `generate`; templates with files now; the trim ratchet kept; ReadyToRun in P8; per-kind
  node data versions (R18).
- The deferred items stay out of P3: L2 and L3, hot enable and disable, collectible load contexts, extension seams,
  `AddRunProfileKind`, NPT010 and NPT011, the per-user run environment, and the P3c items.
