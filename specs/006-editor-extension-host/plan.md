# Implementation Plan: Editor Extension Host

**Branch**: `006-editor-extension-host` | **Date**: 2026-10-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-editor-extension-host/spec.md` (roadmap phase P3).

## Summary

P3 opens the editor to extensions and builds the user features that need that host. It is planned core first.

- **Foundation.** NetPrints gets its own analyzer and generator projects. They enforce the layout rules (NPS), generate
  the service registrations, the smart-enum ids and the settings validators, and the editor composes through MS.DI with
  generated factory registrations (ADR-0031). Unknown document members survive a save. Research R2–R7.
- **Node model before the freeze.** Pins become typed properties declared by convention, with tracked keys. The 13 pin
  renames ship as a hard break in Core 0.4.0. `out` and `ref` parameters become output pins, purity follows pin roles,
  node capabilities become role interfaces, and node search is split into providers owned by node kinds. Saved graphs
  do not change. Research R8–R11.
- **Public contracts and the publish gate.** `NetPrints.Extensibility.Abstractions` is split from the host. Labels
  become localization keys with default text (L1). Contributions get typed contexts, and an editor facet on the
  extension builder replaces the internal registry API. Extensions get services of their own, templates with files,
  node-data migrations, a status-bar kind and dialogs. The C# target types stabilise. Then the packages are packed and
  validated, and the hygiene tests become analyzers. Research R12–R19.
- **The host in the editor.** Contribution groups per app and per project, failure isolation, `--profile`, Settings
  pages generated from section schemas, user key bindings, theme presets, and the P3a editor carry-overs. Research
  R20, R21, R24, R27.
- **The author kit.** The conformance kit, the `netprints-extension` template, `netprints extension verify`, a reusable
  author workflow, and a sample Godot extension built only against the packed packages. Research R25, R26.
- **Run and build.** Run profiles from `launchSettings.json` (`Project` and `Executable`) in the editor and the CLI, a
  configuration selector, console input and an external terminal. Research R22, R23.
- **Command line and the C# target in the editor.** `netprints new`, the `dotnet new` templates, and node search that
  hides what the project's C# version cannot use. Research R17, R19.
- **Tail.** Inspector sections and value editors, graph rules, translator helpers, node kinds by convention, and user
  and extension themes. The owner kept it in P3 on 2026-10-16, with no scope rule. Research R1, R14, R16, R24.
- **NuGet packages.** A package service with the sources and credentials `dotnet restore` uses, package reference edits
  aware of Central Package Management, a change that rolls back byte-identical when its restore fails, the removal
  impact, and a Packages tab in the References dialog. P3c's extension manager and samples gallery reuse the service.
  Research R32–R35, ADR-0042 (new).
- P3 ends with the release preparation and the final review.

## Technical Context

**Language/Version**: C# (latest), .NET 10 SDK (`global.json` 10.0.100, `rollForward: latestFeature`), `net10.0`
everywhere. The new Roslyn components NetPrints ships target `netstandard2.0` (the constitution IV exception).

**Primary Dependencies**:
- Existing: Avalonia 12.1.3, Semi.Avalonia 12.1.0.1, Irihi.Ursa 2.2.0, Nodify.Avalonia 2.0.0, Dock.* `[12.1.0.6]`,
  CommunityToolkit.Mvvm 8.4.2, Xaml.Behaviors 12.0.7, Microsoft.Extensions.* 10.0.12 (Logging, DependencyInjection),
  NLog 6.2.1, Microsoft.Build.Locator 1.11.2, Microsoft.CodeAnalysis.* 5.9.0 (the analyzers pin
  `Microsoft.CodeAnalysis.CSharp` 5.0.0 through `VersionOverride`, as `NetPrints.Annotations` does), System.Text.Json
  source generation (with `JsonSchemaExporter`), Spectre.Console.Cli 0.57.2, xunit.v3 3.2.2, JsonSchema.Net 9.4.0.
- **New**:
  - `Microsoft.Extensions.Options` 10.0.12, for the `[OptionsValidator]` generator and `IOptionsMonitor<T>`;
  - `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12, as a direct dependency of Abstractions;
  - BenchmarkDotNet, only in the M5 spike, which is not merged.
  - `NuGet.Protocol`, `NuGet.Configuration`, `NuGet.Credentials`, `NuGet.Versioning`, `NuGet.Frameworks` and
    `NuGet.ProjectModel` at one version, the one the pinned SDK ships (the H spike pins it; research R32), only in
    `NetPrints.Packages`. `Newtonsoft.Json` comes with `NuGet.Protocol`; `src/` code never uses it (banned symbol).
  - No other new package. `System.Reflection.Metadata` is inbox.
  - The Dock bump to a release that includes Dock#1191 is conditional (FR-088).

**Storage**:
- Graph and project files: schema v1, additive only.
  - New: unknown members are preserved.
  - New: the optional `kindVersion` on extension nodes.
  - New: `out`/`ref` output pins.
