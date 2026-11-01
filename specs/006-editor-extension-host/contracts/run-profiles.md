# Contract: run profiles, configuration and console

FR-051–FR-065, ADR-0037, ADR-0015; research R22, R23. Data shapes in [data-model.md](../data-model.md) §4.

## 1. Reading `Properties/launchSettings.json`

- The file is read from the project folder's `Properties/` only; comments and trailing commas are allowed.
- Profiles keep file order. `commandName` is case-sensitive; profile names are compared case-insensitively.
- Kinds: `Project` and `Executable` run; every other `commandName` (`IISExpress`, `Docker`, `WSL2`, …) is listed disabled
  with the reason "NetPrints cannot run `<kind>` profiles" and is never handed to `dotnet run`.
- The default profile is the first in file order whose kind can run the project: `Project` needs an executable output
  type (NPR011), `Executable` runs on any output type. The reader parses only (NPR001, NPR002); whether a profile can
  run is decided with the project's output type (§7.1). With no file, with no profile that can run, or with
  `--no-launch-profile`, the project runs with `Project` semantics and no profile (§2, "No profile"), which only an
  executable project can do.
- The run profile button shows when `Properties/launchSettings.json` exists, in any state (usable, NPR001, NPR002, or
  no profile that can run), or when the project is an executable. It is hidden only for a project that is not an
  executable and has no file.
- A profile with a missing or non-string `commandName`, a non-string `executablePath`, `commandLineArgs` or
  `workingDirectory`, or an `environmentVariables` that is not an object of strings, makes the file NPR001. Unknown
  members are ignored. A file with no `profiles` member has no profiles and no problem.

## 2. Resolution (one resolver for the editor and the CLI)

Order: choose the profile → expand → split → resolve paths → build the request. Every problem stops before the build
when it blocks.

