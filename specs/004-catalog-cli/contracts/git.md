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
    node <id> <kind> [<target>]                           (sorted by id; target = method/type/variable rendering)
      pin <pin> = <typed value>                           (sorted by pin; only pins with a stored value or name)
    connect <from> -> <to>                                (sorted by from, then to)
```

`class` block first, then members in file order (variables, methods, constructors, event graphs, each with its
graph lines), then `layout <n> entries` (count only). Unknown extension nodes print as `node <id> <kind>
(extension not loaded)`. Additions fixed in F1: a pin line is `pin <pin> [as <name>] [= <type> "<value>"]` (value backslash-escaped, `null` unquoted); modifiers are comma-joined; a variable's type is the `type` node wired into its `typeReturn`, and its `type-graph`, `getter <visibility>` and `setter <visibility>` blocks (depth 2) hold their graph lines; the class graph's lines sit at depth 1. Golden: `tests/NetPrints.Cli.Tests/Git/Snapshots/HelloWorld.show.txt` and
`AllNodes.show.txt`.

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
   `git merge-file -p --marker-size <n> -L ours -L base -L theirs <ours> <base> <theirs>` through `IProcessRunner`;
   write its stdout to `%A`; exit 1 (also when `git merge-file` reports 0 conflicts after a semantic conflict — a
   semantic conflict always exits 1). List each conflict on stderr.

## 3. `git-install`

| Option | Effect |
|---|---|
| (none) | `git config diff.netprints.textconv "<cmd> show"`; `.gitattributes` at the work-tree root gets `*.netpc.json diff=netprints` |
| `--merge` | Also `git config merge.netprints.name "NetPrints graph merge"` and `merge.netprints.driver "<cmd> merge %O %A %B --marker-size %L --path %P"`; the attributes line becomes `*.netpc.json diff=netprints merge=netprints` |
| `--global` | `git config --global …`; attributes in `core.attributesFile` (default `$XDG_CONFIG_HOME/git/attributes` or `~/.config/git/attributes`) |
| `--command <cmd>` | Command used in the config values (default `netprints`) |
| `--uninstall` | Removes exactly those config keys and the line it added |

Rules: runs `git rev-parse --show-toplevel` first (not a work tree → exit 2); an existing identical line or value →
reported as `already installed`, nothing written; an existing `*.netpc.json` line naming another `diff=`/`merge=`
driver → kept, reported, exit 1 without writing; the `.gitattributes` file keeps its line endings and other
lines.

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
| GI-T03 | `Git/GraphMergerTests` | Two branches add different nodes/connections/layout to one method (DF-T23 fixture) → clean, canonical, contains both; plain `git merge-file` on the same inputs reports 1 conflict |
| GI-T04 | `Git/GraphMergerTests` | Same pin value changed differently → exit 1, markers present, both values in the output |
| GI-T05 | `Git/GraphMergerTests` | Node deleted on one side, pin changed on the other → conflict fallback |
| GI-T06 | `Git/GraphMergerTests` | Both sides connect different sources into one data input; a connection to a node the other side deleted → conflict fallback |
| GI-T07 | `Git/GraphMergerTests` | Conflict-marked (unreadable) input → fallback on raw text, exit 1 |
| GI-T08 | `Git/GraphMergerTests` | Both sides move the same node → ours wins, clean |
| GI-T09 | `Commands/GitInstallCommandTests` | Temp repo: install, idempotent second run, `--merge`, `--uninstall`, conflicting line kept (exit 1), outside a repo → 2 |
| GI-T10 | `Git/GitDriversEndToEndTests` | Temp repo with `--command "dotnet <path>/NetPrints.Cli.dll"`: `git diff` shows summary lines; `git merge` of GI-T03's branches succeeds through the driver (SC-009, SC-010) |
| GI-T11 | `Git/SchemaStoreEntryTests` | Each `url` equals a committed schema's `$id`; `fileMatch` values as above |
| GI-T12 | `Git/GraphMergerTests` | The merged file of GI-T03 generates the same C# as the hand-merged reference graph |
