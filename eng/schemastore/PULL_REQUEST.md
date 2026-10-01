# SchemaStore pull request

**Owner action**: submit after the docs site serves both URLs; not done by agents

## Target

Repository `SchemaStore/schemastore`, file `src/api/json/catalog.json`: add the two objects of
`eng/schemastore/catalog-entries.json` to the `schemas` array, in alphabetical position by `name`.

## Title

Add NetPrints graph and catalog configuration schemas

## Body

NetPrints is a visual-programming tool for .NET that stores class graphs as `*.netpc.json` documents and reads an
optional `netprints.catalog.json` to build type catalogs. Both formats have a versioned JSON Schema published with the
project documentation:

- `https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json` (files matching `*.netpc.json`)
- `https://danielmeza.github.io/netprints/schemas/netprints.catalog.v1.schema.json` (files named `netprints.catalog.json`)

The schemas are hosted by the project (`url` entries, no copy in this repository) and validated in the project's CI
against every committed sample document.

## Checklist answers

- The schema is hosted at the project's own URL, not copied into SchemaStore: yes.
- The `fileMatch` patterns are specific to the tool (`*.netpc.json`, `netprints.catalog.json`): yes.
- The schemas declare `$schema` (JSON Schema draft 2020-12) and an `$id` equal to the catalog `url`: yes.
- The schemas are valid against their meta-schema and linted in CI (`eng/validate-schemas.sh`): yes.
- Test files in `src/test` or `src/negative_test`: not needed for remote-hosted schemas; the project validates its own
  sample documents in CI.
