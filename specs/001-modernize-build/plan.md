# Implementation Plan: Modernize Build and Migrate Editor to Avalonia

**Branch**: `001-modernize-build` | **Date**: 2026-09-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-modernize-build/spec.md`

## Summary

Make the whole solution build and test on Linux with the .NET 10 SDK, and replace the WPF editor
with an Avalonia 12 editor at feature parity (60-item inventory). The build moves to `global.json`,
Central Package Management (with transitive pinning) and `Directory.Build.props`. Every project
targets `net10.0` only (constitution 1.2.0); Core uses Roslyn 5.9. Fody is upgraded in Core only, so
it builds. Gapotchenko.FX is removed (`System.HashCode` is built in). A minimal `ReferenceAssemblyResolver`
falls back to the running .NET runtime's assemblies, so compile/run and reflection work on Linux.
The reflection code moves into a UI-free `NetPrints.Reflection`. A new `NetPrints.Editor`
(Avalonia 12.1.3 + Nodify.Avalonia 2.0.0 + CommunityToolkit.Mvvm + DynamicData) and a thin
`NetPrints.Desktop` replace the WPF editor. All tests run on xUnit v3 / Microsoft.Testing.Platform,
UI tests included (Avalonia.Headless), and a single Linux GitHub Actions workflow `CI` gates
merges. The legacy VSIX leaves the solution until P4. All technical decisions and their evidence
are in [research.md](./research.md).

## Technical Context

**Language/Version**: C# `latest` (C# 14) on the .NET 10 SDK (`global.json` 10.0.100,
`latestFeature`). Every project targets `net10.0` only (constitution 1.2.0, scope update 2026-09-24).

**Primary Dependencies** (latest stable for net10.0, see research "Scope update"):
Microsoft.CodeAnalysis.CSharp.Workspaces 5.9.0; Avalonia 12.1.3 (Desktop, Themes.Fluent,
Fonts.Inter, Skia, Headless); Nodify.Avalonia 2.0.0; CommunityToolkit.Mvvm 8.4.2; DynamicData
9.4.33; Xaml.Behaviors.Avalonia 12.0.7; Material.Icons.Avalonia 3.0.2; CommandLineParser 2.9.1;
Fody 6.9.3 + PropertyChanged.Fody 4.1.0 (Core only, until P1).

**Storage**: files — `.netpp`/`.netpc` DataContract XML, unchanged format.

**Testing** (review follow-up, research.md): xUnit v3 3.2.2 on Microsoft.Testing.Platform
(`global.json` test runner). Xunit.DependencyInjection is used for the non-UI editor tests.
Avalonia.Headless.XUnit `[AvaloniaFact(Timeout)]` + Skia runs the UI tests in their own assembly,
with page objects and shared `AutomationIds`. `xUnit1051` is an error, and TRX comes from
`--report-xunit-trx`.

**Target Platform**: Linux first (CI: `ubuntu-latest`); the editor also runs on Windows and macOS.

**Project Type**: libraries + desktop app + CLI.

**Performance Goals**: node search opens in < 2 s and filters in < 300 ms per keystroke over about
119k entries (SC-005); editor start < 5 s (SC-006); CI < 15 min (SC-003).

**Constraints**: view models free of UI types (principle II); reflection stays UI-free; no persisted-format change (principle VI); no Windows CI jobs.

**Scale/Scope**: about 9.2k LOC WPF editor ported (16 XAML files, 10 VMs, 7 converters,
3 dialogs, 60 parity items); 7 projects in the solution after P0.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1 against constitution 1.0.0; re-checked
during implementation against constitution **1.2.0** (net10.0 everywhere).*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Cross-platform, Linux-first | ✅ improves | The WPF editor is removed. Every project in the solution builds and tests on Linux. The VSIX is the documented exception (outside the solution until P4). The Windows `ProgramFilesX86` path stays but gets a cross-platform fallback; full removal is P1 (tracked below). |
| II. UI-agnostic core | ✅ | New `NetPrints.Reflection` has no UI reference. VMs use `GraphPoint`, visual-kind enums and service interfaces instead of `Brush`, `Dispatcher` or `Point` (contracts/editor-services.md). |
| III. Extension-first | ✅ n/a | No target-specific code. Services and VMs are shaped so P3 can add UI contributions. |
| IV. Single target framework (1.2.0) | ✅ | Every project targets `net10.0` only; latest stable dependencies. No analyzers or generators in P0. |
| V. Tests gate every change | ✅ | 11 core tests kept green; new reflection, VM, headless-UI and sample end-to-end tests; Linux CI gate. |
| VI. Readable, deterministic output | ✅ | Deterministic builds verified. Generated C# is unchanged (Core translator untouched; core tests plus a sample compile/run test). No format change. |
| VII. Abstractions for I/O | ✅ | File and folder pickers, dialogs, clipboard, dispatcher, reflection host and process launching go through interfaces. Document stores and formats are P1. |
| VIII. Simplicity, incremental delivery | ✅ (amended, constitution 1.1.0) | Roadmap 1.0 puts the Avalonia editor in P3. The user moved it into P0 (via the coordinator), but the roadmap and constitution edits were **blocked for the spec agent** (protected governance files). They need the user's approval: see "Pending governance amendments". Dead dependencies are removed. |
| Tech constraints | ✅ | .NET 10 SDK, CPM, Directory.Build.props, Roslyn (latest, 5.9), CommunityToolkit.Mvvm, Avalonia 12 + Nodify.Avalonia 2.0 (the canvas now allows Avalonia 12). Nullable is enabled for new projects; the moved and legacy projects are justified below. No Fody in new code. |
| Workflow: CI | ✅ | Linux-only main CI named `CI`. The constitution 1.0.0 wording ("Windows jobs are added where a host requires them") is satisfied: no host requires Windows in P0. The P4 VSIX workflow chains after `CI`. |

**Post-design re-check (after Phase 1)**: unchanged. No new violations; the data model and
contracts keep VMs UI-free.

## Project Structure

### Documentation (this feature)

```text
specs/001-modernize-build/
├── plan.md                 # this file
├── research.md             # Phase 0: decisions + evidence (a–q)
├── data-model.md           # Phase 1: build topology + editor VM graph
├── quickstart.md           # Phase 1: build/test/run + parity walkthrough
├── contracts/
│   ├── ci-workflow.md      # the `CI` workflow contract (P4 chains to it)
│   └── editor-services.md  # VM ↔ host service interfaces and plain value types
├── checklists/requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root, after P0)