- `settings.json` in the per-user config folder:
  - new keys `netprints.language`, `darkPreset` and `lightPreset`;
  - new sections `keyboard` and `run`.
- Per-user session state gains a `run` block.
- User theme files `*.nptheme.json` in the per-user themes folder (`<config>/NetPrints/themes/`, ADR-0033).
- `Properties/launchSettings.json` is read, and edited from the Run section.
- Package changes edit the project file and, under Central Package Management, `Directory.Packages.props`, and read
  `obj/project.assets.json`; NetPrints stores no package source or credential.
- `schemas/settings.v1.schema.json` (new).
- `NodePins.*.txt` tracking files (new).

**Testing**:
- xUnit v3 on Microsoft.Testing.Platform:
  - view-model tests in `NetPrints.Editor.Tests`;
  - headless UI tests in `NetPrints.Editor.UITests`, in its four namespace shards;
  - Desktop E2E in `NetPrints.Desktop.E2ETests` behind `NETPRINTS_E2E`, one class per scenario (ADR-0006);
  - snapshot baselines with `NETPRINTS_UPDATE_SNAPSHOTS` and goldens with `NETPRINTS_UPDATE_GOLDENS`;
  - CLI tests on Linux and on the Windows leg.
- New test projects: `tests/NetPrints.Analyzers.Tests` and `tests/NetPrints.Extensibility.Testing.Tests` (research R28).

**Target Platform**: the desktop editor and the CLI on Linux, Windows and macOS. CI runs the editor on Linux only. The
Windows CLI leg covers FR-086 and FR-087. Windows and macOS editor behaviour is checked by hand ("For the owner" in
the PR).

**Project Type**: a desktop application (an Avalonia editor library and a desktop host), a CLI, published NuGet
libraries and Roslyn components, a `dotnet new` template package, and a sample extension.

**Performance Goals**:
- SC-005: shell start (M1) and graph open (M2) at most 10% over their R5-0 baselines, measured in the perf job.
- Composition at most 150 ms (the ADR-0031 gate M1).
- Node search keeps P3a's budgets: 30,000 rows, open under 2 s, keystroke under 300 ms.
- The analyzer build-time overhead stays under 2%.
- No other performance work (P8).

**Constraints**:
- No CI job runs longer than 15 minutes (SC-016).
- The trim-warning ratchet may not rise; it drops by 2 with SUG2.
- The XAML literal ratchet may not rise.
- Generated output is byte-identical across locales and across the three writers.
- No publishing: tags, NuGet pushes and releases need the owner's approval.

**Scale/Scope**:
- 15 active user stories, 105 active FRs and 15 active SCs (US13 and US14, FR-094–FR-106, SC-013 and SC-014 moved
  to P3c).
- 10 sub-phases and about 195 units of implementation (a unit is about half a day; 155 in sub-phases A–F, 17 in G, 18
  in H, plus 5 of release preparation in batch PR-P), plus about 10 units of checkpoint reviews and fix batches (5
  checkpoints). The units are human half-days; in agent time P3 is about 5 weeks.
- 9 new `src/` projects, 2 new test projects and 1 sample extension.
- 10 new ADRs: 0024–0030, 0032, 0041 and 0042. Amendments to ADR-0003, 0007, 0010, 0020, 0021, 0023 and 0035; ADR-0026
  is written once, with the amendment content the roadmap names.

## Process (owner-confirmed 2026-10-16)

- **One spec and one PR for P3**: this branch, `006-editor-extension-host`, and draft PR #20 on
  `danielmeza/netprints`.
- **Core first**:
  1. the analyzer and generator foundation (A);
  2. the node model reshape before the API freeze (B);
  3. the stable extension API and the publish gate (C1, C2).
  The features built on them follow (D1 to H).
- **The P3a batch and review process**:
  - Implementer batches run sequentially. Each holds 2–4 related tasks, or up to 6 when they are mechanical, which is
    3–6 units. A design-heavy task counts as 2 units.
  - Each batch ends pushed (see "Process: CI and checkpoints" below for who waits on CI).
  - Reviews run at five checkpoints, not one per sub-phase: A+B, C1+C2, D1+D2, E+F and the final review. Each is one
    adversarial opus review batch of the checkpoint's whole diff plus one reserved sonnet fix batch. The next
    checkpoint's work starts after the fix batch is pushed and its CI is green. Since G and H came back (2026-10-16),
    the fourth checkpoint covers E, F and G (E+F+G, about 40 units), and the final one reviews sub-phase H in full
    with the release preparation, then the whole PR lightly (about 23 units of new work), so there are still five.
  - The sub-phase gate reports (the "Checkpoint X" tasks) stay: they record each sub-phase's evidence.
  - Merge and release need the owner's OK.
