# Command-line tool

The `netprints` command-line tool is a global tool installed from the `NetPrints.Cli` package. It can build projects, run programs, regenerate graphs, catalog assemblies, and report on the state of graph files.

## Global options

```
netprints [--verbose] <command> [arguments] [options]
netprints --help | -h | --version
```

- `--version` prints the version (e.g., `NetPrints.Cli 0.2.0`) and exits.
- `--help` or `-h` prints command help and exits. Pass it at any level: `netprints --help` for the overview, or `netprints build --help` for a command.
- `--verbose` lowers the log level to Information and prints stack traces for errors. It works before or after the command name; without a command (`netprints --verbose --help`) it has no effect.
- Output is plain text (no ANSI colours) when redirected or when `NO_COLOR` is set.

### Output streams

Results go to stdout: `Build succeeded.`, `generated:` and `stale:` lines, diagnostics as `file(line,column): code: message`, and the summary line. Everything else goes to stderr: usage errors (exit 2), the "no SDK" message (exit 3), project restore or load errors (exit 1), version warnings, logs and internal-error details. So `netprints build missing 2>err.log` puts the message in `err.log` and leaves stdout empty, and `netprints generate --check > result.txt` captures only the results.

## Project argument

Several commands take an optional `<project>` argument:

```
netprints build [<project>]
netprints run [<project>]
netprints generate [<project>]
netprints migrate [<paths>]
```

`<project>` can be:

- A `.csproj` file path (e.g., `MyApp.csproj`)
- A directory holding a single `*.csproj` (e.g., `src/`)
- Omitted to use the current directory

If zero or several `.csproj` files are found, or the path does not exist or is not a `.csproj`, the tool exits with code 2 and a reason.

## Commands

### `build` — Build a project

```
netprints build [<project>] [--verbose]
```

Compiles the project's graphs to C#, then builds it with `dotnet build`. This runs the NetPrints generator before the C# compilation, and reports errors in `file(line,column): code: message` format.

**Example:**

```bash
netprints build
netprints build samples/HelloWorld
```

**Output:** `Build succeeded.` or `Build failed with N error(s).`

### `run` — Build and run a project

```
netprints run [<project>] [-- <args>...]
```

Builds the project, and if the build succeeds, runs the compiled program. Everything after the first `--` is forwarded to the program exactly as typed (empty values, a lone `-` and values such as `--=` included; the tool does not parse it), and the program's exit code becomes the command's exit code.

The program runs with your terminal's stdin, stdout and stderr, so prompts, streaming output and long-running programs behave as they do under `dotnet run`. Ctrl+C stops the program and the command exits with 130.

**Example:**

```bash
netprints run
netprints run samples/HelloWorld -- arg1 arg2
```

**Output:** Build output as above, then the program's own output, live.

### `generate` — Regenerate or check if graphs are up to date

```
netprints generate [<project>] [--check] [--graph <file>]... [--verbose]
netprints regen [<project>] [--check] [--graph <file>]... [--verbose]
```

Compiles each graph in the project to C#. By default, it writes new or updated files and reports each one. With `--check`, it reports stale or missing generated files and writes no generated file (it still loads the project, which restores packages and may create `obj/`).

The tool renders with the generator built into it, while `dotnet build` uses the generator of the project's `NetPrints.Sdk` package, so the two versions should match. When the project references a `NetPrints.Sdk` package whose version differs from the tool's (ignoring `+build` metadata), plain `generate` prints a warning to stderr and continues; `generate --check` prints an error naming both versions and exits 1. Align them with `dotnet tool update -g NetPrints.Cli --version <sdk version>` or by changing the `PackageReference` to the tool's version. Projects that use the in-repo SDK (`NetPrintsUseLocalSdk`, the samples) are not compared.

**Options:**

- `--check` — Write no generated file. Exit 1 if any generated file is stale or missing, or if the tool and SDK versions differ.
- `--graph <file>` — Limit the run to this graph file of the project (repeatable). The path is relative to the **current directory**, not the project directory, and must name a graph of the project (otherwise exit 2). It is compared case-insensitively on Windows and macOS.

