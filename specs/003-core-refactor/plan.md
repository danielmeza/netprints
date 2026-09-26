# Implementation Plan: Core Refactor and Extension Points

**Branch**: `003-core-refactor` | **Date**: 2026-09-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-core-refactor/spec.md`

> **Start condition (met).** The repository reorganization (PR #5, `docs/adr/0001-repo-layout.md`)
> is merged and `003-core-refactor` is rebased on it: projects live in `src/` and `tests/`
> (`src/NetPrints.Core`, `src/NetPrints.Reflection`, `src/NetPrints.Editor`, `src/NetPrints.Desktop`,
> `src/NetPrints.Cli`; `tests/NetPrints.Core.Tests`, `tests/NetPrints.Editor.Tests`,
> `tests/NetPrints.Editor.UITests`, `tests/NetPrints.Testing.Ui`, `tests/NetPrints.Desktop.E2ETests`),
> the VSIX is in `legacy/NetPrintsVSIX` (not in the solution), the solution is `NetPrints.slnx`, and
> `.editorconfig`, `.gitattributes` and a `dotnet format --verify-no-changes` CI step exist. Namespaces
> did not change. The original Core/Reflection sources are CRLF and marked `-text` in `.gitattributes`;
> edits keep their line endings. All paths in these documents use this layout. The implementer starts
> with T001.

## Summary

**Revised 2026-09-25 (owner decision):** a NetPrints project is an SDK-style `.csproj` referencing the
`NetPrints.Sdk` package. Graphs are `*.netpc.json` files; the package's targets run the net10.0
translator out of process (`Exec` of the bundled `NetPrints.Generator`, research R12) before
`CoreCompile`, writing committed `<name>.netpc.g.cs` files nested under their graphs. The editor opens the
`.csproj` through MSBuild (Microsoft.Build.Locator + MSBuildWorkspace, as UnrealSharp does, research
R14), so references, documentation (D5) and builds come from MSBuild; the custom reference resolver and
`ProjectCompiler` are gone. **Revised 2026-09-26 (owner decision, research R21):** legacy `.netpp`/`.netpc`
projects are not read or converted; the repository's own sample and fixtures are migrated once (T054a)
and the legacy code is deleted (T062a, T063). Ids are strict Snowflake ids on read and in the schema (T054b).

**Graph format (owner-approved research `docs/research/2026-09-25-graph-format/`, research R17):** schema
v1 is designed for git: random node ids, stable member ids that key the layout, pins referenced by name,
a canonical writer with one line per connection/layout entry/pin value/type reference, integer
positions, `$schema` first with a JSON Schema generated from the DTOs, a tolerant reader, edited-only
saves, a `.gitattributes` LF template and tests that keep the committed schema, sample graphs and
`.netpc.g.cs` files up to date. A `netprints merge` git driver is a follow-up after P1.

**Release, packages and docs (owner-approved research `docs/research/2026-09-25-release-and-docs/`,
research R18–R19, sub-phase L):** MinVer versions everything from `v*` tags; `NetPrints.Core`,
`NetPrints.Reflection`, `NetPrints.Sdk` and the `netprints` tool (`NetPrints.Cli`) pack with full metadata;
a git-ignored `local-packages/` feed and scripts prove the tool and the SDK package work from it; a
`release.yml` (kicad-sharp shape) publishes through Trusted Publishing and creates a GitHub Release with
self-contained editor archives, checksums and attestations, and runs as a dry run on the PR; a Docusaurus
site with a DocFX API reference and the graph schema goes to GitHub Pages, and the wiki points to it. The
editor keeps compiling against MSBuild-resolved references only (R19: no `Basic.Reference.Assemblies`), and a
CI job publishes it self-contained and runs a headless `--check-project` on HelloWorld. Nothing is published
during P1.

Reused unchanged (research.md §1): the versioned, cycle-free JSON graph documents behind
`IDocumentFormat`/`IDocumentStore`/`IDocumentMapper` with schema migrations (the legacy import is dropped, R21); the extension
points of the plan page (node libraries, emitters, type catalogs with a composite reflection provider,
project profiles, host channel, per-extension settings) loaded in isolated AssemblyLoadContexts; event
graphs; method-local variables; the AvaloniaEdit C# code view with diagnostics mapped to nodes; and the
P0 review follow-ups (Fody → CommunityToolkit.Mvvm, nullable and warnings-as-errors, narrow VM
dependencies, architecture gate, Nodify commands, explicit composition, `[LoggerMessage]` logging). The
first tasks record golden C# from the unmodified code so "identical C#" is checked mechanically.

## Technical Context

**Language/Version**: C# `latest` (C# 14), .NET 10 SDK, every project `net10.0` (C IV).

**Primary Dependencies**: unchanged from P0 (Roslyn 5.9.0, Avalonia 12.1.3, Nodify.Avalonia 2.0.0,
CommunityToolkit.Mvvm 8.4.2, DynamicData 9.4.33); added Avalonia.AvaloniaEdit 12.0.0,
AvaloniaEdit.TextMate 12.0.0, Microsoft.Extensions.Logging(.Abstractions/.Console) 10.0.12,
Microsoft.Build.Locator 1.11.2, Microsoft.CodeAnalysis.Workspaces.MSBuild 5.9.0, Microsoft.Build(.Framework)
18.0.2 compile-only; MinVer 8.0.0 (`GlobalPackageReference`); removed Fody/PropertyChanged.Fody (research.md §3,
R16, R19). Docs toolchain (not part of the solution build): DocFX 2.81.0 as a local dotnet tool, Node 24 with
Docusaurus 3.10.2 in `website/`.

**Storage**: `<Name>.csproj` (MSBuild, [contracts/project-system.md](./contracts/project-system.md));
graph files `*.netpc.json` through `IDocumentStore` (schema v1, [contracts/document-format.md](./contracts/document-format.md));
generated `*.netpc.g.cs` (committed); legacy `*.netpp`/`*.netpc` are not read (R21). User settings:
`ApplicationData/NetPrints/settings.json`.

**Testing**: xUnit v3 3.2.2 on MTP (research U17). Core, serialization and extensibility tests in
`tests/NetPrints.Core.Tests` (no DI); editor VM/host tests in `tests/NetPrints.Editor.Tests`
(Xunit.DependencyInjection); headless UI in `tests/NetPrints.Editor.UITests` (page objects,
`AutomationIds`, snapshots); desktop E2E once at the end. Golden text files for generated C# and
documents, regenerated with `NETPRINTS_UPDATE_SNAPSHOTS=1` and reviewed.

**Target Platform**: Linux first (CI `ubuntu-latest`); Windows and macOS supported.

**Project Type**: libraries + desktop app + CLI + NuGet package with MSBuild targets and a build-time tool;
released as four NuGet packages and three editor archives, documented by a static site (sub-phase L).

**Hosts that must build NetPrints projects**: `dotnet build` (CI, CLI), Visual Studio 2022/2026 (.NET Framework MSBuild), Rider — hence `Exec` rather than a .NET task (research R12). The editor needs the .NET 10 SDK.

**Performance Goals**: no new work (P8). Guard rails only: project open ≤ 3 s when restored (SC-005; MSBuildWorkspace open measured 0.8 s);
diagnostics ≤ 2 s after the last edit (SC-006); existing SC-005 search budget test unchanged.

**Constraints**: byte-identical C# for the characterization fixtures across their one-time migration (FR-008); deterministic, merge-friendly documents (C VI, FR-043…FR-050); core,
serialization, reflection, extensibility, workspace and generator stay UI-free (C II); project-referenced
extensions load in the editor only after trust (FR-019); the generator never references MSBuild or UI.

**Scale/Scope**: 23 existing built-in node kinds (+1 new: event entry), 4 INPC model roots, ~100 nullable warnings, 5 new projects
(Serialization, Extensibility, Workspace, Generator, Sdk), 1 test-asset project, 7 user stories.

## Constitution Check

*GATE: checked before Phase 0 and re-checked after Phase 1.*

| Principle | Status | Notes |
|---|---|---|
| I. Cross-platform, Linux-first | ✅ improves | Removes every `ProgramFilesX86` path (FR-013); MSBuild resolves references on all OSes; the build works in VS/Rider/CLI via `Exec`. |
| II. UI-agnostic core | ✅ | New Serialization/Extensibility have no UI reference; Core gains CommunityToolkit.Mvvm (UI-agnostic MVVM, per the plan page); architecture gate rule A3 enforces it (FR-039). |
| III. Extension-first | ✅ core of the phase | All seven seams of the plan page; built-ins registered through the same node library (FR-021). |
| IV. Single TFM | ✅ | All `net10.0`; no Roslyn generator mode (R13), so no `netstandard2.0` translator; the net10 generator runs out of process from any MSBuild. |
| V. Tests gate | ✅ | Golden C# + notification-map characterization before refactor; round-trip, determinism, loader failure, UI tests. |
| VI. Deterministic, versioned output | ✅ | `SchemaVersion` + migrations; canonical JSON (one line per leaf record, sorted sets, integer positions); ids are document data, so the same document still gives the same bytes; seeded ids in tests; emitter ordering; `SaveVersion` timestamp-like field dropped from documents. |
| VII. Abstractions for I/O | ✅ | `IDocumentFormat`, `IDocumentStore`, `IProjectSystem` (MSBuild behind it; P5 sidecar can host it), `ITypeCatalog`, `IHostChannel`. |
| Development workflow: CI Linux-only | ✅ | `CI` stays on `ubuntu-latest` (new jobs `packages`, `desktop-publish`); only `release.yml` uses `macos-latest`, for the `osx-arm64` archive (research R18; PATCH clarification proposed below). |
| VIII. Simplicity, one PR | ⚠️ size | One spec/branch/PR as required; see "PR size" below. One Serialization project instead of three (research R10). |
| Tech: no Fody in new code | ✅ | Fody removed entirely. |
| Tech: STJ source-gen default format | ✅ | `NetPrintsJsonContext`; legacy XML is not read at all (R21), stricter than "import-only" (see Governance proposals). |

**Post-design re-check**: unchanged; no violations beyond Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/003-core-refactor/
├── plan.md  research.md  data-model.md  quickstart.md  tasks.md
├── checklists/requirements.md
└── contracts/
    ├── document-format.md       # graph JSON schema v1, IDocumentFormat/Store/Mapper, migrations, ProjectPersistence
    ├── extension-points.md      # INodeLibrary, emitters, ITypeCatalog, IProjectProfile, IHostChannel, settings, manifest, loader
    ├── project-system.md        # .csproj, NetPrints.Sdk props/targets, generator, IProjectSystem
    ├── compilation-and-diagnostics.md  # diagnostics, DiagnosticMapper, SourceMap, quick info
    ├── editor-services.md       # delta to P0: EditorContext, VMs, composition/DI, error model, logging ids, architecture gate
    └── release-and-docs.md      # MinVer, package metadata, local feed, --check-project, release/docs/wiki workflows, owner steps
```