- **Test locations and tooling follow P3a and AGENTS.md**:
  - MTP with xunit.v3, passing `TestContext.Current.CancellationToken`;
  - headless UI tests through page objects and `AutomationIds`, with no sleeps;
  - Desktop E2E behind `NETPRINTS_E2E`, run as a separate pass with `--fail-skips on`;
  - snapshot and golden baselines updated only by a task that says so;
  - the existing test projects, except the two exceptions research R28 justifies.
- **Sizing**: the sub-phases below are sized so that `tasks.md` (the next batch, with `speckit-analyze`) can split
  each into batches of 3–6 units.

## Process: CI and checkpoints (owner, 2026-10-16)

- Implementers push and return; they do not wait for CI.
- The coordinator watches PR #20's CI in the background.
- Each batch first checks CI at the previous head and fixes any red before its own tasks. Fixing a red head is the
  batch's first commit, separate from the task commits.
- Estimates are in agent time (see the Governance proposals).

## Standing constraints (every batch)

**Code**
- AGENTS.md applies in full:
  - analyzers at error; fix causes, never suppress; no `!`;
  - XML docs on every public member;
  - `[LoggerMessage]` logging; injected `TimeProvider`; source-generated JSON only;
  - async all the way, and disposal owned by the creator;
  - sealed classes and records;
  - constants for every identifier used in more than one place.
- Load `avalonia-xaml` and `avalonia-styling` (and `avalonia-behaviors` for interaction) before touching any `.axaml`, or
  any view, style or converter code. New views use theme tokens only (ADR-0035 decision 1).
- **Published API**:
  - every change goes through `PublicAPI.*.txt`, and every settling surface carries its NPXE id (research R16);
  - the 0.4.0 breaks use `*REMOVED*` entries and the release notes, never `[Obsolete]` shims;
  - `ExtensionApi.Version` moves with the Abstractions and Extensibility API files.
- Contracts in Abstractions are UI-free (constitution II): ids, `LocalizedText`, icon ids, and view-model factories
  returning `object`.
- New UI text comes from resources (L1, FR-027). The XAML literal ratchet does not grow.
- Code comments stay short. Rationale goes in commit messages and `specs/006-editor-extension-host/implementation-notes.md`
  ("Decision: …").

**Tests**
- Red/green for every bug fix and every risky refactor: pin the invariant first. This covers:
  - the S0 golden before the renames;
  - the search goldens before SUG1;
  - the M1/M2 baselines before any composition change;
  - each hygiene test, kept until its analyzer is red on the test's planted violation;
  - every carry-over defect.
- Goldens in `tests/NetPrints.Core.Tests/Fixtures/Golden/` stay byte-identical unless a task names the change and its
  reason (the expected ones are the `out`/`ref` pins of research R9).
- E2E tests set `NETPRINTS_STATE_DIR` per lease, plus a temporary `XDG_CONFIG_HOME` for settings and key-binding tests.

**Workflow**
- Never leave changes under `samples/`. Edit repository files only with the Edit and Write tools (the Bash guard
  hook enforces it). Never use `DISPLAY=:1`.
- No machine paths in repository files (grep the diff before committing).
- No publishing. Packages are packed into `local-packages/` locally and in CI only.
- Builds and tests: `dotnet build -v q -tl:off --nologo`, then filtered test runs while iterating. The whole suite
  plus the E2E pass run once at the end of each batch (AGENTS.md).
- `.specify/memory/*` is not edited by implementers. Proposals go in implementation-notes.md.

## Constitution Check

*Gate before Phase 0 and re-checked after Phase 1 design: PASS. The items in Complexity Tracking are justified there;
none needs a constitution amendment.*

| Principle | Check | Result |
|---|---|---|
| I. Cross-platform, Linux-first | Stop uses `CloseMainWindow` and a `libc` `kill` import on Unix (no Windows-only API). External terminals have one path per OS behind one service. Paths come from ADR-0033 folders. Editor CI stays Linux; the Windows CLI leg gains FR-086 and FR-087 tests | Pass |
| II. UI-agnostic core | Abstractions holds only UI-free contracts. `GraphPoint`, `LocalizedText`, `CSharpTarget` and the pin roles live in Core with no UI types. Avalonia stays in Editor and Desktop | Pass |
| III. Extension-first, no forks | Godot is supported by a sample extension through data (a profile, a template, `Executable` profiles), with no Godot code in core (ADR-0037 decision 12). The built-in editor registers through the same facet as extensions | Pass |
| IV. Single target framework | Every project targets `net10.0`, except the four Roslyn projects NetPrints ships, which use `netstandard2.0` under the exception | Pass |
| V. Tests gate every change | Every FR has a test obligation in the contracts. Goldens, the S0 layout golden, the search goldens and the snapshot baselines guard drift. Every analyzer rule has a positive and a negative test; every generator has a snapshot test | Pass |
| VI. Readable, deterministic output | Generated C# is identical across the editor, `regen` and the SDK (one language-version resolver with a shared `MaxKnown`). `settings.json`, the schema, `NodePins` files and the unknown members are written canonically. Schema v1 stays additive | Pass |
| VII. Abstractions for I/O | Settings go through `ISettingsStore`, documents through `IDocumentFormat`, processes through `IProcessLauncher`/`ProgramRunner`, type discovery through `IReflectionProvider`, project edits through `IProjectSystem`, and feeds through `IPackageService` (tests use a local folder feed and a fake) | Pass |
| VIII. Simplicity, incremental delivery | One PR. The P3c, P3b and P6 items stay out (research R1, R31). The new projects are justified below | Pass (Complexity Tracking) |
| Technology constraints | CommunityToolkit.Mvvm; System.Text.Json source generation; `[LoggerMessage]`; source generators instead of weaving. MS.DI in the editor is ADR-0031's decision, and the constitution names no container | Pass |

