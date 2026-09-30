# Command-line tool

The `netprints` command-line tool is a global tool installed from the `NetPrints.Cli` package. It can build projects, run programs, regenerate graphs, and report on the state of graph files.

## Global options

```
netprints [--verbose] <command> [arguments] [options]
netprints --help | -h | --version
```

- `--version` prints the version (e.g., `NetPrints.Cli 0.2.0`) and exits.
- `--help` or `-h` prints command help and exits. Pass it at any level: `netprints --help` for the overview, or `netprints build --help` for a command.
- `--verbose` lowers the log level to Information and prints stack traces for errors. It works before or after the command name.
- Output is plain text (no ANSI colours) when redirected or when `NO_COLOR` is set.

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
netprints regen
```

Builds the project, and if the build succeeds, runs the compiled program. Arguments after `--` are forwarded to the program, and the program's exit code becomes the command's exit code.

**Example:**

```bash
netprints run
netprints run samples/HelloWorld -- arg1 arg2
```

**Output:** Build output as above, then the program's stdout/stderr.

### `generate` — Regenerate or check if graphs are up to date

```
netprints generate [<project>] [--check] [--graph <file>]... [--verbose]
netprints regen [<project>] [--check] [--graph <file>]... [--verbose]
```

Compiles each graph in the project to C#. By default, it writes new or updated files and reports each one. With `--check`, it reports stale or missing generated files without writing anything.

**Options:**

- `--check` — Report differences without writing files. Exit 1 if any generated file is stale or missing.
- `--graph <file>` — Limit the run to this graph file of the project (repeatable). The path is relative to the project directory.

**Examples:**

```bash
netprints generate
netprints generate samples/HelloWorld --check
netprints regen --graph HelloWorld.Program.netpc.json
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
netprints migrate graphs/Extra.netpc.json
```

**Output:** `<path>: schema 1 (current)` per graph, then `No migrations are available; N graph(s) are at schema version 1.`

## Exit codes

| Code | Name | Meaning |
|---|---|---|
| 0 | `Success` | The command completed successfully |
| 1 | `Failed` | Build or generation errors; `--check` found differences; an invalid input file (newer schema version); or other operational failures |
| 2 | `Usage` | Unknown command or option, invalid option value, path argument does not exist, zero or multiple project files, or missing required arguments |
| 3 | `NoSdk` | No compatible .NET SDK found (needed for `build`, `run` and commands that open the project) |
| 4 | `InternalError` | Unhandled exception (use `--verbose` to see the stack trace) |

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

This command is not yet available; see the [release notes](https://github.com/danielmeza/netprints/releases) for what is coming.

## Deprecated P1 flags

NetPrints 0.1 used `-p` and `-r` flags:

```bash
# No longer supported
netprints -p MyApp.csproj -r
netprints --project-path MyApp.csproj --run
```

Use the new command structure instead:

```bash
# Use these instead
netprints build MyApp.csproj
netprints run MyApp.csproj
```

The tool will error if you use the old flags.
