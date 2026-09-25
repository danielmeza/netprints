<!--
Sync Impact Report
- Version change: template → 1.0.0 (initial ratification)
- Principles added: I–VIII (all new)
- Sections added: Technology Constraints, Development Workflow, Governance
- Templates: plan/spec/tasks templates read this file at runtime; no template edits required
- Deferred TODOs: none
-->
# NetPrints Constitution

NetPrints is a visual programming language for .NET: node graphs are edited in a UI and
translated to readable C#. This constitution governs the modernization of NetPrints
(Avalonia editor, modern .NET) and its use as a host for extensions such as NetPrintsUnreal.
The phased roadmap lives in `.specify/memory/roadmap.md`.

## Core Principles

### I. Cross-Platform, Linux-First
Every project except the Visual Studio extension MUST build, test and run on Linux, Windows
and macOS. Linux is the primary development and CI platform. Windows-only APIs
(WinForms, `Microsoft.Win32`, registry, `Program Files` paths, WPF) are forbidden outside
`NetPrints.VisualStudio`. Hard-coded filesystem paths MUST be replaced by SDK/ref-pack
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

### IV. Host Compatibility Targets
Libraries loadable by a host MUST respect that host's runtime: libraries used by the VSIX
multi-target `netstandard2.0` (or `net48`) alongside `net10.0`; libraries used by the browser
build MUST be WASM-safe (no blocking file-system or process access outside abstractions).
Roslyn analyzers/generators target `netstandard2.0`. Applications target `net10.0`.

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
- Compiler services: Roslyn 4.x. MVVM: CommunityToolkit.Mvvm, with ReactiveUI/DynamicData for
  high-throughput UI paths. UI: Avalonia 11.x + Nodify.Avalonia. CLI: Spectre.Console.Cli.
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
- CI (GitHub Actions) MUST pass on Linux; Windows jobs are added where a host requires them.

## Governance

This constitution supersedes other practices in this repository. Amendments require a PR
that updates this file, bumps the version (MAJOR: principle removed/redefined; MINOR:
principle/section added; PATCH: clarifications) and notes affected specs. Reviewers MUST
check PRs against these principles; any deviation MUST be justified in the plan's
Complexity Tracking section.

**Version**: 1.0.0 | **Ratified**: 2026-09-24 | **Last Amended**: 2026-09-24