## Project Structure

### Documentation (this feature)

```text
specs/006-editor-extension-host/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── tasks.md                  # batch P3-S2c (speckit-tasks, then speckit-analyze)
├── implementation-notes.md   # created by batch P3-S2c; decisions and checkpoint reports
├── checklists/requirements.md
└── contracts/extension-api.md, settings-and-keybindings.md, run-profiles.md, cli.md, analyzer-diagnostics.md,
    nuget-references.md
docs/adr/0040-…               (committed with the spec)
```

### Source code (new = added by P3, changed = modified)

```text
src/
├── Shared/Roslyn/                       # new: EquatableArray, diagnostic helpers (linked)
├── NetPrints.Authoring/                 # new, shipped in the Core package: pin generator, NPA analyzers,
│                                        #   NodeKinds/ (the S7 generator, NPA030–NPA033)
├── NetPrints.Authoring.CodeFixes/       # new, shipped in the Core package
├── NetPrints.RepoAnalyzers/             # new, internal: NPS, NPH, NPI; [SmartEnum] and DI registration generators
├── NetPrints.RepoAnalyzers.CodeFixes/   # new, internal: NPS fixes
├── NetPrints.Core/                      # changed: typed pins + attributes, PinRole, purity, out/ref pins, PrimitiveKinds,
│                                        #   GraphPoint, LocalizedText, CSharpLanguageVersion/CSharpTarget,
│                                        #   ProcessStartRequest additions, NodeStrings.resx, ExperimentalApiIds,
│                                        #   NodePins.*.txt, the 13 renames, the package ProjectEdit records
├── NetPrints.Serialization/             # changed: [JsonExtensionData] on every record, kindVersion, node-data migrations
├── NetPrints.Extensibility.Abstractions/# new: contracts moved from Extensibility; NetPrints.Extensibility.Editor
├── NetPrints.Extensibility/             # changed: host only; groups, per-extension providers, HostExports,
│                                        #   string resources, settings store preservation
├── NetPrints.Extensibility.Testing/     # new: ExtensionTest<T>, ExtensionHarness (moved from tests), NPK001–NPK016
├── NetPrints.Extensibility.Testing.Xunit/ # new: ConformanceTests<T>
├── NetPrints.Workspace/                 # changed: C# target capture, Run/ (launch settings, resolver, splitter),
│                                        #   Templates/ (console, library trees), MSBuild variable records
├── NetPrints.Generator/, NetPrints.Sdk/ # changed: the generate request carries the language version
├── NetPrints.Cli/                       # changed: new, extension verify, run -lp/--no-launch-profile
├── NetPrints.Templates/                 # new: dotnet new package (netprints-console, -library, -extension)
├── NetPrints.Packages/                  # new, not packed: IPackageService (sources, credentials, search, versions,
│                                        #   metadata, cache, offline), package changes with rollback, removal impact
├── NetPrints.Editor/                    # changed: Composition/ (generated registrations, scope factories);
│                                        #   Contributions/ (groups, snapshots, built-in extension, typed contexts);
│                                        #   NodeSearch/ (providers by kind, creation service, ranker);
│                                        #   Settings/ (document, generated pages, keyboard page); Theming/ (catalogue,
│                                        #   contrast, presets, overlay, user and extension themes); Run/ (profile
│                                        #   button, configuration, input line); Inspectors/ (contributed sections,
│                                        #   the value-editor contract); References/Packages/ (the Packages tab);
│                                        #   StatusBar items; Strings.resx; BuiltInViews.axaml
└── NetPrints.Desktop/                   # changed: --profile, composition root
schemas/settings.v1.schema.json          # new
extensions/NetPrints.Godot/              # new, outside NetPrints.slnx: src/, tests/, NuGet.config, Directory.*.props
tests/
├── NetPrints.Analyzers.Tests/           # new: every NetPrints analyzer, generator and code fix
├── NetPrints.Extensibility.Testing.Tests/ # new: the kit's own tests
├── Fixtures/Extensions/Conformance/     # new: one failing fixture per NPK check
├── Fixtures/RunProfiles/, Fixtures/CSharpTarget/ (six workspace fixtures)   # new
├── Fixtures/Packages/                   # new: plain, CPM and VersionOverride projects; Fx.Packages.Sample
├── NetPrints.Core.Tests/                # changed: pins, roles, PrimitiveKinds, unknown members, NodePins; hygiene
│                                        #   tests removed after S5 red/green
├── NetPrints.Editor.Tests/              # changed: groups, settings, keyboard, presets, run, search providers,
│                                        #   inspector sections, value editors, themes, Packages/
├── NetPrints.Editor.UITests/            # changed: surfaces per kind, settings and keyboard pages, overlay, run
│                                        #   surfaces, the Packages tab
├── NetPrints.Desktop.E2ETests/          # changed: ExtensionContributions, RebindShortcuts, RunExecutableProfile,
│                                        #   StartWithProfile, ManagePackages
├── NetPrints.Cli.Tests/                 # changed: new, extension verify, run profiles, template parity, CRLF, git-install
├── NetPrints.TestExtension/             # changed: one item of every kind; tested only through the kit
└── NetPrints.Testing/                   # changed: Roslyn test host (moved from Catalog.Tests)
.github/workflows/ci.yml                 # changed: Analyzers leg; packages job validates, packs and uploads; Godot job
.github/workflows/netprints-extension.yml  # new: reusable author workflow
.github/workflows/cli-windows.yml        # changed: FR-086, FR-087 tests
.gitattributes                           # changed: *.npcat.json text eol=lf
docs/guide/: diagnostics.md, run-profiles.md, extension-testing.md, editor/settings.md, editor/themes.md,
             editor/packages.md (new); extensions.md, cli.md, editor/commands-and-shortcuts.md (changed)
docs/api/docfx.json (changed: new packages), .github/release-notes.md (0.4.0), docs/adr/ (new and amended ADRs)
scripts/pack-local.sh, scripts/verify-packages.sh, eng/trim-warnings.txt (changed)
```

