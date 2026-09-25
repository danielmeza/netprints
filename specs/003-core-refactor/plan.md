# Implementation Plan: Core Refactor and Extension Points

**Branch**: `003-core-refactor` | **Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-core-refactor/spec.md`

> **Start condition.** An owner-approved repository reorganization PR lands **before** P1
> implementation: projects move to `src/` and `tests/` (`NetPrints/` → `src/NetPrints.Core`,
> `NetPrintsCLI` → `src/NetPrints.Cli`, `NetPrintsUnitTests` → `tests/NetPrints.Core.Tests`, the VSIX
> to `legacy/NetPrintsVSIX`), the solution becomes `NetPrints.slnx`, `.editorconfig`/`.gitattributes`
> are added and `dotnet format` is enforced in CI. Namespaces do not change. **All paths in these
> documents use the new layout.** The implementer rebases `003-core-refactor` onto `master` after that
> PR merges and starts with T001.

## Summary

Reuse the designs already agreed (research.md §1): a versioned, cycle-free JSON document model behind
`IDocumentFormat`/`IDocumentStore`/`IDocumentMapper` with a read-only legacy XML importer and
migrations; reference-pack resolution that also brings documentation tooltips to Linux; the extension
points of the plan page (node libraries, emitters, type catalogs with a composite reflection provider,
project profiles, host channel, per-extension settings) loaded from manifests in isolated
AssemblyLoadContexts; event graphs; method-local variables; an AvaloniaEdit C# code view with
diagnostics mapped to nodes; and the P0 review follow-ups (Fody → CommunityToolkit.Mvvm, nullable and
warnings-as-errors, narrow VM dependencies, architecture gate, Nodify commands, explicit composition,
`[LoggerMessage]` logging). The first task records golden C# from the unmodified code so "identical
C#" is checked mechanically throughout.

## Technical Context

**Language/Version**: C# `latest` (C# 14), .NET 10 SDK, every project `net10.0` (C IV).

**Primary Dependencies**: unchanged from P0 (Roslyn 5.9.0, Avalonia 12.1.3, Nodify.Avalonia 2.0.0,
CommunityToolkit.Mvvm 8.4.2, DynamicData 9.4.33); added Avalonia.AvaloniaEdit 12.0.0,
AvaloniaEdit.TextMate 12.0.0, Microsoft.Extensions.Logging(.Abstractions/.Console) 10.0.12; removed
Fody/PropertyChanged.Fody (research.md §3).

**Storage**: files through `IDocumentStore`: `*.netpp.json`/`*.netpc.json` (schema v1,
[contracts/document-format.md](./contracts/document-format.md)); legacy `*.netpp`/`*.netpc` read-only.
User settings: `$XDG_CONFIG_HOME/NetPrints/settings.json` (platform config dir elsewhere).

**Testing**: xUnit v3 3.2.2 on MTP (research U17). Core, serialization and extensibility tests in
`tests/NetPrints.Core.Tests` (no DI); editor VM/host tests in `tests/NetPrints.Editor.Tests`
(Xunit.DependencyInjection); headless UI in `tests/NetPrints.Editor.UITests` (page objects,
`AutomationIds`, snapshots); desktop E2E once at the end. Golden text files for generated C# and
documents, regenerated with `NETPRINTS_UPDATE_SNAPSHOTS=1` and reviewed.

**Target Platform**: Linux first (CI `ubuntu-latest`); Windows and macOS supported.

**Project Type**: libraries + desktop app + CLI.

**Performance Goals**: no new work (P8). Guard rails only: project open ≤ +10% vs `master` (SC-005);
diagnostics ≤ 2 s after the last edit (SC-006); existing SC-005 search budget test unchanged.

**Constraints**: byte-identical C# for legacy fixtures (FR-008); deterministic documents (C VI); core,
serialization, reflection and extensibility stay UI-free (C II); no code loads from project folders
(FR-019).

**Scale/Scope**: 23 existing built-in node kinds (+1 new: event entry), 4 INPC model roots, ~100 nullable warnings, 2 new projects,
1 test-asset project, 7 user stories.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| I. Cross-platform, Linux-first | ✅ improves | Removes every `ProgramFilesX86` path (FR-013); ref packs resolved on all OSes. |
| II. UI-agnostic core | ✅ | New Serialization/Extensibility have no UI reference; Core gains CommunityToolkit.Mvvm (UI-agnostic MVVM, per the plan page); architecture gate rule A3 enforces it (FR-039). |
| III. Extension-first | ✅ core of the phase | All seven seams of the plan page; built-ins registered through the same node library (FR-021). |
| IV. Single TFM | ✅ | All `net10.0`; no analyzers/generators added (CTK/STJ/logging generators are consumed, not authored). |
| V. Tests gate | ✅ | Golden C# + notification-map characterization before refactor; round-trip, determinism, loader failure, UI tests. |
| VI. Deterministic, versioned output | ✅ | `SchemaVersion` + migrations; canonical JSON; emitter ordering; `SaveVersion` timestamp-like field dropped from documents. |
| VII. Abstractions for I/O | ✅ | `IDocumentFormat`, `IDocumentStore`, `IReferenceResolver`, `ITypeCatalog`, `IHostChannel`. |
| VIII. Simplicity, one PR | ⚠️ size | One spec/branch/PR as required; see "PR size" below. One Serialization project instead of three (research R10). |
| Tech: no Fody in new code | ✅ | Fody removed entirely. |
| Tech: STJ source-gen default format | ✅ | `NetPrintsJsonContext`; legacy XML import-only. |

**Post-design re-check**: unchanged; no violations beyond Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/003-core-refactor/
├── plan.md  research.md  data-model.md  quickstart.md  tasks.md
├── checklists/requirements.md
└── contracts/
    ├── document-format.md       # JSON schema v1, IDocumentFormat/Store/Mapper, migrations
    ├── extension-points.md      # INodeLibrary, emitters, ITypeCatalog, IProjectProfile, IHostChannel, settings, manifest, loader
    ├── references-and-compilation.md  # IReferenceResolver probes, ProjectCompiler, diagnostics, SourceMap, quick info
    └── editor-services.md       # delta to P0: EditorContext, VMs, composition/DI, error model, logging ids, architecture gate
```

