# 0014: `NetPrints.Annotations` is a generator-only package with embedded catalogs

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`, research R11, R12).

## Context

Library authors should ship "prints" (curated nodes) with their NuGet package: mark types and methods, build,
and any NetPrints project that references the package offers those nodes. Projects that wrap a third-party
assembly (NetPrintsUnreal's catalog of `UnrealSharp.dll`) want the same from `[assembly: NetPrintsCatalog(...)]`.
Constraints: constitution IV (net10.0 everywhere except the generator itself), the editor never loads user
assemblies to discover types, generators cannot add embedded resources or read files, and the compiler does not
give generators the XML documentation of referenced assemblies.

## Decision

- **Package shape.** `NetPrints.Annotations` contains only the source generator (netstandard2.0, in
  `analyzers/dotnet/cs/`) and `build/NetPrints.Annotations.targets`; it is a development dependency. The
  attributes (`NetPrintsCatalogAttribute`, `NetPrintsTypeAttribute`, `NetPrintsNodeAttribute`,
  `NetPrintsIgnoreAttribute`, and the output attribute `NetPrintsEmbeddedCatalogAttribute`) are injected into
  each consuming compilation as `internal` types marked `[Microsoft.CodeAnalysis.Embedded]`. Nothing is added to
  the consumer's run-time references, so any project the .NET 10 SDK compiles can use it.
- **Output.** Each catalog is emitted as `[assembly: NetPrintsEmbeddedCatalog(id, schemaVersion, json)]` with a
  plain escaped string literal. Referenced-assembly catalogs also get an accessor class for extensions.
- **Discovery.** The editor reads embedded catalogs from its project's references with
  `System.Reflection.Metadata` (never loading them); an extension reads its own with `CustomAttributeData`.
- **Documentation.** The package's targets pass the XML documentation files of non-framework references as
  `AdditionalFiles` (framework ones on request); the generator reads summaries only from there and from source
  symbols, so both flavors normalize the same text.
- **Compiler version.** The generator builds against `Microsoft.CodeAnalysis.CSharp` 5.0.0, the compiler of the
  minimum SDK (`global.json` 10.0.100), through a project-level `VersionOverride`; the repo's central version is
  unchanged.
- **Pipeline.** Own-source annotations use `ForAttributeWithMetadataName` and walk only annotated symbols;
  referenced-assembly catalogs are keyed on the metadata references and the attribute requests, computed in a
  private compilation, so editing source does not re-walk large assemblies.

## Consequences

- Library authors add one private package reference; consumers get curated nodes with no extension and no
  run-time dependency.
- Catalog JSON inside an attribute grows the assembly's metadata by the catalog size; acceptable for curated
  catalogs, and compression is a P8 follow-up for large ones.
- Discovery depends on a well-known attribute name, which becomes part of the catalog format's contract.
- Summaries of referenced assemblies need their `.xml` files next to the DLLs (normal for NuGet packages).
- Rejected (research R11): a manifest resource (a generator cannot add resources, and it is read from the same PE
  bytes; it may return with P8 compression) and a catalog file inside the NuGet package (reaches only package
  consumers and can drift from the DLL).
