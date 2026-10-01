# Contract: `netprints` command-line tool (P2)

Implements FR-001–FR-011 (ADR-0015). Source: `src/NetPrints.Cli/`. Tests: `tests/NetPrints.Cli.Tests/`.

## 1. Global

```
netprints [--verbose] <command> [arguments] [options]
netprints --help | -h | --version
```

- `--version` prints exactly `NetPrints.Cli <AssemblyInformationalVersion>` and exits 0.
- `--help`/`-h` at any level print the help and exit 0.
- `--verbose` lowers the stderr log level from Warning to Information and prints internal-error stack traces. It
  is an option of every command (shared `CommandSettingsBase`); a `--verbose` given before the command name is moved
  after it by the pre-parse, so both positions work. With no command (`--verbose --help`, `--verbose --version`) it
  is dropped and has no effect.
- Output: plain text (no ANSI) when stdout is redirected or `NO_COLOR` is set. **Stream split:** results and
  command diagnostics (`generated:`, `stale:`, canonical diagnostic lines, summaries) go to stdout; everything else
  goes to stderr: every exit-2 and exit-3 message, project restore/load errors (exit 1), version-skew warnings and
  errors (§4 `generate`), logs, and internal-error details. `run` starts the program with stdin, stdout and stderr
  inherited, so its output streams live and is never captured or reordered.
- Ctrl+C (SIGINT) cancels the running command: nothing is printed and the exit code is 130 (`ExitCodes.Canceled`, the shell
  convention 128 + SIGINT), not 4.
- P1 flags (`-p`, `--project-path`, `-r`, `--run`) anywhere before a command → stderr
  `The -p/--project-path and -r/--run options were replaced: use 'netprints build <project>' or 'netprints run <project>'.`,
  exit 2.

## 2. Exit codes (`src/NetPrints.Cli/ExitCodes.cs`)

| Code | Name | When |
|---|---|---|
| 0 | `Success` | The command did what it was asked |
| 1 | `Failed` | Build or generation errors; `--check` found differences; a restore or project-load failure; a tool/SDK version mismatch under `generate --check`; merge conflict; an unreadable or invalid input file (graph, catalog configuration, profile), including a newer or missing `schemaVersion` |
| 2 | `Usage` | Unknown command/option, invalid option value, a path argument that does not exist, no or several project files, P1 flags, a `--graph` that is not a graph of the project, `git-install` outside a work tree |
| 3 | `NoSdk` | No compatible .NET SDK registered (commands that evaluate or build projects) |
| 4 | `InternalError` | Unhandled exception, including dependency-injection and command-construction faults |
| 130 | `Canceled` | The command was cancelled with Ctrl+C. A documented exception to the 0-4 range |

`run` returns the program's exit code after a successful build.

## 3. Project argument

`<project>` is optional for `build`, `run`, `generate` and `catalog --project` (`migrate` takes paths, see §4). Resolution: an existing `.csproj` file → it; an existing directory → its single `*.csproj`; omitted →
the current directory's single `*.csproj`. Zero or several candidates, a non-existent path, or a file that is not a
`.csproj` → exit 2 with the reason.

## 4. Commands