### Source Code (after the reorganization PR; new = added by P1)

```text
src/NetPrints.Core/            # model (ModelObject, Node.Id, EventGraph, LocalVariable), translator (+node translator registry,
                               # emitters, SourceMap), Compilation/, References/ (IReferenceResolver, ReferencePackResolver)
src/NetPrints.Serialization/   # new: Documents/ (DTOs), Json/ (NetPrintsJsonContext, NodeListConverter), Legacy/ (XML importer),
                               # Mapping/ (class/project mappers, node converters), Migrations/, Stores/, ProjectPersistence
src/NetPrints.Reflection/      # + ITypeCatalog, CompositeReflectionProvider, InMemoryTypeCatalog, pack-aware DocumentationUtil
src/NetPrints.Extensibility/   # new: INetPrintsExtension, IExtensionBuilder, ExtensionRegistry, INodeLibrary, manifest,
                               # ExtensionLoadContext/ExtensionLoader, IHostChannel (+Null, InMemory), settings store, profiles
src/NetPrints.Editor/          # + CodeView/ (AvaloniaEdit), Diagnostics/, Events/, Variables/ locals, Logging, Assets/Fonts
src/NetPrints.Desktop/         # + console logging, extension host startup
src/NetPrints.Cli/             # reads both formats via ProjectPersistence (no redesign, P2)
tests/NetPrints.Core.Tests/    # + Characterization/, Serialization/, References/, Extensibility/, Translator/Events, Locals
tests/NetPrints.Editor.Tests/  # + CodeView/, Diagnostics/, Architecture/ (gate + violating fixture)
tests/NetPrints.Editor.UITests/# + CodeView/, Events/, Variables/ page objects and baselines
tests/NetPrints.TestExtension/ # new: test asset extension (node kind, emitter, catalog, profile, settings, host channel)
samples/HelloWorld/            # converted to JSON; legacy copy → tests/NetPrints.Core.Tests/Fixtures/Legacy/
```

