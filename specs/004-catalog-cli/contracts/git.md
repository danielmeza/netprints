# Contract: graph tooling and git integration (P2)

Implements FR-032–FR-037 (ADR-0016). Sources: `src/NetPrints.Cli/Commands/{Format,Show,Merge,GitInstall}Command.cs`,
`src/NetPrints.Cli/Git/`, `eng/schemastore/`. Tests: `tests/NetPrints.Cli.Tests/Git/`, `tests/NetPrints.Cli.Tests/Commands/`.

## 1. `show` summary grammar

One item per line, LF, UTF-8, two-space indentation per nesting level. Values use the graph format's rendering
(`TypeRef` names, typed values as `<type> <value>`).

```
class <namespace>.<name> <visibility> [<modifiers>]
  generic <name>                                          (per class generic argument)
  variable <id> <name> : <type> <visibility> [<modifiers>]
  method <id> <name> <visibility> [<modifiers>]
  constructor <id> <visibility>
  event-graph <id> <name>
    local <name> : <type>                                 (sorted by name)
    node <id> <kind> [<target>] [<key>=<value>...]        (sorted by id; target = method/type/variable rendering)
      pin <pin> = <typed value>                           (sorted by pin; only pins with a stored value or name)
      raw <json>                                          (unknown extension nodes only)
    connect <from> -> <to>                                (sorted by from, then to)
```

`class` block first, then members in file order (variables, methods, constructors, event graphs, each with its
graph lines), then `layout <n> entries` (count only; layout-only changes stay invisible by design). Unknown extension nodes print as `node <id> <kind>
(extension not loaded)` and, when they have properties besides `$kind` and `id`, a `raw <json>` line (compact, file order) below.

Node properties (F-F2): after the target, `key=value` pairs, scalar values unquoted, `name` quoted and escaped like a pin value; a property at its default is omitted. Keys, in this order per kind: `name` (every kind, when non-empty); methodEntry `args`, `generics` (comma-joined); constructorEntry `args`; return `returns`; classReturn `interfaces`; eventEntry `args`, `visibility`, `modifiers`, `overrides`; callMethod `pure`, `genericArgs`, then `modifiers`, `visibility`; constructor, explicitCast, ternary, await `pure`; makeDelegate `modifiers`, `visibility`; variableGetter and variableSetter `visibility`, `getter`, `setter`, `modifiers`, `scope`; makeArray `predefinedSize`, `elements`; reroute `count`, `types` (groups `;`-separated, types comma-joined). `pure` and `predefinedSize` print `=true`; `visibility` and the method `modifiers` are omitted at `Public` and `None`, `scope` at `Member`, counts at 0. A method reference renders as `<type>.<name>[<generic args>](<params>)[-><return types>]`; a parameter is `[<pass type> ]<type>[=<default type>:"<value>"]` (`null` unquoted). `overrides=` prints the signature and return types only. Kinds without properties (typeReturn, makeArrayType, typeOf, ifElse, forLoop, throw, default, type, literal) print the kind and target only. A built-in kind missing from the writer prints its registered `$kind`, never a CLR name. Golden: a hand-written graph, `Fixtures/ShowGrammar/graph.txt` with `expected.show.txt`, written from this text.

`show --textconv`: a file that cannot be read as a graph (read failure, unsupported schema, I/O error) is printed as its raw text and the exit code is 0; a missing file still exits 2. Plain `show` keeps exit 1 for an unreadable file.

Additions fixed in F1: a pin line is `pin <pin> [as <name>] [= <type> "<value>"]` (value backslash-escaped, `null` unquoted); modifiers are comma-joined; a variable's type is the `type` node wired into its `typeReturn`, and its `type-graph`, `getter <visibility>` and `setter <visibility>` blocks (depth 2) hold their graph lines; the class graph's lines sit at depth 1. Golden: `tests/NetPrints.Cli.Tests/Git/Snapshots/HelloWorld.show.txt` and `AllNodes.show.txt`.

## 2. `merge`

