# NetPrints Modernization Roadmap

Source plan: https://claude.ai/artifact/Dpr7MBWPLeTrFiXBEMbSfh (NetPrints Unreal Roadmap).
Each phase = one Spec Kit feature (`specs/NNN-*`), one branch, one PR. Only specify a phase
when it is started.

## Repositories
- **NetPrints** (this repo, `danielmeza/netprints`): general-purpose visual language for .NET,
  extension-first. Phases P0–P5.
- **NetPrintsUnreal** (new repo, later): UnrealSharp integration built purely on NetPrints
  extension points. Phases U1–U3.

## Baseline findings (code read, 2026-09-24)
- Core `src/NetPrints.Core/`: netstandard2.0, ~7.8k LOC, no WPF. Custom name-based type specifiers,
  string-built codegen (`Translator/ExecutionGraphTranslator.cs`, goto state machine), Roslyn
  2.10 `CSharpCompilation.Emit`, DataContract XML (`.netpc`/`.netpp`), PropertyChanged.Fody.
- .NET Framework assumptions: `Core/Project.cs:46-48`, `Core/FrameworkAssemblyReference.cs:34`,
  `Reflection/ReflectionProvider.cs:173`, `DocumentationUtil.cs:63` (ProgramFilesX86 v4.x paths).
- Editor `NetPrintsEditor/`: net461 WPF, MahApps, MvvmLight, 16 XAML files; `ReflectionProvider`
  (Roslyn metadata) lives here but is UI-free.
- CLI: 64 LOC, CommandLineParser. VSIX: old-style csproj, VS 15.9 SDK. Tests: MSTest 1.4, 11
  core tests; editor tests empty.
- Unused deps: Gapotchenko.FX, System.Management.

## Phases