**Structure decision**: follow ADR-0001's layout, with feature folders inside each project. The new projects follow
research R2, R12, R25 and R26 and are justified in Complexity Tracking. Dock stays referenced only by
`NetPrints.Editor/Shell/Docking`.

## Implementation sub-phases (drives tasks.md)

Units are about half a day each. "Batches" assumes 3–6 units per batch. The five review checkpoints (A+B, C1+C2,
D1+D2, E+F+G, and the final one, which reviews H in full) are each an adversarial review of the group's diff plus a
reserved fix batch, which the units do not include. The units and batches are the ones tasks.md refined from this
plan's first estimate (A 16, B 25, D1 25, D2 14, G at most 22 with its release preparation). G's release preparation
(5 units) runs in batch PR-P, before the final review. H is new on 2026-10-16. The total is about 195 units. FR-116
(docs) and FR-118 (test locations) apply to every sub-phase: each one's guides and ADRs land in its own tasks.

| Sub-phase | Stories, FRs | Content | Units | Batches | Checkpoint gate |
|---|---|---|---|---|---|
| **A. Foundation** | US1; FR-001–FR-008, FR-082–FR-087 | S1: the four Roslyn projects, `Shared/Roslyn`, wiring, `NetPrints.Analyzers.Tests` and its CI leg; NPS001–NPS003 with fixes and the one-commit sweep; ADR-0024 and `docs/guide/diagnostics.md`. R5-0: the M1 and M2 baselines and the M5 spike (gate). DI attributes, the registration generator, `[ScopeFactory]`, NPI001–NPI004. `[SmartEnum]` with NPI010–NPI011; `CommandKey`/`CommandKeyId`; `CommandGesture` span parsing; typed `DefaultGestures`. `[OptionsValidator]` sections and start-up validation. Unknown document members (FR-007). FR-082, FR-083, FR-085, FR-086, FR-087 (red then green). FU-5 triage (FR-084) | 19 | 5 | A: 0 NPS violations; every A rule and generator tested; FR-007 round trip; Windows CLI tests green; SC-015; R5-0 numbers and the M5 outcome recorded |
| **B. Node model and search (Core 0.4.0)** | US2; FR-009–FR-018, FR-117 | SUG1: providers grouped by kind, the two view-model renames, catalog metadata. SUG2 and SUG3: creation service, `Func<NodeGraph, Node>` factories, ranker; trim −2. S2a: typed pins, attributes, the convention generator, the pack spike. S2b: NPA001–NPA009 with fixes. S4: `NodePins` files, NPA010/NPA011. S3: the 13 renames and 2 overrides, `*REMOVED*`, release notes. R2: role interfaces. Pin roles, `out`/`ref` output pins, purity. `PrimitiveKinds` with `sbyte` and `decimal`. ADR-0025, ADR-0028, ADR-0029; the extension guide's "Declaring pins" | 26 | 7 | B: SC-002; FR-017 budget; trim ratchet down by at least 2; the pack spike's outcome recorded |
| **C1. Public contracts** | US3, US9 (core); FR-019, FR-022, FR-027, FR-028, FR-070–FR-072, FR-075 | R7: the Abstractions split, the architecture test, ADR-0030. L1: keys, `LocalizedText`, the resx files, `netprints.language`, Semi/Ursa locale, NPA040–NPA042, per-pin automation ids, ADR-0027 and the ADR-0023 amendment. SUG4 (NPXE0006, NPX013). The C# target: types, evaluation capture, generate request, translator `Target`, literal formatting, NPW006–NPW008, the six fixtures, the three-writer test. R5a and R5b: typed contexts, `ICommandContext`, `IProjectSession`, `IGraphEditor`, `GraphPoint` to Core. DI steps: app and shell (gate M1), project session (resolve-all), class and graph (gate M2) | 24 | 6 | C1: SC-005, SC-010 (writers); API files reviewed for shape |
| **C2. Facets and the publish gate** | US3; FR-020, FR-021, FR-023–FR-026, FR-029–FR-031, FR-038 | N1: the facet, the Null Object, the built-in editor as `netprints.editor`, `BuiltInViews.axaml`. N8: view resources and icon ids. Extension services and `HostExports` (NPXE0008). R4: dialogs. N3: templates with files, with the built-in trees moved to Workspace. N4: node-data migrations and `kindVersion`. The status-bar kind and the built-in segments. The settings page kind and contracts (NPXE0009/0010). The manifest's `themes` field. The `ExtensionApi.Version` tie. Packaging: validation, READMEs, `pack-local.sh`, `verify-packages.sh`. **Publish gate.** Then S5 (NPH001–NPH006, replaced tests removed after red/green, ADR-0003 and ADR-0007 amendments) and S6 (NPA020–NPA024). ADR-0020 and ADR-0021 amendments; the extensions guide rewritten on the packages, with the NPXE table and the CLI trust note | 22 | 6 | C2 (publish gate): SC-001 (NPH); package validation and API checks pass; 100% XML docs on published packages; `NetPrints.TestExtension` builds against the packed packages; SC-005 rechecked |
| **D1. Extension host in the editor** | US4, US5, US6, US8; FR-032–FR-036, FR-039–FR-050, FR-066–FR-069, FR-088 | Contribution groups, snapshots, surface rebuilds and the owner shown in the palette and the sheet. Failure isolation (research R27) with NPX009–NPX011 in Errors. The per-user app group and NPX012. `--profile`. The Settings document: generated pages, search, validation, the restart bar, the built-in pages, the S10 schema file and docs table, store preservation, the unreadable copy. The Keyboard shortcuts page: overrides, conflicts, reset, "Not loaded", the `RebindShortcuts` E2E. B0 presets: catalogue, contrast math, overlay, the four presets, the Appearance page. FR-088: the `IsPinned` warning, the cable-middle hover, the `ShellMainFlow` watch, the Dock bump check. The settings and theming guides | 26 | 7 | D1: SC-004, SC-006, SC-007, SC-009 |
| **D2. Author kit and the Godot sample** | US10; FR-037, FR-076–FR-081 | `NetPrints.Extensibility.Testing` and the xUnit adapter; NPK001–NPK016 and a failing fixture for each; `NetPrints.TestExtension` tested only through the kit; the ADR-0010 amendment. The extension package format and the `netprints-extension` template. `netprints extension verify` (NPK017, JSON) and ADR-0041. The reusable author workflow. `extensions/NetPrints.Godot`, built from the packed packages in CI. The extension testing guide | 15 | 4 | D2: SC-003, SC-011 |
| **E. Run and build** | US7; FR-051–FR-065 | Launch settings reader; the resolver (expansion, splitter and joiner, paths, NPR codes); `ProcessStartRequest` additions; the MSBuild variable scrub; `Project`/`Executable` in the editor and in `netprints run` (`-lp`, `--no-launch-profile`, `-- <args>`); the split button, Run enablement, the first-run line; stop and kill tree; the unload prompt; Project settings › Run; the configuration selector (B23); the Output input line and the external terminal (B5); `RunExecutableProfile` E2E; the run profiles guide and CLI reference | 17 | 5 | E: SC-008 |
| **F. Command line and the C# target in the editor** | US15, US9 (editor); FR-073, FR-074, FR-107, FR-108 | `netprints new` and `--list`; the `NetPrints.Templates` package and the parity test; node-search availability (hidden count, "Show unavailable"), quick fixes ("Set LangVersion", PolySharp hint). The ADR-0035 amendment records B1 and C in G and C's distribution in P3c | 6 | 2 | F: the US15 tests and SC-010 (editor); the ADR-0035 amendment |
| **G. Tail** | US16, US17; FR-109–FR-115 | R6: the value-editor contract, inspector sections, value editors (NPXE0014). N2: graph rules (NPXE0015), NPK014 and ADR-0032. N5 (NPXE0016). S7 and SUG5 (NPXE0017, NPA030–NPA033) and ADR-0026 with its amendment content. B1: user theme files, Reload and Duplicate. C: the themes of loaded extensions. The extensions and themes guides. The model-driven grid stays in P3b | 17 | 4 | G: the US16 and US17 tests; SC-002 rechecked after the built-in kinds move to the convention; SC-001 for NPA030–NPA033; NPK014's failing fixture (SC-011) |
| **H. NuGet packages** | US12; FR-089–FR-093, SC-012 | The NuGet spike (library pin, credential plugins, trim ratchet, the `packageType` note for P3c) and ADR-0042. `NetPrints.Packages`: sources and credentials, search, versions, metadata, cache, offline fallback, installed and transitive packages. The package `ProjectEdit` records with Central Package Management. Apply, restore and roll back. The removal impact. The Packages tab. `ManagePackages` E2E. The packages guide | 18 | 4 | H: SC-012; US12 scenarios 1–5; no secret in state or logs; the trim ratchet not up |
| **Release preparation (batch PR-P)** | FR-116, FR-117, FR-118, SC-016, SC-017 | The regenerated guide images. The 0.4.0 release notes and a check of FR-116's list (the guides and the API reference land in the sub-phases that build them). The quickstart run. The CI budget, ratchet and docs-site checks; the SC table | 5 | 1 | SC-016, SC-017, all SCs; reviewed by the final review |

