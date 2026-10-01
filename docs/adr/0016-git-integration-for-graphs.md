# 0016: Git integration for graph files: text conversion, merge driver and SchemaStore

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`, research R14–R17). Implements the follow-ups of
`docs/research/2026-09-25-graph-format/` §5.3–§5.4 and §7.

## Context

Graph files (`*.netpc.json`) are canonical JSON designed to merge well in plain git: stable ids, one line per
connection and layout entry. Two gaps remain: appends to the same method's `nodes` array on two branches still
conflict textually, and a JSON diff is harder to read than the change it encodes. Hosted web UIs run neither
custom diff text conversions nor custom merge drivers, so the file format must stay reviewable on its own.
Editors can validate `.netpc.json` from SchemaStore without a `$schema` property once the schema is registered
there; registering means a pull request to `SchemaStore/schemastore`, and nothing is opened upstream without the
owner's approval.

## Decision

- **Text conversion.** `netprints show <file>` prints a stable line-oriented summary (class header, members,
  nodes and pin values sorted by id, connections sorted, locals). It never fails on nodes of unloaded extensions.
  Git uses it through `diff.netprints.textconv`.
- **Merge driver.** `netprints merge %O %A %B --marker-size %L --path %P` merges by identity: members by id,
  nodes by id (then per pin), connections as sets, locals by name, layout per node with ours winning; the result
  is validated (endpoints exist, one connection per data input, unique member ids and names) and written
  canonically. Any semantic conflict, invalid result or unreadable input falls back to `git merge-file` over the
  canonical texts, so the user gets familiar markers and the driver exits 1. It never exits 0 after dropping a
  change. Generated `.netpc.g.cs` files are regenerated after a merge, never merged by hand.
- **Installation.** `netprints git-install` sets the diff driver (and with `--merge` the merge driver) in the
  repository's git config (`--global` for the user's), adds `*.netpc.json diff=netprints [merge=netprints]` to
  `.gitattributes` (or the global attributes file), is idempotent, leaves conflicting existing lines alone, and
  has `--uninstall`. `--command` sets how git invokes the tool (for example `dotnet netprints` for a local tool).
- **CI gates.** `netprints format --check` and `netprints regen --check` are the recommended CI steps and run in
  this repository's CI over the samples.
- **SchemaStore.** The repository holds a ready entry for `*.netpc.json` and `netprints.catalog.json`
  (`eng/schemastore/`) and the PR text; a test keeps the URLs equal to the committed schemas' `$id`s.
  Submitting it is an owner action once the docs site serves the schemas.

## Consequences

- Teams that run `git-install --merge` get clean merges of disjoint graph edits locally; web merges keep plain
  git behaviour, which the format already makes tolerable.
- `git diff` and `git log -p` show readable summaries; the JSON and the committed `.netpc.g.cs` remain the review
  surface on GitHub.
- The merge driver is internal to the CLI (no public API); an in-editor visual diff is P6 work.