### Source Code (after the reorganization PR; new = added by P1)

```text
src/NetPrints.Core/            # model (ModelObject, Node.Id, EventGraph, LocalVariable, Project from snapshot), translator
                               # (+node translator registry, emitters, SourceMap), Compilation/ (diagnostics, CodeAnalysisSession),
                               # Projects/ (IProjectSystem, ProjectSnapshot, ProjectEdit, IProcessRunner), Profiles/
src/NetPrints.Serialization/   # new: Documents/, Json/, Legacy/ (T038 importer + DataContract copies, one-time migration only, deleted in T062a), Mapping/,
                               # Migrations/, Stores/, ProjectPersistence
src/NetPrints.Reflection/      # + ITypeCatalog, CompositeReflectionProvider, InMemoryTypeCatalog; docs from ResolvedAssembly
src/NetPrints.Extensibility/   # new: extension API, registry, loader/ALC, IExtensionHost, host channel, settings
src/NetPrints.Workspace/       # new: MsBuildRegistration (Locator), MsBuildProjectSystem (evaluation, MSBuildWorkspace,
                               # ProjectRootElement edits, restore/build/run), MsBuildMessageParser
src/NetPrints.Generator/       # new: GraphCodeGenerator + "generate" entry point (build-time tool)
src/NetPrints.Sdk/             # new: pack-only; build/NetPrints.Sdk.props/.targets, tools/net10.0 = Generator
src/NetPrints.Editor/          # + CodeView/, Diagnostics/, Events/, Variables/ locals, Logging, Assets/Fonts, project trust
src/NetPrints.Desktop/         # + MSBuild registration, console logging, extension host startup
src/NetPrints.Cli/             # builds and runs .csproj via the new services (no redesign, P2)
tests/NetPrints.Core.Tests/    # + Characterization/, Serialization/, Projects/ (targets, generator, project system),
                               # Extensibility/, Translator/Events, Locals
tests/NetPrints.Editor.Tests/  # + CodeView/, Diagnostics/, Architecture/ (gate + violating fixture)
tests/NetPrints.Editor.UITests/# + CodeView/, Events/, Variables/ page objects and baselines
tests/NetPrints.TestExtension/ # new: test asset extension
samples/HelloWorld/            # HelloWorld.csproj + .netpc.json + .netpc.g.cs + .gitattributes; samples/Directory.Build.* for in-repo SDK import
schemas/                       # new: netpc.v1.schema.json (generated from the DTOs, committed, golden-tested)
Directory.Build.targets        # new (L): icon + package README for packable projects
NuGet.config, local-packages/  # new (L): local feed (.gitkeep tracked, contents ignored)
eng/PackageReadme.targets      # new (L): nuget.org-safe README generated at pack time
assets/icons/netprints-icon.png# new (L): package and site icon
scripts/                       # new (L): pack-local, verify-packages, smoke-desktop, archive-desktop, build-docs
.config/dotnet-tools.json      # new (L): docfx
website/                       # new (L): Docusaurus 3 site reading ../docs
docs/                          # + index.md, guide/, contributing/, api/ (DocFX), _category_.json files, adr/0002
.github/                       # + workflows/release.yml, docs.yml, wiki.yml; release.yml (notes categories),
                               #   release-notes.md, wiki/Home.md, wiki/_Sidebar.md; dependabot npm entry
```