**Order rationale.**
- A gives every later batch the analyzers, the DI attributes and the typed gestures.
- B changes the public shapes of Core while nothing outside depends on them yet. That is the owner's 0.4.0 window.
- C1 defines the contracts on top of the reshaped Core.
- C2 opens the facets and closes the publish gate. After C, a public shape changes only under an NPXE id.
- D1 builds the visible host on the frozen API.
- D2 builds the kit and the sample on the published packages.
- E and F do not depend on D, but come after it by story priority.
- G extends the C2 facet and the D1 presets with experimental surfaces (NPXE0014–NPXE0017), so it may follow the
  publish gate. It reviews with E and F.
- H depends on nothing in E to G and nothing depends on it, so it runs last although US12's priority (P3) is above the
  tail's (P4): the fourth checkpoint stays near the others' size (about 40 units against 41–46), and the self-contained
  package work is reviewed in full at the final checkpoint, right after its quickstart section runs in PR-P.
- The release preparation and the final review close P3.
- The MVP slice is A–C plus D1's contribution groups (US1–US4).

**Gate outcomes.**
- **M5 spike (A).** If MS.DI with the registration generator fails the ADR-0031 terms, the composition steps of C1 stop.
  The coordinator rewrites them for the alternative before C1 starts, and records it in the implementation notes.
