# Implementation Plan: Catalog Tooling and Spectre CLI

**Branch**: `004-catalog-cli` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-catalog-cli/spec.md` (roadmap phase P2).

## Summary

P2 turns NetPrints' command line into a real tool and adds catalogs. The `netprints` dotnet tool is rebuilt on
Spectre.Console.Cli with `build`, `run`, `generate`/`regen --check`, `migrate` (report-only while the schema is
v1), `catalog`, `format --check`, `show`, `merge` and `git-install`, under one exit-code contract (0/1/2/3/4) that
also fixes P1's `--version`/`--help` exit code. A new `NetPrints.Catalog` library holds one catalog engine over
Roslyn symbols, a versioned `*.npcat.json` schema and declarative profiles; the tool flavor resolves sources
through SDK projects, and the annotations flavor is a generator-only `NetPrints.Annotations` package (netstandard2.0,
linking the engine as source) that embeds catalogs in assembly metadata, which the editor discovers without
loading anything. Snapshot tests prove both flavors byte-identical and a `public-api` catalog identical to the live
provider. The extension loader decides the two hazards of ADR-0010 (share only what the host provides; `dependsOn`
shares types), a host multi-extension suite pins them, and the extension-facing libraries gain PublicApiAnalyzers
and `[Experimental]` markers. Git integration (text conversion, identity merge driver, `git-install`) and a
prepared SchemaStore entry complete the graph-format follow-ups. Decisions: ADR-0010 (accepted), ADR-0012–ADR-0016.

## Technical Context

**Language/Version**: C# (latest), .NET 10 SDK (`global.json` 10.0.100, `rollForward: latestFeature`); `net10.0`
everywhere except `src/NetPrints.Annotations` (`netstandard2.0`, Roslyn source generator, constitution IV).

**Primary Dependencies**: Spectre.Console.Cli 0.57.2 (+ Spectre.Console.Testing 0.57.2 in tests),
Microsoft.Extensions.DependencyInjection 10.0.12, Microsoft.CodeAnalysis.CSharp(.Workspaces) 5.9.0 (central; the
generator builds against 5.0.0 via `VersionOverride`), Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0,
System.Reflection.Metadata (in-box), System.Text.Json source generation (in-box), existing Microsoft.Build(.Locator),
MinVer. `Microsoft.CodeAnalysis.Analyzers` reaches the generator transitively. Removed: CommandLineParser.

**Storage**: Files only — `*.npcat.json`, `*.npprofile.json`, `netprints.catalog.json`, `*.netpc.json`, git config and
`.gitattributes`, `PublicAPI.*.txt`, JSON Schemas under `schemas/`.

**Testing**: xUnit v3 (3.2.2) on Microsoft.Testing.Platform; new `tests/NetPrints.Catalog.Tests` and
`tests/NetPrints.Cli.Tests`; multi-extension suite in `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/`;
`CSharpGeneratorDriver` for generator tests; snapshots updated only with `NETPRINTS_UPDATE_SNAPSHOTS=1`.

**Target Platform**: Linux CI (ubuntu-latest); tools cross-platform (Windows, macOS) by construction.

**Project Type**: Libraries + CLI dotnet tool + Roslyn source generator package.

**Performance Goals**: SC-006 (fixture catalog < 5 s; ≥ 1,000-type reference assembly < 30 s); SC-007 (50
extensions load < 10 s). No P8 work.

**Constraints**: see "Standing constraints" below.

**Scale/Scope**: 106 tasks in 26 batches; 2 new src projects, 2 new test projects, 13 fixture projects; 6 ADRs.

## Standing constraints (every batch)

- `net10.0` everywhere except the Roslyn source generator (`netstandard2.0`). Shared engine files compile on both.
- Analyzers at error (ADR-0003); hygiene tests forbid `!` null-forgiving, `#pragma`/`[SuppressMessage]`
  suppressions outside the ADR-0003 ledger, `#nullable disable`, sync-over-async and `<NoWarn>` (the single
  ADR-0010 opt-in line excepted). Fix causes, never suppress; never change analyzer packages, severities or
  `.editorconfig` to get green (the only planned analyzer changes are PublicApiAnalyzers and the experimental
  opt-in, both in sub-phase B).
- XAML over code-behind if any UI is touched (only `ReflectionHost`, a non-UI host class, changes in the editor).
- Red/green: pin invariants with a test before removing or changing code (loader rules, CLI exit codes, generator
  behaviour); a behaviour change starts with a failing test.
- Tests are xUnit v3 on Microsoft.Testing.Platform and pass `TestContext.Current.CancellationToken`.
- Keep code comments short (match repo density); rationale goes to commit messages and
  `specs/004-catalog-cli/implementation-notes.md` ("Decision: …").
- Every public API in `src/` has XML docs; tracked libraries update `PublicAPI.Unshipped.txt` with every API change.
- An identifier used in more than one place is a named constant (diagnostic codes `NPC…`, `NPXE…`, exit codes,
  profile ids, attribute names).