**Structure Decision**: dependency direction `Core ← Reflection`, `Core ← Serialization`,
`{Core, Reflection, Serialization} ← Extensibility ← {Editor ← Desktop, Cli}`. Serialization does not
reference Reflection. No cycles; UI only in Editor/Desktop.

## Implementation sub-phases (drives tasks.md)

Each sub-phase ends green (`dotnet test --solution NetPrints.slnx`) and is a natural commit/review point.

| # | Sub-phase | Stories | Gate at the end |
|---|---|---|---|
| A | Setup + characterization (golden C#, legacy fixtures, notification map) on unmodified code | — | golden tests pass on the current code |
| B | Core foundations: Fody → CTK, nullable + warnings-as-errors in Core/Reflection, `Node.Id`, `GraphTypeInference`, `[LoggerMessage]` infrastructure | US7 (part) | 0 warnings; notification map and golden C# unchanged |
| C | Serialization: DTOs, JSON, legacy importer, mappers, migrations, stores, persistence, sample conversion, editor/CLI switch-over | US1 | SC-001, SC-002 |
| D | Reference packs + documentation | US2 | SC-003 |
| E | Extension points + loader + test extension, editor/CLI composition | US3 | SC-004 |
| F | Event graphs | US4 | translator snapshot + run test |
| G | Method-local variables | US5 | translator snapshot + run test |
| H | Code view + diagnostics + navigation + hover | US6 | UI tests + baselines reviewed |
| I | VM/editor follow-ups: narrow deps, wrappers, gate, Nodify commands, explicit composition, logging call sites | US7 | gate demonstrated; SC-007, SC-008 |
| J | Polish: README, full suite, E2E once, PR | — | CI green |

**PR size.** The constitution and the owner prefer one PR per phase. P1 is the largest phase so far
(≈3.5 weeks manual, ~100 tasks). Recommendation: keep **one branch and one PR**, but review it in
sub-phase-sized commits (A–J) and let the reviewer review A–C (the data-format change) early as a
draft PR. If the owner wants smaller merges, the only clean split is **P1a = A–D** (format, references,
Fody; satisfies the roadmap "done when") and **P1b = E–J** (extension points, events, locals, code view,
follow-ups). This needs the owner's decision and a roadmap note.

## Complexity Tracking

| Violation / deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Model keeps `[DataContract]` attributes although the format is JSON | Legacy import reuses the existing, proven DataContract loader (research R7) | Mirror XML DTOs would duplicate the reference-preserving graph format |
| `MVVMTK0032` suppressed once on `ModelObject` | `ObservableObject` breaks DataContract import (research R6) | Hand-written INPC loses `[ObservableProperty]` generation |
| One `NetPrints.Serialization` project instead of the plan page's three | No dependency to isolate (research R10) | Three projects add build graph and packaging cost for nothing |
| Plan page (Mar) listed Core as `netstandard2.0 + net10.0` | Superseded by C 1.2.0 | — |

## Pending governance proposals (not applied; coordinator/owner decision)

1. Roadmap P1 text: mention that the grid `ViewportTransform` follow-up is superseded by P0.1 D8, and
   that `MetadataReference` caching stays in P8.
2. Roadmap P1: record the JSON file extensions (`.netpp.json`/`.netpc.json`) and "legacy files left
   untouched on save".
3. Roadmap: optional P1a/P1b split (above), only if the owner wants it.
4. Constitution tech constraints: add AvaloniaEdit as the code-view component and
   Microsoft.Extensions.Logging (`[LoggerMessage]`) as the logging stack (PATCH/MINOR).
5. Constitution IV wording: CTK/STJ/logging source generators are consumed from packages; the
   `netstandard2.0` exception applies only to generators NetPrints authors (clarification, PATCH).