- **M1 or M2 (C1).** A failed gate stops the migration at that step, which is reported (FR-004). The later steps wait
  for the owner.
- **Pack spike (B).** If the `PrivateAssets` route does not deliver the analyzers exactly once, the template
  references Core directly (research R12). The coordinator adjusts the D2 template task.
- **No scope rule.** The owner kept the tail and the NuGet packages in P3 on 2026-10-16 with no cut line, so Checkpoint
  F cuts nothing. The ADR-0035 amendment there records the B1 and C placement.
- **NuGet spike (H).** If the credential plugins cannot run non-interactively in-process, the service reports NPW012
  with the `dotnet restore --interactive` hint for such sources (research R32) and the batch goes on. If the NuGet
  libraries raise the trim ratchet, the batch fixes the new warnings in NetPrints' code or reports to the coordinator
  before T159; the ratchet is never raised.

## ADR schedule

| ADR | Content | Sub-phase |
|---|---|---|
| 0024 | Analyzer and generator infrastructure; NPS, NPA, NPH, NPI; amends ADR-0003's StyleCop note | A |
| 0025 | Declarative node pins by convention; pin-key tracking; the 0.4.0 rename policy | B |
| 0028 | Node search providers and node creation | B |
| 0029 | Node capabilities as role interfaces; pin roles and purity | B |
| 0027 | Localization: stable keys and display text | C1 |
| 0030 | The Extensibility split and the editor facet; CLI trust for project-declared extensions | C1 (split), C2 (facet) |
| 0041 (new) | Extension packages, `netprints extension verify` and the author workflow | D2 |
| 0032 | Graph rules | G |
| 0026 | Node kinds by convention, with the amendment content (`PrepareFromSearchAsync`, offers from typed pins) | G |
| 0042 (new) | The NuGet package service and package reference edits: the NuGet client libraries in `NetPrints.Packages` (with `Newtonsoft.Json` transitive and banned in `src/`), sources and credentials from `nuget.config` only, no secrets in NetPrints state, CPM-aware edits through the MSBuild construction model, byte-identical rollback, the removal impact, reuse by P3c | H |
| Amendments | ADR-0023 (Semi and Ursa locale) in C1; ADR-0003 and ADR-0007 (S5) in C2; ADR-0020 (typed contexts, groups, `AppliesTo`, new kinds, label keys, menu ids, factories through DI) and ADR-0021 (extension icon ids) in C2; ADR-0010 (conformance checks for providers, graph rules and contributions) in D2; ADR-0035 (B1 and C in G, C's distribution in P3c) in F | |

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Four `netstandard2.0` Roslyn projects (Authoring and RepoAnalyzers, each with CodeFixes) | Shipped and internal rules must not ship together, and RS1038 keeps code fixes out of compiler-loaded assemblies | One project would ship internal rules to authors. Fixes inside the analyzer break RS1038. Constitution IV allows `netstandard2.0` for analyzers NetPrints ships |
| `NetPrints.Extensibility.Abstractions` | Authors compile against contracts without the loader (SOLID decision 3, ADR-0030) | A single package would pin authors to host internals and to `System.Runtime.Loader` |
| `NetPrints.Extensibility.Testing` and `.Testing.Xunit` | The kit is a published product (ADR-0010), framework-agnostic, with an optional adapter | A test-only helper cannot reach authors. Folding xUnit into the kit forces one test framework on authors |
| `NetPrints.Templates` package | `dotnet new` needs a template package (FR-108) | Shipping templates inside the CLI tool would not register with `dotnet new` |
| `NetPrints.Packages` project with the NuGet client libraries (and `Newtonsoft.Json` through them) | One service for the Packages tab and P3c's extension manager and samples gallery, with the sources and credentials of `dotnet restore` (FR-089, research R32) | Inside `NetPrints.Workspace`, the libraries would reach the CLI and the build host; a hand-written V3 client would re-implement `nuget.config` and the credential plugins; `dotnet package search` per query is too slow and lacks the metadata |
| `tests/NetPrints.Analyzers.Tests` and `tests/NetPrints.Extensibility.Testing.Tests`, and one more CI leg | Roslyn hosts for `netstandard2.0` analyzers; the kit's own tests (the spec exempts them, FR-118) | Hosting analyzer tests in every test project needs extern aliases. Catalog.Tests is about catalogs |
| `extensions/NetPrints.Godot` outside the solution | It must build only against the packed packages (FR-037) | Inside the solution, project references would hide packaging defects. Under `samples/` it would inherit the samples' props |
| A DI container in the editor (MS.DI) | ADR-0031: one registration style with extensions, options, per-extension providers | A hand-written root gives extensions no way to register services (ADR-0031 decision 6) |
| Optional `kindVersion` on node documents | Extension node-data migrations need a version (research R18) | Shape-sniffing migrations are ambiguous. The member is written only by kinds that version, so every existing file stays byte-identical |
| `x-netprints-*` keywords in the settings schema | Generated pages need restart, path and choices hints the JSON Schema vocabulary lacks | Hard-coding per-section UI would defeat generated pages (ADR-0040 decision 4) |

## Governance proposals

- **Roadmap phases table (owner-approved 2026-10-16, updated in batch P3-S2e).** P3's estimate is about 5 weeks in
  agent time: about 195 implementation units plus 5 review checkpoints (it was about 4 weeks for 160 units). Plan
  units are human half-days and overstate calendar time by about 5 times, so the roadmap states estimates in agent
  time. P3b no longer holds the tail. P3c shrinks to the extension manager and the remote samples, about 12 human days
  (about 24 units), on P3's package service: about 1 week in agent time.
- **Roadmap P3 section (optional wording).** Name the C1/C2 and D1/D2 checkpoints. Name ADR-0041 for the extension
  package format and `verify`, with "`netprints-verify` is `netprints extension verify`" (already a spec decision).
- **Constitution.** No change needed.

## Follow-ups (recorded, not in P3)

- **P3c**: the extension manager and its per-user folder, remote samples, the samples gallery's `--profile` filter,
  and installing extension packages, all on P3's package service (which P3c extends with package-type searches and
  download, verify and extract). P3c may change the extension package format after its feed spike.
- **P3b**: declaration kinds, member defaults and metadata, the model-driven grid, user attributes, NPT010 and NPT011,
  target probes, the style matrix, and `Requires` on the other descriptors.
- **P6**: L2 and L3.
- **v1.1**: the `netprints package` verbs on the package service.
- **Later**:
  - hot enable and disable, collectible load contexts and extension seams;
  - `AddRunProfileKind` (U3 or NetPrintsUnity);
  - a per-user run environment;
  - `netprints check` for the C# target and `ICSharpTargetProvider`;
  - keeping the Windows external-terminal window open after exit;
  - the nightly co-loading job, once two published extensions exist.
