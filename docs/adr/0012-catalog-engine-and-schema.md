# 0012: One catalog engine over Roslyn symbols, shared as source with the generator

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`, research R6, R7, R9, R10).

## Context

P2 adds precomputed type catalogs: a library's types and members, filtered by a profile, stored as data so the
editor can offer them without loading the library through the compiler on every start (UnrealSharp in U1). The
roadmap commits to two flavors — a tool (`netprints catalog`) and an annotations source generator — that must
produce the same catalogs and are snapshot-tested against each other. Constitution IV allows `netstandard2.0`
only for the generator NetPrints ships; everything else is `net10.0`. P1 already has the consumption point:
`IExtensionBuilder.AddTypeCatalog(ITypeCatalog)`, and the live provider skips the assemblies a catalog covers.

## Decision

- **Engine.** `NetPrints.Catalog` (net10.0, packable) walks Roslyn symbols (`IAssemblySymbol` and below). The
  model, the engine (`CatalogBuilder`, `ICatalogFilter`, profiles, documentation-id and summary helpers), the
  canonical writer and the C# emitters are written to compile on both `net10.0` and `netstandard2.0`, and the
  generator links those files as source. The writer is hand-written (no System.Text.Json at write time); readers,
  run-time adapters, configuration and source resolution are `net10.0` only.
- **Format.** Catalog files are `*.npcat.json`, `schemaVersion: 1`, with a JSON Schema at
  `https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json`. Content is sorted by documentation-comment
  id, uses the graph format's `TypeRef` and typed-value shapes, omits empty optional properties, and has no
  timestamps or machine paths. Readers reject newer versions and tolerate unknown properties.
- **Consumption.** A loaded catalog is a `CatalogTypeCatalog : ITypeCatalog` that answers every reflection query
  for its covered assemblies, including subclass and implicit-cast queries. Catalogs reach the host through an
  extension (`AddTypeCatalog`) or embedded in a referenced assembly (ADR-0014). Same catalog id twice: first in
  registry order wins. The editor and the generator hosts reference `NetPrints.Catalog`, so extensions share it.
- **Tool flavor.** `netprints catalog` lives in the existing `netprints` tool; there is no separate catalog tool
  package. Every source (`assembly`, `package`, `project`) is resolved through an SDK project evaluated by the P1
  project system (a temporary one for assemblies and packages), so references, the framework reference pack and
  documentation files come from MSBuild and the user's NuGet configuration. The tool never downloads anything
  itself.

## Consequences

- Tool and generator output are identical by construction, and the snapshot tests guard it.
- Shared files carry a small compatibility burden: no API missing from netstandard2.0, a `Guard` helper and one
  polyfill file; the generator build proves they compile.
- `netprints catalog` requires an installed .NET SDK, like every other project command.
- The catalog schema versions independently of the graph schema; a v2 needs a reader migration, like graphs.
- Catalog data is uncompressed in P2; compression for very large catalogs is a P8 follow-up.
