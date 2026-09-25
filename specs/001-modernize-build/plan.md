# Implementation Plan: Modernize Build and Migrate Editor to Avalonia

**Branch**: `001-modernize-build` | **Date**: 2026-09-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-modernize-build/spec.md`

## Summary

Make the whole solution build and test on Linux with the .NET 10 SDK, and replace the WPF editor
with an Avalonia 11 editor at feature parity (60-item inventory). The build moves to `global.json`,
Central Package Management (with transitive pinning) and `Directory.Build.props`. Core
multi-targets `netstandard2.0;net10.0` on Roslyn 4.14. Fody is upgraded in Core only, so it builds.
Gapotchenko.FX is replaced by `Microsoft.Bcl.HashCode`. A minimal `ReferenceAssemblyResolver`
falls back to the running .NET runtime's assemblies, so compile/run and reflection work on Linux.
The reflection code moves into a UI-free `NetPrints.Reflection`. A new `NetPrints.Editor`
(Avalonia 11.3.22 + Nodify.Avalonia 1.0.2 + CommunityToolkit.Mvvm + DynamicData) and a thin
`NetPrints.Desktop` replace the WPF editor. All tests run on MSTest 4 / Microsoft.Testing.Platform,
UI tests included (Avalonia.Headless), and a single Linux GitHub Actions workflow `CI` gates
merges. The legacy VSIX leaves the solution until P4. All technical decisions and their evidence
are in [research.md](./research.md).

## Technical Context

**Language/Version**: C# `latest` (C# 14) on the .NET 10 SDK (`global.json` 10.0.100,
`latestFeature`). Libraries target `netstandard2.0;net10.0`; apps and tests target `net10.0`.

**Primary Dependencies**: Microsoft.CodeAnalysis.CSharp.Workspaces 4.14.0; Avalonia 11.3.22
(Desktop, Themes.Fluent, Fonts.Inter, Skia, Headless); Nodify.Avalonia 1.0.2;
CommunityToolkit.Mvvm 8.4.2; DynamicData 9.4.33; Avalonia.Xaml.Behaviors 11.3.0.6;
Material.Icons.Avalonia 2.4.1; CommandLineParser 2.9.1; Fody 6.9.3 + PropertyChanged.Fody 3.4.1
(Core only, until P1); Microsoft.Bcl.HashCode 6.0.0 (ns2.0 only).

**Storage**: files — `.netpp`/`.netpc` DataContract XML, unchanged format.

**Testing**: MSTest 4.4.1 (metapackage) on Microsoft.Testing.Platform (`global.json` test runner);
Avalonia.Headless + Skia for UI tests through a `UiTest.RunAsync` exception-capturing helper and
`[Timeout]`; TRX reports.

**Target Platform**: Linux first (CI: `ubuntu-latest`); the editor also runs on Windows and macOS.

**Project Type**: libraries + desktop app + CLI.

**Performance Goals**: node search opens in < 2 s and filters in < 300 ms per keystroke over about
119k entries (SC-005); editor start < 5 s (SC-006); CI < 15 min (SC-003).

**Constraints**: view models free of UI types (principle II); reflection stays UI-free and
ns2.0-capable (P4); no persisted-format change (principle VI); no Windows CI jobs.

**Scale/Scope**: about 9.2k LOC WPF editor ported (16 XAML files, 10 VMs, 7 converters,
3 dialogs, 60 parity items); 7 projects in the solution after P0.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1. Checked against constitution **1.0.0**
(the committed version).*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Cross-platform, Linux-first | ✅ improves | The WPF editor is removed. Every project in the solution builds and tests on Linux. The VSIX is the documented exception (outside the solution until P4). The Windows `ProgramFilesX86` path stays but gets a cross-platform fallback; full removal is P1 (tracked below). |
| II. UI-agnostic core | ✅ | New `NetPrints.Reflection` has no UI reference. VMs use `GraphPoint`, visual-kind enums and service interfaces instead of `Brush`, `Dispatcher` or `Point` (contracts/editor-services.md). |
| III. Extension-first | ✅ n/a | No target-specific code. Services and VMs are shaped so P3 can add UI contributions. |
| IV. Host compatibility | ⚠️ justified | Core and Reflection target `netstandard2.0;net10.0`, and apps target `net10.0`. `NetPrints.Editor` is `net10.0`-only because Nodify.Avalonia 1.0.2 ships only net7.0. P4 needs a ns2.0 editor for the VS host: see Complexity Tracking and research R1. |
| V. Tests gate every change | ✅ | 11 core tests kept green; new reflection, VM, headless-UI and sample end-to-end tests; Linux CI gate. |
| VI. Readable, deterministic output | ✅ | Deterministic builds verified. Generated C# is unchanged (Core translator untouched; core tests plus a sample compile/run test). No format change. |
| VII. Abstractions for I/O | ✅ | File and folder pickers, dialogs, clipboard, dispatcher, reflection host and process launching go through interfaces. Document stores and formats are P1. |
| VIII. Simplicity, incremental delivery | ⚠️ pending governance | Roadmap 1.0 puts the Avalonia editor in P3. The user moved it into P0 (via the coordinator), but the roadmap and constitution edits were **blocked for the spec agent** (protected governance files). They need the user's approval: see "Pending governance amendments". Dead dependencies are removed. |
| Tech constraints | ✅ | .NET 10 SDK, CPM, Directory.Build.props, Roslyn 4.x, CommunityToolkit.Mvvm, Avalonia 11.x + Nodify.Avalonia. Nullable is enabled for new projects; the moved and legacy projects are justified below. No Fody in new code. |
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
NetPrints.sln                    # updated: 7 Linux-buildable projects
.github/workflows/ci.yml         # new: name "CI"
.travis.yml                      # deleted
README.md                        # updated
samples/HelloWorld/              # new: HelloWorld.netpp, HelloWorld.Program.netpc
NetPrints/                       # Core: ns2.0;net10.0 (+ Core/ReferenceAssemblyResolver.cs)
NetPrints.Reflection/            # new: moved from NetPrintsEditor/Reflection
NetPrints.Editor/                # new: EditorApp, ViewModels, Views, Services(+Avalonia impls), Converters, Commands, Messages, Assets
NetPrints.Desktop/               # new: Program.cs, icon
NetPrintsCLI/                    # net10.0
NetPrintsUnitTests/              # net10.0, MSTest 4
NetPrints.Editor.Tests/          # new: Reflection/, ViewModels/, Ui/, Samples/
NetPrintsVSIX/                   # kept, out of the solution, README "pending P4"
NetPrintsEditor/                 # deleted (WPF)
NetPrintsEditorUnitTests/        # deleted
```

