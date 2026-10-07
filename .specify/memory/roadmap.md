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
| P1 | Core refactor + extension points | ~3.5 w | P0 | **merged** 2026-09-29 (PR #6, cc96a93); released `v0.1.0`, `v0.1.1` |
| P2 | Catalog tooling + Spectre CLI | ~2.5 w | P1 | **merged** 2026-10-01 (PR #9, 3a6eafc); released `v0.2.0` |
| P3a | Editor shell | ~2–3 w | P0, P1 | in progress (spec and tasks on draft PR #12, `specs/005-editor-shell/`) |
| P3 | Editor extension host | ~2.5 w | P0, P1, P3a | not started |
| P3b | Declarations and code style | ~3–4 w | P1, P3a, P3 | not started (owner-approved 2026-09-26) |
| P4 | VSIX (WpfAvaloniaHost) | ~2 w | P3 | **deferred** by owner (2026-09-24) |
| P5 | VS Code extension + browser build + sidecar | ~2.5–3 w | P3 | not started |
| P6 | Usability (Blueprint-level ease of use) | ~4–5 w | P2, P3 | not started |
| P7 | Structured code generation | ~2–3 w | P1 | not started |
| P8 | Performance | ~1.5–2 w | P1 | not started |
| U1 | UnrealSharp codegen + catalog (NetPrintsUnreal) | ~2 w | P2 | not started |
| U2 | Unreal nodes | ~2–3 w | U1, P3b | not started |
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
Roslyn-backed hover/quick info. Read-only in P1; editable C# ("code nodes") is planned in P7.
Method-local variables (owner idea, 2026-09-25): each `MethodGraph`/`ConstructorGraph` owns local
variables with getter/setter nodes like class variables. They are declared at the top of the generated
method, which fits the current goto translator. The Variables panel shows two groups: *Class* and
*Method: <name>*.
Follow-ups deferred from the P0 reviews (PR #1): child view models stop calling back into the
parent `ClassEditorViewModel` (dependency direction; P0 only fixes the undo cleanup); a Roslyn-based
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
- **Extension testing — multi-extension suite.** Host-side test suite with purpose-built fixture extensions (baseline pair with `dependsOn`, id squatter, duplicate id, private-dependency v1/v2, host-assembly skew, shared-prefix private dependency, type provider/consumer, throws-mid-register, native dependency). Scenarios: id conflicts, load-order permutation invariance, dependency-version isolation, extension-on-extension types, documents across extension subsets, failure isolation, scale, and reload caching. See research in `docs/research/2026-09-29-extension-testing/`.
- **API compatibility tracking.** Add `PublicApiAnalyzers` to `NetPrints.Extensibility`, `NetPrints.Core`, `NetPrints.Reflection` and `NetPrints.Serialization`; mark unstable API with `[Experimental]`. When the extension API is first published, set `PackageValidationBaselineVersion` and tie `ExtensionApi.Version` bumps to Shipped/Unshipped changes.
- **ADR: extension shared-assembly and extension-on-extension hazards.** Decided: ADR-0010 accepted (P2 spec). Two behaviours are currently unpinned: (1) a private dependency whose name starts with a shared prefix (e.g. `NetPrints*`, `Avalonia*`) is deferred to the Default context and fails with `NPX007`; (2) `dependsOn` only orders loading, so extension B cannot resolve extension A's types. Decide with an ADR before NetPrintsUnreal ships more than one extension (owner request 2026-09-29). See research section 1 and recommendation section (d).
Done when (owner decision 2026-09-28, applies P2 onward): the phase's features work end-to-end and
docs updated (guides, API reference, ADRs as applicable).

### P3a — Editor shell (owner-approved 2026-09-25; source: `docs/research/2026-09-25-ux-audit/`)
Spec: `specs/005-editor-shell/` (decisions: ADR-0018 docking, ADR-0019 CI matrix and Windows CLI leg, ADR-0020
contribution registry; the rest in its `research.md`).
- **CI and test infrastructure** (carried over from P2's final review, done first because P3a adds many Desktop
  E2E scenarios): capture timings, a UI dump, a screenshot, logs and the launched program's state when a Desktop E2E
  test times out or fails (FU-4, the `EditCompileAndRun` flake, issue #11); split the "Build and test (Linux)" job
  into a test-project matrix under an aggregate check of the same name (FU-7, ADR-0019); a Windows CLI workflow with
  a UTF-8 `show --textconv` test (FU-3, ADR-0019).
- **View model naming** (owner decision 2026-10-01): view model types end in `ViewModel`, never `VM`. The 25
  existing `*VM` types are renamed mechanically first, before any new shell code, and a hygiene test fails on any
  type name ending in `VM` (ADR-0007 amendment).
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
  testability before committing to it. Decided: adopted (Dock 12.1.0.6, exact pin) behind an `IShell` seam, gated
  by a spike, with a plain Avalonia layout as the fallback (ADR-0018).
- **Start dashboard** (owner idea, 2026-09-25; moved here from P6 onboarding because it replaces the launcher):
  recent projects (pin, search, remove), "Open folder or `.csproj`", **New project from templates** (console,
  library, UnrealSharp; Unity later) driven by the P1 project profiles, samples, and what's new in this
  version (an update notice later, with Velopack).
- **Internal contribution points** (owner idea, 2026-09-25): commands, panels, dashboard tiles, project
  templates, context-menu items, tooltip providers and go-to providers are registered through one registry,
  and the built-in editor uses it. P3 then opens the same points to plugins.
- **Type-scoped search and embedded catalogs** (deferred from P2 Review E-R15; spec decision first): a search opened
  from a pin of a type in a covered assembly still lists the type's public members, including those the embedded
  catalog omits and `[NetPrintsIgnore]` ones (extension catalogs too). Decide whether type-scoped search respects the
  embedded catalog of the type's assembly, hiding omitted and `[NetPrintsIgnore]` members, with the live provider as
  the fallback for assemblies no catalog covers. Binding through `GetTypeFromSpecifier` stays as it is. Decided:
  type-scoped search respects the covering catalog, the live provider answers only for uncovered assemblies, and
  binding is unchanged (P3a spec, research R10).
- **Adopt Xaml.Behaviors across the editor** (owner request, 2026-09-28): replace every remaining
  code-behind handler a prebuilt behavior covers (catalog: `.claude/skills/avalonia-behaviors/`), custom
  behaviors for the rest; done when the XAML hygiene allowlists are empty or hold only justified
  gestures.
- **Navigation basics** (owner ideas, 2026-09-25):
  - a command palette and "go to anything" (Ctrl+P / Ctrl+Shift+P: graphs, nodes, variables, methods, commands);
  - go to the source or target of a connection with Ctrl+click or the context menu, with a navigation
    history (Alt+← / Alt+→);
  - a tooltip on hover over a connection: `Node.pin → Node.pin`, the type and the member documentation;
  - breadcrumbs (Project › Class › Method).
- **Event graph and entry inspector** (owner report, 2026-09-27):
  - Rename an event graph inline in the list or in the inspector. P1 names it `EventGraph`, `EventGraph1`, … and
    shows the name read-only. The name only organizes the graphs; each entry emits its own method.
  - Selecting an event entry or opening its graph shows the entry's properties in the inspector. You can rename it, with the name kept unique (FR-027), and edit its
  arguments (name and type; modifiers once P3b adds them). An override takes its signature from the base
  method, and that signature is read-only. P1 only names entries at creation (`CustomEvent1`, …) and gives
  them no arguments.
- **Editor guides** (owner decision 2026-09-28; moved out of P1 because the shell they screenshot didn't
  exist yet): screenshot-heavy user guides for the editor UI (the shell layout, docking, the start
  dashboard, navigation basics), added to the docs site once this phase's UI is stable enough to shoot.
- **Starter `Main` (gap research 2026-10-06)**: sub-phase E's New project (T067/FR-042) seeds an Executable template with a `Program` class
  graph holding an empty `public static void Main()`, so a new console project builds and runs (no CS5001). A Library
  template seeds nothing. P3a's docs pass (T107) also documents a hand-written `Properties/launchSettings.json`
  (`commandLineArgs`, `environmentVariables`) for F5 and `netprints run`, after a test pins that the run command applies it.
- **Visual polish in sub-phase G (gap research 2026-10-06)**: G grows by about 5-7 days with the visual-polish items: one vector icon
  family behind an icon id (replacing the raster icons; family chosen in an ADR; `THIRD-PARTY-NOTICES`), the semantic
  node-header palette and kind glyph, pin-type, density and motion tokens, the app mark, selection and wire tokens, an
  empty-state control, focus and hover tokens, a `Font.Mono` token, high-DPI snapshots, one dialog shell for the new
  dialogs, and a visual contact sheet per sub-phase review. The detailed tasks come in a later batch.
- **Done when:** also docs updated (guides, API reference, ADRs as applicable), including the editor
  guides above.

### P3 — Editor extension host
`NetPrints.Desktop --profile`, plugin-loaded editor extensions, UI contributions (commands,
inspector sections, panels, settings pages), sample non-Unreal extension, anything functional
left beyond P0 parity. Publishes the `NetPrints.Serialization` and `NetPrints.Extensibility`
packages for extension authors. No performance work here (owner decision 2026-09-25: see P8).
- **Carried over from P2** (final review findings FU-1, FU-2, FU-5, FU-6): NPX008 should check host-provided and transitive references; the live `ReflectionProvider` should skip unreadable references and log them; info-level analyzer backlog cleanup; decide on editor `ProjectCheck` generation-skip behavior; two Windows defects found in P3a Review A (R13): `catalog --check` compares bytes, so a `.npcat.json` checked out with CRLF (`core.autocrlf=true`, no `.gitattributes` rule) always reads as stale, and `git-install --command` with backslash paths is written verbatim and `sh` drops the backslashes. (The three CI items FU-3, FU-4 and FU-7 moved to P3a.)
- **Extension testing — author conformance kit (may start in P2; the internal `ExtensionHarness` lands in P2, ADR-0010).** Ship `NetPrints.Extensibility.Testing` (NuGet package) with `ExtensionTest<TExtension>` (declarative `TestState`, `RunAsync`), an `ExtensionHarness` for real or folder-based testing, and a conformance suite with 12 checks (manifest, packaging, type identity, pure/repeatable registration, no issues, node round-trip, translation compilation, deterministic emitters, settings, host channel lifecycle, coexistence with built-ins and a "noisy neighbour", disposal). Optional xUnit adapter. `NetPrints.TestExtension` is tested only through this kit. See research.
- **Before the first NetPrintsUnreal release:** a `netprints-verify` tool (modelled on IntelliJ Plugin Verifier), a reusable author CI workflow, a nightly job co-loading published extensions, and a `dotnet new netprints-extension` template.
- **Run profiles, first batch (gap research 2026-10-06)** (size M, about 5 days). Needs P3a's command bar and sub-phase E state.
  - Profiles live in `Properties/launchSettings.json` (shared with `dotnet run`, VS and Rider); the selected profile
    name is stored per user in the P3a state store (`sessions/<project-key>.json`).
  - NetPrints applies the profile itself: `dotnet run --project <csproj> --no-build --no-launch-profile -- <args>`, with
    an explicit argv, environment and working directory on the process request. Same behaviour in the editor and the CLI.
  - The argument string is split with the Windows `CommandLineToArgvW` rules on every OS and passed through
    `ArgumentList`; a join/split round-trip test pins it. Unknown profile kinds are delegated to `dotnet run`.
  - Project settings gets a **Run** section (profile list, arguments with an argv preview, working directory,
    environment variables). The command bar gets a profile split button next to Run and Stop (Executable projects only;
    commands `run.selectProfile` and `run.editProfiles`). Run profile kinds are one contribution point (U3 adds a launch kind).
  - CLI: `netprints run` uses the first Project profile, plus `-lp|--launch-profile <name>` and `--no-launch-profile`;
    `-- <args>` replace the profile's args (env and cwd still apply); an unknown profile exits 2.
- **Interactive stdin / external terminal (gap research 2026-10-06)** (B5): an input line in Output and a "Run in external terminal"
  profile option; today stdin is not redirected, so `Console.ReadLine()` sees EOF.
- **NuGet package manager (gap research 2026-10-06)** (B7, size M, about 6 days after the shared package service of about 5 days).
  A Packages tab in the References dialog; one `NetPrints.Packages` service shared with B17.
  - Service: sources and credentials come from the `nuget.config` hierarchy and the NuGet credential plugins (the same ones
    `dotnet restore` uses), search and versions through `NuGet.Protocol`, prerelease toggle, 5-minute cache, offline fallback to
    the global packages folder. No secrets in NetPrints state; source URLs are redacted in logs.
  - Edits: new `ProjectEdit` records (`AddPackageReference`, `SetPackageVersion`, `RemovePackageReference`). Aware of Central
    Package Management (`Directory.Packages.props`), `VersionOverride`, ranges and conditional items; comments and order are
    preserved; `NetPrints.Sdk` can be updated but not removed.
  - Apply: write, restore through `LoadAsync`, and on a restore failure put the exact bytes back (csproj and props), then
    refresh the reflection provider and re-validate graphs. Removal first lists the nodes that use the package; they stay as
    unresolved nodes if the user proceeds.
  - UI: Installed, Browse and Updates tabs, source drop-down and panel, version picker with a compatibility filter,
    deprecated and vulnerable badges, transitive packages read-only.
  - CLI (v1.1): `netprints package list|add|remove|search|update` (exit codes per ADR-0015, `--format json`).
  - Tests: fixture CPM and plain projects, golden comparison with `dotnet package add`, rollback and cancellation, impact scan,
    local-folder feed; one E2E class.
  - Spike first: the `NuGet.*` version to pin and the credential plugin hooks (no `NuGet.*` package is referenced yet).
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b07-b17-packages-and-extensions.md` (outside this repo).
- **`netprints new` and `dotnet new` templates (gap research 2026-10-06)** (B15): the CLI gains `new`, using the project template registry.
- **Extension manager (gap research 2026-10-06)** (B17, size M, about 6 days for v1; needs the shared package service of B7
  and the extension template). A separate Extensions dialog (palette `extensions.manage`), not a Packages tab. If P3 is tight,
  v1 shrinks to a list, folder install, enable/disable and the trust list (about 3 days).
  - Package format: a `NetPrintsExtension` package type holding the folder the loader already reads (manifest, assembly,
    private dependencies); installed by download and extract, with no NuGet dependency resolution. Stored per user in
    `<data>/extensions/<id>/<version>/`; project scope and "dev" folders later.
  - List and details: Installed, Browse and Updates tabs; state badges (Enabled, Disabled, Failed with NPX code, Restart
    needed, Incompatible); load results and contribution counts; the built-in extension cannot be disabled.
  - Enable, disable, install, update and uninstall apply after a restart ("Restart to apply" bar), because load contexts are
    not collectible; hot enable and disable follow once the contribution registry proves disposal. Documents using a disabled
    extension keep their nodes as unknown nodes.
  - Trust: an install prompt (publisher, source, signature, API compatibility; "always trust this publisher" for
    author-signed packages) and the existing per-project prompt, plus a revoke list. `nuget.config` signature mode is honoured.
  - Safety: nupkg hash and signature checks, zip-slip and size limits, extract to a temp folder and move, uninstall at next start.
  - Conformance: load diagnostics NPX001-NPX008 now; display of a `netprints-verify` report carried in the package later.
  - CLI (v1.1): `netprints extension list|install|uninstall|enable|disable|verify` (`--yes` to skip the prompt; exit 2 without it).
  - Tests: harness-based install, rejection table, state persistence, round trip of nodes of a disabled extension.
  - Spike first: whether `dotnet restore` and feeds accept the custom package type.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b07-b17-packages-and-extensions.md` (outside this repo).
- **Build configuration selector (gap research 2026-10-06)** (B23): a Debug/Release selector next to the run profile (the Publish dialog is in P6).
- **Status-bar item contribution kind (gap research 2026-10-06)** (C-15): added to the contribution registry before P3 publishes it; the
  built-in segments (error and warning counts, build glyph, run state, zoom, selection count) use it.
Done when: also docs updated (guides, API reference, ADRs as applicable).

### P3b — Declarations and code style (owner-approved 2026-09-26)
One extensible engine for what NetPrints can declare and how it writes C#. It is **extensible from the
start**: the built-in kinds, styles and modifiers are registered through the same public API that
profiles and P3 extensions use, so custom emitters need no core changes.
- **Declaration kinds registry.** Each kind's descriptor has:
  - its creation template;
  - the members, graphs and modifiers it allows;
  - its inspector sections and editor UI (through the P3 contribution points);
  - its emission styles.

  The built-in kinds are `class` (today's only kind), `interface`, `struct`/`readonly struct`/`record struct`,
  `record`, `enum` (underlying type, members with values, `[Flags]`) and `delegate`. The class document gets a
  `kind` field (default `class`); the schema stays v1 until the version cut, so no migration is needed.
- **Emission styles.** A feature can have several C# forms. This phase ships **one default style per kind**;
  the other forms are registered in later phases without changing the model. Examples:
  - extension methods (`this` or C# 14 `extension` blocks);
  - the `field` keyword or a backing field;
  - primary constructors;
  - collection expressions;
  - file-scoped or block namespaces.

  A style declares its minimum `LangVersion`. Reading referenced code recognizes every form; today only
  classic `this` extension methods are recognized.
- **Code style from `.editorconfig`, natively.** The project system already loads the project with
  `MSBuildWorkspace`, so Roslyn resolves the project's or solution's `.editorconfig` per document.
  - Formatting: the generated C# goes through `Formatter.FormatAsync` with the document's options.
  - Code-style options are read from `AnalyzerConfigOptions` and followed by the emitters: modifier order
    (`csharp_preferred_modifier_order`), namespace style (`csharp_style_namespace_declarations`), primary
    constructors, collection expressions, `var` and braces.
  - The default style comes from, in order: an `.editorconfig` preference, then the profile, then a per-class
    override (stored only when it differs).
- **Events and subscriptions** (owner report, 2026-09-27):
  - Declare C# `event` members with a delegate type (built-in or a P3b `delegate` kind).
  - Bind an event-graph entry as the handler of an event on this class, a variable or a referenced type.
  - Each binding chooses a **binding mode**:
    - **direct**: `+=`, with `-=` generated on dispose;
    - **weak reference**: a generated weak-handler or weak-event-manager pattern, so the subscriber doesn't keep
      the source alive.
  - Each mode is an emission style. U2's `[UMultiDelegate]`/BlueprintAssignable builds on this.
  - Under the `unreal` profile the mode is fixed. Unreal dynamic delegates bind an object plus a function name
    through a weak object pointer, so they are weak by construction.
- **Parameter modifiers** (moved here from the candidates). Today, arguments of a user-defined method are by
  value only; `ref`/`out`/`in` and optional values work only when calling existing .NET methods; `params` is
  not detected.
  - The modifier registry uses the same descriptor style. Each descriptor has the C# it emits, where it
    applies, an **exclusivity group** and its constraints: last parameter, array or collection type, needs a
    default, first parameter of a static class, minimum C# version.
  - Built-in: `ref`/`out`/`in`/`ref readonly` (one exclusive group), `params`, a default value, `scoped`, `this`.
  - Per-parameter editing: selecting a parameter pin shows its modifiers. Exclusive groups are radio options,
    and invalid combinations are disabled with the reason. The pin context menu and the inspector share one
    model.
  - On the call side, a `params` method shows one pin per element plus "add pin".
- **Custom kinds from libraries.** Example and P3 sample extension: an Ardalis **SmartEnum** kind, edited like
  an enum's member list and emitted as `sealed class X : SmartEnum<X>` with `static readonly` fields.
  UnrealSharp's `[UStruct]`/`[UEnum]`/`[UInterface]` (U2) build on the built-in kinds.
- **P1 guard:** P1's translation seams and class/member emitters (sub-phase F, T065) must not assume every
  declaration is a class. The P1 Opus review checks this; nothing of this phase is built in P1.
- **Designer-authored comments as XML docs** (owner idea, 2026-09-27; ADR-0002, widened same day). A designer
  writes prose that the C#-side developer can read without opening the graph, and that NetPrints itself
  reuses in its own UI. Plain text fields, no new node kind; each reuses an existing UI slot rather than
  adding one:
  - **Node** `Comment`: canvas note (builds on P6's comment boxes/regions), the node's hover tooltip (reuses
    the T063 built-in-doc tooltip); emitted as `//` above the generated statement.
  - **Connection** `Comment`: shown in P6's planned connection tooltip, alongside the live thumbnail/value;
    emission into code as a trailing `//` on the consuming statement rides on sub-phase I's `SourceMap`, so it
    may land a release later than the rest.
  - **Parameter/return pin** `Description`: pin tooltip and inspector; emitted as `<param>`/`<returns>` on the
    owning method's XML doc.
  - **Method/constructor/event graph** `Summary` + `Remarks`: shown in node search results (extends the P6 UX
    audit's doc-preview item to a designer's own methods, not just built-ins) and the method tooltip; emitted
    as `/// <summary>`/`/// <remarks>`.
  - **Class** `Summary` + `Remarks`: class inspector; emitted the same way on the class declaration.
  - **Variable/field** `Summary`: variable list and tooltip; emitted as `/// <summary>` on the field.
  - **Local variable** (sub-phase H, P5) `Comment`: inspector; emitted as an inline `//` (locals get no XML
    doc in C#).

  Whether XML docs are required or optional, and their exact shape, follows the `.editorconfig`-driven style
  this phase already builds.
- **Graph unit tests (gap research 2026-10-06)** (B12, size L, about 2 weeks here and 1 week for the P6 panel). A test is a
  method role, not a declaration kind; tests live in a normal NetPrints test project, so FR-018 is not touched.
  - Test project: a "Test project" template (Exe, xUnit v3 on Microsoft.Testing.Platform, a `ProjectReference` to the
    code under test, one seeded passing test). Needs a richer `ProjectTemplateDescriptor` (packages, project reference,
    seeded files). Spike first: a `ProjectReference` to a graph library must give the editor its types.
  - Declaration: an optional `Test` block on a method graph (display name, skip, traits, cases). The emitter writes
    `[Fact]`, or `[Theory]` with one `[InlineData(...)]` per case, through `IMemberEmitter.Attributes`; no translator change.
    Cases are a typed table (C# constants); non-constant data (`MemberData`) is v1.1. Signature rules are diagnostics.
  - Setup and teardown: inspector buttons add a constructor graph, `IDisposable`/`Dispose`, or `IAsyncLifetime`. Async
    tests return `Task` and use the Await node.
  - Assertions: a curated "Assert" category (Equal, Not Equal, approximate Equal, True/False, Null, Same, collection
    Equal, Contains, Empty, Throws with a delegate pin, Fail), built as a catalog profile over `xunit.v3.assert` plus two
    custom nodes. The block form of Throws waits for P7.
  - CLI: `IProjectSystem.GetTestCommand(project, TestOptions)` (filters, report path, debug) and `netprints test` with
    `--filter-class/-method`, `--report`, `--list`; failures print as node-level canonical lines; MTP exit codes pass through.
    Plain `dotnet test` also works in CI.
  - Tests: emitter goldens, document round trip, signature diagnostics table, Assert translation-compiles, TRX reader
    fixtures, expected/actual parser table, a real seeded project passing and failing (Linux and Windows CLI legs).
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b12-graph-tests.md` (outside this repo).
- **Console template `Main(string[] args)` (gap research 2026-10-06)**: once per-parameter names exist, the Console template seeds
  `public static int Main(string[] args)` (returning 0) in place of P3a's empty `static void Main()`.
- **Done when:** also docs updated (guides, API reference, ADRs as applicable).

### P4 — VSIX (deferred)
Deferred by the project owner on 2026-09-24; revisit after P3/P5. When resumed:
Spike WpfAvaloniaHost in VS 2022/2026 (assembly conflicts); editor factory for `.netpc.json`
with VS project `IDocumentStore`; Community.VisualStudio.Toolkit, SDK-style. Separate VSIX CI
workflow chained after `CI` (`workflow_run`, windows-latest, path-filtered). Resolve the
Nodify.Avalonia 1.0.2 net7.0-only limitation (editor cannot target netstandard2.0) — e.g.
out-of-process editor window or a maintained/forked canvas.
Done when: also docs updated (guides, API reference, ADRs as applicable).

### P5 — VS Code extension + browser build
Spike Avalonia.Browser + Nodify in a webview (CSP `wasm-unsafe-eval`, size, startup);
`NetPrints.Sidecar` (Roslyn/codegen, JSON-RPC `IHostChannel`, shipped as dotnet tool with
bundled self-contained binaries as fallback); TS `CustomEditorProvider`; standalone browser build.
Done when: also docs updated (guides, API reference, ADRs as applicable).

### P8 — Performance
Dedicated optimization phase (owner decision 2026-09-25: performance is kept out of feature
phases). SC-005 target: P0 accepts "typical developer machine" (node search cold start
1.5–1.6 s dev, 3.4 s on the CI runner); P8 brings it within 2 s on slower machines including
the CI runner. Also: translate the generated-code preview off the UI thread; DynamicData /
ReactiveUI throughput for large graphs and catalogs; `MetadataReference` caching; startup
time; memory profile of reflection caches. UX audit items L3 (search cold start) and L4. Benchmarks (BenchmarkDotNet) + performance budgets
enforced in CI.
Done when: also docs updated (guides, API reference, ADRs as applicable).

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
Done when (each of U1, U2, U3): also docs updated (guides, API reference, ADRs as applicable).

### P6 — Usability (Blueprint-level ease of use)
Goal: someone who does not know C# can build complete classes comfortably; the C# stays visible
for those who want to learn it.
- Context-sensitive node menu: drag from a pin → only compatible nodes, ranked; strong filtering
  of the ~119k raw suggestions; categories, favorites, recent nodes.
- Curated catalogs per profile (builds on P2 catalog profiles), high-level nodes, class
  templates (e.g. a new class comes with its lifecycle/event entry points ready).
- Pin-type colors, automatic conversion nodes when linking compatible types.
- Per-node error markers (from P1 diagnostics mapping), collapse selection to function/macro,
  comment boxes/regions (the underlying `Comment`/`Summary` fields and their emission as XML docs are in P3b).
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
    value once visual debugging exists (the wire's `Comment` from P3b's designer-comments item joins the
    same tooltip);
  - edge-of-screen indicators pointing to connected nodes that are off screen;
  - hovering a pin highlights all its connections; for a pin with many connections, a context-menu list
    to jump to any of them;
  - bookmarks (Ctrl+1…9, as in Unreal); **find references** is its own item below (symbol index).
- Canvas editing (2026-09-25):
  - reroute nodes ("knots") and straightening connections;
  - auto-layout of a selection, align and distribute;
  - **copy/paste as text**: a canonical JSON fragment that can be pasted into another instance, an issue or
    an AI chat;
  - a keyboard-only flow: type on the canvas to create a node, Tab to move between pins;
  - an undo history panel, and F8 to jump between errors.
- UX audit additions (2026-09-25, IDs from `docs/research/2026-09-25-ux-audit/`):
  - H5–H9, M1, M2, M4–M15, M17–M21, L7;
  - discoverable node creation, context menus, rich error list, search ranking with doc preview (extended
    by P3b's designer comments to preview a designer's own method `Summary`, not just built-in doc);
  - start page and samples, accessibility (names for icon buttons, contrast ≥ 4.5:1, keyboard navigation);
  - Nodify built-ins not used yet (minimap, fit to view, groups/comments, alignment, keyboard navigation).
- **In-editor visual diff of graphs** (P1 follow-up, moved from P2 per `specs/004-catalog-cli/research.md` R27; needs the P3a shell).
- Items from the gap research 2026-10-06 (each marked "(gap research 2026-10-06)"):
  - Authoring: rename refactoring (B2, own item below);
    graph lint with quick fixes and "dim inactive nodes" (B4); disable (bypass) node (B10); Math Expression node (B11, own item below);
    split/recombine struct, record and tuple pins (B16, after P3b); format string node (B18, own item below); insert a node by dropping
    it on a wire (B19); snippet library (B20, own item below).
  - Navigation: go to definition (B3, own item below); full-text find in project (B8, own item below); member
    categories and folders (B22).
  - Debug and run: clickable stack traces that open the node (B6, own item below); Tests panel over P3b's test role and
    `netprints test` (B12, own item below); hot reload and live edit after debugging B (B13, own item below).
  - Project and collaboration: export graph as SVG/PNG plus `netprints render` (B14); git status decorations and Compare
    with HEAD (B21); the Publish dialog (B23: RID, self-contained); Help > Report a problem diagnostics bundle (B24, if
    not done in P3a sub-phase H).
  - Visual: connection styling, exec versus data wires and a thickness token (C-9); the behaviour halves of the node
    palette (C-2), pin-type colouring (C-3), wire brightening and selection count (C-8), compact/comfortable density and
    zoom-level detail (C-10), and motion with a reduce-motion setting (C-13); the old dialogs adopt the dialog shell (C-14);
    the custom title bar (C-5) is already planned above (L1).
- **Snippet library (gap research 2026-10-06)** (B20, size M, about 5 days for v1). Builds on copy/paste as text: a snippet is the
  same canonical JSON fragment plus a small header. Not a function or macro: it is an unlinked copy (use collapse to function
  for one definition).
  - Format: `*.npsnippet.json` with `fragmentVersion`, `name`, `description`, `tags`, `graphKinds`, `requires` (usings,
    packages), `members`, the existing `GraphDocument` and a local layout. Clipboard, file and snippet share one reader.
  - Scopes: user (per-user data folder) and project (`.netprints/snippets/`, checked into git); project overrides user.
    Extension-contributed snippets come later, through the P3 contribution registry (shared with class templates).
  - Use: "Save as snippet…" in the selection menu; a "Snippets" category in node search (filtered by graph kind; with a dragged
    pin it auto-wires the first compatible open pin); a Snippets panel with preview and drag-to-insert in v1.1.
  - Insert is one pure plan, then one undo step: new node ids; existing members are mapped; missing variables are created;
    missing methods and types are kept as unresolved nodes and reported; a non-modal bar lists what happened.
  - No placeholders in v1 (open pins and literals are the parameters); typed type and literal placeholders are a later item.
  - CLI: `netprints snippet list|validate|add` (invalid file exits 2). Tests: round-trip, inserter table, selection-to-graph
    isomorphism, generated-C# equivalence, precedence, one-step undo.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b20-snippet-library.md` (outside this repo).
- **Symbol index and find references (gap research 2026-10-06)** (size M, about 6 days). One UI-free index in
  `NetPrints.Core`, built on project load and updated per graph, that find references, rename (B2), go to
  definition (B3), full-text find (B8) and later graph lint (B4) read.
  - Index: declarations (classes, methods, variables, locals, parameters, events), references by node with a role
    (call, get, set, delegate, override, type use) and text entries (titles, literals, comments, summaries). Key is
    (kind, declaring type, name, parameter types), the identity `MemberRename` already uses; no format change.
  - Incremental: a stale graph is re-indexed lazily on a background task; external file changes replace one class.
    A benchmark test on a 100k-node project fixes the time and memory budget.
  - Find references (Shift+F12) from a node, the tree or the Variables panel, into a shared Search Results panel
    (Class > Graph > site; click frames the node; F8 next). Works for library members too.
  - Pin test first (P3a): after a rename that edits other classes, every touched class is dirty and saved.
  - B4 reads it for unused symbols and dangling references; optional CLI `netprints refs` and `find` later.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b02-b03-b08-symbol-index.md` (outside this repo).
- **Rename refactoring (gap research 2026-10-06)** (B2, size M, about 5 days for v1). Builds on `MemberRename` and the
  symbol index.
  - A planner returns sites, cascades and conflicts without touching the model; Apply runs the existing retarget.
    v1: methods, variables, custom events, parameters, locals, overrides and interface implementations.
  - Preview dialog (Ctrl+Shift+R, or automatically when other classes are touched): tree Class > Graph > site, live
    conflict text (invalid name, same-signature clash, hides a base member, shadowing). Call, get and set sites are
    all-or-none so no node is left dangling; checkboxes only for override cascades and optional comment and string text.
  - F2 on a selected call, get or set node renames that node's member. One undo step across classes: every touched
    class is marked dirty, and undo re-scans by identity so nodes added after the rename revert too.
  - v1.1: class rename with the file rename; a heuristic list of possible uses in hand-written C# (listed, not edited).
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b02-b03-b08-symbol-index.md` (outside this repo).
- **Go to definition (gap research 2026-10-06)** (B3, size S, about 2 days). F12 or double-click on a node.
  - Project members open their graph (method, constructor), select the variable (accessors with Alt+F12) or open the
    class. The project half needs no index and can land in P3a sub-phase F after connection navigation (about 0.5 day).
  - Library members open a read-only Definition panel: signature, declaring type, assembly, XML summary, parameters
    and returns from the reflection provider, a fallback when the library ships no documentation file, and links to
    find references and copy signature.
  - Navigation history (Alt+Left) records every jump. Peek (inline) is later.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b02-b03-b08-symbol-index.md` (outside this repo).
- **Find in project (gap research 2026-10-06)** (B8, size M, about 4 days for v1). Ctrl+Shift+F into the shared
  Search Results panel.
  - Scopes: project, class, graph, open documents. Kind chips: names, titles, literals, comments and summaries, pin
    names, types. Match case, whole word and regex (non-backtracking or timed out); incremental, cancelled on typing.
  - Results Class > Graph > hit with the match highlighted; click or Enter frames the node; Ctrl+P gets a
    "search in project" footer.
  - v1.1: replace limited to literals and comments, with a per-hit preview and one undo step across classes. Names
    stay with rename. Optional CLI `netprints find`.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b02-b03-b08-symbol-index.md` (outside this repo).
- **Referenced graph projects (gap research 2026-10-06)** (B25 tier 1, size M-L, about 9 days; the build-library bar, S,
  can go to P3). Keeps one project per window (FR-018); a `ProjectReference` to another graph project becomes visible, not
  editable. Today such a reference resolves to the other project's built DLL and nothing builds it on load.
  - Build-library bar: when a referenced graph project has graph files newer than its built DLL, a non-modal bar offers one
    click to build it and reload references. Never builds on open.
  - `ReferencedGraphIndex`: a headless read of the referenced project's graph documents maps types and members to their
    owning graph, so ownership is correct even for members newer than the DLL. Lives in `NetPrints.Core`/`Workspace` so the
    P5 sidecar can reuse it.
  - Go to definition on such a node opens the other project in a new window at the graph and node (or focuses its window);
    renaming a member used by a referencing project in the same folder tree shows a warning, not an edit.
  - Needs B3 go to definition, the B12 spike (types from a `ProjectReference`) and P3a's command bar.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b25-multi-project-workspace.md` (outside this repo).
- **Tests panel (gap research 2026-10-06)** (B12, size M, about 5 days). A dockable panel over P3b's test role.
  - Tree from the graph model (Project, Namespace, Class, Method, theory cases), present before any build; results are
    matched by fully qualified name; results with no graph counterpart appear as "C#" rows.
  - Run all, selected, failed and re-run last through `GetTestCommand` (builds first; errors go to the Error List);
    results come from the TRX the test host writes; state, text and trait filters, a summary bar, group by class or state.
  - Failure detail: message, expected and actual as a diff (parsed from xUnit's message), stack trace; "Open test" selects
    the method entry node. Frames in `*.netpc.g.cs` open the failing node through B6's frame-to-node service.
  - Debug a test launches with `--debug` and attaches through P6 visual debugging. Live results over MTP `--server`
    are a later source behind the same interface.
  - Tests: headless tree/filter/run-failed cases, the stack-frame lookup table (stale file included), one desktop E2E class.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b12-graph-tests.md` (outside this repo).
- **Text nodes: format string and Math Expression (gap research 2026-10-06)** (B18 and B11, size M, about 8.5 days with
  the shared infrastructure). One "text node" mechanism: the user types text, the editor parses it and the node grows
  input pins from the names in it.
  - Shared: a pure `TextNode` in `NetPrints.Core` (`Text`, inputs, one output, diagnostics) with a grammar per kind. Pin
    key is the identifier, so wires follow names; editing keeps a pin's wire and value by name. Parse on typing
    (debounced), apply on Enter or blur as one undo step; a syntax error keeps the old pins.
  - Pin types: the wired type; else a type declared on the pin ("Set type…"); else the grammar default (`double` for
    math, `object` for format). The output type comes from Roslyn binding. Document: `text`, declared types, option
    fields, pin values; pins are rebuilt from the text on load.
  - Format string (B18, about 1.5 days): `Hello {name}`, `{x,5:F2}`, `{{` for a brace; items are names, not
    expressions. Emits an interpolated string; option Culture (current, invariant, named) emits
    `string.Create(CultureInfo, $"...")`.
  - Math Expression (B11, about 3 days): restricted grammar checked on the Roslyn parse tree (C# precedence; operators of
    `OperatorUtil`, `?:`, a closed `System.Math` function list, `pi`, `tau`; no member access or calls elsewhere).
    One node emitting one line; option "Promote to double/float/decimal". v1.1: "Expand to nodes" (about 1 day) replaces
    it with the operator and call nodes, wires kept.
  - Errors: parse and bind diagnostics show on the node, as a squiggle in the text box and in the Error list with the
    node id. Tests: scanner and precedence tables, emission goldens compiled and run, wire-keeping re-parse, undo,
    serialization round trip, expand equivalence.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b09-b11-b18-expression-nodes.md` (outside this repo).
- **Inline C# expression node (gap research 2026-10-06)** (B9 part 1, size M, about 5 days). Builds on the text nodes
  above and on Roslyn binding in a synthetic class built from the translated class's members (a new "bind this text in
  class X" entry point on `CodeAnalysisSession`).
  - A pure node with one C# expression. A free identifier becomes an input pin only if it binds to no member, type or
    namespace and is not a lambda parameter; a later member with the same name removes the pin and says so on the node.
  - No statements, `await` or `ref`/`out` in v1. Pin types as for the text nodes; "Set the type of `a`" when the
    expression needs more than `object`. Output type from binding. Info hint when the expression calls a method (a pure
    node runs wherever its output is used).
  - No sandbox: the text compiles into the user's own program, like a method body typed in code. The guide says that a
    graph from someone else with a code node runs on build.
  - Rename (B2): the symbol index records member references found by binding the text; rename rewrites identifier
    tokens by syntax and checks that the new name does not capture a pin. Snippets (B20) record the usings and members
    the text binds to.
  - Tests: free-name table (lambdas, members, types, `nameof`, named arguments, initializers), static versus instance
    context, member-captures-pin, syntax-based rename, snippet requires.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b09-b11-b18-expression-nodes.md` (outside this repo).
- **Clickable stack traces to nodes (gap research 2026-10-06)** (B6, size M, about 1 week). One frame-to-node service shared
  by Output, the error dialog, the Tests panel (B12) and debugging A.
  - Service: `IFrameResolver` tokenises .NET stack-trace lines and resolves `file:line` of a generated `*.netpc.g.cs`, an
    `np:<graph>/<node>` pseudo-path, or a method name to a class, graph and node. No `#line` on disk: the PDB already
    carries the file and line, in Debug and in Release (checked with the 10.0 SDK). Debugging A maps DAP stop events
    through the same service.
  - Maps come from a run snapshot taken at process start (translated text and file hash), not the live graph. A node
    deleted or changed since the run opens the graph with an info bar; a pasted trace with no snapshot links to the method.
  - Header offset: the generated file has a 3-line header that `DiagnosticMapper.FromBuild` does not subtract (it maps
    against the headerless `TranslatedClass.Code`), so build errors map to the wrong node; fix it here if P3a has not.
  - UI: links in Output lines, in the error dialog (`ex.ToString()`) and in test failures; unresolved frames stay plain.
    Click-to-node reuses the existing `IHostChannel` `focus-document` (`{path, nodeId}`) message.
  - Tests: trace table, resolve table (header offset, `np:`, stale, deleted node), a real fixture run in Debug and Release,
    headless click-through.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b13-b06-hot-reload-and-traces.md` (outside this repo).
- **Hot reload and live edit for desktop runs (gap research 2026-10-06)** (B13, size M, about 5 days; after debugging B,
  B6 and run profiles). "Run with live edit" launches the Debug build through `dotnet watch --non-interactive`; NetPrints
  owns the trigger. A NetPrints-owned `MetadataUpdater` host is deferred (L: Roslyn's EnC analysis is internal and the
  watch protocol changes between SDKs).
  - Trigger: graph change after a debounce (or on save, a setting) re-translates the changed class, writes the `.g.cs` only
    if it changed, and lets `dotnet watch` apply the delta. Translation errors keep the last good file.
  - Status chip from watch's output: applied, restart needed (state reset), not applied. A symbol-level diff of the
    generated files warns before a rename, delete or signature change; the SDK stays the authority.
  - Limits stated in the UI: running frames keep old IL; a restart resets state; stack frames of an edited method lose
    file and line (checked in a spike), so they link to the method only; Release builds and Unreal profiles are excluded.
  - Constraints: instrumentation (debugging B) comes from a translator mode with a fixed call shape, and live mode does
    not run its own `dotnet build` beside watch.
  - Tests: idempotent regeneration and one-method golden pairs, the edit classifier table, watch-output parser fixtures,
    an opt-in integration test of the spike (body edit keeps state, signature edit restarts).
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b13-b06-hot-reload-and-traces.md` (outside this repo).
- **Done when:** also docs updated (guides, API reference, ADRs as applicable).

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
- **Flow-control node set (gap research 2026-10-06)** (B1): ForEach (element and index), While, DoWhile, Switch on int, string or enum,
  Sequence, Break, Continue, Select, optionally DoOnce, Gate and FlipFlop; built on this phase's block scopes (the goto
  translator could ship some earlier).
- **Inline C# statement node (gap research 2026-10-06)** (B9 part 2, size L, about 8 days). The expression node ships in
  P6; this adds statements. Needs this phase's block scopes.
  - Impure node with exec pins and a body of statements. Inputs are the names read and not defined in the body
    (`DataFlowAnalysis` `ReadInside` minus `DefinedInside` minus members); outputs are the outer names the body assigns.
    Emitted as a `{ }` block with locals for the inputs, then the assigned values copied to the outputs.
  - No `return`, `break` or `continue` until the flow-control set (B1); `await` only where the translator supports it.
  - Errors inside the text: the analysis copy emits `#line (l,c)-(l,c) "np:<graph>/<node>"` around the body, so Roslyn
    diagnostics carry the node and a position in its text; files on disk stay without `#line`. `SourceMap` stays the
    fallback for compiler errors elsewhere.
  - Multi-line editor on the code view's AvaloniaEdit control; completion later (RoslynPad evaluated in
    `specs/003-core-refactor/research.md` R4).
  - Tests: free and assigned names, per-block local scope against user locals, `#line` round trip and survival of
    formatting, C# 9 fallback.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b09-b11-b18-expression-nodes.md` (outside this repo).
Done when: also docs updated (guides, API reference, ADRs as applicable).

### Candidates (unscheduled)
- Parameter modifiers and C# syntax variants: moved into **P3b** (2026-09-26).
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
- **Multi-project (solution) workspace (gap research 2026-10-06)** (B25, tier 2 size L, about 25 days; candidate after P5). FR-018
  holds one project per window by design, because compilation, catalogs and extensions are per project. Most of the value comes
  from tier 1 (P6 "Referenced graph projects"); a full solution workspace waits for demand and for proven per-project extension
  isolation (P3).
  - Open a `.sln` or `.slnx` (one API for both through `Microsoft.VisualStudio.SolutionPersistence`) that mixes graph and C#
    projects; a UI-free `Workspace` in `NetPrints.Core` holds the entries, `ProjectSnapshot` stays one project.
  - Tree with a solution root; one active project; C# projects are read-only. Startup project and expanded state are per user in
    `sessions/<workspace-key>.json`; the solution file stays the only shared record.
  - Run builds the solution with `dotnet build` (MSBuild orders the build) and runs the startup project through the run profile.
  - Cross-project find references and rename over one index per graph project; one undo step per project; C# hits are reported,
    never edited.
  - CLI: `<project>` accepts a `.sln` or `.slnx` (or a folder with exactly one); `build`, `generate` and `run` act on every
    graph project; an ambiguous folder exits 2.
  - Extensions load per graph project in their own context with the per-project trust prompt; VS Code (P5) keeps one graph
    project per editor, since C# Dev Kit owns the solution; U1 modules are covered by tier 1.
  - Design and prior art: `.agent-archive/2026-10-06-roadmap-gaps/b25-multi-project-workspace.md` (outside this repo).