| Step | Rule |
|---|---|
| Expand `%NAME%` | from the process environment |
| Expand `$(Name)` | from the project's properties evaluated in process in the selected configuration (first target framework when it multi-targets); `ProjectDir`, `TargetDir`, `TargetPath`, `OutDir`, `Configuration` and `TargetFramework` are always defined; a name the project does not define is NPR006, and a defined empty property expands to the empty string. The project is evaluated at most once per run: the editor evaluates the chosen profile's names, and nothing when it has no `$(`; `netprints run` always evaluates once, `OutputType` with every name any profile of the file uses (owner question OWNER-1 in implementation-notes) |
| Expansion | one left-to-right pass over `%NAME%` (`NAME` = one or more characters other than `%` and whitespace) and `$(Name)` (`Name` = a letter or `_`, then letters, digits, `_`, `-`); expanded text is not expanded again; a lone `%` stays; environment names compare case-insensitively on Windows, case-sensitively elsewhere; property names case-insensitively |
| Split | `CommandLineToArgvW` rules on every OS (backslashes before quotes, doubled quotes, empty arguments) |
| Working directory | relative → from the project folder; absent → the project folder |
| Executable | after expansion: empty is NPR008 ("executablePath is empty"); `/` (or `\` on Windows) in it means a path relative to the working directory; otherwise each rooted `PATH` entry in order (empty and relative entries skipped; a hit is made absolute with `Path.GetFullPath`), on Windows the name as written when it has an extension, then `<name>.exe`, then `<name>.com`; a macOS `.app` folder resolves through `Contents/Info.plist` (XML) `CFBundleExecutable` to `Contents/MacOS/<value>`, and a missing or binary plist is NPR010; on Windows a `.cmd` or `.bat` result is NPR009; a result that is not an existing file is NPR008 |
| `Project` kind | program `dotnet`; arguments `run --project <csproj> --no-build --no-launch-profile -c <configuration>`, then `-e NAME=VALUE` per profile variable in file order and `-e DOTNET_LAUNCH_PROFILE=<name>` last, then `--` and the arguments when there are any |
| No profile | as the `Project` kind with no `-e` and no `DOTNET_LAUNCH_PROFILE`; working directory the project folder |
| `Executable` kind | the executable with the argument list, after the build, for any output type |
| Environment | `Executable`: the profile's variables, then `DOTNET_LAUNCH_PROFILE=<name>` (it overrides a profile value), go in the request's environment. `Project`: they go as `-e` arguments, and only the scrub touches the `dotnet` process environment. Both kinds: each variable NetPrints' MSBuild registration added is removed (`EnvironmentRemovals`), and each one it overwrote is set back to the value it had before NetPrints started. Exception for `Executable`: a name the profile sets (or `DOTNET_LAUNCH_PROFILE`) is neither removed nor set back; the profile's value wins. For `Project` the scrub applies to `dotnet` in full and the profile's value reaches the program through `-e`. Names compare case-insensitively on Windows, case-sensitively elsewhere |

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
| NPR011 | the `Project` kind on a project that is not an executable | error | listed disabled with the reason; `netprints run --launch-profile` exits 2 (decided by `LaunchProfile.AvailabilityFor`, not the reader) |

## 3. Editor surfaces

- **Command bar**: a run profile split button next to Run and Stop, shown by the rule of §1. Its label is the selected
  profile's name, or "No profile" when none applies. The selected profile is the remembered one when the file still
  has it (Run is then disabled with its reason when it cannot run), else the default profile.
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
- **Problems in the editor**: a blocked run appends each problem's line (`error NPR005: …`) to Output and shows the
  first error in the status bar ("Run stopped: …"); nothing is built. Warnings of a run that goes on are written to
  Output right after the build and stay when the program starts. NPR001 and NPR002 also show on the run profile
  button (error icon, message as tooltip) and disable Run.
- **When profiles are read**: at load, before each run, after each edit in the Run section, and when the drop-down
  opens. No file watcher.
- **Leave**: the program keeps running and its output is no longer shown; a later run replaces it in Output. Nothing
  NetPrints does afterwards kills it. A `Project`-kind run is still stopped when the project unloads, without asking.
- **Exit**: the application's exit asks to stop a running program (as today) and kills it at once when confirmed.
- **Stop details**: on Windows a program with no main window is killed at once; on Linux and macOS, children that
  outlive a program that closed on SIGTERM are not reached by the tree kill.
- **External terminal** (ADR-0042): the program starts in a new terminal window with no redirection; Output shows
  "Started in an external terminal." at once. Linux and macOS hand the run off: NetPrints does not follow the terminal
  process, the run ends for NetPrints at once (Run is enabled again, no exit code is shown), Stop, the unload prompt and
  the exit prompt do not apply, and the window waits for Enter after the program exits. Linux: `run.linuxTerminal` is
  split with the §2 splitter and `/bin/sh` and the script path are appended as two arguments; when the split is empty,
  the first of `xdg-terminal-exec --`, `x-terminal-emulator -e`, `ptyxis --`, `gnome-terminal --`, `kgx --`,
  `konsole -e`, `xfce4-terminal -x`, `xterm -e` found on `PATH` is used. None found is the message "No terminal was
  found on PATH (looked for xdg-terminal-exec, x-terminal-emulator, ptyxis, gnome-terminal, kgx, konsole,
  xfce4-terminal, xterm); set run.linuxTerminal in Settings › Run." and nothing starts; a configured program that
  cannot start is "The terminal command '<program>' could not be started: <reason>. Check run.linuxTerminal in
  Settings › Run.". macOS: `open -a Terminal <script>.command`, the script in the per-user temp folder; the program
  gets the environment of Terminal's login shell plus the profile's changes. Windows: the program itself in a console
  window of the default terminal application (Windows Terminal on Windows 11 22H2+, else conhost), which closes when
  the program exits; NetPrints follows it, so Stop, the unload prompt and its exit code apply.

## 4. CLI: `netprints run`

```text
netprints run [<project>] [-c|--configuration <name>] [-lp|--launch-profile <name>] [--no-launch-profile] [-- <args>]
```

- No option: the default profile (§1). `--no-launch-profile`: no profile (§2, "No profile"). `--launch-profile` with
  `--no-launch-profile` is a usage error (2). `-c|--configuration <name>` (default `Debug`) is passed to the build and
  to `dotnet run`; `netprints build` takes the same option.
- `--launch-profile <name>` has no short form. `dotnet run` spells it `-lp`, but Spectre.Console.Cli 0.57.2 refuses a
  short option name of more than one character (`Short option names can not be longer than one character.`, checked
  with a throwaway console app: the template `-lp|--launch-profile <name>` fails at startup, `--launch-profile <name>`
  alone parses), and a one-character short would match nothing in `dotnet run`. This is a deliberate difference from
  `dotnet run`; `-lp` is read as the short options `-l` and `-p` and is rejected by the parser. The other CLI commands
  also use long names only for their own options.
- Problems are written to stderr, one `ToLine()` per line, before the build; warnings do not change the exit code.
- `-- <args>` replace the profile's arguments; its environment and working directory still apply (the project's
  `RunArguments`, if any, still apply).
- Exit codes (ADR-0015): the program's exit code after a successful run; 1 for NPR001, NPR002, NPR005, NPR006 (path),
  NPR008–NPR010 and build failures; 2 for NPR003, NPR004, NPR011 (a profile chosen with `--launch-profile`) and usage errors;
  3 no SDK; 130 cancelled.
- `--help` names the profile rules and the `workingDirectory` difference from `dotnet run`.

## 5. Persisted state

- Per user and per project in `sessions/<project-key>.json`, member `run` (C# `RunSessionState`; not the `run` section
  of `settings.json`, which is `RunSettings`): the selected profile, the configuration, and per profile "external
  terminal" and "capture output" ([data-model.md](../data-model.md) §4). Never in `launchSettings.json`. Written when
  the user changes one of them; the session save that follows keeps it.

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
  installation), over the byte copy in `tests/Fixtures/RunProfiles/GodotCopy/`; the Godot job pins the copy to the
  template.
- The shared cases (`tests/NetPrints.Run.Testing/RunProfileCases.cs`) feed the resolver, `netprints run` and the
  editor's run path, each with the case's platform, so every case runs on every OS; each compares with the case's
  expected request through `RunRequestAssert.Same`.

## 7. Types and names

`NetPrints.Workspace` is not packed, so its run types are public without a PublicAPI entry. "Internal kinds table"
(FR-051, ADR-0037 decision 4) means "not an extension point". Workspace types live in `src/NetPrints.Workspace/Run/`,
namespace `NetPrints.Workspace.Run`, one type per file named after it.

### 7.1 Codes, problems, launch settings (T113)

`NprDiagnosticCodes` (`NprDiagnosticCodes.cs`), `public static class`, one `public const string` per code:

| Constant | Code | Severity | Message (`{…}` are values) |
|---|---|---|---|
| `LaunchSettingsInvalid` | NPR001 | error | `'{file}' is not valid launch settings: {detail}.` (`{detail}`: the `JsonException` message, `the root is not an object`, `'profiles' is not an object`, `profile '{name}' is not an object`, `profile '{name}' has no string commandName`, `'{member}' of profile '{name}' is not a string`, `'environmentVariables' of profile '{name}' is not an object of strings`, or the IO error message) |
| `ProfileNamesDifferByCase` | NPR002 | error | `The profile names '{first}' and '{second}' in '{file}' differ only by case.` |
| `KindNotRunnable` | NPR003 | error | `NetPrints cannot run {commandName} profiles.` |
| `UnknownProfile` | NPR004 | error | `No launch profile is named '{name}'.` |
| `PathVariableUnset` | NPR005 | error | `The variable {NAME} is not set ({field} of profile '{profile}').` (`{field}`: `executablePath` or `workingDirectory`) |
| `PropertyUndefined` | NPR006 | error in a path, warning elsewhere | `The project does not define the property {Name} ({field} of profile '{profile}').` |
| `ArgumentVariableUnset` | NPR007 | warning | `The variable {NAME} is not set ({field} of profile '{profile}'); the text stays as written.` (`{field}`: `commandLineArgs` or `environmentVariables.{KEY}`) |
| `ExecutableNotFound` | NPR008 | error | `The executable '{path}' was not found (profile '{profile}').` / `The executablePath of profile '{profile}' is empty.` |
| `ScriptTargetRefused` | NPR009 | error | `'{path}' runs through cmd.exe, which parses the arguments again; set the interpreter as the executable path and the script as its first argument (profile '{profile}').` |
| `AppBundleUnresolved` | NPR010 | error | `'{path}' is an app bundle with no executable named in Contents/Info.plist (profile '{profile}').` |
| `ProjectKindNeedsExecutable` | NPR011 | error | `The Project kind runs only an executable project; this project's output type is {outputType}.` |

```csharp
public enum RunProblemSeverity { Warning, Error }

public sealed record RunProblem(string Code, RunProblemSeverity Severity, string Message)
{
    /// "error NPR005: The variable GODOT4 is not set (executablePath of profile 'Game')."
    public string ToLine();
}

public static class RunProfileKinds
{
    public const string Project = "Project";
    public const string Executable = "Executable";
    public static bool IsRunnable(string commandName); // ordinal: "Project" or "Executable"
}

public sealed record RunProfileAvailability(bool CanRun, RunProblem? Reason)
{
    public static RunProfileAvailability Runnable { get; }
}

public sealed record LaunchProfile
{
    public required string Name { get; init; }
    public required string CommandName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CommandLineArgs { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<KeyValuePair<string, string>> EnvironmentVariables { get; init; } = []; // file order
    public RunProfileAvailability AvailabilityFor(BinaryType outputType); // NPR003, then NPR011
}

public sealed record LaunchSettings
{
    public const string RelativePath = "Properties/launchSettings.json";
    public static LaunchSettings None { get; }            // no file
    public string? FilePath { get; init; }                // full path; null for None
    public IReadOnlyList<LaunchProfile> Profiles { get; init; } = []; // file order; kept with NPR002
    public IReadOnlyList<RunProblem> Problems { get; init; } = [];    // NPR001, NPR002 only
    public bool HasComments { get; init; }                // T121 read-only rule
    public bool IsUsable { get; }                         // Problems.Count == 0
    public LaunchProfile? Find(string name);              // OrdinalIgnoreCase; null when none
    public LaunchProfile? DefaultProfile(BinaryType outputType); // null when !IsUsable or none can run
}

public static class LaunchSettingsReader
{
    public static LaunchSettings Read(string projectDirectory); // None when the file is absent; NPR001 on IOException or UnauthorizedAccessException
    public static LaunchSettings Parse(string json, string filePath); // JsonDocument with CommentHandling.Skip and AllowTrailingCommas; HasComments from a Utf8JsonReader pass with CommentHandling.Allow
}
```

### 7.2 The splitter (T114)

`CommandLineSplitter` (`CommandLineSplitter.cs`), `public static class`:
- `IReadOnlyList<string> Split(string commandLine)`: the argument rules of `CommandLineToArgvW` as .NET's
  `Process.ParseArgumentsIntoList` applies them. Space and tab outside quotes end an argument, and runs of them
  collapse. A run of n backslashes followed by `"` gives n/2 backslashes; when n is odd the `"` is a literal, and when
  n is even it is handled as a quote. A run of n backslashes not followed by `"` gives n backslashes. A `"` inside
  quotes followed by another `"` gives one literal `"`, and the parser stays inside quotes. Any other `"` toggles
  quoting. A quoted empty string (`""`) is an empty argument.
- `string Join(IReadOnlyList<string> arguments)`: one space between arguments. An argument that is not empty and has
  no space, tab or `"` is written as is. Any other is wrapped in quotes: each run of n backslashes before a `"`
  becomes 2n+1 backslashes and the `"`, a run of n backslashes at the end becomes 2n, and other backslashes stay.
  `Split(Join(x))` equals `x` for every list.

### 7.3 Configuration and project properties (T115, T118, T122)

```csharp
public static class RunConfigurations
{
    public const string Debug = "Debug";
    public const string Release = "Release";
    public static IReadOnlyList<string> All { get; } // [Debug, Release]
}

public interface IRunProjectSystem
{
    /// dotnet build as IProjectSystem.BuildAsync does, plus "-c <configuration>".
    Task<BuildResult> BuildAsync(string projectFilePath, string configuration, CancellationToken cancellationToken);

    /// In-process evaluation with the global property Configuration (and the first framework when the project
    /// multi-targets); returns the names the project defines, case-insensitive keys; an undefined name is absent.
    Task<IReadOnlyDictionary<string, string>> EvaluatePropertiesAsync(string projectFilePath, string configuration,
        IReadOnlyList<string> names, CancellationToken cancellationToken);
}
```
`MsBuildProjectSystem` and the editor's `NoSdkProjectSystem` implement it. `MsBuildProjectSystem.EvaluatePropertiesAsync`
calls the private `Evaluate`, which gains a `IDictionary<string, string>? globalProperties` parameter (merged with the
`TargetFramework` it adds when it retargets); a name is defined when `MSBuildProject.GetProperty(name)` is not null,
and its value is that property's `EvaluatedValue`. `MsBuildProjectSystem.BuildAsync(path, configuration, ct)` runs
today's argument list with `-c <configuration>` inserted after the project path. `NoSdkProjectSystem` throws the same
`ProjectSystemException` (`NoSdkRegistered`) its other members throw.

When `RunRequest.OutputType` is null (the CLI), the planner adds `OutputType` to the names it evaluates and maps the
value with `OutputTypes.FromProperty(string)` (`src/NetPrints.Workspace/OutputTypes.cs`, `internal static class`):
`Exe`, compared `OrdinalIgnoreCase`, → `BinaryType.Executable`; anything else, `WinExe` included, →
`BinaryType.SharedLibrary`. `MsBuildProjectSystem.LoadAsync` uses the same method for the snapshot's `OutputType`, so
the editor and the CLI cannot disagree.

### 7.3a Resolution and planning (T115)

```csharp
/// Platform rules, a smart enum with private nested sealed members (WindowsPlatform, MacOSPlatform, LinuxPlatform),
/// as EditorPlatform is written; tests pass a member explicitly, production passes Current.
public abstract class RunPlatform
{
    public static RunPlatform Windows { get; } = new WindowsPlatform();
    public static RunPlatform MacOS { get; } = new MacOSPlatform();
    public static RunPlatform Linux { get; } = new LinuxPlatform();
    public static RunPlatform Current { get; }                   // OperatingSystem.IsWindows() ? Windows : IsMacOS() ? MacOS : Linux
    public abstract string Name { get; }                         // "Windows", "MacOS", "Linux"
    public abstract char PathListSeparator { get; }              // Windows ';', others ':'
    public abstract StringComparer VariableNameComparer { get; } // Windows OrdinalIgnoreCase, others Ordinal
    public abstract bool HasDirectorySeparator(string path);     // Windows: contains '\\' or '/'; others: contains '/'
    public abstract IReadOnlyList<string> CandidateNames(string bareName); // Windows: the name as written when Path.HasExtension(name), then name + ".exe", then name + ".com"; others: [name]
    public virtual bool IsBundle(string path) => false;          // MacOS: ends with ".app" (OrdinalIgnoreCase) and Directory.Exists(path)
    public virtual string? BundleExecutable(string bundlePath) => null; // MacOS: Contents/Info.plist read as XML, the <string> after <key>CFBundleExecutable</key>, returned as Path.Combine(bundlePath, "Contents", "MacOS", value); null when the plist is missing, not XML (a binary plist) or has no such key
    public virtual bool RefusesTarget(string path) => false;     // Windows: extension ".cmd" or ".bat", OrdinalIgnoreCase (NPR009)
}

public sealed record RunInputs
{
    public required string ProjectFilePath { get; init; }
    public required BinaryType OutputType { get; init; }
    public required string Configuration { get; init; }
    public required LaunchSettings LaunchSettings { get; init; }
    public string? ProfileName { get; init; }               // null: the default profile
    public bool NoLaunchProfile { get; init; }
    public IReadOnlyList<string>? ArgumentOverride { get; init; } // CLI "-- <args>"; replaces the profile's arguments
    public IReadOnlyDictionary<string, string> ProjectProperties { get; init; } = ImmutableDictionary<string, string>.Empty; // case-insensitive keys
    public required IReadOnlyDictionary<string, string> Environment { get; init; } // snapshot of the process environment
    public IReadOnlyList<MsBuildVariable> MsBuildVariables { get; init; } = [];
}

public sealed record RunResolution
{
    public LaunchProfile? Profile { get; init; }             // null: no profile
    public ProcessStartRequest? Request { get; init; }       // null when an error problem blocks
    public IReadOnlyList<RunProblem> Problems { get; init; } = []; // in the order found
    public IReadOnlyList<string> AddedEnvironment { get; init; } = []; // names added for the program, for the first-run print
    public bool IsBlocked { get; }                           // Request is null
}

public sealed record RunProfileChoice(LaunchProfile? Profile, IReadOnlyList<RunProblem> Problems)
{
    public bool IsBlocked { get; }                           // Problems holds an error
}

public static class RunProfileResolver
{
    public static RunResolution Resolve(RunInputs inputs, RunPlatform platform); // Choose → expand → split → paths → request
    public static RunProfileChoice Choose(LaunchSettings settings, BinaryType outputType, string? profileName, bool noLaunchProfile); // the choosing rules below
    public static IReadOnlyList<string> PropertyNames(LaunchProfile profile);   // distinct $(Name)s in its fields (executablePath, commandLineArgs, workingDirectory, then environment values in file order), OrdinalIgnoreCase, first spelling kept
}

public sealed record RunRequest(string ProjectFilePath, string Configuration)
{
    public BinaryType? OutputType { get; init; }             // null: evaluated (the CLI)
    public string? ProfileName { get; init; }
    public bool NoLaunchProfile { get; init; }
    public IReadOnlyList<string>? ArgumentOverride { get; init; }
}

public sealed class RunPlanner(
    IRunProjectSystem projects,
    RunPlatform platform,
    Func<IReadOnlyDictionary<string, string>> environment,
    Func<IReadOnlyList<MsBuildVariable>> msBuildVariables)
{
    public static RunPlanner Create(IRunProjectSystem projects); // Current, the process environment, MsBuildRegistration.Variables
    public LaunchSettings ReadLaunchSettings(string projectFilePath); // LaunchSettingsReader.Read(project folder); LaunchSettings.None when Path.GetDirectoryName gives null or empty (a project with no path yet)
    public Task<RunResolution> PlanAsync(RunRequest request, CancellationToken cancellationToken); // reads the file, evaluates at most once, resolves; logs 4006
}
```
Choosing the profile (`Choose`, also step 1 of `Resolve`): NPR001 or NPR002 block with those problems. `NoLaunchProfile`
means no profile. A `ProfileName` that `Find` misses is NPR004. A named profile that cannot run blocks with its
availability reason (NPR003, NPR011). With no name, the result is `DefaultProfile(OutputType)`, or no profile when it
returns null. A blocked choice holds only these problems, so no problem list mixes them with the later steps' codes.

`PlanAsync`: (1) read the file. (2) With `OutputType` set: `Choose`; when blocked, return its problems with no
evaluation; else evaluate `PropertyNames(profile)` once when the list is not empty. With `OutputType` null: evaluate
once, `OutputType` plus every profile's `PropertyNames` (distinct, file order), map `OutputType` with
`OutputTypes.FromProperty`. (3) `Resolve` with the evaluated properties. Tests see which names were evaluated through
`FakeRunProjectSystem.Evaluations` (design card C-06, "Shared test data").

### 7.4 The process request (T116; Core, `PublicAPI.Unshipped.txt`)

`ProcessStartRequest` keeps its four positional members and gains:
```csharp
/// Names removed from the inherited environment before EnvironmentVariables apply.
public IReadOnlyList<string> EnvironmentRemovals { get; init; } = [];
/// Whether stdout and stderr are redirected and reported line by line (the editor's Output). Default true.
public bool CaptureOutput { get; init; } = true;
/// Whether stdin is a pipe the launcher writes to; only meaningful with CaptureOutput. Default false.
public bool RedirectInput { get; init; }
/// Whether the program starts in a new terminal window (editor only). Default false.
public bool ExternalTerminal { get; init; }
```
`ProgramRunner` (CLI) applies `EnvironmentRemovals` and ignores the three flags: the CLI always inherits the console.
The editor sets the flags from the run options after planning. The resolver never sets them, so the CLI and the
editor compare equal on the SC-008 fields.

### 7.5 The editor's process layer (T116, T119, T120, T123)

```csharp
namespace NetPrints.Editor.Hosting;

/// A live process's input and termination; returned by IProcessLauncher.Start.
public interface IRunningProcess
{
    int Id { get; }                 // the start id of ProcessStarted, LineReceived and ProcessExited
    bool HasExited { get; }
    bool CanWriteInput { get; }     // RedirectInput, input not closed, not exited
    event EventHandler? InputStateChanged; // CanWriteInput changed; raised on any thread
    Task WriteLineAsync(string line, CancellationToken cancellationToken); // UTF-8, Environment.NewLine, flushed; InvalidOperationException when !CanWriteInput
    void CloseInput();              // end of file; idempotent
    Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken); // ProcessCloseRequest.Current.Send, then Kill(entireProcessTree) after timeout, or at once when Send returns false; completes when exited
    void Kill();                    // Kill(entireProcessTree: true) now; idempotent
    bool IsHandedOff { get; }       // an external-terminal start NetPrints does not follow (Linux, macOS); false otherwise
}

public interface IProcessLauncher
{
    IRunningProcess Start(ProcessStartRequest request, CancellationToken cancellationToken = default); // was void
    // the four events unchanged
}

public interface ITerminalLauncher
{
    /// Starts request in a new terminal window; returns the program (Windows) or the terminal launcher (Linux, macOS).
    /// TerminalNotFoundException (message as in §3) when no Linux terminal is found or the configured one cannot start.
    Process Start(ProcessStartRequest request, int startId);

    bool FollowsProgram { get; }    // true when Start returns the program itself (Windows)
}
```
Every double of `IRunningProcess` (`CapturingProcessLauncher`, the fakes) returns false for `IsHandedOff`.
`ProcessLauncher` gets one constructor, `ProcessLauncher(ITerminalLauncher terminals)`. `TerminalLauncher` is
`public sealed class TerminalLauncher(Func<RunSettings> settings, string scriptDirectory) : ITerminalLauncher`,
registered as `[Singleton<ITerminalLauncher>(Factory = "Create")]` with
`internal static TerminalLauncher Create(ISettingsStore settings)` =
`new(() => settings.Get(RunSettings.Descriptor), OperatingSystem.IsMacOS() ? Path.Combine(Path.GetTempPath(), "NetPrints", "terminal") : Path.Combine(EditorDataPaths.Resolve().StateDirectory, "terminal"))`
and `FollowsProgram => TerminalStart.Current.FollowsProgram`. `ProjectRunner.Start` calls `Detach()` before it returns
when the handle's `IsHandedOff` is true (ADR-0042).
`TerminalNotFoundException` (`Hosting/Avalonia/TerminalNotFoundException.cs`) is
`public sealed class TerminalNotFoundException : Exception` with the message of §3.

`RunStateTracker` gains:
- `void WriteNotice(string text)`: raises `LineAppended` with `new RunOutputLine(ProcessStream.Output, text) { IsNotice = true }`.
  `WriteLine` is unchanged (the extensions' `IEditorOutput` lines stay `Output` lines).
- `void Announce(IReadOnlyList<string> lines)`: replaces the pending lines (an empty list clears them); the pending
  lines are raised as notice lines right after the next `ProcessStarted`'s `PhaseChanged`, then cleared. `Forget`
  clears them too (B-8).
- `IRunningProcess? Input { get; }` and `event EventHandler? InputChanged`: the current run's handle while
  `CanWriteInput`.
- `void AttachInput(IRunningProcess process)` (ignored unless `process.Id` is the current id).
- `void Forget()`: the current run is no longer followed (Leave). The phase becomes `NotStarted`, and later lines and
  the exit of that id are dropped; `Input` becomes null and `InputChanged` is raised when it was not null, so a later
  run's Output panel never writes to the detached program (B-9).

`RunOutputLine` gains `public bool IsNotice { get; init; }`, and `OutputLineKind` gains `Notice`; the Output panel maps
`IsNotice` to `Notice` before it looks at the stream, and a `Notice` line has the default line style
(`OutputLineViewModel.IsError` stays `Kind == Error`). `RemoveProgramOutput` removes only `Output` and `Error` lines.
`Apply(Building)` still clears every line, so each run's Output starts empty.

### 7.6 MSBuild registration (T117)

```csharp
namespace NetPrints.Workspace;

/// A variable NetPrints' MSBuild registration added (PriorValue null) or overwrote.
public sealed record MsBuildVariable(string Name, string? PriorValue)
{
    /// The variables whose value differs between the two snapshots (names compared OrdinalIgnoreCase on Windows, else Ordinal).
    public static IReadOnlyList<MsBuildVariable> Between(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after);
}

// MsBuildRegistration gains:
public static IReadOnlyList<MsBuildVariable> Variables { get; } // empty until a call registers; set once by that call
```
Log event 4007 `MsBuildVariablesRecorded` (Debug): the names. Event 4006 `RunResolved` (Debug): profile, kind, program,
argument count, working directory.

### 7.7 Stop and unload (T120)

- `ProcessCloseRequest` (`src/NetPrints.Editor/Hosting/Avalonia/ProcessCloseRequest.cs`), a smart enum with nested
  sealed members `Windows` and `Posix`: `public static ProcessCloseRequest Current`,
  `public static ProcessCloseRequest For(bool isWindows)`, `public abstract bool Send(Process process)`.
- `PosixSignals` (`Hosting/Avalonia/PosixSignals.cs`), `internal static partial class`: `Terminate(int pid)` returns
  `true` when `kill(pid, 15)` returns 0.
- `RunUnloadChoice` (`src/NetPrints.Editor/Dialogs/RunUnloadChoice.cs`): `Cancel` (the default), `Leave`, `Stop`.
- `IEditorDialogs.ConfirmLeaveRunningAsync(string programName)` returns `Task<RunUnloadChoice>`. The view is
  `RunningProgramDialog.axaml` over `RunningProgramDialogViewModel : DialogViewModel<RunUnloadChoice>`, modelled on
  `SampleTargetDialog`.
- Log event 1090 `ProgramStopped` (Information): start id, `closed` or `killed`, elapsed milliseconds.

### 7.8 Session state (T119, T122)

```csharp
namespace NetPrints.Editor.State;

/// The "run" member of sessions/<project-key>.json (data-model §4). Not the "run" section of settings.json (RunSettings).
public sealed record RunSessionState
{
    public string? Profile { get; init; }
    public string Configuration { get; init; } = RunConfigurations.Debug;
    public IReadOnlyDictionary<string, RunProfileOptionsState> Profiles { get; init; } = new Dictionary<string, RunProfileOptionsState>();
}

public sealed record RunProfileOptionsState
{
    public bool ExternalTerminal { get; init; }
    public bool CaptureOutput { get; init; } = true;
}

// SessionState gains (non-positional):
public RunSessionState? Run { get; init; }
```
`StateJsonContext` serializes both. `JsonEditorStateStore.LoadSession` returns `state with { Run = null }` when `Run`
has a null `Profiles` entry or a `Configuration` outside `RunConfigurations.All`. `SessionService.Save` writes
`Run = store.LoadSession(projectPath)?.Run`. `StateFile.CurrentVersion` is unchanged, since the member is additive.

### 7.9 CLI (T118)

- `src/NetPrints.Cli/Infrastructure/ConfigurationSettings.cs`: `internal class ConfigurationSettings : ProjectSettings`,
  `[CommandOption("-c|--configuration <name>")] public string Configuration { get; init; } = RunConfigurations.Debug;`
  with the description `The build configuration (default Debug).`; `Validate` refuses an empty or whitespace value
  with `The configuration must not be empty.` (exit 2).
- `src/NetPrints.Cli/Commands/RunCommandSettings.cs`: `internal sealed class RunCommandSettings : ConfigurationSettings`
  with `[CommandOption("--launch-profile <name>")] public string? LaunchProfile { get; init; }` and
  `[CommandOption("--no-launch-profile")] public bool NoLaunchProfile { get; init; }`. `Validate` gives
  `--launch-profile and --no-launch-profile cannot be used together.` (exit 2). No short option is declared (B-1):
  `dotnet run`'s `-lp` cannot be a Spectre option name, and the CLI contract text says so (T-04).
- `BuildCommand` uses `ConfigurationSettings` and takes `Lazy<IRunProjectSystem>` instead of `Lazy<IProjectSystem>`;
  `BuildCommand.BuildAsync(IRunProjectSystem projects, IAnsiConsole console, string projectPath, string configuration,
  CancellationToken cancellationToken)`.
- `RunCommand` takes `Lazy<IRunProjectSystem>` and `Lazy<RunPlanner>` instead of `Lazy<IProjectSystem>`, so nothing of
  Microsoft.Build loads before `EnsureRegistered` (the reason `CliServices` already keeps `IProjectSystem` lazy).
  `CliServices.CreateDefault` keeps its `Lazy<IProjectSystem>`
  registration unchanged and adds
  `services.AddSingleton(provider => new Lazy<IRunProjectSystem>(() => CreateRunProjectSystem(provider)))` and
  `services.AddSingleton(provider => new Lazy<RunPlanner>(() => RunPlanner.Create(provider.GetRequiredService<Lazy<IRunProjectSystem>>().Value)))`,
  where `CreateRunProjectSystem` is `[MethodImpl(MethodImplOptions.NoInlining)]` and returns
  `provider.GetRequiredService<Lazy<IProjectSystem>>().Value as IRunProjectSystem ?? throw new InvalidOperationException("The CLI's project system does not take a configuration.")`.
  The two `Lazy<IProjectSystem>`/`Lazy<IRunProjectSystem>` values share the one `MsBuildProjectSystem` instance.
  `CliTestHost` registers `new Lazy<IRunProjectSystem>(() => RunProjects)` and `new Lazy<RunPlanner>(() => Planner)`
  next to its `Lazy<IProjectSystem>`.
- `--help` for `run` adds the example lines `netprints run --launch-profile "Godot editor"` and `netprints run -c Release -- --verbose`,
  and the description adds: `Profiles come from Properties/launchSettings.json. A relative workingDirectory is
  resolved from the project folder; dotnet run resolves it from Properties/.`

### 7.10 Editor names (T119, T121, T122)

| Kind | Name |
|---|---|
| Command ids | `BuildContributions.RunId` = `ContributionIds.CommandPrefix + "run"` (the existing inline id at `BuildContributions.cs:32` now uses it; `CommandBarViewModel` finds the run slot with it); `BuildContributions.SelectRunProfileId` = `netprints.command.selectRunProfile`; `BuildContributions.EditRunProfilesId` = `netprints.command.editRunProfiles` (Build menu, group `run`, orders 2, 3) |
| Handlers | `Commands/SelectRunProfileCommandHandler.cs` (enabled when `context.Shell.RunTarget is { IsVisible: true }`; calls `RunTarget.RequestDropDown()`), `Commands/EditRunProfilesCommandHandler.cs` (enabled with a session; opens `DocumentId.ProjectSettings`); `IShell` gains `RunTargetViewModel? RunTarget { get; }`, implemented by `DockShellAdapter` as `shell.RunTarget` (its `ShellViewModel`); the test shells `FakeShell` (Editor.Tests) and `KeyStubShell` (Editor.UITests) implement it with `public RunTargetViewModel? RunTarget { get; set; }` |
| Owners | `ShellViewModel.RunTarget` (`RunTargetViewModel?`): in `OnSessionChanged` the old one is disposed and, for a new session, `new RunTargetViewModel(newValue.Runner, id => Commands?.TryRun(id) ?? false, dispatcher)` is made (`dispatcher`: the `IUiDispatcher` the constructor receives, kept in a new private field); it is passed to `CommandBar.RunTarget` there and again in `AttachCommands` after the command bar is rebuilt; `ShellViewModel.Dispose` disposes it. `ProjectSessionViewModel.Runner` (`ProjectRunner`): made in its constructor by `ProjectRunner.Create(project, context)` unless the new optional constructor parameter `ProjectRunner? runner = null` passes one; disposed in `ProjectSessionViewModel.Dispose` |
| View models | `Run/RunTargetViewModel.cs`, `Run/RunProfileItemViewModel.cs`, `Run/RunSectionViewModel.cs`, `Run/RunProfileEditorViewModel.cs` |
| Services | `Run/ProjectRunner.cs` (`public static ProjectRunner Create(Project project, EditorContext context)`, design card C-08; no container registration), `Run/RunOptionsStore.cs` (`new RunOptionsStore(IEditorStateStore? store, string projectPath)`, made by `ProjectRunner.Create`; with a null store it keeps the values in memory only), `Run/RunProjectSystems.cs` (`From(IProjectSystem)`). Nothing of the run path is added to `EditorServiceRegistrations` or to the session container |
| Process layer files | `Hosting/IRunningProcess.cs`, `Hosting/ITerminalLauncher.cs`, `Hosting/Avalonia/LaunchedProcess.cs` (`internal sealed`), `Hosting/Avalonia/TerminalLauncher.cs`, `Hosting/Avalonia/TerminalNotFoundException.cs`, `Hosting/Avalonia/TerminalScript.cs` (`internal static`), `Hosting/Avalonia/TerminalStart.cs` (`internal abstract`, smart enum), `Hosting/Avalonia/LinuxTerminals.cs` (`internal static`), `Hosting/Avalonia/ProcessCloseRequest.cs`, `Hosting/Avalonia/PosixSignals.cs` |
| Behaviour | `Behaviors/OpenFlyoutOnRequestBehavior.cs` |
| Views | the accessory template in `Shell/ShellWindow.axaml` (a `DataTemplate x:DataType="run:RunTargetViewModel"`); `Run/RunSectionView.axaml` used by `Shell/ProjectSettingsView.axaml`; `Dialogs/RunningProgramDialog.axaml`; the input row in `Output/OutputPanelView.axaml` |
| Automation ids (`AutomationIds.cs`) | `CommandBarRunProfile` = `CommandBar.RunProfile`; `CommandBarRunProfileItemPrefix` = `CommandBar.RunProfile.Item.` (+ index); `CommandBarRunProfileEdit` = `CommandBar.RunProfile.Edit`; `CommandBarRunProfileExternalTerminal` = `CommandBar.RunProfile.ExternalTerminal`; `CommandBarRunProfileDontCapture` = `CommandBar.RunProfile.DontCapture`; `CommandBarConfiguration` = `CommandBar.Configuration`; `OutputInput` = `Output.Input`; `OutputInputSend` = `Output.InputSend`; `OutputCloseInput` = `Output.CloseInput`; `ProjectSettingsRunProfiles` = `ProjectSettings.Run.Profiles`; `ProjectSettingsRunKind` = `ProjectSettings.Run.Kind`; `ProjectSettingsRunExecutablePath` = `ProjectSettings.Run.ExecutablePath`; `ProjectSettingsRunArguments` = `ProjectSettings.Run.Arguments`; `ProjectSettingsRunArgumentPreview` = `ProjectSettings.Run.ArgumentPreview`; `ProjectSettingsRunWorkingDirectory` = `ProjectSettings.Run.WorkingDirectory`; `ProjectSettingsRunEnvironment` = `ProjectSettings.Run.Environment`; `ProjectSettingsRunOpenFile` = `ProjectSettings.Run.OpenFile`; `ProjectSettingsRunReadOnly` = `ProjectSettings.Run.ReadOnly`; `ProjectSettingsRunWorkingDirectoryWarning` = `ProjectSettings.Run.WorkingDirectoryWarning`; `RunningProgramDialogLeave` / `…Stop` / `…Cancel` = `RunningProgramDialog.Leave` / `.Stop` / `.Cancel` |

### 7.11 Resource keys (`src/NetPrints.Editor/Strings.resx`)

| Key | Value |
|---|---|
| `Command.netprints.command.selectRunProfile.Label` | `Select run profile` |
| `Command.netprints.command.editRunProfiles.Label` | `Edit run profiles…` |
| `Run.Profile.None` | `No profile` |
| `Run.Profile.ExternalTerminal` | `Run in external terminal` |
| `Run.Profile.DontCapture` | `Don't capture output` |
| `Run.Configuration.Label` | `Configuration` |
| `Run.Blocked` | `Run stopped: {0}` |
| `Run.FirstRun.Header` | `Running profile '{0}':` |
| `Run.FirstRun.Program` | `  program: {0}` |
| `Run.FirstRun.Arguments` | `  arguments: {0}` |
| `Run.FirstRun.WorkingDirectory` | `  working directory: {0}` |
| `Run.FirstRun.Added` | `  environment added: {0}` |
| `Run.FirstRun.Removed` | `  environment removed: {0}` |
| `Run.NotCaptured` | `Output is not captured for this profile; only its start and exit are shown.` |
| `Run.ExternalTerminalStarted` | `Started in an external terminal.` |
| `Output.Input.Placeholder` | `Type a line for the program and press Enter` |
| `Output.Input.Send` | `Send` |
| `Output.Input.Close` | `Close input` |
| `Run.Section.Header` | `Run` |
| `Run.Section.Kind` | `Kind` |
| `Run.Section.ExecutablePath` | `Executable path` |
| `Run.Section.Arguments` | `Arguments` |
| `Run.Section.ArgumentPreview` | `Arguments as the program receives them` |
| `Run.Section.WorkingDirectory` | `Working directory` |
| `Run.Section.Environment` | `Environment variables` |
| `Run.Section.EnvironmentInvalid` | `Each line must be NAME=VALUE.` |
| `Run.Section.OpenFile` | `Open file` |
| `Run.Section.ReadOnlyComments` | `This file has comments, which saving would remove; edit it in a text editor.` |
| `Run.Section.ReadOnlyKind` | `NetPrints cannot run {0} profiles; edit this one in a text editor.` |
| `Run.Section.ReadOnlyInvalid` | `The file cannot be read: {0}` |
| `Run.Section.NoFile` | `This project has no Properties/launchSettings.json.` |
| `Run.Section.WorkingDirectoryWarning` | `NetPrints and Visual Studio resolve a relative working directory from the project folder; dotnet run resolves it from Properties/.` |
| `Dialog.RunningProgram.Title` | `Program still running` |
| `Dialog.RunningProgram.Message` | `{0} is still running. Leave it running?` |
| `Dialog.RunningProgram.Leave` | `Leave` |
| `Dialog.RunningProgram.Stop` | `Stop` |
| `Dialog.RunningProgram.Cancel` | `Cancel` |