```text
global.json                      # new: SDK 10.0.100 latestFeature; test runner MTP
Directory.Build.props            # new: shared settings
Directory.Packages.props         # new: CPM + transitive pinning
NetPrints.slnx                    # updated: 7 Linux-buildable projects
.github/workflows/ci.yml         # new: name "CI"
.travis.yml                      # deleted
README.md                        # updated
samples/HelloWorld/              # new: HelloWorld.netpp, HelloWorld.Program.netpc
src/NetPrints.Core/                       # Core: net10.0 (+ Core/ReferenceAssemblyResolver.cs)
src/NetPrints.Reflection/            # new: moved from NetPrintsEditor/Reflection
src/NetPrints.Editor/                # new: EditorApp + feature folders (Main, ClassEditor, Graph, Search, Variables, References,
                                 #      Inspectors, Dialogs, UndoRedo, ModelSync, Hosting/Avalonia), Assets
src/NetPrints.Desktop/               # new: Program.cs, icon
src/NetPrints.Cli/                    # net10.0
tests/NetPrints.Core.Tests/              # net10.0, xUnit v3 (Core/, Translator/, Compilation/, Samples/)
tests/NetPrints.Editor.Tests/          # new: xUnit + DI; feature folders mirroring the editor
tests/NetPrints.Editor.UITests/        # new: Avalonia.Headless.XUnit; page objects per feature
legacy/NetPrintsVSIX/                   # kept, out of the solution, README "pending P4"
NetPrintsEditor/                 # deleted (WPF)
NetPrintsEditorUnitTests/        # deleted
```

**Structure Decision**: a multi-project .NET solution. Libraries are separated by UI dependency
(Core → Reflection → Editor → Desktop) so that non-UI hosts (CLI, P5 sidecar, P4 VS host) never
pull in Avalonia. See data-model.md §1.

## Implementation phases (drives tasks.md)

1. **Build foundation** (US1/US4): global.json, props files, Core multi-target and Roslyn/Fody/
   CLI and core tests retarget, 11 tests green. The WPF editor and VSIX are removed from
   the solution first, so the build is green on Linux from the first commit.
2. **Cross-platform references** (FR-008..010): `ReferenceAssemblyResolver` in Core, compile and
   run on Linux, sample project + end-to-end test.
