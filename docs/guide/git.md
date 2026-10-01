# Graph files in git

NetPrints graph files are text, designed to be reviewed and merged in git like any other source. This guide covers the CLI tools for that workflow.

## Keeping graphs canonical

A graph's bytes must be identical every time it is saved, so `git diff` is meaningful and merges work correctly. The editor enforces this; for hand-edited or imported graphs, use `format`:

```bash
netprints format samples/
netprints format --check samples/
```

See the [command-line guide](cli.md) for the full details. In CI, `--check` fails if any graph is not canonical:

```bash
netprints format --check
```

## Keeping generated code current

A `.netpc.g.cs` generated file is committed alongside every `.netpc.json` graph. After a graph change, the build regenerates it automatically, but if you hand-edit the graph or merge it, always regenerate to make sure the code is current. In CI:

```bash
netprints generate --check
# or the alias
netprints regen --check
```

This exits 0 if all generated files are current, or 1 if any are stale or missing. See [Projects](projects.md) and the [command-line guide](cli.md) for details.

## Viewing graph changes

By default, `git diff` of a graph file shows raw JSON. To see a readable summary instead, register the `show` command with git:

```bash
netprints git-install
```

After that, `git diff` shows one line per member, node, connection and value, in a stable order. This is the text the `show` command produces (see the [command-line guide](cli.md)):

```
class <namespace>.<name> <visibility> [<modifiers>]
  variable <id> <name> : <type> <visibility> [<modifiers>]
  method <id> <name> <visibility> [<modifiers>]
  ...
```

A node line carries the node's target and then its non-default properties as `key=value` pairs, for example `node n1 callMethod System.Console.WriteLine(System.String) pure=true modifiers=Static`, so a change to `pure`, an argument count or a parameter default shows in the diff. Only layout changes stay out of it: the summary counts the layout entries and does not list positions.

`git diff` never fails on a graph: `git-install` registers `show --textconv`, which prints a file that cannot be read as a graph (for example the conflict-marked file left by a failed merge, or a graph of a newer schema) as its raw text and exits 0. Run `netprints show <graph>` at any time to print the summary directly; without `--textconv` an unreadable file exits 1.

## Merging graphs

When two branches edit the same graph, merge conflicts happen less often. Conflicts that do happen are simpler to resolve, because the merge driver understands the graph structure. To enable it:

```bash
netprints git-install --merge
```

This registers the `merge` driver in addition to the diff configuration. When you merge two branches that touch the same graph, git uses the driver to merge by identity instead of line-by-line:

- Class fields and member fields are merged three-way.
- Variables, methods, constructors and event graphs are merged by id.
- Nodes are merged by id; a node that changes on only one side uses the new version. A node changed differently on both sides is a conflict.
- A node whose properties (for example `pure`) changed on one side, while the other side changed that node's pins or rewired it, is a conflict: its pins follow from its properties, so the driver does not guess.
- Connections are calculated as: `base + additions from your side - removals from your side + additions from their side - removals from their side`.
- Layout positions (where nodes appear in the editor) prefer your version if both sides moved the same node.

When the merge succeeds, the result is written in canonical form and exits 0. When there is a conflict (a node or field changed differently on both sides, both sides connect into the same data or type input pin, or both sides connect a different node after the same exec output pin), the driver exits 1 and falls back to a text merge with conflict markers.

### Limits

The merge driver does not run in web-based merges (GitHub and GitLab UI merges and PR mergeability checks), so resolve conflicts locally with git before pushing. Every clone needs `git-install --merge` because git configuration is not versioned and a committed `.gitattributes` alone falls back to the text merge. When merging generated files (`.netpc.g.cs`), take either side and then run `netprints regen` to update the generated code from the merged graph. Layout-only moves of nodes produce no lines in the `show` summary. Use `git diff --no-textconv` to view raw JSON instead of the summary.

See the [command-line guide](cli.md) for the full driver details on the `merge` command.

### Local vs global installation

By default, `git-install` sets up the drivers for the current repository:

```bash
netprints git-install
```

To set them up for your user account (applies to all your repositories):

```bash
netprints git-install --global
```

The `--global` flag writes to your user git configuration (`core.attributesFile`, or `$XDG_CONFIG_HOME/git/attributes`) and works outside any repository.

### Using a local tool

If `netprints` is installed as a local tool in your project (not global), use:

```bash
netprints git-install --command "dotnet tool run netprints"
```

### Removing the configuration

To undo the installation:

```bash
netprints git-install --uninstall
netprints git-install --global --uninstall
```

This removes exactly the configuration keys and attributes line that `git-install` added. Files that become empty are deleted.

## Schema validation and editor support

Both `.netpc.json` and `netprints.catalog.json` files have published JSON Schemas:

- `netpc.v1.schema.json` for `*.netpc.json` (class graphs)
- `netprints.catalog.v1.schema.json` for `netprints.catalog.json` (catalog configuration)

Every graph file starts with a `$schema` property that points to the schema URL. Editors that understand JSON Schema (VS Code, Rider, JetBrains IDEs) use it to validate and auto-complete the file.

The schemas are prepared for submission to [SchemaStore](https://www.schemastore.org/), so editors like VS Code can validate and complete `*.netpc.json` files even without an explicit `$schema` property (submission to SchemaStore is pending as an owner action).
