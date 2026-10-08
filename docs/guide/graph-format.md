# Graph file format

Each class is a `*.netpc.json` file. It is a plain, deterministic JSON document designed to be
readable and mergeable in an ordinary git workflow: opened in a diff viewer, reviewed in a pull
request, or resolved by hand when two branches touch the same file.

## Schema and editor support

Every graph starts with a `$schema` pointer:

```json
{
  "$schema": "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json",
  "schemaVersion": 1,
  ...
}
```

`$schema` is informational only — NetPrints never fetches it, and ignores it on read. Editors that
understand JSON Schema (VS Code, Rider) use it for validation and completion once the URL resolves
(it is published from the [NetPrints Sdk](https://www.nuget.org/packages/NetPrints.Sdk) repository's
own site, see [`schemas/netpc.v1.schema.json`](../../schemas/netpc.v1.schema.json) in the repository
for the schema itself).

## Version control

A `.netpc.json` file is written in a canonical form: 2-space indentation, sorted maps, and a fixed
property order, so the same graph always serializes to the same bytes. Short, self-contained values
(a node's connections, a layout position, a typed constant) are written on a single line each rather
than spread across several; everything else is one property per line. A file is only rewritten when
its class actually changed, so an unrelated build or open never touches files you didn't edit.
A graph's connections are always written sorted by `from`, then `to` (ordinal string order), by both the
editor and `netprints format`; the loader accepts any order, and `format --check` reports a file whose
connections are in another order.

Every project gets a `.gitattributes` file next to it (written once, when the project is created)
with:

```gitattributes
*.netpc.json text eol=lf
*.netpc.g.cs text eol=lf
```

This keeps line endings consistent across Windows, Linux and macOS checkouts, which matters for a
line-oriented diff to stay meaningful.

### What a change looks like in a diff

Moving a node changes exactly one line, the node's entry in `layout`:

```diff
-      "n00000000057k3": [420, 112]
+      "n00000000057k3": [460, 140]
```

Adding a node adds one node block (or, for a simple node, one line), one line per new connection,
and one new layout line — nothing else in the file shifts. Node ids are randomly generated rather
than sequential counters, so two branches that each add a node do not collide; connections and
layout entries reference nodes and members by id, not by position in an array, so reordering a
method or inserting a parameter into a called method's signature does not silently retarget an
unrelated connection or move someone else's layout entry.

That combination is what keeps a merge to "both sides added a block; git keeps both" instead of a
conflict spanning the whole nodes array — see the
[graph file format contract](../../specs/003-core-refactor/contracts/document-format.md) for the
full schema, identifier rules and canonical-writing rules behind this.

## Custom event arguments

A custom event entry (`"$kind": "eventEntry"`) can declare arguments. They are written as an optional
`arguments` array, after `argumentCount`:

```json
{ "$kind": "eventEntry", "argumentCount": 2, "arguments": [
  { "name": "amount", "type": { "name": "System.Int32" } },
  { "name": "source", "type": { "name": "System.String" } } ] }
```

The property is optional in version 1 and is written only when an argument has a declared type other
than `object`; an entry without it is written exactly as before, so existing files do not change.
`argumentCount` is still written, which is what a reader that does not know `arguments` uses: it
creates that many arguments typed `object`. This means **an older editor drops custom event arguments
when it saves the graph**: it reads the entry with untyped arguments and writes it back without the
`arguments` array. An override entry never writes `arguments`; its arguments come from the base method.

## Version control

Graph files are designed to work well in git: they are text, canonical (deterministic bytes), and
merge-friendly. See [Git workflow](git.md) for the `show` command (readable diffs), the merge driver
(smart merging), and checks for canonical form and current generated code. Both `.netpc.json` and
`netprints.catalog.json` files have schemas prepared for SchemaStore registration (see the [Git
workflow](git.md#schema-validation-and-editor-support) section for details).

## Generated code

Each graph file has a matching `<Name>.netpc.g.cs`, generated at build time and committed alongside
it. It is not marked as generated for diff purposes, so it shows up in pull request reviews too —
see [Projects](projects.md) for how that pairing works and why the generated file should never be
hand-edited.