**Examples:**

```bash
netprints generate
netprints generate samples/HelloWorld --check
netprints regen --graph samples/HelloWorld/HelloWorld.Program.netpc.json
```

**Output:**
- `generated: <path>` for each file written.
- `stale: <path>` for each file that differs when using `--check`.
- Last line: `N generated file(s) up to date, M written.` or `M stale.` with `--check`.

### `migrate` — Report the schema version of graphs

```
netprints migrate [<paths>...] [--verbose]
```

Reports the schema version of graph files. No migrations exist yet; all graphs are at version 1. Pass one or more graph files, directories to search recursively, or a project whose graphs to check.

**Examples:**

```bash
netprints migrate
netprints migrate samples/HelloWorld
netprints migrate samples/HelloWorld/HelloWorld.Program.netpc.json
```

**Output:** `<path>: schema 1 (current)` per graph, then `No migrations are available; N graph(s) are at schema version 1.` A graph with a newer schema prints `<path>: schema <n> is not supported (this tool supports 1)`, one without a `schemaVersion` prints `<path>: unreadable: Missing 'schemaVersion'.`, and both exit 1. Directory searches do not follow symbolic links, and a directory that cannot be read is reported as `<dir>: unreadable: <reason>` (exit 1).

### `format` — Canonicalize graph files

```
netprints format [<paths>...] [--check]
```

Rewrites `.netpc.json` graphs into their canonical form (the form the editor writes), touching only files whose bytes change. Paths are graph files or directories searched recursively (`bin`, `obj` and symbolic links are skipped); the default is the current directory. With `--check` it writes nothing and exits 1 if any graph is not canonical or cannot be read.

```
netprints format samples
netprints format --check samples
```

**Output:** `formatted: <path>` per rewritten file (or `not canonical: <path>` with `--check`), `unreadable: <path>: <reason>` per unreadable file, then a summary line. A path that does not exist or is neither a graph nor a directory exits 2.

### `show` — Summarize a graph

```
netprints show <graph>
```

Prints a stable, line-oriented summary of one graph (class header, members, nodes sorted by id, pin values, connections, layout entry count), independent of the node order in the file. Nodes of extension kinds print as `node <id> <kind> (extension not loaded)`. This is the text `git diff` shows once `git-install` has configured it.

```
netprints show samples/HelloWorld/HelloWorld.Program.netpc.json
```

### `merge` — Git merge driver for graphs

```
netprints merge <base> <ours> <theirs> [--marker-size <n>] [--path <name>]
```

The merge driver `git-install --merge` registers (git passes `%O %A %B`, `%L` as the marker size and `%P` as the path). It reads the three versions, merges them by identity and, when the result is valid, writes the canonical graph over `<ours>` and exits 0: class fields and member fields three-way; variables, methods, constructors and event graphs by id; nodes by id, pins by key (name and unconnected value), connections as additions and removals against the base, layout with `<ours>` winning when both sides moved the same node.

It falls back to `git merge-file -p` over the three texts, writes that over `<ours>` (conflict markers `--marker-size` long, labelled `ours`, `base`, `theirs`) and exits 1 when a file cannot be read (for example it still holds conflict markers), when a member, node or pin was changed differently on both sides, when a node was deleted on one side and changed on the other, or when the merged graph would be invalid (two sources into one data input, a connection to a deleted node, duplicate member ids or names). Each conflict is listed on stderr as `<path>: conflict: <kind> at <location>`. A missing input file exits 2. `--path` names the file in the work tree and chooses the document format; without it the graph format is used.

An unreadable graph exits 1 with the reason on stderr; a missing file exits 2.

### `git-install` — Register the graph drivers with git

```
netprints git-install [--merge] [--global] [--command <cmd>] [--uninstall]
```

