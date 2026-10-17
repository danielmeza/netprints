# 0024: Analyzer and generator infrastructure: four Roslyn projects, four rule families

## Status

Accepted (2026-10-16, P3 spec `specs/006-editor-extension-host/`, research R2; lands in sub-phase A). It amends the
StyleCop note of [ADR-0003](0003-analyzer-promotion-and-string-literal-discipline.md): layout rules NetPrints needs and
`dotnet format` cannot express are now written as NetPrints analyzers.

## Context

- ADR-0003 chose Sonar, IDisposableAnalyzers and the threading analyzers, and left layout to `dotnet format`. Two
  conventions the owner decided cannot be expressed that way: an attribute never shares a line with its member, and a
  blank line separates members by a scope rule (contracts/analyzer-diagnostics.md §2).
- P3 adds more compile-time work for authors: declarative pins, constants checked at build time, node kinds by
  convention. These must ship with the `NetPrints.Core` they were written against, or the attributes and the
  generator skew (the lesson of ADR-0022).
- Repository-only checks (hygiene rules now in `SourceHygieneTests`, the DI registration generator, the smart-enum
  generator) must never reach a user's project.
- `NetPrints.Annotations` already shows that a generator is a `netstandard2.0` project that links shared sources and
  ships in `analyzers/dotnet/cs` (ADR-0014).

## Decision

### Four projects

All four are `netstandard2.0` (the constitution IV exception for Roslyn components) and reference
`Microsoft.CodeAnalysis.CSharp` 5.0.0 through `VersionOverride`, the way `NetPrints.Annotations` does.

| Project | Ships | Holds |
|---|---|---|
| `src/NetPrints.Authoring` | inside the `NetPrints.Core` package, `analyzers/dotnet/cs` | the pin convention generator, the NPA analyzers, later the node-kind generator |
| `src/NetPrints.Authoring.CodeFixes` | next to it | the NPA code fixes |
| `src/NetPrints.RepoAnalyzers` | never (`IsPackable=false`) | the NPS style rules, the NPH hygiene rules, the smart-enum and DI registration generators with the NPI analyzers |
| `src/NetPrints.RepoAnalyzers.CodeFixes` | never | the NPS fixes that `dotnet format analyzers` applies |

Code fixes live apart from the analyzers so the compiler never loads the Workspaces reference (RS1038). Attributes the
internal generators need are emitted as `internal` post-initialization sources, so no runtime attribute assembly is
added. `src/Shared/Roslyn/` holds `EquatableArray<T>` and the diagnostic help-link helper, linked into every project
that needs them (as `NetPrints.Annotations` links the Catalog sources).

### Wiring

Analyzers do not flow through `ProjectReference`, so each consumer lists them:

- `src/Directory.Build.props` and `tests/Directory.Build.props` reference the two RepoAnalyzers projects with
  `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`; the RepoAnalyzers projects skip themselves.
- `src/NetPrints.Core/NetPrints.Core.csproj` references the two Authoring projects the same way and packs both
  assemblies under `analyzers/dotnet/cs`.
- `tests/Directory.Build.targets` adds the Authoring pair to every test project that references Core, so tests see
  what a user's project sees. It imports the repository `Directory.Build.targets` first.

### Rule families

| Prefix | Owner | Shipped | Runs in |
|---|---|---|---|
| NPS | RepoAnalyzers | no | `src/`, `tests/` |
| NPA | Authoring | yes | every project that references Core |
| NPH | RepoAnalyzers | no | `src/` (threading switches also in `tests/`) |
| NPI | RepoAnalyzers (generators) | no | `src/` |

The codes, severities and the sub-phase each lands in are in `contracts/analyzer-diagnostics.md`. A code is a constant
in a `*DiagnosticCodes` class (ADR-0003 literal discipline), is tracked in `AnalyzerReleases.Shipped.md` and
`Unshipped.md` (RS2008), is never reused for another meaning, and has a row in `docs/guide/diagnostics.md`; the
`DiagnosticsGuideTests` check keeps the page honest. Shipped rules default to `error` only where a build cannot be
correct without the fix; a rule that guides rather than protects defaults to `warning`.

### Test style

One project, `tests/NetPrints.Analyzers.Tests`, covers every analyzer, fix and generator NetPrints owns, and runs as
the `Analyzers` leg of the CI matrix.

- Generators: snapshot tests of the generated source, compile tests of the result, and incrementality tests (the
  pipeline caches on equal input) through the shared host in `tests/NetPrints.Testing/Roslyn`.
- Analyzers: a positive and a negative test per rule at exact spans, through `CompilationWithAnalyzers`.
- Code fixes: tested through an `AdhocWorkspace`, with the fixed text compared as a whole.
- `Microsoft.CodeAnalysis.Testing` is not added: it would be a second test style for the same job.
- The infrastructure test loads each of the four assemblies, checks every analyzer declares supported diagnostics that
  the release files track, and that every code fix targets a diagnostic of its sibling analyzer assembly.

### Build-time budget

The cost of the analyzers is measured with `-p:ReportAnalyzer=true` before and after each sub-phase that adds rules
(S1, then S2a). The budget is under 2% of the clean build. Measurements go in the P3 implementation notes.

## Consequences

- Authors get checks that match the exact `NetPrints.Core` they compile against, with no extra package to keep in step.
- The repository can retire hygiene tests as analyzers (NPH, in sub-phase C2) only after each analyzer is red on the
  planted violation and green on the repository.
- Every project in `src/` and `tests/` builds the two RepoAnalyzers projects first; a rule that is slow shows up in
  the budget measurement, not in a surprise CI time.
- The `NetPrints.Core` package gains two small assemblies; package validation and the local-feed job cover them.

## References

- `specs/006-editor-extension-host/research.md` R2, R3; `contracts/analyzer-diagnostics.md`.
- ADR-0003 (analyzer promotion, amended), ADR-0014 (the annotations generator package), ADR-0022.