**Structure Decision**: dependency direction `Core ← Reflection`, `Core ← Serialization`,
`{Core, Reflection, Serialization} ← Extensibility ← {Generator, Workspace ← Editor ← Desktop, Cli}`;
`Workspace` references Core only (+ MSBuild/Roslyn workspaces); `Generator` never references Workspace
or MSBuild. No cycles; UI only in Editor/Desktop.

## Implementation sub-phases (drives tasks.md)

Each sub-phase ends green (`dotnet test --solution NetPrints.slnx`) and is a natural commit/review point.

| # | Sub-phase | Stories | Gate at the end |
|---|---|---|---|
| A | Setup + characterization (golden C#, legacy fixtures — migrated to JSON in E, notification map) on unmodified code | — | golden tests pass on the current code |
| B | Core foundations: Fody → CTK, nullable + warnings-as-errors, id generation, node and member ids, graph keys, pin keys, auto-placement, dirty flag, `GraphTypeInference`, logging infrastructure, new project shells | US7 (part), US1 (part) | 0 warnings; A-gates unchanged; DF-T18…T20, T22 (model part) |
| C | Graph serialization: DTOs, canonical writer, tolerant reader, JSON Schema, legacy graph import (T038; only for the one-time migration in E, R21), mappers, migrations, stores | US1 | DF-T01…T10, T12–T14, T16, T18–T25 (DF-T11, T15, T26 in E; DF-T17 needs the test extension, F) |
| D | Build pipeline: `GraphCodeGenerator`, generator entry point, `NetPrints.Sdk` props/targets, in-repo dev mode, package test | US1 | PS-T01…T06 |
| E | Project system: `IProjectSystem`/MSBuild, `.gitattributes`, one-time migration of the repository's legacy files and strict ids (R21), `ProjectPersistence` (edited-only saves), Project model from snapshot, sample switch-over, editor dirty tracking, editor/CLI switch-over, legacy and DataContract code removal, docs (D5) | US1, US2 | SC-001…SC-003, SC-009, SC-010; PS-T07…T11, PS-T13, PS-T15; DF-T11, DF-T15, DF-T26, DF-T28 |
| F | Extension points + loader + trust + test extension, composition | US3 | SC-004; PS-T14; DF-T17 |
| G | Event graphs | US4 | translator snapshot + build/run test |
| H | Method-local variables | US5 | translator snapshot + build/run test |
| I | Code view + diagnostics + navigation + hover | US6 | UI tests + baselines reviewed |
| J | VM/editor follow-ups: narrow deps, wrappers, gate, Nodify commands, explicit composition, logging call sites | US7 | gate demonstrated; SC-007, SC-008 |
| K | Polish: README, full suite, E2E once, IDE checks | — | CI green |
| L | Release, packages and docs: MinVer, package metadata, packable projects, local feed + verification, `--check-project` + self-contained smoke, release workflow (dry run), DocFX + Docusaurus + docs workflow, wiki, README install, ADR 0002; then the PR is marked ready (T108) | US8 | SC-011…SC-015; RL-T01…T12; release dry run green on the PR |

**PR size (owner decision: one PR).** One branch and one PR, opened as a **draft after sub-phase E**
so the reviewer reviews A–E (model, graph format, build pipeline, project system; the roadmap "done
when") early, then F–L as they land. Each sub-phase is a separate, reviewable commit range.

## Complexity Tracking

| Violation / deviation | Why needed | Simpler alternative rejected because |
|---|---|---|
| Model keeps `[DataContract]` attributes although the format is JSON — *until T063 (revised 2026-09-26, R21)* | The T038 importer reuses the existing DataContract loader (research R7), now only for the one-time migration (T054a); T063 removes the attributes and hooks | Mirror XML DTOs would duplicate the reference-preserving graph format |
| `MVVMTK0032` suppressed once on `ModelObject` | `ObservableObject` breaks DataContract import (research R6); can be revisited once T063 removes DataContract | Hand-written INPC loses `[ObservableProperty]` generation |
| One `NetPrints.Serialization` project instead of the plan page's three | No dependency to isolate (research R10) | Three projects add build graph and packaging cost for nothing |
| Plan page (Mar) listed Core as `netstandard2.0 + net10.0` | Superseded by C 1.2.0 | — |
| Build-time code generation by `Exec` of a tool instead of an MSBuild task | VS 2022 cannot host .NET tasks; `Runtime="NET"` needs MSBuild 18 and has dotnet/msbuild#12514 (research R12) | A `netstandard2.0`/`net472` task would need a second build of the translator (C IV) |
| Generated `.netpc.g.cs` committed to the repo | Owner requirement: reviews show the C# | Generating into `obj/` hides the C# from reviews |
| Editor requires the .NET SDK | MSBuild evaluation/build of the `.csproj` | A custom project format (the previous design) — rejected by the owner |
| Custom canonical JSON writer (~200 lines) on top of STJ | STJ `WriteIndented` puts every scalar on its own line, so a position or connection spans 4 lines and neighbouring edits conflict (graph-format research §3) | STJ indentation alone; `WriteRawValue` in converters (indentation interaction unverified) |
| Node.js toolchain (Docusaurus) next to the .NET build | Owner-approved docs stack (research R18): the look the owner liked on nuke.build, used by Avalonia, Silk.NET, Fallout | DocFX alone (no Node): weaker site; kept for the API reference only |
| A headless `--check-project` mode in the desktop app | The only way to test the published editor's own MSBuild/Roslyn layout headless (R19, RL-T06) | Smoke through the CLI: a different application with a different publish layout |
| Release workflow uses a macOS runner | `osx-arm64` apphost must be signed ad hoc on macOS (research release §6) | Cross-publishing from Linux produces an unsigned apphost that macOS kills |
| Ambient id generator (`IdGeneration.Current`, `AsyncLocal`) | Node and member constructors have no services; tests need seeded ids | Passing a generator to every `Node` constructor changes every node type and extension author code |

## Governance proposals

Applied by the coordinator with the owner's approval on 2026-09-25 (roadmap P1 text, constitution 1.2.1):
the superseded `ViewportTransform` follow-up and `MetadataReference` caching in P8; the csproj model and
the restated "done when"; the SDK requirement and "no source-generator mode"; AvaloniaEdit and
Microsoft.Extensions.Logging in the tech constraints; the narrowed `netstandard2.0` exception; the graph
format for version control. Also applied later the same day: U1 adds `NetPrints.Sdk` to UnrealSharp's Script `.csproj`; the graph-format follow-ups are in P2; the release and docs stack is in P1. **Applied (sub-phase L, research R18; constitution 1.2.2)**: a constitution PATCH
clarification that release packaging workflows may use macOS or Windows runners (the main `CI` workflow stays
Linux-only). **Applied (2026-09-26, research R21; constitution 1.2.3, roadmap cd424da)**: the constitution's "Legacy DataContract XML is
import-only once JSON lands" and the roadmap P1 text ("legacy XML importer", "Legacy `.netpp`/XML files are
import-only", and the "done when" "the old sample imports without modifying the legacy file") change to: legacy
XML is not read; the repository's own files are migrated once in P1.