Inputs: `%O` base, `%A` ours (also the output), `%B` theirs; `--marker-size` (default 7); `--path` (display name).

Algorithm (research R15):
1. Read the three files through the `IDocumentFormat` that `DocumentFormatRegistry` resolves for `--path` (git
   passes temporary files without the graph extension; default: the `.netpc.json` format). Any failure → text
   fallback (step 5) on the raw texts. A null in a member the document declares non-nullable (e.g. `"nodes": null`)
   is a read failure, as is an exception thrown by the merge itself.
2. Merge by identity: scalar class fields three-way; variables/methods/constructors/event graphs by id; per member
   scalar fields three-way and the graph merged as below; delete vs modify → conflict.
3. Graph: nodes by id — unchanged on one side → take the other; both changed → per-pin `(Name, Value)` three-way,
   other node properties three-way; add/add with the same id and different content → conflict. Node shape: when a
   node's non-pin properties (e.g. `pure`) changed on exactly one side and the other side changed that node's pins or
   added or removed a connection touching it → `NodeProperty` conflict (a node's pins follow from its properties,
   which the driver cannot evaluate). Connections:
   `base ∪ addedOurs ∪ addedTheirs − removedOurs − removedTheirs`. Locals by name. Node order: base order, then
   ours' additions, then theirs'. Layout: per graph key and node id, three-way with ours winning; entries of
   deleted nodes dropped.
4. Validate: connection endpoints exist; ≤ 1 connection into each data input pin and each type input pin, and ≤ 1
   connection out of each exec output pin (exec inputs and data/type outputs may have many);
   member ids unique; member names unique per kind. Clean and valid → write canonical bytes to `%A`, exit 0.
5. Fallback: write the canonical texts of the three documents (or raw texts) to temp files and run
   `git merge-file --marker-size <n> -L ours -L base -L theirs <ours> <base> <theirs>` through `IProcessRunner`
   (no `-p`: it merges into the temporary `ours` file); read that file back as bytes and write the bytes to `%A`, so a
   byte order mark and bytes that are not UTF-8 survive; exit 1 (also when `git merge-file` reports 0 conflicts after a semantic conflict — a
   semantic conflict always exits 1). List each conflict on stderr.

**Decisions**

- When the same id is added on both sides, the conflicts raised are `DuplicateMember` (if contents differ) or `NodeProperty` (if the same). For locals and accessor changed/added differently, the conflict is `Scalar`.
- When an item (member, node, pin, connection, layout entry) is deleted on one side and unchanged on the other, the deletion wins; a delete vs modify conflict is raised only when the base had the item (step 2).

## 3. `git-install`

| Option | Effect |
|---|---|
| (none) | `git config diff.netprints.textconv "<cmd> show --textconv"`; `.gitattributes` at the work-tree root gets `*.netpc.json diff=netprints` |
| `--merge` | Also `git config merge.netprints.name "NetPrints graph merge"` and `merge.netprints.driver "<cmd> merge %O %A %B --marker-size %L --path %P"`; the attributes line becomes `*.netpc.json diff=netprints merge=netprints` |
| `--global` | `git config --global …`; attributes in `core.attributesFile` (default `$XDG_CONFIG_HOME/git/attributes` or `~/.config/git/attributes`) |
| `--command <cmd>` | Command used in the config values (default `netprints`) |
| `--uninstall` | Removes exactly those config keys and the line it added |

Rules: runs `git rev-parse --show-toplevel` first (not a work tree → exit 2); an existing identical line or value →
reported as `already installed`, nothing written; an existing `*.netpc.json` line naming another `diff=`/`merge=`
driver → kept, reported, exit 1 without writing; the `.gitattributes` file keeps its line endings and other
lines.

`show` writes UTF-8 without a byte order mark whatever the console code page (the entry point sets the console output
encoding).

**Decisions**

