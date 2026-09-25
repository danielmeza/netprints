<!--
Sync Impact Report
- Version change: 1.2.0 → 1.2.1 (PATCH: clarifications, owner-approved 2026-09-25)
  - IV: the netstandard2.0 exception covers only analyzers/generators NetPrints ships, not
    generators it consumes.
  - Technology Constraints: Avalonia 12.x (current pin), AvaloniaEdit, Microsoft.Extensions.Logging,
    SDK-style project model.
- Previous: 1.1.0 → 1.2.0 (MINOR: principle IV redefined — net10.0 everywhere)
- Reason: Visual Studio/.NET Framework hosting is out of scope; owner approved net10.0 for all
  projects and dependencies (2026-09-24)
- Previous: 1.0.0 → 1.1.0 (MINOR: principles I and IV redefined in scope, workflow rules added)
- Modified principles: I (WPF/WinForms forbidden everywhere; VSIX out of the build), IV (VS host
  deferred; editor stack targets net10.0)
- Modified sections: Technology Constraints (Avalonia 11.x pin, DynamicData/ReactiveUI usage),
  Development Workflow (Linux-only main CI `CI`; VSIX chained workflow when resumed)
- Reason: P0 now migrates the editor from WPF to Avalonia; Visual Studio integration deferred
  by the project owner (2026-09-24)
- Templates: plan/spec/tasks templates read this file at runtime; no template edits required
- Deferred TODOs: VS host compatibility (netstandard2.0 editor / Nodify) revisited when P4 resumes
-->
# NetPrints Constitution

NetPrints is a visual programming language for .NET: node graphs are edited in a UI and
translated to readable C#. This constitution governs the modernization of NetPrints
(Avalonia editor, modern .NET) and its use as a host for extensions such as NetPrintsUnreal.
The phased roadmap lives in `.specify/memory/roadmap.md`.

## Core Principles

### I. Cross-Platform, Linux-First
Every project in the solution MUST build, test and run on Linux, Windows and macOS. Linux is
the primary development and CI platform. WPF and WinForms are forbidden in every project;
other Windows-only APIs (`Microsoft.Win32`, registry, `Program Files` paths) are forbidden too.
The legacy Visual Studio extension is kept in the repo but out of the solution build and CI
until P4 is resumed; any future VS host is a thin shim and the only place Windows APIs may appear. Hard-coded filesystem paths MUST be replaced by SDK/ref-pack
resolution or configuration.

### II. UI-Agnostic Core
`NetPrints.Core`, serialization, reflection and catalog libraries MUST NOT reference any UI
framework. View models MUST NOT use UI-toolkit types (e.g. `Brush`, `Dispatcher`, WPF
`Point`); they use plain structs or abstractions so the same logic serves desktop, VSIX,
browser (WASM) and headless (CLI/sidecar) hosts.

### III. Extension-First, No Forks
Anything target-specific (Unreal, Godot, ASP.NET, …) MUST plug in through NetPrints
extension points (node libraries, emitters, type catalogs, project profiles, host channel,
document stores, UI contributions, settings). If a target needs a behavior NetPrints cannot
express, the fix is a new extension point in NetPrints — never a fork or a target-specific
`if` in core code.

### IV. Single Target Framework
All projects target `net10.0` and may use the latest dependency versions that support it; no
`netstandard2.0` or .NET Framework multi-targeting. The only exception is Roslyn analyzers and
source generators that NetPrints itself ships, which MUST target `netstandard2.0` because the
compiler requires it; third-party generators NetPrints consumes are not covered by this rule.
Libraries used by the browser build MUST be WASM-safe (no blocking file-system or process
access outside abstractions). Visual Studio (.NET Framework) hosting is out of scope.

### V. Tests Gate Every Change (NON-NEGOTIABLE)
Every phase ships with automated tests that run on Linux in CI. Behavior-preserving
refactors MUST keep existing tests green and add characterization or snapshot tests where
generated code or serialized output could drift. A PR with failing or skipped-without-reason
tests MUST NOT be merged.

### VI. Readable, Deterministic Output
Generated C#, catalogs and serialized documents are user-facing artifacts: they MUST be
deterministic (stable ordering, no timestamps unless requested) and diff-friendly. Formats
MUST be versioned (`SchemaVersion`) with migrations for older versions.

### VII. Abstractions Over Concretions for I/O
Serialization formats (`IDocumentFormat`), storage locations (`IDocumentStore`), type
discovery (`IReflectionProvider`/`ITypeCatalog`) and host communication (`IHostChannel`) are
accessed only through interfaces so new formats, stores and hosts can be added without
touching callers.

### VIII. Simplicity and Incremental Delivery
Each roadmap phase is one spec, one branch and one PR that leaves `master` releasable.
Do not implement work belonging to later phases; record it as a follow-up instead. Remove
dead dependencies rather than carrying them forward.

## Technology Constraints

- SDK: .NET 10 (`global.json`, `rollForward: latestFeature`); SDK-style projects only.
- Central Package Management (`Directory.Packages.props`) and shared `Directory.Build.props`;
  nullable reference types and deterministic builds enabled for new/modernized projects.
- Compiler services: Roslyn (latest stable). MVVM:
  CommunityToolkit.Mvvm; DynamicData for search/filtering and large collections; ReactiveUI
  only where it clearly helps. UI: Avalonia 12.x + Nodify.Avalonia; AvaloniaEdit for code views. Logging:
  Microsoft.Extensions.Logging with `[LoggerMessage]` source-generated methods. CLI: Spectre.Console.Cli.
- Project model: NetPrints projects are SDK-style `.csproj` files using the `NetPrints.Sdk`
  package; generated C# is written next to each graph by an MSBuild step, not a source generator.
- Default document format: System.Text.Json with source-generated contexts, behind the
  serialization abstraction. Legacy DataContract XML is import-only once JSON lands.
- No IL weaving (Fody) in new code; prefer source generators.

## Development Workflow

- Spec Kit flow per phase: `speckit-specify` → `speckit-clarify` (as needed) → `speckit-plan`
  (with `research.md`) → `speckit-tasks` → `speckit-analyze` → `speckit-implement`.
- Branch naming follows Spec Kit (`NNN-short-name`); specs live in `specs/NNN-short-name/`.
- One PR per phase against `danielmeza/netprints:master` (never the upstream parent repo).
- Every PR is reviewed by a reviewer other than its implementer. The implementer addresses
  each review comment in code and replies on that comment describing the resolution before
  the PR is merged.
- Main CI is the Linux-only GitHub Actions workflow named `CI` (`.github/workflows/ci.yml`,
  ubuntu-latest). It builds the whole solution and runs all tests headless, and MUST pass
  before merge. The single exception is the VS extension: when P4 resumes it gets its own
  Windows workflow chained after `CI` via `workflow_run`, path-filtered.

## Governance

This constitution supersedes other practices in this repository. Amendments require a PR
that updates this file, bumps the version (MAJOR: principle removed/redefined; MINOR:
principle/section added; PATCH: clarifications) and notes affected specs. Reviewers MUST
check PRs against these principles; any deviation MUST be justified in the plan's
Complexity Tracking section.

**Version**: 1.2.1 | **Ratified**: 2026-09-24 | **Last Amended**: 2026-09-25