| ID | Name | Est. | Depends on | Status |
|----|------|------|------------|--------|
| P0 | Modernize build + Avalonia editor at parity | ~6 w (manual est.) | — | **merged** 2026-09-25 (PR #1, e24ebec) |
| P0.1 | Grid rendering (shader + pixel-identical fallback) | ~3–5 d | P0 | **merged** 2026-09-25 (PR #2, 0e1add1) |
| P1 | Core refactor + extension points | ~3.5 w | P0 | spec ready (`specs/003-core-refactor/`) |
| P2 | Catalog tooling + Spectre CLI | ~2.5 w | P1 | not started |
| P3a | Editor shell | ~2–3 w | P0, P1 | not started |
| P3 | Editor extension host | ~2.5 w | P0, P1, P3a | not started |
| P4 | VSIX (WpfAvaloniaHost) | ~2 w | P3 | **deferred** by owner (2026-09-24) |
| P5 | VS Code extension + browser build + sidecar | ~2.5–3 w | P3 | not started |
| P6 | Usability (Blueprint-level ease of use) | ~4–5 w | P2, P3 | not started |
| P7 | Structured code generation | ~2–3 w | P1 | not started |
| P8 | Performance | ~1.5–2 w | P1 | not started |
| U1 | UnrealSharp codegen + catalog (NetPrintsUnreal) | ~2 w | P2 | not started |
| U2 | Unreal nodes | ~2–3 w | U1 | not started |
| U3 | UE plugin, launcher, upstream PR | ~1.5 w | U1, P3 | not started |

### P0 — Modernize build + Avalonia editor at parity
- `global.json` (.NET 10 SDK), Central Package Management, `Directory.Build.props`.
- Core and a new UI-free `NetPrints.Reflection` (moved out of the editor) target `net10.0` only
  (no netstandard2.0); latest Roslyn and dependencies; drop dead deps; minimal reference-assembly fallback.
- Replace the WPF editor with `NetPrints.Editor` (Avalonia 12 + Nodify.Avalonia 2,
  CommunityToolkit.Mvvm, DynamicData search) + `NetPrints.Desktop`, at feature parity (60-item
  parity inventory in `specs/001-modernize-build/spec.md`).
- Tests on xUnit v3 (xunit.v3 3.2.2, pinned: Avalonia.Headless.XUnit 12.1.3 is incompatible with
  xUnit 4.x) + Avalonia.Headless.XUnit UI tests in their own assembly; Xunit.DependencyInjection in
  non-UI test projects; the test's CancellationToken everywhere (xUnit1051 as error).
- Linux-only CI workflow `CI` (`.github/workflows/ci.yml`). Legacy VSIX removed from the
  solution build (source kept, pending P4).
- Additions approved 2026-09-25 (in the same PR):
  - UX audit defects D1–D4 (see `docs/research/2026-09-25-ux-audit/`):
    - D1: dark grey background like WPF, not pure black;
    - D2: a single green selection border (override Nodify's defaults);
    - D3: an in-editor Output pane for Run on every platform;
    - D4: C# preview with no wrapping, horizontal scroll and a monospace font.
    - D5 (documentation tooltips on Linux) moves to P1.
  - (Moved out of P0 on 2026-09-25: it becomes a small follow-up PR, "P0.1 Grid rendering", right after the P0 merge and before P1, with its own review.) Background grid rewrite per `docs/research/2026-09-25-grid-rendering/`: a `GridBackground` control behind a
    transparent NodifyEditor, drawing via ICustomDrawOperation + SKCanvas in device pixels. Two paths share one
    `GridStyle`/`GridFrame` definition:
    - an SkSL shader when a GPU context exists and the effect compiles;
    - a pixel-identical fallback with two SKPaths, used for raster/headless or when the shader fails.
    Owner decisions: major line every 8 cells, lines (not dots), no origin line, minor fade 6–14 DIP, theme resources.
    Still open: verify the shader on Windows (ANGLE) and macOS (Metal).
- **Done when:** the whole solution builds and all tests pass on Linux CI and the parity
  checklist is verified.

### P1 — Core refactor and extension points
Reference-assembly resolution (ref packs / configured paths); serialization layer (versioned
DTOs, `IDocumentFormat`/`IDocumentStore`, JSON, schema migrations); composite provider
over `NetPrints.Reflection` (moved in P0); extension points (node
libraries, class/member emitters, type catalogs, project/target profiles, `IHostChannel`,
per-extension settings, plugin manifest/loading via AssemblyLoadContext); event graphs
(multiple entry points); model INPC from Fody to CommunityToolkit.Mvvm.
Project model (owner-approved 2026-09-25): a NetPrints project is a standard SDK-style `.csproj`
with a `NetPrints.Sdk` PackageReference. Each `*.netpc.json` graph has a committed `*.netpc.g.cs`
nested next to it (`DependentUpon`), regenerated by one MSBuild `Exec` of a bundled net10 generator.
There is no Roslyn source generator (generators can't see each other's output, so UnrealSharp's
generators would miss NetPrints code). The editor evaluates the project through MSBuild.Locator
and therefore requires an installed .NET SDK. Legacy `.netpp`/XML files are not read or converted
(owner, 2026-09-26: no real projects exist in the old format); the repo's own sample and fixtures are
migrated once and the legacy code is removed.
Graph format (owner-approved 2026-09-25, `docs/research/2026-09-25-graph-format/`): canonical
System.Text.Json output designed for version control: random stable node ids, a stable `id` on
every method/constructor/variable/event graph (layout keyed by it), pins referenced by name,
one line per connection/layout entry, integer positions, `$schema` first, tolerant read and
canonical write, only edited graphs rewritten. A `netprints merge` git driver is a later, optional idea.
Release and docs (owner-approved 2026-09-25, `docs/research/2026-09-25-release-and-docs/`): packages
via MinVer (`v*` tags), NuGet libraries with symbols/icon/README/package validation published by
Trusted Publishing, the CLI as the `netprints` dotnet tool, self-contained editor archives
(linux-x64, win-x64, osx-arm64; no single-file or trimming) with SHA256SUMS and provenance
attestations, a GitHub Release per tag, a local `local-packages/` feed, a Docusaurus site with a
DocFX API reference on GitHub Pages, and a two-page wiki that points to the site. Plain GitHub
Actions plus small scripts (no NUKE/Cake). Installers, auto-update (Velopack) and code signing come later.
C# code view: replace the plain read-only generated-C# preview (parity item PAR-34) with
AvaloniaEdit (TextMate C# highlighting, folding, line numbers) showing live Roslyn diagnostics
(squiggles + error list linked to the originating node); evaluate RoslynPad.Editor.Avalonia for
Roslyn-backed hover/quick info. Read-only in P1; editable C# ("code nodes") is a later idea.
Method-local variables (owner idea, 2026-09-25): each `MethodGraph`/`ConstructorGraph` owns local
variables with getter/setter nodes like class variables. They are declared at the top of the generated
method, which fits the current goto translator. The Variables panel shows two groups: *Class* and
*Method: <name>*.
Follow-ups deferred from the P0 reviews (PR #1): child view models stop calling back into the
parent `ClassEditorVM` (dependency direction; P0 only fixes the undo cleanup); a Roslyn-based
architecture gate (no Avalonia types in view models, dependency direction); Nodify
command-based gestures instead of code-behind (split, disconnect, connection completed; the
grid `ViewportTransform` item was superseded by P0.1); replace the `SetProperty(model, …)` wrappers when
Fody is removed; remove the
`EditorComposition` test hook in favour of explicit DI; `[LoggerMessage]` source-generated
logging for the app-level error handler (and new logging generally). `MetadataReference`
caching stays in P8.
Done when: the migrated HelloWorld sample is a `.netpc.json` graph in an SDK-style project, `dotnet build`
generates the same C# as before the migration (the committed `.netpc.g.cs` is up to date), and no legacy
code remains.

### P2 — Catalog tooling + Spectre CLI
`NetPrints.Catalog` engine (versioned schema, `ICatalogFilter` profiles), tool flavor
(`netprints.catalog.json` + CLI overrides, dotnet tool), annotations flavor
(`NetPrints.Annotations` + source generator), cross-flavor snapshot tests; `NetPrints.Cli` on
Spectre.Console.Cli (`build`, `generate`, `run`, `catalog`, `migrate`). `migrate` is for graph schema
versions only; it is implemented when the first new schema version exists. The schema stays v1 and may
change without migrations until the version cut after the planned phases (owner, 2026-09-26), so no
intermediate schema versions are shipped.
Graph-format follow-ups from P1: `format --check` and `regen --check`, `netprints git-install`
(a `textconv` diff and the optional `netprints merge` driver), and SchemaStore registration of the
`.netpc.json` schema.

### P3a — Editor shell (owner-approved 2026-09-25; source: `docs/research/2026-09-25-ux-audit/`)
- **Layout:** a single window with a project tree, tabbed graphs, an inspector and a bottom panel (Errors / Output / C#).
  Replaces the separate launcher and per-class windows (H2).
- **Commands:** a command registry feeding a command bar, a menu and keyboard shortcuts (H3, H4). This is also the P3
  contribution point.
- **Document lifecycle** (H1): dirty flag and "*" in the title, a prompt on close, autosave/backup.
- **Visual system** (M3, L1): design tokens (type ramp, spacing, colors) and theme overrides for Nodify.
- **Persistence** (L2): layout, open tabs, per-graph zoom and window position.
- **Undo feedback** (M16, shared with P6).
- **Docking** (owner idea, 2026-09-25): build the layout on Dock.Avalonia (wieslawsoltes/Dock): dockable,
  floatable and tabbed panes (project tree, graphs, inspector, Errors/Output/C#) with serialized layouts, which
  also covers L2. Verify Avalonia 12 compatibility, MVVM integration (CommunityToolkit.Mvvm) and headless and E2E
  testability before committing to it.
- **Start dashboard** (owner idea, 2026-09-25; moved here from P6 onboarding because it replaces the launcher):
  recent projects (pin, search, remove), "Open folder or `.csproj`", **New project from templates** (console,
  library, UnrealSharp; Unity later) driven by the P1 project profiles, samples, and what's new in this
  version (an update notice later, with Velopack).
- **Internal contribution points** (owner idea, 2026-09-25): commands, panels, dashboard tiles, project
  templates, context-menu items, tooltip providers and go-to providers are registered through one registry,
  and the built-in editor uses it. P3 then opens the same points to plugins.
- **Navigation basics** (owner ideas, 2026-09-25):
  - a command palette and "go to anything" (Ctrl+P / Ctrl+Shift+P: graphs, nodes, variables, methods, commands);
  - go to the source or target of a connection with Ctrl+click or the context menu, with a navigation
    history (Alt+← / Alt+→);
  - a tooltip on hover over a connection: `Node.pin → Node.pin`, the type and the member documentation;
  - breadcrumbs (Project › Class › Method).

### P3 — Editor extension host
`NetPrints.Desktop --profile`, plugin-loaded editor extensions, UI contributions (commands,
inspector sections, panels, settings pages), sample non-Unreal extension, anything functional
left beyond P0 parity. Publishes the `NetPrints.Serialization` and `NetPrints.Extensibility`
packages for extension authors. No performance work here (owner decision 2026-09-25: see P8).

### P4 — VSIX (deferred)
Deferred by the project owner on 2026-09-24; revisit after P3/P5. When resumed:
Spike WpfAvaloniaHost in VS 2022/2026 (assembly conflicts); editor factory for `.netpc.json`
with VS project `IDocumentStore`; Community.VisualStudio.Toolkit, SDK-style. Separate VSIX CI
workflow chained after `CI` (`workflow_run`, windows-latest, path-filtered). Resolve the
Nodify.Avalonia 1.0.2 net7.0-only limitation (editor cannot target netstandard2.0) — e.g.
out-of-process editor window or a maintained/forked canvas.

### P5 — VS Code extension + browser build
Spike Avalonia.Browser + Nodify in a webview (CSP `wasm-unsafe-eval`, size, startup);
`NetPrints.Sidecar` (Roslyn/codegen, JSON-RPC `IHostChannel`, shipped as dotnet tool with
bundled self-contained binaries as fallback); TS `CustomEditorProvider`; standalone browser build.

### P8 — Performance
Dedicated optimization phase (owner decision 2026-09-25: performance is kept out of feature
phases). SC-005 target: P0 accepts "typical developer machine" (node search cold start
1.5–1.6 s dev, 3.4 s on the CI runner); P8 brings it within 2 s on slower machines including
the CI runner. Also: translate the generated-code preview off the UI thread; DynamicData /
ReactiveUI throughput for large graphs and catalogs; `MetadataReference` caching; startup
time; memory profile of reflection caches. UX audit items L3 (search cold start) and L4. Benchmarks (BenchmarkDotNet) + performance budgets
enforced in CI.

### U1–U3 (NetPrintsUnreal repo)
Linux go/no-go spike (2026-09-25, UE 5.8.3 prebuilt): **go with caveats**. Build, editor start,
.NET 10 hosting, GenerateProject, a `[UClass]` actor, hot reload (0.93 s), a Blueprint subclass
and PIE all work on Linux after 5 small fixes on `danielmeza/UnrealSharp` branch `spike/linux`
(UE 5.8 version detection, dotnet host discovery, clang 20 fixes, POD array view for hot-reload
assembly names across native→managed, native path separators via `FPaths::MakePlatformFilename`).
Caveats: visible editor UI not yet exercised on Linux; error dialogs freeze headless editors; audit
other by-value struct interop; rely on the fork until fixes are upstreamed (est. 2–5 dev-days incl.
audit, UI pass and upstream PR). Hot reload is disabled with `-unattended`.
UnrealSharp Blueprint interop findings (code read of the fork, 2026-09-25): C# `[UClass]` types
are `UBlueprintGeneratedClass` and Blueprints can subclass them; Blueprint-callable/pure functions,
Read/Write/Edit property flags, metadata attributes (`[Category]`, `[DisplayName]`, `[ToolTip]` —
the `Category=` named argument does NOT work), BlueprintNativeEvent-style events
(`partial X` + `partial X_Implementation`), engine event overrides via `override`,
BlueprintAssignable multicast delegates, interfaces, structs/enums, async Blueprint nodes and
hot reload of Blueprint children are supported. Not supported: body-less
BlueprintImplementableEvent, C# classes deriving from Blueprint classes, by-name calls into
Blueprint functions/variables. Emitters: U1 = ClassFlags (no "Blueprintable" flag; Blueprint-ability
is inherited metadata, override with `[IsBlueprintBase]`), FunctionFlags BlueprintCallable/Pure,
PropertyFlags on `partial` properties, metadata attributes, `override` for engine events, `partial`
classes. U2 = BlueprintEvent pairs, `[UMultiDelegate]` + `TMulticastDelegate` + BlueprintAssignable,
`[UInterface]`/`[UStruct]`/`[UEnum]`, replication flags/`ReplicatedUsing`,
DefaultComponent/RootComponent, `[UMetaData]` fallback; never emit Blueprint-derived C# classes or
by-name Blueprint calls.
U1: `[UClass] partial` emitters, `unreal-blueprint` catalog profile, CLI loop into UnrealSharp
`Script/` with hot reload (first live loop ~week 8); graphs live in UnrealSharp's Script `.csproj`
through a `NetPrints.Sdk` PackageReference, so UnrealSharp's own generators see the committed `.g.cs`. U2: event entry points, latent nodes as
async/await, delegates, components, containers. U3: `UnrealSharpNetPrints` C++ plugin
launcher, Unreal `IHostChannel` pipe, `unreal` project profile, upstream PR implementing
`CSAssetTypeAction_CSBlueprint::OpenAssetEditor` as an external-editor hook.

### P6 — Usability (Blueprint-level ease of use)
Goal: someone who does not know C# can build complete classes comfortably; the C# stays visible
for those who want to learn it.
- Context-sensitive node menu: drag from a pin → only compatible nodes, ranked; strong filtering
  of the ~119k raw suggestions; categories, favorites, recent nodes.
- Curated catalogs per profile (builds on P2 catalog profiles), high-level nodes, class
  templates (e.g. a new class comes with its lifecycle/event entry points ready).
- Pin-type colors, automatic conversion nodes when linking compatible types.
- Per-node error markers (from P1 diagnostics mapping), collapse selection to function/macro,
  comment boxes/regions.
- Live C# side-by-side view synced with the graph selection (builds on the P1 AvaloniaEdit view).
- Visual debugging of compiled C# (potential strongest differentiator vs Blueprint, whose debugger
  only covers VM-interpreted graphs): (B) instrumented debug builds report node execution + pin
  values through `IHostChannel` for live flow highlighting and watches; (A) real breakpoints and
  stepping: the translator emits C# `#line` span directives mapping statements to node IDs, and
  NetPrints acts as a DAP client of `netcoredbg` attached to the host (desktop runner, or
  UnrealEditor hosting CoreCLR via UnrealSharp). Do B first, then A; a first cut of B belongs in
  the U1 prototype.
- Natural-language/AI assist that proposes nodes from a description (opt-in, reviewable diff).
- Scope UI for block-scoped variables (with P7):
  - blocks shown as nested, softly tinted regions on the canvas, with their local variables listed in the
    region header (building on Nodify grouping containers);
  - a synchronized tree in the Variables panel (Class → Method → For → If) for overview, rename, drag and
    keyboard access; selecting in one highlights the other.
- Node search (owner ideas, 2026-09-25):
  - results grouped into **collapsible categories** (tree/expanders, collapsed by default when not filtering),
    so browsing with the mouse is easy;
  - a second tab with **Favorites / Most used / Recent** (per user, persisted), also collapsible;
  - keyboard navigation stays first-class.
- Onboarding (the audit found none; the start dashboard is in P3a): a first-run guided tour of the canvas (create node, connect pins, compile, run); hints in empty areas.
- Connection and canvas navigation (owner ideas, 2026-09-25):
  - the connection tooltip shows a **live thumbnail of the other end** when it is off screen, and the live
    value once visual debugging exists;
  - edge-of-screen indicators pointing to connected nodes that are off screen;
  - hovering a pin highlights all its connections; for a pin with many connections, a context-menu list
    to jump to any of them;
  - bookmarks (Ctrl+1…9, as in Unreal) and **find references** of a variable or method across all graphs.
- Canvas editing (2026-09-25):
  - reroute nodes ("knots") and straightening connections;
  - auto-layout of a selection, align and distribute;
  - **copy/paste as text**: a canonical JSON fragment that can be pasted into another instance, an issue or
    an AI chat;
  - a keyboard-only flow: type on the canvas to create a node, Tab to move between pins;
  - an undo history panel, and F8 to jump between errors.
- UX audit additions (2026-09-25, IDs from `docs/research/2026-09-25-ux-audit/`):
  - H5–H9, M1, M2, M4–M15, M17–M21, L7;
  - discoverable node creation, context menus, rich error list, search ranking with doc preview;
  - start page and samples, accessibility (names for icon buttons, contrast ≥ 4.5:1, keyboard navigation);
  - Nodify built-ins not used yet (minimap, fit to view, groups/comments, alignment, keyboard navigation).

### P7 — Structured code generation
Emit `if/else`, `for/foreach/while`, `Sequence` blocks and `return` from the exec graph using
dominator/post-dominator analysis; keep the goto + jump-stack translator as fallback for
irregular graphs; snapshot tests plus execution-equivalence tests between both translators.
Scheduled after the owner deferred it on 2026-09-24 (current output works). Can run in
parallel with P3–P6.
Block-scoped local variables (owner idea, 2026-09-25): variables owned by for/foreach/while/if bodies.
- Scope is inferred from the structured graph (e.g. the body of a For is what's reachable from its Loop Body
  pin, bounded by dominator analysis), and optionally declared with explicit scope regions.
- Generated C# declares each variable inside its own `{ }`.
- Using a variable outside its scope is an error shown on the node.
- Semantics to define in the spec: per-iteration reset (C# semantics), capture by async/latent nodes, and
  irregular graphs where a node belongs to several scopes.

### Candidates (unscheduled)
- **Parameter modifiers** (owner-approved as a candidate, 2026-09-26; not in P1). Today, arguments of a
  user-defined method are always by value; `ref`/`out`/`in` and optional values work only when calling
  existing .NET methods, and `params` is not detected (callers pass the array).
  - An extensible **modifier registry**. Each descriptor has:
    - an id and the C# it emits;
    - where it applies: method, constructor, delegate or event;
    - an **exclusivity group**;
    - constraints: last parameter only, array or collection type, needs a default value, first parameter of
      a static class, minimum C# version from the profile;
    - its pins and data.
  - Built-in descriptors: the pass modes `ref`/`out`/`in`/`ref readonly` (one exclusive group), `params`
    (excludes the pass modes, last parameter), an optional default value (excludes `out`/`ref`/`params`),
    `scoped`, and `this` for extension methods.
  - Profiles and P3 extensions can register more, for example UnrealSharp specifiers.
  - Stored as optional fields on the argument in the graph document, so older files stay valid.
  - Per-parameter editing: selecting a parameter pin on the entry node shows its modifiers.
    - Exclusive groups show as radio options and the rest as checkboxes.
    - Invalid combinations are disabled, with the reason in a tooltip.
    - The same model feeds both the pin's context menu and a parameter section in the P3a inspector, so
      schedule it after the P3a inspector/docking; a context-menu-only version before it would be rework.
  - The call side: a `params` method shows one pin per element, plus "add pin".
- **C# syntax variants** (owner idea, 2026-09-26; planned with P7 because it is emission work). Some features
  have an old and a new C# form, and NetPrints must both **emit** and **recognize** either one.
  - Today, classic `this` extension methods are recognized in referenced code (`ReflectionProvider`), but
    C# 14 extension members (`extension(T x) { … }` blocks, extension properties) are not. NetPrints graphs
    can't define extensions in either form.
  - A **syntax-variant registry** in the translator: per feature, the forms, the minimum C# version from the
    project's `LangVersion`, and an emitter per form. Examples:
    - extension methods: `this` parameter or `extension` block;
    - an auto property with the `field` keyword, or a backing field;
    - primary constructors;
    - collection expressions;
    - file-scoped namespaces.
  - The choice comes from the profile, with a per-class override: for example, an extension class chooses
    classic or block syntax. The class document stores it only when it differs from the default.
  - Reading referenced code recognizes every form, so the catalog shows one "extension method" node either way.
  - Profiles and P3 extensions can add variants. It shares the descriptor style and the `this` modifier with
    Parameter modifiers.
- **Visual graph diff** (2026-09-25): compare two versions of a graph (from git) with added nodes and
  connections in green, removed in red and moved in grey. The stable ids of P1 make it feasible; it serves
  PR review and is the base for the `netprints merge` driver.
- **Release follow-ups after P1**: installers and auto-update (Velopack), code signing (Windows
  signing, Apple Developer ID and notarization), a Windows smoke test of the win-x64 archive,
  `PackageValidationBaselineVersion` after the first stable release.
- **Name and NuGet prefix**: keep the NetPrints name (free on nuget.org, 2026-09-25). Before
  reserving the `NetPrints.*` prefix, the owner contacts Robin Kahlow (RobinKa/netprints).
- **CI build-artifact reuse** (draft PR #4): the E2E job reuses the build job's output. Kept as a
  draft because total wall time rose from 7m02s to 9m49s; revisit together with P8 CI budgets.
- **NetPrintsUnity** (owner-approved as a candidate, 2026-09-25). A UPM package whose
  ScriptedImporter/AssetPostprocessor generates the `.g.cs` next to each `.netpc.json` (Unity
  doesn't compile through a csproj, so `NetPrints.Sdk` doesn't apply); a `unity` catalog profile over
  `UnityEngine.dll` and `Library/ScriptAssemblies`; emitters for MonoBehaviour, ScriptableObject,
  `[SerializeField]` and `[RequireComponent]`; entry points Awake/Start/Update/OnCollision*;
  latent nodes as coroutines or Unity 6 `Awaitable`; a C# language-version profile in the
  translator. P1 extension points and project profiles should keep this case in mind.
