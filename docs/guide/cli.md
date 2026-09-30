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

This command is not yet available; see the [release notes](https://github.com/danielmeza/netprints/releases) for what is coming.

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
