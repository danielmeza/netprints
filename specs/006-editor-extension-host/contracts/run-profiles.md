# Contract: run profiles, configuration and console

FR-051–FR-065, ADR-0037, ADR-0015; research R22, R23. Data shapes in [data-model.md](../data-model.md) §4.

## 1. Reading `Properties/launchSettings.json`

- The file is read from the project folder's `Properties/` only; comments and trailing commas are allowed.
- Profiles keep file order. `commandName` is case-sensitive; profile names are compared case-insensitively.
- Kinds: `Project` and `Executable` run; every other `commandName` (`IISExpress`, `Docker`, `WSL2`, …) is listed disabled
  with the reason "NetPrints cannot run `<kind>` profiles" and is never handed to `dotnet run`.
- The default profile is the first in file order whose kind can run the project. With no file, the project runs as
  today (`Project` semantics, no profile), and the button is shown only for executable projects.

## 2. Resolution (one resolver for the editor and the CLI)

Order: choose the profile → expand → split → resolve paths → build the request. Every problem stops before the build
when it blocks.

| Step | Rule |
|---|---|
| Expand `%NAME%` | from the process environment |
| Expand `$(Name)` | from captured project properties: `ProjectDir`, `TargetDir`, `TargetPath`, `OutDir`, `Configuration`, `TargetFramework`, and names added with `AddProjectProperty` |
| Split | `CommandLineToArgvW` rules on every OS (backslashes before quotes, doubled quotes, empty arguments) |
| Working directory | relative → from the project folder; absent → the project folder |
| Executable | contains a separator → relative to the working directory; bare name → `PATH` (Windows adds `.exe`, `.com`); macOS `.app` folder → `Contents/MacOS/<CFBundleExecutable>` |
| `Project` kind | `dotnet run --project <csproj> --no-build --no-launch-profile -c <configuration> -e NAME=VALUE… -- <args>` |
| `Executable` kind | the executable with the argument list, after the build, for any output type |
| Environment | the profile's variables and `DOTNET_LAUNCH_PROFILE=<name>` reach the program only; the MSBuild variables NetPrints' locator set (and that were not set before NetPrints started) are removed |

### Problems

| Code | Case | Severity | Effect |
|---|---|---|---|
| NPR001 | `launchSettings.json` is not valid JSON | error | Run disabled for profiles; `netprints run` exits 1 naming the file |
| NPR002 | two profile names differ only by case | error | as NPR001 |
| NPR003 | the selected profile's kind cannot run | error | listed disabled; `netprints run -lp` exits 2 |
| NPR004 | unknown profile name (`-lp`) | error | `netprints run` exits 2 |
| NPR005 | `%NAME%` unset in `executablePath` or `workingDirectory` | error | stops before the build; "the variable is not set" |
| NPR006 | `$(Name)` not captured | error (path) / warning (arguments, environment) | stops / stays literal |
| NPR007 | `%NAME%` unset in arguments or environment | warning | stays literal |
| NPR008 | executable not found | error | stops before the build |
| NPR009 | a `.cmd` or `.bat` target on Windows | error | refused; the message suggests making the interpreter the executable |
| NPR010 | a macOS `.app` with no resolvable binary | error | stops before the build |
| NPR011 | the `Project` kind on a project that is not an executable | error | listed disabled with the reason |

## 3. Editor surfaces

- **Command bar**: a run profile split button next to Run and Stop when the project has profiles or is an executable.
  The drop-down lists the profiles in file order (disabled ones with their reason), a check on the selected one, and
  "Edit run profiles…". Commands: `netprints.command.selectRunProfile`, `netprints.command.editRunProfiles`.
- **Configuration selector**: Debug or Release, next to the profile button; build and run use it.
- **Run enabled**: when the selected profile's kind can run the project (FR-061), not only for `OutputType` Exe.
- **First run**: the first run of each `Executable` profile in a session prints the resolved command line in Output
  (program, quoted arguments, working directory, added and removed environment names). No trust prompt.
- **Stop**: asks the program to close (the main window on Windows, SIGTERM elsewhere), then kills the process tree after
  `run.stopTimeoutSeconds`.
- **Unload**: with a live `Executable` run, asks "Leave it running?" (Leave, Stop, Cancel).
- **Output input**: with captured output, an input line sends a line to standard input; "Close input" sends end of
  file. No input line when output is not captured or the run is in an external terminal.
- **Per-profile options** (per user and project): "Run in external terminal", "Don't capture output" (a GUI program such
  as `UnrealEditor.exe`: Output shows only start and exit).
- **Project settings › Run**: the profile list; per profile its kind, executable path, arguments with an argument
  preview (the split list), working directory and environment variables; other kinds read-only with "Open file"; a
  warning that `dotnet run` resolves a relative `workingDirectory` from `Properties/`. Edits keep unknown members and
  property order; a file with comments is read-only with "Open file".

## 4. CLI: `netprints run`

```text
netprints run [<project>] [-c|--configuration <name>] [-lp|--launch-profile <name>] [--no-launch-profile] [-- <args>]
```

- No option: the default profile (§1). `--no-launch-profile`: today's behaviour.
- `-- <args>` replace the profile's arguments; its environment and working directory still apply (the project's
  `RunArguments`, if any, still apply).
- Exit codes (ADR-0015): the program's exit code after a successful run; 1 for NPR001, NPR002, NPR005, NPR006 (path),
  NPR008–NPR010 and build failures; 2 for NPR003, NPR004 and usage errors; 3 no SDK; 130 cancelled.
- `--help` names the profile rules and the `workingDirectory` difference from `dotnet run`.

## 5. Persisted state

- Per user and per project in `sessions/<project-key>.json`: the selected profile, the configuration, and per profile
  "external terminal" and "capture output" ([data-model.md](../data-model.md) §4). Never in `launchSettings.json`.

## 6. Tests that pin this contract

- The splitter and joiner round trip: at least 30 cases (quotes, backslashes, spaces, empty arguments, trailing
  backslashes, Unicode) (SC-008).
- The resolver table: one fixture per row of §2 and per NPR code, editor and CLI producing the identical request
  (program, argument list, working directory, environment) (SC-008).
- Default profile choice, case rules, disabled kinds, `.run.json` ignored.
- Environment scrubbing: a variable set by the locator is removed; the same variable set by the user before start is
  kept.
- Stop: a test program that ignores SIGTERM is killed after the timeout with its child.
- Headless UI: the split button, the configuration selector, Run enablement for a library with an `Executable`
  profile, the first-run command line in Output, the input line.
- Desktop E2E: `RunExecutableProfile` (a fixture `Executable` profile starting a test program that echoes its argv,
  cwd and one environment variable, and reads a line from stdin).
- The Godot template's profiles resolve with `GODOT4` set to a stub and fail with NPR005 when it is unset (no Godot
  installation).