**Structure Decision**: a multi-project .NET solution. Libraries are separated by UI dependency
(Core → Reflection → Editor → Desktop) so that non-UI hosts (CLI, P5 sidecar, P4 VS host) never
pull in Avalonia. See data-model.md §1.

## Implementation phases (drives tasks.md)

1. **Build foundation** (US1/US4): global.json, props files, Core multi-target and Roslyn/Fody/
   HashCode, CLI and core tests retarget, 11 tests green. The WPF editor and VSIX are removed from
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
| `NetPrints.Editor` is `net10.0`-only (principle IV expects ns2.0 for VS-host libraries) | Nodify.Avalonia 1.0.2 (the only Avalonia-11 Nodify) ships only `lib/net7.0` | Avalonia 12 / Nodify 2.0 drop ns2.0 entirely, which is worse for P4. A custom canvas contradicts the agreed stack. Resolution is deferred to P4 (research R1), with Nodify isolated in views. |
| Windows `ProgramFilesX86` framework-path logic kept (principle I) | Changing reference semantics or format is P1 scope; P0 needs Linux to work now | Replacing it now means designing the P1 ref-pack/target model early. The fallback is additive and format-neutral. |
| `Nullable=disable` in Core, NetPrintsUnitTests and NetPrints.Reflection | ~100 unique warnings in Core; the reflection code is a pure move | Enabling it would bury P0 in unrelated churn. P1 enables it when those projects are refactored. |
| `TreatWarningsAsErrors=false` | Fody/RS1024/MSTEST0017 warnings in legacy code | Fixing them is P1 work; errors still fail the build. |
| Editor scope (P3 work) in P0 (principle VIII vs roadmap 1.0) | Explicit user decision relayed by the coordinator | Needs roadmap re-scoping; see below. |

## Pending governance amendments (user approval required, NOT applied)

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