- Deterministic output everywhere (ordinal ordering, invariant culture, LF, no timestamps).
- No publishing: no tags, no NuGet push, no wiki edits, no posts to upstream/UnrealSharp/SchemaStore repositories.
  SchemaStore submission is an **owner action**; the repo only prepares `eng/schemastore/`.
- One branch (`004-catalog-cli`), one PR (draft now; ready after Checkpoint H and review).
- Every batch runs on Linux CI: build `dotnet build -v q -tl:off --nologo`; tests: build first, then
  `dotnet test --no-build --no-progress --no-ansi`; whole suite once at the end of a batch (AGENTS.md batch rules).
- Governance files (`.specify/memory/*`) are not edited by implementers; proposals go in implementation-notes.md.

## Constitution Check

*Gate before Phase 0 and re-checked after Phase 1 design: PASS.*

| Principle | Check | Result |
|---|---|---|
| I. Cross-platform, Linux-first | All new code is platform-neutral; CI Linux-only; native fixture Linux-only with an explicit skip reason elsewhere; git integration shells out to `git` (cross-platform) | Pass |
| II. UI-agnostic core | `NetPrints.Catalog` references no UI; `AssemblyReferenceGateTests` extended to it; editor change is in the non-UI `ReflectionHost` | Pass |
| III. Extension-first | Catalog profiles contributable by extensions (`AddCatalogProfile`); catalogs through the existing `AddTypeCatalog`; no Unreal-specific code | Pass |
| IV. Single target framework | Only `NetPrints.Annotations` (a generator NetPrints ships) is netstandard2.0; `NetPrints.Catalog` is net10.0 and shares source instead of multi-targeting | Pass |
| V. Tests gate every change | Every behaviour change has test obligations (CL, CT, AN, GI, MX, AP) run in CI | Pass |
| VI. Readable, deterministic output | Canonical catalog writer, sorted summaries, byte-identical parity tests, versioned catalog schema | Pass |
| VII. Abstractions over concretions for I/O | Catalogs reach consumers as `ITypeCatalog`; CLI uses `IProjectSystem`/`IProcessRunner`; documents via `IDocumentFormat` | Pass |
| VIII. Simplicity, incremental delivery | One PR; later-phase work recorded as follow-ups (research R27); CommandLineParser removed | Pass |
| Tech constraints | CLI on Spectre.Console.Cli (constitution); System.Text.Json source-gen; `[LoggerMessage]` logging; no Fody | Pass |

## Project Structure

### Documentation (this feature)

```text
specs/004-catalog-cli/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
├── implementation-notes.md      # created in T004; decisions and checkpoint reports
├── checklists/requirements.md
└── contracts/cli.md, catalog.md, annotations.md, git.md, extensions.md
docs/adr/0010-…, 0012-…–0016-…  (committed with this spec)
```

### Source Code (new = added by P2, changed = modified)

```text
src/
├── NetPrints.Catalog/                 # new, net10.0, packable
│   ├── Model/  Engine/  Json/CanonicalCatalogWriter.cs  Emit/      # shared source (also compiled by the generator)
│   ├── Json/CatalogReader.cs, CatalogJsonContext.cs, CatalogSchema.cs
│   ├── Runtime/CatalogTypeCatalog.cs, CatalogLoader.cs, EmbeddedCatalogReader.cs
│   ├── Config/CatalogConfig.cs, CatalogConfigResolver.cs, CatalogConfigJsonContext.cs
│   ├── Sources/CatalogSourceResolver.cs, CatalogCompilationFactory.cs
│   └── PublicAPI.Shipped.txt, PublicAPI.Unshipped.txt
├── NetPrints.Annotations/             # new, netstandard2.0 generator package
│   ├── CatalogGenerator.cs, AttributeSources.cs, GeneratorDiagnostics.cs
│   ├── build/NetPrints.Annotations.targets
│   └── AnalyzerReleases.Shipped.md, AnalyzerReleases.Unshipped.md
├── NetPrints.Cli/                     # changed: Spectre rewrite
│   ├── Program.cs, CliApplication.cs, CliServices.cs, ExitCodes.cs
│   ├── Infrastructure/TypeRegistrar.cs, TypeResolver.cs, ProjectLocator.cs, ProjectCommandBase.cs
│   ├── Commands/Build, Run, Generate, Migrate, Catalog, Format, Show, Merge, GitInstall (…Command.cs)
│   └── Git/GraphSummaryWriter.cs, GraphMerger.cs, TextMergeFallback.cs, GitConfig.cs, GitAttributesFile.cs
├── NetPrints.Generation/              # changed: GenerateRequestFactory.cs, GenerationMode, UpToDate; references Catalog
├── NetPrints.Extensibility/           # changed: Loading/HostAssemblies.cs, ExtensionLoadContext, ExtensionLoader,
│                                      #   Log.cs, AddCatalogProfile, ExperimentalApis.cs, PublicAPI files
├── NetPrints.Core/ Reflection/ Serialization/   # changed: PublicAPI files, [Experimental] on emitters (Core)
└── NetPrints.Editor/Hosting/ReflectionHost.cs   # changed: embedded catalog discovery
tests/
├── NetPrints.Catalog.Tests/           # new: Format/, Engine/, Runtime/, Config/, Emit/, Generator/, EndToEnd/, Snapshots/, Profiles/
├── NetPrints.Cli.Tests/               # new: Commands/, Git/, EndToEnd/ (CliBuildTests moved here)
├── Fixtures/Catalog/CatalogFixtureLib/                 # new
├── Fixtures/Extensions/Fx.*, Fixture.SharedLib.V1/V2, NetPrintsFixture.Runtime   # new
├── NetPrints.Core.Tests/Extensibility/MultiExtension/  # new
├── NetPrints.Core.Tests/Architecture/PublicApiTrackingTests.cs, ExperimentalApiTests.cs   # new
└── NetPrints.Testing/Extensions/ExtensionHarness.cs    # new
schemas/npcat.v1.schema.json, netprints.catalog.v1.schema.json   # new
eng/schemastore/catalog-entries.json, PULL_REQUEST.md            # new
eng/validate-schemas.sh, scripts/build-docs.sh, scripts/verify-packages.sh, .github/workflows/ci.yml   # changed
docs/guide/cli.md, catalogs.md, git.md (new); extensions.md, install.md, graph-format.md, projects.md (changed)
Directory.Packages.props, Directory.Build.props, Directory.Build.targets, NetPrints.slnx   # changed
```