3. **Reflection library** (US5): move, namespace change, `RefReadOnlyParameter` fix, tests.
4. **Editor shell** (US2): Editor + Desktop projects, `EditorApp`, theme, services and fakes,
   `MainEditorVM`, main window, project lifecycle, references dialog, class list.
5. **Class editor** (US2): `ClassEditorVM`, class window, lists, inspectors, undo/redo, generated
   code, compile/run panel.
6. **Graph canvas** (US2): `NodeGraphVM`, `NodeVM`, `NodePinVM`, `ConnectionVM`, Nodify views,
   pins, cables, selection, dragging, pan and zoom, keyboard.
7. **Search, dialogs and drag & drop** (US2): `SuggestionListVM` with DynamicData, search popup,
   Select Type/Method dialogs, Get/Set chooser, list-to-canvas drag & drop.
8. **Tests, CI and cleanup** (US3): headless UI smoke tests, CI workflow, README, parity sign-off.

## Complexity Tracking

| Violation / deviation | Why needed | Simpler alternative rejected because |
|-----------------------|------------|-------------------------------------|
| Windows `ProgramFilesX86` framework-path logic kept (principle I) | Changing reference semantics or format is P1 scope; P0 needs Linux to work now | Replacing it now means designing the P1 ref-pack/target model early. The fallback is additive and format-neutral. |
| `Nullable=disable` in Core, NetPrints.Core.Tests and NetPrints.Reflection | ~100 unique warnings in Core; the reflection code is a pure move | Enabling it would bury P0 in unrelated churn. P1 enables it when those projects are refactored. |
| `TreatWarningsAsErrors=false` for Core and Reflection only | Fody and RS1024 warnings in legacy code | Fixing them is P1 work. The editor, the desktop app and all test projects have it on (review follow-up). |
| Editor scope (P3 work) in P0 (principle VIII vs roadmap 1.0) | Explicit user decision relayed by the coordinator | Needs roadmap re-scoping; see below. |

## Governance amendments (approved by the owner and applied 2026-09-24)

Applied as constitution 1.1.0 and the updated roadmap. The owner also **deferred Visual Studio
integration (P4)**, so the Nodify.Avalonia net7.0-only limitation (R1) no longer constrains P0;
the editor stack targets net10.0.

**Constitution 1.2.0 (applied by the owner, 2026-09-24)**: Visual Studio / .NET Framework hosting is
out of scope; every project targets `net10.0` only and uses the latest stable dependencies. The
implementation follows 1.2.0 (see research.md "Scope update").

Original proposal, kept for the record:

The spec agent tried to apply the following edits as the coordinator asked. The environment's
safety classifier **denied** the edits to `.specify/memory/constitution.md` and
`.specify/memory/roadmap.md`, so both files are unchanged at their committed versions. They should
be applied by the user, or with the user's explicit approval, before `/speckit-implement`:

1. **Constitution 1.0.0 → 1.1.0** (MINOR):
   - Principle I: WPF and WinForms are forbidden in every project, except the thin VS host shim in
     `NetPrints.VisualStudio` (P4).
   - Technology Constraints: Avalonia pinned to 11.x while the VS host needs ns2.0/.NET Framework;
     DynamicData for search/filtering; ReactiveUI only where it clearly helps.
   - Development Workflow: main CI is the Linux-only workflow `CI` (`.github/workflows/ci.yml`),
     and it builds the whole solution and runs all tests headless. The single exception is the VS
     extension: its own Windows workflow is chained after `CI` via `workflow_run`, path-filtered,
     and created in P4. Until then the legacy VSIX is out of the solution build and out of CI.
   - Update the Sync Impact Report and set Last Amended to 2026-09-24.
2. **Roadmap**:
   - P0 becomes "Modernize build + Avalonia editor at parity" (~6 w; done when the whole solution
     builds and all tests pass on Linux CI and the parity checklist is verified).
   - P3 becomes "Editor extension host" (~2.5 w, depends on P0 and P1): `--profile`, UI
     contributions, plugin-loaded extensions, DynamicData performance work beyond parity, a sample
     non-Unreal extension, and anything left beyond parity.
   - P1 text changes from "move `ReflectionProvider` to `NetPrints.Reflection`" to "composite
     provider over `NetPrints.Reflection` (moved in P0)".
   - P4 gains "separate VSIX CI workflow chained after main CI (`workflow_run` on `CI`,
     windows-latest, path-filtered)" and the Nodify ns2.0 follow-up (R1).
   - P0's line "GitHub Actions CI on Linux (+ Windows)" becomes Linux-only.