Makes `git diff` print the `show` summary of graphs and, with `--merge`, makes `git merge` use the `merge` driver. It sets `diff.netprints.textconv` to `<cmd> show` (with `--merge` also `merge.netprints.name` and `merge.netprints.driver` = `<cmd> merge %O %A %B --marker-size %L --path %P`) in the repository's git configuration and adds `*.netpc.json diff=netprints` (with `--merge`, `*.netpc.json diff=netprints merge=netprints`) to the `.gitattributes` at the work-tree root. Other lines and the file's line endings are kept.

| Option | Effect |
|---|---|
| `--merge` | Also register the merge driver; an existing diff-only line is upgraded in place. |
| `--global` | Write the user's git configuration and attributes file (`core.attributesFile`, else `$XDG_CONFIG_HOME/git/attributes`, else `~/.config/git/attributes`). Works outside a repository. |
| `--command <cmd>` | The command the configuration runs (default `netprints`); use `dotnet tool run netprints` for a local tool. |
| `--uninstall` | Remove exactly the configuration keys and the attributes line it added; an attributes file left empty is deleted. |

```
netprints git-install
netprints git-install --merge --command "dotnet tool run netprints"
netprints git-install --global --uninstall
```

**Output:** `installed: <what>`, `already installed: <what>` (nothing is written when everything is in place), `removed: <what>` or `not installed: nothing to remove`. Outside a git work tree (without `--global`) it exits 2. When the attributes already name another `diff=` or `merge=` driver for `*.netpc.json` it keeps that line, writes nothing, reports the line on stderr and exits 1. `git` missing or failing exits 1.

### `catalog` — Write a type catalog

```
netprints catalog [--config <file>] [--assembly <path>]... [--package <id@version>]... [--project <path>]
                  [--profile <id|file>] [--output <path>] [--format catalog|csharp] [--check] [...]
```

Catalogs the public surface of assemblies, packages or a project's references into a `*.npcat.json` file (or a C# class). With `--check` it writes nothing and exits 1 when the output is missing or differs. The full option list, the configuration file, profiles and the diagnostics are in [Type catalogs](catalogs.md).

**Output:** `wrote <path> (N types, M members)`, `up to date: <path> (...)`, or with `--check` `stale: <path>`.

## Exit codes

| Code | Name | Meaning |
|---|---|---|
| 0 | `Success` | The command completed successfully |
| 1 | `Failed` | Build or generation errors; `--check` found differences or a tool/SDK version mismatch; a project that fails to restore or load; an invalid input file (newer or missing schema version); or other operational failures |
| 2 | `Usage` | Unknown command or option, invalid option value, path argument does not exist, zero or multiple project files, a `--graph` that is not a graph of the project, missing required arguments, or the removed 0.1 flags |
| 3 | `NoSdk` | No compatible .NET SDK found (needed for `build`, `run` and commands that open the project) |
| 4 | `InternalError` | Unhandled exception (use `--verbose` to see the stack trace) |
| 130 | `Canceled` | The command was cancelled with Ctrl+C. This is the one code outside 0-4, following the shell convention (128 + SIGINT) |

`run` returns the program's exit code after a successful build.

## CI recipes

### Build and verify compilation

```bash
netprints build
```

### Verify generated code is current

```bash
netprints generate --check
```

### Format and verify graphs are canonical

```bash
netprints format --check
```

## Removed 0.1 flags

NetPrints 0.1 used `-p` and `-r` flags. They are removed, not deprecated: there is no compatibility shim, and using one is a usage error. The tool prints this to stderr and exits with code 2:

```
The -p/--project-path and -r/--run options were replaced: use 'netprints build <project>' or 'netprints run <project>'.
```

| 0.1 | Now |
|---|---|
| `netprints -p MyApp.csproj` (or `--project-path MyApp.csproj`) | `netprints build MyApp.csproj` |
| `netprints -p MyApp.csproj -r` (or `--run`) | `netprints run MyApp.csproj` |

Other exit codes changed too: `--help` and `--version` now exit 0 (they exited 2), and a project path that does not exist exits 2 (it exited 1).