**Structure Decision**: Follow ADR-0001's layout (`src/`, `tests/`, fixtures under `tests/Fixtures/`). One new
library (`NetPrints.Catalog`) and one generator package (`NetPrints.Annotations`); the CLI stays the only tool.

## Implementation sub-phases (drives tasks.md)

| Sub-phase | Stories | Content | Checkpoint |
|---|---|---|---|
| A. Setup | — | Packages, project skeletons, CI steps, implementation notes | A: solution builds, suite green |
| B. API tracking | US6 | PublicApiAnalyzers + files; `[Experimental]` + opt-in + hygiene gate | B: SC-011 |
| C. CLI | US1 | Spectre app, exit codes, build/run/migrate, generate/regen `--check`, CI smoke flipped, CLI guide | C: SC-001, SC-002 (run + regen) |
| D. Catalog engine + tool | US2 | Model, writer/reader, schema, engine + profiles, fixture, runtime catalog + parity, config + sources, `catalog` command, e2e, catalogs guide | D: SC-004, SC-005 (extension path), SC-006 |
| E. Annotations | US3 | Generator, embedded catalogs, package + targets, cross-flavor snapshots, editor discovery, e2e | E: SC-003, SC-005 (embedded path), SC-013 |
| F. Graph tooling + git | US4 | `format`, `show`, `merge`, `git-install`, SchemaStore entry, git guide | F: SC-002 (format), SC-009, SC-010 |
| G. Extensions coexist | US5 | Fixtures, harness, pins (red), loader rules (green), scenarios, extensions guide | G: SC-007, SC-008 |
| H. Polish | all | Docs build, quickstart run, release dry run, full suite + E2E, review | H: SC-012, all SC |

The attribute-injecting generator skeleton lands in D (before the fixture library that uses the attributes); the
catalog emission part of the generator lands in E.

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| netstandard2.0 project (`NetPrints.Annotations`) | The compiler loads generators only as netstandard2.0 | Constitution IV explicitly allows it for generators NetPrints ships |
| Shared source between `NetPrints.Catalog` and the generator | One engine for both flavors (byte-identical parity) | Multi-targeting `NetPrints.Catalog` violates IV; two engines drift |
| One `<NoWarn>` line (experimental opt-in) | In-repo consumers of `[Experimental]` API must opt in | Hygiene gate allows exactly this line and checks its content (ADR-0010, ADR-0003 ledger row) |
| ~12 fixture projects | Real `.deps.json` and private dependencies for coexistence tests | Roslyn-at-runtime fixtures cannot produce dependency manifests reliably (research §6(b)) |
| `VersionOverride` for the generator's Roslyn | Generators must build against the minimum compiler they run in | Lowering the central version would downgrade the whole repo's Roslyn |
| AN-T10 builds a throwaway netstandard2.0 consumer in a temporary directory | Proves FR-025 (any consumer framework) with the real package | It is test data created at run time, not a repository project; constitution IV governs repository projects |

## Governance proposals (for the coordinator and owner; not edited by agents)

- Roadmap: P1 row → merged (PR #6, `cc96a93`, released `v0.1.0`/`v0.1.1`); P2 row → "spec ready
  (`specs/004-catalog-cli/`)"; the P2 ADR bullet → "ADR-0010 accepted"; the P3 kit bullet → "internal harness landed
  in P2 (ADR-0010 §5)"; the P1 follow-up "in-editor visual diff" → P6.
- Constitution: no change needed.

## Follow-ups (recorded, not in P2)

See research R27: in-editor visual diff (P6), public conformance kit and validation baseline (P3),
`netprints-verify` and author CI (before NetPrintsUnreal's first release), catalog compression (P8), real schema
migrations (first schema v2), collectible contexts and unload tests (only if hot reload is scheduled), Windows leg for
the native fixture.