| Command | Synopsis | Behaviour | Output |
|---|---|---|---|
| `build` | `build [<project>]` | `EnsureSdk` → `IProjectSystem.BuildAsync` | Errors as `file(line,column): code: message`; last line `Build succeeded.` / `Build failed with N error(s).` |
| `run` | `run [<project>] [-- <args>...]` | Build; on success run `IProjectSystem.GetRunCommand` with `<args>` appended | Build output as `build`, then the program's own output, live (inherited stdio). `<args>` is everything after the first `--`, forwarded verbatim (empty values, `-` and `--=` included); Spectre never parses it |
| `generate` (alias `regen`) | `generate [<project>] [--check] [--graph <file>]...` | Load project snapshot → `GenerateRequestFactory.FromSnapshot` → `GraphCodeGenerator.LoadExtensions` → `GenerateAsync(mode)` | Canonical diagnostic lines; `generated: <relative path>` per written file, or `stale: <relative path>` per stale/missing file with `--check`; last line `N generated file(s) up to date, M written.` / `M stale.`. `--check` writes no generated file (it still loads the project, so a restore and design-time build may create `obj/`). `--graph <file>` is resolved against the current directory (not the project directory), compared case-insensitively on Windows and macOS, and must name a graph of the project (else exit 2). **Version rule (ADR-0015 amendment 1):** the project's `NetPrintsSdkVersion` (set by the `NetPrints.Sdk` package; empty for the in-repo local SDK, which is not compared) is compared with the tool's version ignoring `+build` metadata; on a difference plain `generate` prints `warning: ...` to stderr and continues, and `--check` prints `error: ...` naming both versions and how to align them to stderr and exits 1 |
| `migrate` | `migrate [<path>...]` | Paths are graph files, directories searched recursively for graphs, or a `.csproj` (its graphs); no argument → the §3 project rule. Each graph is read through its `IDocumentFormat`; while v1 is current, report only | `<relative path>: schema <n> (current)`; last line `No migrations are available; N graph(s) are at schema version 1.`; a newer version → `<path>: schema <n> is not supported (this tool supports 1)`, a missing one → `<path>: unreadable: Missing 'schemaVersion'.`, both exit 1. Directory walks skip reparse points (symlinks) and report an unreadable directory as `<dir>: unreadable: <reason>`, exit 1 |
| `catalog` | see contracts/catalog.md §4 | Build or check one catalog | `wrote <path> (<T> types, <M> members)` or `stale: <path>`; `NPC` diagnostics as `<source>: <severity> <code>: <message>` |
| `format` | `format [<paths>...] [--check]` | Canonicalize `*.netpc.json` (directories recursive; default `.`) | `formatted: <path>` / `not canonical: <path>` (check) / `unreadable: <path>: <reason>`; last line summary |
| `show` | `show <file>` | Print the graph summary (contracts/git.md §1) | Summary only |
| `merge` | `merge <base> <ours> <theirs> [--marker-size <n>] [--path <name>]` | Three-way identity merge into `<ours>` (contracts/git.md §2) | Nothing on success; on conflict `conflict: <name>: <kind> at <location>` lines on stderr |
| `git-install` | `git-install [--merge] [--global] [--uninstall] [--command <cmd>]` | contracts/git.md §3 | One line per change, or `already installed` |

Every command has at least one `.WithExample(...)`; `config.ValidateExamples()` passes in a test.

## 5. Composition

- `Program.Main(string[] args)` → `CliApplication.RunAsync(args, CliServices.CreateDefault(), cancellationToken)`.
- `CliApplication.RunAsync(IReadOnlyList<string> args, IServiceCollection services, CancellationToken)` builds the
  `CommandApp` with `TypeRegistrar(services)` and is what tests call with fakes (`IProjectSystem`,
  `IProcessRunner`, `IMsBuildRegistration`, `IAnsiConsole` = `TestConsole`, and `CliEnvironment` — current directory
  and environment variables).
- `ProjectCommandBase<TSettings>` holds the project resolution and the SDK check (`MsBuildRegistration`
  before any Microsoft.Build type loads).

## 6. Test obligations

| Id | Test (file) | Case |
|---|---|---|
| CL-T01 | `CliExitCodeTests` | `--help`, `-h`, `--version` at the top level and `<command> --help` for every registered command exit 0 (the test iterates the registered commands, so each new command is covered); `--version` line format |
| CL-T02 | `CliExitCodeTests` | Unknown command, unknown option, invalid value → 2; P1 flags → 2 with the replacement message |
| CL-T03 | `ProjectLocatorTests` | File, directory with one project, none, two, non-existent path, non-`.csproj` file |
| CL-T04 | `BuildCommandTests` (ported `CliBuildTests`) | Success 0; failure 1 with formatted errors (fake project system) |
| CL-T05 | `RunCommandTests` | Arguments after `--` reach the program; child exit code returned; build failure → 1 without running |
| CL-T06 | `BuildCommandTests` | No SDK → 3 (fake registration) |
| CL-T07 | `CliExitCodeTests` | A command that throws → 4; stack trace only with `--verbose`, given before or after the command name |
| CL-T08 | `GenerateCommandTests` | Temp copy of HelloWorld: fresh → 0 and nothing written; one stale → `generate` rewrites only it; `--check` → 1 naming it, no write; failing extension folder → 1, no write |
| CL-T09 | `MigrateCommandTests` | All v1 → 0 with report; `schemaVersion: 2` file → 1 |
| CL-T10 | `CliExitCodeTests` | Redirected output and `NO_COLOR` contain no ANSI escape |
| CL-T11 | `HelloWorldCliTests` | Real SDK: `run samples/HelloWorld` (temp copy) prints `Hello, World!`, exit 0 |
| CL-T12 | `CliExitCodeTests` | `ValidateExamples()` passes |
| CL-T13 | `GenerateRequestFactoryTests` (Core.Tests) | For HelloWorld, the factory's request equals the SDK target's `netprints.generate.rsp` parsed by `GenerateRequestFile.Parse` |
| CL-T14 | `GenerationModeTests` (Core.Tests) | `Check` never writes; `UpToDate` true/false per file |
