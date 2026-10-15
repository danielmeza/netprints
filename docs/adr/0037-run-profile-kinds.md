# 0037: Run profile kinds

## Status

Accepted (owner, 2026-10-14, after a research on launch profiles and engines and its adversarial audit; lands in the P3
run-profiles batch). It takes the first free number after 0036. Where the owner delegated a choice the choice is recorded
as a coordinator default.

## Context

- Run is enabled only for `OutputType=Exe` (`Project.CanCompileAndRun`, `RunCommandHandler`), so it is disabled today for
  Godot and UnrealSharp projects, which are libraries, whatever their launch profile says.
- `Properties/launchSettings.json` is shared with `dotnet run`, Visual Studio and Rider. `Executable` is its standard kind
  for "build this project, then start another program". Godot's C# docs and the profiles UnrealSharp generates use it.
- `dotnet run` runs an `Executable` profile only from SDK 10.0.200; 10.0.1xx skips it. `GetRunCommand` passes no
  `--no-launch-profile`, so what F5 does depends on the user's SDK band. The CLI has no `OutputType` gate and already
  starts UnrealEditor from a library.
- An unsupported kind selected by name makes `dotnet run` print an error and then run the project without the profile.
- Relative paths differ between consumers: `dotnet run` resolves a relative `workingDirectory` from the folder holding
  `launchSettings.json` (`Properties/`), Visual Studio from the project folder.
- The research proposed a public `IRunProfileKind` extension point and a trust prompt. The audit found both premature or
  misfit: the cases cited for the point (U3's open editor, Unity) start no process, and the trust model has no place for a
  per-command prompt.

## Decision

1. **`Project` and `Executable` are supported natively.** NetPrints reads `Properties/launchSettings.json` and applies the
   profile itself, with an explicit argv and working directory on the process request, so the result does not depend on
   the SDK band. The editor and the CLI behave the same. `Project` runs
   `dotnet run --no-build --no-launch-profile -- <args>`; `Executable` starts `executablePath` after the build, on any
   output type.
2. **Unknown kinds are disabled, not delegated.** `IISExpress`, `Docker`, `WSL2` and the rest are listed with the reason,
   skipped when choosing the default profile, and never handed to `dotnet run`. In the CLI a selected unknown kind exits 2.
3. **Run is gated by the selected profile's kind**, not by `OutputType=Exe`. The `Project` kind needs an executable
   output; `Executable` does not.
4. **Kinds are an internal table in P3.** A public experimental `AddRunProfileKind`, under a new NPXE id, is added only when
   U3 or NetPrintsUnity needs it, and is shaped then: build or no build, an outcome handled without a process, and a
   process lifetime hint.
5. **No new trust prompt.** Building already trusts the project. The first run of an `Executable` profile prints the
   resolved command line in Output, and the Run section shows an argv preview.
6. **Paths follow Visual Studio.** A `workingDirectory` is relative to the project folder and an absent one is the project
   folder (the `dotnet run` default). An `executablePath` with a separator is relative to the working directory, and a bare
   name is looked up on `PATH`. The Run section and `netprints run --help` warn that `dotnet run` resolves a relative
   `workingDirectory` from `Properties/`.
7. **Variables.** `%NAME%` is expanded from the environment and `$(Name)` from captured project properties, then the string
   is split. An unset `%NAME%` in `executablePath` or `workingDirectory`, an uncaptured property or a missing executable
   blocks the run before anything starts; in arguments and env it is a warning and the text stays literal.
8. **Environment.** The profile's environment and `DOTNET_LAUNCH_PROFILE` reach the program only: `dotnet run -e NAME=VALUE`
   for `Project` (SDK 9.0.200 or later), the process request for `Executable`. Passing them on the `dotnet run` process
   would turn them into MSBuild properties.
9. **Stop and unload.** Stop asks the program to close (the main window, or SIGTERM) and kills the process tree after a
   timeout. Unloading a project whose `Executable` run is still alive asks "leave it running?".
10. **Executable resolution.** NetPrints passes the OS an absolute path: on Windows `PATH` is searched with `.exe` and
    `.com`; a macOS `.app` folder resolves to `Contents/MacOS/<binary>` or is reported. A `.cmd` or `.bat` target is
    rejected or warned about, because it runs through `cmd.exe`. Arguments are split with the Windows
    `CommandLineToArgvW` rules on every OS and passed through `ArgumentList`; no shell is involved.
11. **Desktop launchers** may not inherit shell variables. The message says the variable is not set; a per-user run
    environment setting is a later follow-up.
12. **Godot needs no core code.** Its profile is an `Executable` one that starts `%GODOT4%`; the sample extension ships it
    as template data and registers no kind. U3 reuses the `Executable` profiles UnrealSharp generates.

## Alternatives rejected

- **A public `IRunProfileKind` in P3** (the research). Nothing in P3 needs it; the cases that would are not "build, then
  start a process", and a shape fixed now is likely wrong. ADR-0013's precedent applies: data that cannot express a rule is
  pressure for a profile feature, not for a plug-in.
- **A trust prompt keyed to `TrustedProjects`.** The list means "project extensions may load", so trusting a command would
  load extensions on the next open, and a changed `executablePath` after a pull would not prompt again.
- **Delegating unknown kinds to `dotnet run`.** It runs the project without the profile.
- **A tool locator or a project-kind detector in the core.** `%NAME%` plus `PATH` covers the first; project templates
  (ADR-0020) cover the second.
- **`dotnet run` relative-path rules.** Godot's own instructions (working directory `.`) assume Visual Studio's.

## Consequences

- About 7 days for the run-profiles batch, 2 of them for `Executable`, the kind table and the Run gate.
- Run profiles need the P3a command bar and the Run section; the CLI gets `-lp|--launch-profile` and `--no-launch-profile`.
- Cases to cover with tests: argument join and split round-trip, expansion before splitting, case-sensitive `commandName`
  and profile names that differ only by case, `.app` resolution, and the Windows `PATH` lookup.
- Known limits, documented rather than fixed:
  - the `MSBUILD_EXE_PATH`, `MSBuildSDKsPath` and `MSBuildExtensionsPath` values MSBuildLocator sets in NetPrints' process
    reach engines that run their own `dotnet build`, and a process request cannot unset a variable;
  - the Godot editor loads `.godot/mono/temp/bin/Debug`, so with a Release build selected it runs a stale assembly;
  - upstream UnrealSharp writes empty `executablePath` values on Linux (the fork's `feature/linux-support` fills them), and
    its `Debug` profile starts DebugGame with no debugger;
  - a GUI-subsystem program such as `UnrealEditor.exe` has no stdout to capture.
- Writing `launchSettings.json` back must keep unknown keys, kinds and their order, and warn about comments.
- Open: the `AddRunProfileKind` shape, decided with U3 or NetPrintsUnity.
