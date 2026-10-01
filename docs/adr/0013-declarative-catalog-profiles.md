# 0013: Catalog profiles are declarative data

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`, research R8).

## Context

A catalog profile decides which types and members a catalog offers ("public API", "only annotated",
NetPrintsUnreal's "Blueprint-visible"). The roadmap names `ICatalogFilter` profiles. The tool runs on `net10.0`
and could load profile code from extensions; the source generator runs inside the compiler on `netstandard2.0`
and cannot load NetPrints extensions. Both flavors must produce byte-identical catalogs (ADR-0012).

## Decision

- `ICatalogFilter` is the engine's filtering interface. The tool and the generator apply only **profiles**:
  `CatalogProfile` data compiled into a `CatalogProfileFilter`.
- A profile has an id, a base (`public-api`, `annotated` or `none`), namespace and type include/exclude globs,
  required or excluded attributes on types and on members (full name, optionally one argument by name or position
  matched by `equals` or `contains` against its rendered constant; enum flags render as member names joined with
  `, `), and obsolete handling (`include`, `exclude`, `excludeErrors`, default `excludeErrors`).
- Built-ins: `public-api` (public types and members plus protected members of unsealed public types, as the live
  provider presents them; `[NetPrintsIgnore]` excluded) and `annotated` (`[NetPrintsType]` types with their
  public members, `[NetPrintsNode]` methods with only their declaring type).
- Custom profiles come from a `*.npprofile.json` file (tool option, config file, or an `AdditionalFiles` item
  for the generator), inline in `netprints.catalog.json`, or from an extension through
  `IExtensionBuilder.AddCatalogProfile(CatalogProfile)` (duplicate ids → `NPX006`, first wins).
- A project's default catalog profile is its project profile's `CatalogProfileId` (P1), else `public-api`.
- Code filters (`ICatalogFilter` implementations) are accepted by the library API (`CatalogBuilder`) only.
- The profile surface is `[Experimental("NPXE0004")]` until the U1 profile has been built on it.

## Consequences

- One profile means the same thing in both flavors; a custom-profile snapshot test covers attribute-argument
  matching the way `unreal-blueprint` will use it.
- NetPrintsUnreal ships its profile as data: an extension contribution for the tool and the editor, and a
  `*.npprofile.json` in its package's build props for the generator.
- Rules that data cannot express need a new profile feature (a schema change) rather than a plug-in; that is
  the intended pressure.