- `--global` skips the `git rev-parse` work-tree check (not a work tree → still succeeds, writing global config).
- `--merge` sets up the merge driver; downgrading back to diff-only (removing `merge=netprints` from attributes) requires running `git-install` again without `--merge` (downgrade without re-run is not performed).
- Our own config keys (`diff.netprints.*`, `merge.netprints.*`) with existing different values are overwritten.
- `--uninstall` removes the attributes line it added; if `.gitattributes` becomes blank afterwards, the file is deleted. Empty `[diff "netprints"]` and `[merge "netprints"]` sections in git config are left behind (they are harmless and easy to spot if needed).

## 3a. `format`

`format` rewrites each graph with the JSON format's write path, the same one the editor saves through. That path writes
every graph's `connections` (class graph, methods, constructors, event graphs) sorted by ordinal `from`, then ordinal
`to`: a total, stable order (`StringComparison.Ordinal`, never culture-sensitive). Reading accepts any order; only writing
canonicalises, so `format --check` flags a file whose connections are in another order (a hand edit or a hand-resolved
merge). Pin order inside a node is the editor's declaration order and cannot be derived without node definitions, so
`format` keeps it. Messages name a path relative to the current directory when it lies inside it and absolute otherwise;
an unreadable directory and an unreadable file both print as `unreadable: <path>: <reason>`.

## 4. SchemaStore entry (`eng/schemastore/catalog-entries.json`)

```json
[
  {
    "name": "NetPrints graph",
    "description": "NetPrints visual-programming class graph (.netpc.json)",
    "fileMatch": ["*.netpc.json"],
    "url": "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json"
  },
  {
    "name": "NetPrints catalog configuration",
    "description": "Configuration of the netprints catalog command",
    "fileMatch": ["netprints.catalog.json"],
    "url": "https://danielmeza.github.io/netprints/schemas/netprints.catalog.v1.schema.json"
  }
]
```

`eng/schemastore/PULL_REQUEST.md` holds the upstream PR title, body and SchemaStore's checklist answers, and the
line "**Owner action**: submit after the docs site serves both URLs; not done by agents."

## 5. Test obligations

| Id | Test (file) | Case |
|---|---|---|
| GI-T01 | `Commands/FormatCommandTests` | Non-canonical file → `--check` exit 1, no write; `format` rewrites it; canonical files untouched (bytes and timestamp); invalid JSON → `unreadable`, exit 1, others processed; directory recursion |
| GI-T02 | `Git/GraphSummaryTests` | HelloWorld and AllNodes goldens; unknown-extension node line; node order in the file does not change the output |
| GI-T03 | `Git/GraphMergerTests` | Two branches add different nodes/connections/layout to one method (DF-T23 fixture) → clean, canonical, contains both; plain `git merge-file` on the same inputs reports at least one conflict |
| GI-T04 | `Git/GraphMergerTests` | Same pin value changed differently → exit 1, markers present, both values in the output |
| GI-T05 | `Git/GraphMergerTests` | Node deleted on one side, pin changed on the other → conflict fallback |
| GI-T06 | `Git/GraphMergerTests` | Both sides connect different sources into one data input; a connection to a node the other side deleted → conflict fallback |
| GI-T07 | `Git/GraphMergerTests` | Conflict-marked (unreadable) input → fallback on raw text, exit 1 |
| GI-T08 | `Git/GraphMergerTests` | Both sides move the same node → ours wins, clean |
| GI-T09 | `Commands/GitInstallCommandTests` | Temp repo: install, idempotent second run, `--merge`, `--uninstall`, conflicting line kept (exit 1), outside a repo → 2 |
| GI-T10 | `Git/GitDriversEndToEndTests` | Temp repo with `--command "dotnet <path>/NetPrints.Cli.dll"`: `git diff` shows summary lines; `git merge` of GI-T03's branches succeeds through the driver (SC-009, SC-010) |
| GI-T11 | `Git/SchemaStoreEntryTests` | Each `url` equals a committed schema's `$id`; `fileMatch` values as above |
| GI-T12 | `Git/GraphMergerTests` | The merged file of GI-T03 generates the same C# as the hand-merged reference graph |
