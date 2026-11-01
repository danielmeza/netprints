# Sub-phase E design cards

One card for each E task that creates a type with behaviour, changes how types collaborate, or adds a subsystem. The
names, members, codes and texts come from [contracts/run-profiles.md](contracts/run-profiles.md) §7. A card fixes the
inside. The implementer builds against it and records any deviation in
[implementation-notes.md](implementation-notes.md) under the batch's heading, and the code review checks the code
against the card. This file is the repository copy of the sub-phase E design cards (2026-10-19, revised after an
adversarial review); a change to a card is made here.

Facts the cards rely on, checked at bd77bcfe and by experiment:
- MSBuildLocator 1.11.2 with SDK 10.0.400 sets four variables on registration (`MSBuildExtensionsPath`,
  `MSBuildSDKsPath`, `DOTNET_HOST_PATH`, `MSBUILD_EXE_PATH`) and overwrites a value set before (research R22).
- `dotnet run` forwards SIGTERM to the program and waits for it (research R22, the Stop bullet).
- `[LibraryImport]` needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (SYSLIB1062 and CS0227 without it).
- `Process.CloseMainWindow()` returns `false` on Linux without throwing (a scratch run with `sleep 5`).
- The session container (`ProjectSessionScope`) holds only `EditorContext`'s instances; `IEditorStateStore` lives in
  the host container only, and resolving `IProcessLauncher` or `RunStateTracker` from the session container builds a
  second instance. `BuildStarted` clears Output. `ProcessLauncher.Start` kills on its token's cancellation, and
  `ProjectSessionViewModel.Dispose` cancels the run token. The CLI keeps its project system behind `Lazy<>` so that
  Microsoft.Build loads after `EnsureRegistered`.

Costs not measured are marked "not measured". The batch logs the measured figure in implementation-notes.

Cards: C-01 reader (T113) · C-02 splitter (T114) · C-03 MSBuild record (T117) · C-04 echo tool (T116) · C-05 request
and handle (T116) · C-06 resolver and planner (T115) · C-07 CLI (T118) · C-08 the editor's run target (T119, T122) ·
C-09 stop and unload (T120) · C-10 terminal and input line (T123) · C-11 the Run section (T121). T124 and T125 add
only tests and docs.

---

## C-01 · Launch settings reader and availability (T113)

**Data owners.** The file on disk owns the profiles. `LaunchSettings` is an immutable snapshot of one read and owns
nothing after it. The project owns the output type (`Project.OutputBinaryType` in the editor, the evaluated
`OutputType` in the CLI), and availability takes it as an argument.

**Pattern.** A parser that produces immutable records, with availability as a method of the record that holds the data
(`LaunchProfile.AvailabilityFor`, information expert).
- Rejected, deserializing into DTOs with `JsonSerializer`: the per-member messages of NPR001 and the "differ only by
  case" check need the raw property names in order, and a deserialized dictionary loses or rejects them.
- Rejected, plain polymorphism (`ProjectProfile`, `ExecutableProfile`, `UnsupportedProfile` subclasses): the kind
  changes only availability and request building. A factory would still switch on `commandName`, and the Run section
  shows every kind with one editor. Subclasses would also pre-shape the `AddRunProfileKind` point that ADR-0037
  decision 4 leaves open.

**Types.**
- `LaunchSettingsReader`: turns text into a `LaunchSettings`. `Read` depends on the file system only (one
  `File.ReadAllText`); `Parse` is pure.
- `LaunchSettings`: the snapshot, plus `Find` and `DefaultProfile`. It depends on nothing.
- `LaunchProfile`: one entry, plus `AvailabilityFor(BinaryType)`. It depends on `RunProfileKinds` and `NprDiagnosticCodes`.
- `RunProfileKinds`, `NprDiagnosticCodes`, `RunProblem` and `RunProfileAvailability` are data, with no dependencies.

**Inside `Parse`.**
1. A `Utf8JsonReader` pass with `JsonCommentHandling.Allow` sets `HasComments` when it meets a comment token.
2. `JsonDocument.Parse` with `CommentHandling = Skip` and `AllowTrailingCommas = true`. A `JsonException` gives NPR001
   with its message.
3. The root must be an object. `profiles` is optional, and when present it must be an object.
4. `EnumerateObject` keeps file order. Each value must be an object with a string `commandName`. The optional string
   fields are read as written (no trimming). `environmentVariables` must be an object of strings, in order.
5. NPR002: walk the profiles in order with a `Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)` of the
   names seen so far. The first collision of each name adds one problem naming the earlier and the later spelling.
6. Any NPR001 empties `Profiles`, so a half-read file is never offered. NPR002 keeps them for display, and `IsUsable`
   is false.

**Seams.** Data only: text in, records out. The editor and the CLI reach it through `RunPlanner.ReadLaunchSettings`
(C-06).

**First red test.** `LaunchSettingsReaderTests.AProjectProfileOnALibraryIsDisabledWithNpr011`, against a stub
`AvailabilityFor` that returns `Runnable`: it fails on the assertion that `Reason.Code == "NPR011"`.

**Cost per call.** One file read and one parse, linear in the file size (a few hundred bytes for real files). Called
at load, before each run, when the drop-down opens and after a Run-section edit; never per frame.

**Guide rows (`docs/guide/diagnostics.md`, written with the constants).** After the prefix row
`| NPK | The extension test kit's conformance checks (`NetPrints.Extensibility.Testing`) |` add:
```text
| NPR | Run profiles: reading `Properties/launchSettings.json` and resolving a profile (the editor and `netprints run`) |
```
After the NPK section's table, before `## NPS: repository style`, add the section below. T125 adds the line
`Run profiles are described in [Run profiles](run-profiles.md).` under its heading when it writes that page.
```markdown
## NPR: run profiles

| Code | Severity | Cause |
|---|---|---|
| NPR001 | error | `Properties/launchSettings.json` is not valid JSON or does not have the launch settings shape; Run is disabled for the project and `netprints run` exits 1. |
| NPR002 | error | Two profile names differ only by case; as NPR001. |
| NPR003 | error | The profile's `commandName` is a kind NetPrints cannot run (`IISExpress`, `Docker`, `WSL2`, …); it is listed disabled, and `netprints run --launch-profile` exits 2. |
| NPR004 | error | `netprints run --launch-profile` names no profile of the file; exit 2. |
| NPR005 | error | A `%NAME%` in `executablePath` or `workingDirectory` is not set; the run stops before the build. |
| NPR006 | error | A `$(Name)` the project does not define, in `executablePath` or `workingDirectory` (error, the run stops before the build) or in the arguments or environment (warning, the text stays). |
| NPR007 | warning | A `%NAME%` in the arguments or environment is not set; the text stays as written. |
| NPR008 | error | The executable was not found (or `executablePath` is empty); the run stops before the build. |
| NPR009 | error | On Windows, a `.cmd` or `.bat` executable is refused, because `cmd.exe` parses the arguments again. |
| NPR010 | error | A macOS `.app` bundle names no executable in `Contents/Info.plist`; the run stops before the build. |
| NPR011 | error | A `Project` profile on a project that is not an executable; it is listed disabled, and `netprints run --launch-profile` exits 2. |
```
NPR006 has two severities; `DiagnosticsGuideTests.EveryRowHasASeverityAndACause` accepts a severity that starts with
a known word, so the row reads `error` and the cause names the warning case.

---

## C-02 · Splitter and joiner (T114)

**Data owners.** None. These are pure functions.

**Pattern.** A hand-written scanner that mirrors .NET's `Process.ParseArgumentsIntoList`, which is the rule .NET itself
uses for `ProcessStartInfo.Arguments` on Unix. The joiner is its inverse ("ArgvQuote").
- Rejected, a regular expression: the parity of a backslash run before a quote is a counting rule, and the regex
  would be unreadable and hard to prove.
- Rejected, a P/Invoke of `CommandLineToArgvW` on Windows: the rule must hold on every OS (FR-054), and Linux CI could
  not test it.

**Types.** `CommandLineSplitter`, a static class whose one job is to turn an argument string into a list and back. It
has no dependencies.

**Seams.** None. The resolver (C-06), the Run section's preview (C-11) and the terminal launcher's `run.linuxTerminal`
(C-10) call it.

**First red test.** `CommandLineSplitterTests.RoundTrip` over the 30+ cases, with `Join` stubbed as
`string.Join(' ', args)`: it fails on the first case with a space.

**Cost per call.** O(n) in the string length, one `StringBuilder`; called a few times per run.

---

## C-03 · MSBuild variable record (T117)

**Data owners.** `MsBuildRegistration` owns `Variables`. It is process-global, as the locator is, and is written once,
inside the existing `Gate` lock, by the call that registers. Later calls never change it.

**Pattern.** Snapshot and diff: copy `Environment.GetEnvironmentVariables()` into a dictionary before the locator call
and again after it; `MsBuildVariable.Between(before, after)` reports every name added or changed with its earlier
value.
- Rejected, a hard-coded list of names: the measurement (research R22) found four names where ADR-0037 named three, and a future locator may set
  others.
- Rejected, reading the locator's internals: they are private and version-bound.

**Types.**
- `MsBuildVariable`: a record with a pure `Between`.
- `MsBuildRegistration`: records around the call it already makes. Its only new dependency is `Environment`.

**Seams.** Data: `MsBuildRegistration.Variables` is read by `RunPlanner.Create` (C-06) through a
`Func<IReadOnlyList<MsBuildVariable>>`, so tests pass their own list.

**Inside.** The name comparer is `StringComparer.OrdinalIgnoreCase` when `OperatingSystem.IsWindows()`, else
`Ordinal`. `Between` takes the comparer as an overload parameter, so its tests run both branches on Linux. Event 4007
(Debug) lists the names.

**First red test.** `MsBuildVariableTests.AnOverwrittenVariableKeepsItsEarlierValue`, with `Between` stubbed to report
added names only: it fails on `PriorValue == "/user/value"`.

**Cost.** Two snapshots of the environment (about a hundred entries) once per process.

---

## C-04 · Echo tool fixture (created in T116)

**Data owners.** The program owns nothing persistent. Its behaviour is set by environment variables only, so that its
argument list is free for the tests.

**Behaviour** (`tests/Fixtures/RunProfiles/EchoTool/Program.cs`, `net10.0`, `OutputType` `Exe`,
`AppendTargetFrameworkToOutputPath` false):
- It writes, each line flushed: `pid=<pid>`; `argc=<n>`; `argv[<i>]=<value>` for each argument, 0-based, the program
  name excluded; `cwd=<Environment.CurrentDirectory>`; then, for each name in `ECHO_TOOL_ENV` (`;`-separated, default
  `NETPRINTS_ECHO`), `env:<NAME>=<value>` or `env:<NAME> unset`.
- With `ECHO_TOOL_OUT=<file>` it also appends the same lines to that file (for the terminal tests, where output is not
  captured).
- `ECHO_TOOL_MODE` is a comma list:
  - `read-line`: after the report it prints `ready`, then for each stdin line prints `stdin=<line>`, and at end of file
    prints `stdin-eof`.
  - `sleep`: it prints `ready` and then sleeps until it is killed.
  - `ignore-sigterm`: it registers `PosixSignalRegistration.Create(PosixSignal.SIGTERM, c => c.Cancel = true)` (with
    `sleep`).
  - `spawn-child`: it starts itself (`Environment.ProcessPath` with the DLL path) with
    `ECHO_TOOL_MODE=sleep,ignore-sigterm` and prints `child=<pid>`.
  - With no mode it exits 0 after the report. `ECHO_TOOL_EXIT=<n>` sets the exit code.

**Rejected.** A shell script stub: it cannot run on Windows, and `sh -c` would re-parse arguments, which is what the
tests must rule out.

**Seams.** `tests/NetPrints.Testing/RunFixtures.cs`, `public static class RunFixtures`:
- `EchoToolDll()`: `Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "Fixtures", "RunProfiles", "EchoTool",
  "bin", LocalSdkLayout.DetectConfiguration(), "EchoTool.dll")`, as `FixtureExtensions` builds its paths.
- `DotnetHost()`: `Environment.ProcessPath` when its file name without extension is `dotnet`, else the value of
  `DOTNET_HOST_PATH` when it is set and not empty, else the bare name `dotnet`, which the OS finds on `PATH` as
  `ExternalProcess.cs` and `ExecutableTemplateBuildTests` already start it (B-6). It never throws: a `dotnet test`
  host's `ProcessPath` is the test executable.

Tests start the tool as `DotnetHost()` followed by `EchoToolDll()` and the test's arguments.

**Project and references.** `EchoTool.csproj` is added to `NetPrints.slnx` under `/tests/Fixtures/` and referenced
with `ReferenceOutputAssembly="false"` (and a one-line comment, as the other fixture references have) from the four
test projects that start it: Cli.Tests, Editor.Tests, Editor.UITests and Desktop.E2ETests.

**First red test.** `ProcessLauncherTests.TheArgumentListReachesTheProgramExactly`: with the fixture missing it fails
on `File.Exists(RunFixtures.EchoToolDll())`, then on the argv lines once the launcher is wired.

---

## C-05 · Process request additions and the running-process handle (T116)

**Data owners.**
- `ProcessStartRequest` owns what to start. The four new members default to today's behaviour.
- `LaunchedProcess` (internal, `Hosting/Avalonia/`) owns one started process: the `Process`, its stdin writer, the
  `inputClosed` and `exited` flags, and the kill-on-cancel registration.
- `ProcessLauncher` owns only the id counter.

**Pattern.** A factory that returns a handle (a proxy over `Process`): `Start` returns `IRunningProcess`, and the events
stay for observers (`RunStateTracker`).
- Rejected, input methods on `IProcessLauncher` keyed by id: that puts a registry of live processes in a singleton,
  and any caller holding an int could write to any process.
- Rejected, returning `Process`: callers could change streams and the stop rules would spread across callers.
- Rejected, plain polymorphism with launcher subclasses per capture mode: capture is one flag on one start path, and
  the branches are three `ProcessStartInfo` settings.

**Types.**
- `IRunningProcess`: a live process's input and termination, and `IsHandedOff` (an external-terminal start NetPrints
  does not follow).
- `LaunchedProcess`: implements it over `Process` and `ProcessCloseRequest` (C-09).
- `ProcessLauncher`: builds the `ProcessStartInfo` from the request and wires the events. It hands
  `ExternalTerminal` to `ITerminalLauncher` (C-10). T116 leaves its constructor as it is. T123 gives it one
  constructor, `ProcessLauncher(ITerminalLauncher terminals)`, resolved by the `[Singleton<IProcessLauncher>]`
  registration from the `[Singleton<ITerminalLauncher>(Factory = "Create")]` one (C-10), and updates the hand-built
  launchers (`ProcessLauncherTests`, `CapturingProcessLauncher`) to pass a `TerminalLauncher`.

**Inside `Start`.**
- `RedirectStandardOutput` and `RedirectStandardError` follow `CaptureOutput`. `RedirectStandardInput` is
  `CaptureOutput && RedirectInput`, with `StandardInputEncoding = new UTF8Encoding(false)`.
- Each name in `EnvironmentRemovals` is removed from `startInfo.Environment` before `EnvironmentVariables` apply.
- Order kept from today: start, register the kill on cancel, `ProcessStarted`, begin the reads (when captured),
  `EnableRaisingEvents`, so a fast exit never comes before the start.
- Uncaptured: no lines. `ProcessExited` still fires; the drain wait is skipped because there are no streams.
- `WriteLineAsync` is serialized by a `SemaphoreSlim(1)`. It throws `InvalidOperationException` when `!CanWriteInput`.
  An `IOException` from a closed pipe sets `inputClosed` and raises `InputStateChanged`.
- `CloseInput` is idempotent under a lock. `InputStateChanged` is raised outside the lock.
- `Kill` is idempotent and swallows "already exited", as today's `Kill` helper does.

**`ProgramRunner` (CLI).** It applies `EnvironmentRemovals` and ignores the three flags, documented on the class.

**Seams.** Port: `IProcessLauncher` (OS processes). Signals: `ProcessStarted`, `LineReceived` and `ProcessExited`
(unchanged), and `IRunningProcess.InputStateChanged`. Test doubles: `CapturingProcessLauncher` (Editor.UITests)
returns the inner launcher's handle; `FakeProcessLauncher` (Editor.Tests `Hosting/Fakes.cs`) returns a new
`FakeRunningProcess` (same file) that counts `WriteLineAsync`, `CloseInput`, `StopAsync` and `Kill` calls and keeps
the written lines; `ControlledLauncher` (Editor.UITests `Shell/RegistrySurfaceTests.cs`) keeps throwing
`NotSupportedException` from `Start`.

**First red test.** `ProcessLauncherTests.RemovedVariablesDoNotReachTheProgram`: the echo tool prints
`env:NETPRINTS_REMOVE_ME unset`. With removals ignored it prints the value and fails.

**Cost.** Per start: one `ProcessStartInfo`. Per line: unchanged. Per input line: one write and flush.

---

## C-06 · Resolver and planner (T115)

**Data owners.**
- The resolver owns nothing. It is a stateless static function of `RunInputs`, `RunPlatform` and the file system it
  probes (`File.Exists`, `Directory.Exists`, the macOS `Info.plist` read); it is not pure, and its tests use real
  files under a temp folder.
- `RunPlanner` owns the composition: which file, which property names, which environment snapshot.
- `IRunProjectSystem` owns MSBuild evaluation and the build.
- `MsBuildRegistration` owns the variable record.

**Pattern.**
- A functional core: one static `Resolve` running `Choose` → expand → split → paths → request over immutable
  records, with private helpers per step and one problem list threaded through.
- Platform differences go through a smart enum with private nested sealed members (`WindowsPlatform`,
  `MacOSPlatform`, `LinuxPlatform`, exposed as `RunPlatform.Windows`, `MacOS`, `Linux`, the `EditorPlatform` shape).
  The members are those of contracts/run-profiles.md §7.3a: `PathListSeparator`, `VariableNameComparer`,
  `HasDirectorySeparator`, `CandidateNames`, `IsBundle`, `BundleExecutable`, `RefusesTarget`.
- `RunPlanner` is a facade over reader, port and resolver, so both front ends take the same three steps.

Rejected:
- One class per step (a pipeline of strategies): the steps share the profile, the field being expanded and the
  problem list, so each class would need a context object. That is the context bag the design rules forbid.
- Plain polymorphism on the kind (`ProjectKind.BuildRequest`, `ExecutableKind.BuildRequest`): it fixes the shape of
  the future `AddRunProfileKind` point (ADR-0037 decision 4 leaves it open). Two private methods, `ProjectRequest` and
  `ExecutableRequest`, selected by `commandName`, are enough for two kinds.
- `if (OperatingSystem.IsWindows())` inside the resolver: no test could run the Windows and macOS rows on Linux, and
  the resolver tests need every row on every OS.
- A file-system port (`IRunFileSystem`) to make the resolver pure: three probes, all cheap on real temp files; a port
  would add a type and a fake for no case the temp folder cannot write.

**Types and their dependencies.**
- `RunProfileResolver`: inputs to resolution. Depends on `CommandLineSplitter`, `RunPlatform` and the file system.
- `RunProfileChoice`: the result of `Choose` (the profile or none, and the blocking problems).
- `RunPlatform`: OS rules only; `MacOSPlatform.BundleExecutable` reads the plist with `XDocument.Load`, and an
  `XmlException` (a binary plist) gives null.
- `RunPlanner`: depends on `IRunProjectSystem`, `RunPlatform`, `Func<IReadOnlyDictionary<string, string>>` (the
  environment) and `Func<IReadOnlyList<MsBuildVariable>>`. `Create` binds `RunPlatform.Current`, a snapshot of
  `Environment.GetEnvironmentVariables()` taken at each `PlanAsync` (keys with the platform comparer), and
  `() => MsBuildRegistration.Variables`.
- `IRunProjectSystem`: implemented by `MsBuildProjectSystem`. Its `EvaluatePropertiesAsync` calls the private
  `Evaluate`, which gains an `IDictionary<string, string>? globalProperties` parameter (merged with the
  `TargetFramework` it adds when it retargets), with `Configuration` set; a name is defined when `GetProperty(name)` is
  not null. Its `BuildAsync` inserts `-c <configuration>` after the project path in today's argument list.
- `OutputTypes.FromProperty` (`internal static`, Workspace): `Exe` (`OrdinalIgnoreCase`) → `Executable`, else
  `SharedLibrary`; used by the planner and by `MsBuildProjectSystem.LoadAsync`.

**Inside `PlanAsync`.** (1) `ReadLaunchSettings`. (2) With `RunRequest.OutputType` set (the editor): `Choose`; a
blocked choice returns a `RunResolution` with its problems and no evaluation; else `PropertyNames(profile)` is
evaluated once when the list is not empty. With `OutputType` null (the CLI): one evaluation of `OutputType` plus every
profile's `PropertyNames` (distinct `OrdinalIgnoreCase`, file order), and `OutputType` is mapped with
`OutputTypes.FromProperty`. (3) `Resolve` over `RunInputs` built from the request, the file, the evaluated
properties, the environment snapshot and the variable record.

**Inside `Resolve`.**
1. `Choose` (contracts/run-profiles.md §7.3a). A blocked choice returns at once with only its problems.
2. Expand each field of the chosen profile in one pass of the regex
   `%(?<env>[^%\s]+)%|\$\((?<prop>[A-Za-z_][A-Za-z0-9_-]*)\)`: environment names with
   `platform.VariableNameComparer`, property names `OrdinalIgnoreCase`. An unset `%NAME%` gives NPR005 (path fields)
   or NPR007 (arguments, environment). An undefined `$(Name)` gives NPR006, an error in path fields and a warning
   elsewhere. Unresolved text stays literal; expanded text is not expanded again.
3. Split the expanded `commandLineArgs` with `CommandLineSplitter.Split`, or take `ArgumentOverride` when it is set.
4. Working directory: absent or empty is the project folder; otherwise
   `Path.GetFullPath(Path.Combine(projectFolder, expanded))`.
5. Executable (the `Executable` kind), in this order: empty after expansion is NPR008 (the "is empty" message);
   `platform.HasDirectorySeparator(value)` → `Path.GetFullPath(Path.Combine(workingDirectory, value))`; otherwise
   each entry of `PATH` (read from `RunInputs.Environment` with the platform comparer, split on `PathListSeparator`,
   empty entries and entries that are not rooted (`Path.IsPathRooted`) skipped, B-13) × `CandidateNames(value)`, the
   first that `File.Exists`, made `Path.GetFullPath(Path.Combine(entry, candidate))` (B-2, FR-055: the OS always gets
   an absolute path); then `IsBundle` →
   `BundleExecutable`, null being NPR010; then `RefusesTarget` is NPR009; then a path that is not an existing file is
   NPR008.
6. Build the request (`ProcessStartRequest`; the editor sets the three flags later):
   - `Executable`: `FileName` the resolved path, `Arguments` the list, `WorkingDirectory` from step 4.
     `EnvironmentVariables`: the profile's variables in file order, then `DOTNET_LAUNCH_PROFILE=<profile name>`, then
     each MSBuild variable that has a `PriorValue`, set back to it. `EnvironmentRemovals`: the MSBuild names with no
     `PriorValue`. A name the profile sets (or `DOTNET_LAUNCH_PROFILE`), compared with the platform comparer, is
     skipped by both: the profile's value wins.
   - `Project`: `FileName` `dotnet`; `Arguments` `run`, `--project`, the project path, `--no-build`,
     `--no-launch-profile`, `-c`, the configuration, then `-e`, `NAME=VALUE` per profile variable in file order, then
     `-e`, `DOTNET_LAUNCH_PROFILE=<name>`, then `--` and the arguments when the list is not empty; `WorkingDirectory`
     from step 4. `EnvironmentVariables` holds only the set-back MSBuild values and `EnvironmentRemovals` every MSBuild
     name with no `PriorValue`: the scrub applies to `dotnet` in full.
   - No profile: as `Project` with no `-e` pair; `WorkingDirectory` the project folder.
   - `AddedEnvironment`: the profile's variable names in file order, then `DOTNET_LAUNCH_PROFILE`; empty with no
     profile.
7. Any error problem leaves `Request` null. Event 4006 (Debug).

**Shared test data (`tests/NetPrints.Run.Testing`).**
- `NetPrints.Run.Testing.csproj`: `net10.0`, `ImplicitUsings` enabled; `PackageReference` `xunit.v3.assert`, and
  `Microsoft.Build` and `Microsoft.Build.Framework` with `ExcludeAssets="runtime" PrivateAssets="all"` (the MSBL001
  pin, with the comment `NetPrints.Testing.Ui` carries); `ProjectReference` to `NetPrints.Workspace` and
  `NetPrints.Testing`. Listed in `NetPrints.slnx` next to `NetPrints.Testing.Ui`. Referenced by Core.Tests, Cli.Tests
  and Editor.Tests, which already carry the pin. Namespace `NetPrints.Run.Testing`.
- `RunProfileCase` (`public sealed record RunProfileCase(string Name)`), `init` members: `LaunchSettingsJson`
  (`string?`, null: no file), `ProfileName`, `NoLaunchProfile`, `OutputType` (default `BinaryType.Executable`),
  `Configuration` (default `RunConfigurations.Debug`), `Environment` and `Properties`
  (`IReadOnlyDictionary<string, string>`), `Files` (`IReadOnlyList<KeyValuePair<string, string>>`, relative path →
  content, `""` for an empty file), `Platform` (`required RunPlatform`), `ArgumentOverride`, `MsBuildVariables`
  (`IReadOnlyList<MsBuildVariable>`), `ExpectedProblems` (codes, in order), `ExpectedRequest`
  (`ProcessStartRequest?`, null when blocked), `CliOnly` (`bool`: the case uses an input the editor does not have,
  `--no-launch-profile`, `-- <args>` or a profile name the file lacks; set on `NoLaunchProfileOption`,
  `ArgumentOverride` and `Npr004UnknownProfile`). Any string may hold the token `{root}`.
- `RunProfileCase.Materialize(string root)`: writes `<root>/App/App.csproj` (`<Project Sdk="Microsoft.NET.Sdk" />`),
  `<root>/App/Properties/launchSettings.json` when `LaunchSettingsJson` is not null, and each `Files` entry under
  `<root>` (folders created). `{root}` is replaced before anything is written or compared (B-3): in `LaunchSettingsJson`
  by `root` with every `\` doubled (JSON string escaping, so a Windows root stays valid JSON), and in every other
  string (`Files` contents, `Environment`, `Properties`, `ArgumentOverride`, `MsBuildVariables`, `ExpectedRequest`) by
  `root` as it is. The file on disk, the inputs and the expected request therefore hold the same root. In
  `ExpectedRequest`, a `FileName`, a `WorkingDirectory` or an `Arguments` element that begins with `{root}` is a path:
  after the token is replaced, every `/` in the rest of it becomes `Path.DirectorySeparatorChar` (B-2), which is what
  `Path.GetFullPath` gives on the host, since the resolver passes every path it builds through it. Cases are written
  with `/` and run on every OS. The project file path is `Path.Combine(root, "App", "App.csproj")`.
- `RunProfileCase.ToInputs()` (on a materialized case) builds the `RunInputs` the resolver test passes;
  `RunProfileCase.CreatePlanner(IRunProjectSystem projects)` returns
  `new RunPlanner(projects, Platform, () => Environment, () => MsBuildVariables)`.
- `RunProfileCases`: `All` (`IReadOnlyList<RunProfileCase>`), `Names`, `EditorNames` (the names of the cases with
  `CliOnly` false), `Named(string)`. The resolver and CLI tests expose
  `public static TheoryData<string> Cases => new(RunProfileCases.Names);`, `ProjectRunnerTests` the same over
  `EditorNames`, and set the case's `ProfileName` through `RunOptionsStore.SetProfile`. The cases, by name: `ExpandEnvironment`,
  `ExpandProperty`, `SplitQuotesAndBackslashes`, `WorkingDirectoryRelative`, `WorkingDirectoryAbsent`,
  `ExecutableWithSeparator`, `ExecutableOnPathLinux`, `ExecutableOnPathWindowsExe`, `ExecutableOnPathWindowsCom`, `ExecutableOnPathRelativeEntry`,
  `MacAppBundle`, `ProjectKind`, `ExecutableOnLibrary`, `ScrubExecutable`, `ScrubProject`,
  `ProfileVariableWinsOverScrub`, `NoFile`, `NoLaunchProfileOption`, `ArgumentOverride`, `Npr001InvalidJson`,
  `Npr002NamesDifferByCase`, `Npr003Kind`, `Npr004UnknownProfile`, `Npr005PathVariableUnset`, `Npr006PathProperty`,
  `Npr006ArgumentPropertyWarns`, `Npr007ArgumentVariableWarns`, `Npr008NotFound`, `Npr008Empty`, `Npr009Cmd`,
  `Npr010NoBundleExecutable`, `Npr011ProjectOnLibrary`.
- `RunRequestAssert.Same(ProcessStartRequest expected, ProcessStartRequest actual)`: `Assert.Equal` on `FileName`,
  `Arguments` (sequence), `WorkingDirectory`, `EnvironmentVariables` (pairs sorted by name, ordinal) and
  `EnvironmentRemovals` (sorted, ordinal).
- `FakeRunProjectSystem(IReadOnlyDictionary<string, string> properties) : IRunProjectSystem`: `For(RunProfileCase)`
  adds `OutputType` (`Exe` for `Executable`, `Library` for `SharedLibrary`) to the case's properties; `Evaluations`
  (`List<(string Configuration, IReadOnlyList<string> Names)>`) and `Builds`
  (`List<(string ProjectFilePath, string Configuration)>`) record the calls; `EvaluatePropertiesAsync` returns the
  requested names it holds (`OrdinalIgnoreCase`); `BuildAsync` returns `new BuildResult(true, [], null, "")`.

**Seams.** Port: `IRunProjectSystem`. Data: `RunRequest` in, `RunResolution` out. Shared test data: the library
above.

**First red test.** `RunProfileResolverTests.AnUnsetVariableInTheExecutablePathBlocksBeforeTheBuild` (NPR005), against
a stub `Resolve` that returns a request: it fails on `IsBlocked`.

**Cost per run.**
- Resolve: O(field length) regex plus at most (PATH entries × 3) `File.Exists` calls.
- Planner: one file read, plus at most one in-process MSBuild evaluation: in the editor only when the chosen profile
  uses `$(`, in the CLI always (for `OutputType`). The evaluation's duration is not measured; the batch logs it from
  `MsBuildProjectSystemTests`.

---

## C-07 · `netprints build -c` and `netprints run` with profiles (T118)

**Data owners.** Spectre owns the parsed settings (`ConfigurationSettings`, `RunCommandSettings`). The command owns
only the flow. The program's exit code belongs to the program.

**Pattern.** A transaction script in the command, as every CLI command is written, plus a pure mapping
`RunExitCodes.For(IReadOnlyList<RunProblem>)` (`internal static class RunExitCodes`, in `RunCommand.cs`): the first
problem with `Severity == Error` decides; NPR003, NPR004 and NPR011 give 2 (`ExitCodes.Usage`), any other error gives
1 (`ExitCodes.Failed`); a list with no error gives 0. The choose step blocks alone (contracts/run-profiles.md §7.3a),
so a list never holds both an exit-2 code and a later step's error; the first-error rule only makes the result
independent of that fact.
- Rejected, problem subclasses carrying their exit code: the codes are data shared with the editor, which has no exit
  codes.
- Rejected, a mapping table in the Workspace: exit codes are an ADR-0015 CLI concern.

**Flow of `RunCommand.ExecuteProjectAsync`.**
1. `planner.Value.PlanAsync(new RunRequest(projectPath, settings.Configuration) { ProfileName = settings.LaunchProfile,
   NoLaunchProfile = settings.NoLaunchProfile, ArgumentOverride = forwarded.Values.Count > 0 ? forwarded.Values : null }, ct)`.
2. Write each problem's `ToLine()` to stderr (`Environment.Error.WriteLineAsync`), in list order.
3. When `resolution.Request is not { } request`, return `RunExitCodes.For(resolution.Problems)`.
4. `BuildCommand.BuildAsync(runProjects.Value, Console, projectPath, settings.Configuration, ct)`; return its exit
   code when it is not `ExitCodes.Success`.
5. `programs.RunAsync(request, ct)`.

**Types and dependencies.** `RunCommand` depends on `Lazy<IRunProjectSystem>` (build), `Lazy<RunPlanner>` and
`IProgramRunner`, plus the base class's console, environment, registration and loggers. It drops
`Lazy<IProjectSystem>`. `BuildCommand` takes `Lazy<IRunProjectSystem>`. Every project-system dependency stays behind
`Lazy<>`, because the container builds the command before `ProjectCommandBase.ExecuteAsync` calls `EnsureRegistered`,
and building `MsBuildProjectSystem` earlier loads Microsoft.Build too soon.
- Rejected, `IRunProjectSystem` and `RunPlanner` as plain constructor dependencies: they would build
  `MsBuildProjectSystem` with the command.

**Seams.** DI registrations in `CliServices.CreateDefault` (contracts/run-profiles.md §7.9): the existing
`Lazy<IProjectSystem>` is unchanged; `Lazy<IRunProjectSystem>` returns the same `MsBuildProjectSystem` instance through
the `NoInlining` helper `CreateRunProjectSystem`; `Lazy<RunPlanner>` wraps `RunPlanner.Create` over it. No
`CachingProjectSystem` is involved. `CliTestHost` gains two settable properties and registers them lazily:
`RunProjects` (`FakeRunProjectSystem`, default `new FakeRunProjectSystem(new Dictionary<string, string> { ["OutputType"] = "Exe" })`)
behind `new Lazy<IRunProjectSystem>(() => RunProjects)`, and `Planner` (`RunPlanner`, default
`new RunPlanner(RunProjects, RunPlatform.Current, () => new Dictionary<string, string>(), () => [])`) behind
`new Lazy<RunPlanner>(() => Planner)`. A SC-008 test sets `RunProjects = FakeRunProjectSystem.For(c)` and
`Planner = c.CreatePlanner(RunProjects)` before it runs the command.

**First red test.** `RunCommandTests.AnUnknownProfileExitsTwo` (NPR004), with `RunExitCodes.For` stubbed to return 1:
it fails on exit code 2.

**Cost.** Per invocation: one plan (one evaluation, for `OutputType` and the profiles' `$(Name)`s), one build, one
program run.

---

## C-08 · The editor's run target: options, runner, button, configuration (T119, T122)

**Data owners.**

| Datum | Owner | Where it lives |
|---|---|---|
| Selected profile name, configuration, per-profile options | `RunOptionsStore` (one per session, owned by its `ProjectRunner`) | memory, written at once to `SessionState.Run` through `IEditorStateStore` (memory only when the context has no store) |
| The launch settings snapshot | `ProjectRunner` (one per session, owned by `ProjectSessionViewModel`) | memory, replaced by `Refresh()` |
| Run enablement (`CanRun`), button visibility (`IsVisible`) and the selected profile | `ProjectRunner` | cached, recomputed on `Refresh`, on a selection change and on `Project.OutputBinaryType` change |
| The live run (handle, kind, profile name, detached flag) and the announced profiles | `ProjectRunner` | memory |
| Phase, output lines, the input endpoint | `RunStateTracker` (the editor's singleton, `EditorContext.RunState`) | memory |
| Rows, check marks, label, error text, flyout requests | `RunTargetViewModel` (owned by `ShellViewModel.RunTarget`) | derived, never persisted |

**Pattern.** A per-session application service (`ProjectRunner`) is the single owner of the run flow.
`RunTargetViewModel` is a projection of it and of its `RunOptionsStore`. `ProjectSessionViewModel` delegates Run and
Compile's build to the runner and keeps saving, the compile outcome and command-state pulses.

Rejected:
- Growing `ProjectSessionViewModel` (640 lines, holding save, undo, compile, run and extension sessions): plan,
  announce, stop and detach would make it the god type the design rules forbid.
- A State-pattern lifecycle (`Idle`, `Resolving`, `Building`, `Running`, `Stopping` classes with polymorphic
  transitions): the transitions are linear and driven by awaits. One async method expresses them with less code, and
  `RunStateTracker` already publishes the phase the UI needs.
- An `IRunProjectSystem` member on `EditorContext`: it grows the context bag. `RunProjectSystems.From(IProjectSystem)`
  is the one composition helper, called by `ProjectRunner.Create`.
- Container services (`[SessionScoped<RunOptionsStore>]`, singletons `IRunProjectSystem` and `RunPlanner` in
  `EditorServiceRegistrations`, a `ProjectSessionScope.CreateRunner`): the session container holds no
  `IEditorStateStore`, and resolving `IProcessLauncher` or `RunStateTracker` from it builds second instances that the
  Output panel never hears. Everything the runner needs is already on `EditorContext`, so one static factory builds it.

**Types: one job and narrow dependencies each.**
- `RunOptionsStore(IEditorStateStore? store, string projectPath)` keeps the run block of one project.
  - `Profile` (`string?`), `Configuration`, `OptionsFor(string? name)` (a `RunProfileOptionsState`, the defaults when
    the name is null or has none; names `OrdinalIgnoreCase`), `SetProfile(string? name)`, `SetConfiguration(string)`,
    `SetOptions(string name, RunProfileOptionsState options)`.
  - It reads `store?.LoadSession(projectPath)?.Run` once, in the constructor. Every setter updates memory, then, when
    `store` is not null, writes `(store.LoadSession(projectPath) ?? new SessionState(StateFile.CurrentVersion,
    projectPath, [], null, new Dictionary<string, ViewportState>())) with { Run = … }` through `SaveSession`, then
    raises `Changed`. A setter given the current value does nothing.
- `ProjectRunner(Project project, RunPlanner planner, IRunProjectSystem projects, RunOptionsStore options, IProcessLauncher launcher, RunStateTracker tracker, Func<RunSettings> runSettings) : IDisposable`
  owns the run flow of one session.
  - `public static ProjectRunner Create(Project project, EditorContext context)`: `projects =
    RunProjectSystems.From(context.Projects)`; `new ProjectRunner(project, RunPlanner.Create(projects), projects,
    new RunOptionsStore(context.StateStore, project.Path), context.Processes, context.RunState,
    () => context.Settings.Get(RunSettings.Descriptor))`.
  - `Options`, `ProjectFilePath`, `Settings`, `SelectedProfile`, `CanRun`, `IsVisible`, `Configuration`,
    `HasLiveExecutableRun` (a handle that has not exited, of an `Executable` run), `LiveProfileName` (that run's
    profile name, or null), `Changed`.
  - `SelectedProfile`: null when `!Settings.IsUsable`; else `Settings.Find(remembered)` when `Options.Profile is { } remembered` and that finds one; else
    `Settings.DefaultProfile(project.OutputBinaryType)`. A remembered name that is gone stays stored until the user
    picks another.
  - `CanRun`: `Settings.IsUsable && (SelectedProfile is { } p ? p.AvailabilityFor(outputType).CanRun : outputType ==
    BinaryType.Executable)`.
  - `IsVisible`: `Settings.FilePath is not null || outputType == BinaryType.Executable`
    (contracts/run-profiles.md §1).
  - `Refresh()`: re-reads the file through `planner.ReadLaunchSettings` and recomputes; raises `Changed`. The
    constructor reads the file the same way without raising `Changed`. The runner also recomputes (and raises
    `Changed`) on `Options.Changed` and on `project`'s `PropertyChanged` for `OutputBinaryType`, and unsubscribes in
    `Dispose`.
  - `Task<BuildResult> BuildAsync(CancellationToken)`: `projects.BuildAsync(project.Path, Options.Configuration, ct)`.
  - `Task<RunResolution> PlanAsync(CancellationToken)`: `Refresh()`, then `planner.PlanAsync(new RunRequest(project.Path,
    Options.Configuration) { OutputType = project.OutputBinaryType, ProfileName = SelectedProfile?.Name }, ct)`. The
    file is read twice (the runner's snapshot and the planner's own read); it is a few hundred bytes, and the planner
    keeps the one code path the CLI also runs.
  - `IRunningProcess Start(RunResolution resolution)`: throws `ArgumentException` when `resolution.Request is not
    { } request` (a blocked resolution is never started, and no `!` is used); sets the flags on `request`, announces, calls
    `launcher.Start(request, CancellationToken.None)` (no kill-on-cancel: Leave must survive the session), keeps the
    handle, and attaches input. When the handle's `IsHandedOff` is true, `Start` calls `Detach()` before it returns
    (ADR-0042).
  - `Task StopAsync(CancellationToken cancellationToken)`: the live handle's
    `StopAsync(TimeSpan.FromSeconds(runSettings().StopTimeoutSeconds), cancellationToken)`, or a completed task.
  - `void Detach()`: `tracker.Forget()`, then drops the handle without killing it.
  - `void Kill()`: the live handle's `Kill()`. `Dispose()`: `Kill()`, so a detached run (no handle) survives.
- `RunTargetViewModel(ProjectRunner runner, Func<string, bool> runCommand, IUiDispatcher dispatcher) : ObservableObject, IDisposable`
  is the button and the selector. `runCommand` takes a full command id (`BuildContributions.EditRunProfilesId`) and is
  `id => Commands?.TryRun(id[ContributionIds.CommandPrefix.Length..]) ?? false` from `ShellViewModel`, because
  `CommandInvoker.TryRun(string)` adds `ContributionIds.CommandPrefix` itself (B-5).
  - `IsVisible` (`runner.IsVisible`), `Label` (`SelectedProfile.Name`, or the `Run.Profile.None` text), `Error` (the
    first `Settings.Problems` message, or null), `HasError`, `Profiles` (`RunProfileItemViewModel` rows in file
    order: `AvailabilityFor(outputType)` gives `isEnabled` and `reason`), `HasProfile` (`SelectedProfile` not null),
    `Configurations` (`RunConfigurations.All`), `Configuration` (two-way, through `Options.SetConfiguration`),
    `ExternalTerminal` and `DontCaptureOutput` (two-way, the selected profile's options through `Options.SetOptions`;
    shown only when `HasProfile`).
  - `RunEntry` (`CommandEntryViewModel?`): set by `CommandBarViewModel` on every rebuild to the slot's `run` entry. The
    split button's primary part binds `RunEntry.RunCommand`, so enablement has one source.
  - `SelectProfileCommand` (parameter `RunProfileItemViewModel`; `Options.SetProfile(item.Name)` for an enabled row,
    then `runner.Refresh()`), `RefreshCommand` (`runner.Refresh()`, bound to the flyout's `Opening`),
    `EditProfilesCommand` (`runCommand(BuildContributions.EditRunProfilesId)`).
  - `DropDownRequested`, `RequestDropDown()`.
  - It listens to `runner.Changed` and rebuilds its derived members through `dispatcher.Post`; `Dispose` unsubscribes.
- `RunProfileItemViewModel(string name, bool isEnabled, string? reason, bool isSelected, int index)` is one row. The
  automation id is the prefix plus the index.
- `RunProjectSystems.From(IProjectSystem)` returns the instance when it implements `IRunProjectSystem`, else a private
  adapter: Debug builds through `BuildAsync(path, ct)`, other configurations throw `NotSupportedException`, and the
  evaluation returns no names.

**Wiring (composition: who builds what, in which container, in what order).**
- No container is involved. `EditorServiceRegistrations`, `AddSessionHost`, `ProjectSessionScope` and `ProjectLoader`
  do not change.
- `ProjectSessionViewModel(Project project, EditorContext context, ClassContextFactory? classContexts = null,
  ProjectRunner? runner = null)`: the constructor sets `Runner = runner ?? ProjectRunner.Create(project, context)`
  before it subscribes to anything, so the existing call sites compile unchanged and get a real runner over their
  context's `Processes`, `RunState`, `StateStore` and `Settings`. `Dispose` calls `Runner.Dispose()` last. Existing
  tests that assert today's `dotnet run --project <p> --no-build` request now see the "No profile" request
  (contracts/run-profiles.md §2) and are updated in the same commit.
- Order on open: `ProjectLoader.SetProjectAsync` builds the session (and with it the runner and its options store),
  then sets `shell.Session`; `ShellViewModel.OnSessionChanged` disposes the old `RunTarget`, builds
  `new RunTargetViewModel(newValue.Runner, id => Commands?.TryRun(id[ContributionIds.CommandPrefix.Length..]) ?? false, dispatcher)` and hands it to
  `CommandBar.RunTarget`; the previous session is disposed after that, as today.
- `ShellViewModel.AttachCommands` sets `CommandBar.RunTarget = RunTarget` after it rebuilds the command bar.
  `CommandBarViewModel.RunTarget` (setter) stores it and sets it as the `Accessory` of the slot whose `Items` hold
  `BuildContributions.RunId` (null on every other slot), and sets `RunTarget.RunEntry` to that item, on the setter and on
  every `Rebuild`.
- `IShell.RunTarget` is answered by `DockShellAdapter` from its `ShellViewModel`; `SelectRunProfileCommandHandler`
  reads it from `context.Shell`.

**Flow (`ProjectSessionViewModel.RunFlowAsync`).**
1. Wait for the save.
2. `resolution = await Runner.PlanAsync(runCancellation.Token)`.
3. When blocked: write each problem's `ToLine()` with `tracker.WriteNotice`, in list order; report `Run.Blocked` with
   the first error's `Message`; return false. Nothing is built, so nothing clears Output.
4. Build through the session's `BuildAsync`, whose project-system call becomes `Runner.BuildAsync`
   (`BuildStarted` clears Output).
5. Write each warning's `ToLine()` with `tracker.WriteNotice`, whether the build succeeded or not.
6. When the build failed or `runCancellation` was cancelled, return false.
7. `Runner.Start(resolution)`; when the handle it returns has `IsHandedOff`, report `Run.ExternalTerminalStarted` in
   the status bar.

`Start` sets the request flags from `o = Options.OptionsFor(resolution.Profile?.Name)` (the defaults with no profile; the view
model's `DontCaptureOutput` is `!o.CaptureOutput`):
- `CaptureOutput = o.CaptureOutput`;
- `RedirectInput = o.CaptureOutput && !o.ExternalTerminal`;
- `ExternalTerminal = o.ExternalTerminal`.

Then, before `launcher.Start`, it calls `tracker.Announce(lines)` once per start (an empty list included, so a pending
list never outlives the start it was made for) with:
- on the first run of an `Executable` profile in this runner's life: `Run.FirstRun.Header` (profile name),
  `Run.FirstRun.Program` (the request's `FileName`), `Run.FirstRun.Arguments` (`CommandLineSplitter.Join` of the
  arguments), `Run.FirstRun.WorkingDirectory`, `Run.FirstRun.Added` (names joined with `", "`, only when
  `AddedEnvironment` is not empty) and `Run.FirstRun.Removed` (only when `EnvironmentRemovals` is not empty);
- on every run with capture off: `Run.NotCaptured`; on every run in an external terminal:
  `Run.ExternalTerminalStarted` instead.
When `launcher.Start` throws (a `Process.Start` failure, a `TerminalNotFoundException`), the runner calls
`tracker.Announce([])` and rethrows, so a failed start leaves no first-run block for the next run (B-8). After `launcher.Start` it calls `tracker.AttachInput(handle)` when
`RedirectInput` is set. Compile's build also goes through `Runner.BuildAsync`. `RunCommandHandler.CanExecute` becomes
`Session is { IsRunning: false, IsBuilding: false, Project.CanCompile: true, CanRun: true }`, with
`ProjectSessionViewModel.CanRun => Runner.CanRun`. `Runner.Changed` raises the session's `CommandStatesChanged`
through `context.Dispatcher.Post`.

**Seams.**
- Ports: `IRunProjectSystem`, `IProcessLauncher`, `IEditorStateStore`, `ISettingsStore` (through `Func<RunSettings>`).
- Signals: `ProjectRunner.Changed`, `RunOptionsStore.Changed` (the runner recomputes and raises its own `Changed`),
  `RunTargetViewModel.DropDownRequested` (consumed by `OpenFlyoutOnRequestBehavior`, a `Behavior<SplitButton>` that
  calls `AssociatedObject.Flyout?.ShowAt(AssociatedObject)`).
- Data: `RunSessionState`.

**First red test.** `ProjectRunnerTests.ALibraryWithAnExecutableProfileCanRun`, with `CanRun` stubbed to
`OutputType == Executable`: it fails on the assertion.

**Cost.**
- Per command-state pulse (every command-bar refresh): `CanRun` is a field read, O(1). No file access ever happens on
  a pulse.
- Per drop-down open: one file read and parse (C-01).
- Per option change: one session load and save, a few KB.
- Per run: C-06's plan plus the build.

---

## C-09 · Stop, the process tree and the unload prompt (T120)

**Data owners.**
- `LaunchedProcess` owns the stop sequence of its process.
- `ProjectRunner` owns the live handle, whether the live run is `Executable`, its profile name (`LiveProfileName`),
  and what Leave means (`tracker.Forget()`, and the handle is dropped without a kill). It is the only code that kills
  a started program: the launcher gets `CancellationToken.None` (C-08), so no token cancellation reaches the process.
- `ProjectSessionViewModel.runCancellation` guards only the gap between the build and `Runner.Start`.
- `ShellProjectActions` owns the order of the questions.
- `RunSettings.StopTimeoutSeconds` (settings store) owns the timeout.

**Pattern.** A strategy chosen once per process, written as a smart enum with nested members: `ProcessCloseRequest.Windows`
(`CloseMainWindow()`) and `ProcessCloseRequest.Posix` (`PosixSignals.Terminate`).

Rejected:
- Plain polymorphism through an interface with two classes registered by OS in DI: the choice never changes at run
  time, so DI adds nothing, and the platform-branch test needs `For(isWindows)` anyway.
- An inline `if (OperatingSystem.IsWindows())`: it leaves no seam for the Windows branch's unit test.
- Graceful stop on application exit: the shutdown does not wait for it, so the program could get SIGTERM and never the
  kill.
- Keeping the kill-on-cancel token and skipping the cancel when detached: two owners of one kill, and `Dispose` would
  have to know about Leave.

**Types.**
- `ProcessCloseRequest`: asks one process to close. It depends on `Process` and `PosixSignals`.
- `PosixSignals`: `kill(pid, 15)` through `[LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]`.
  `NetPrints.Editor.csproj` gains `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`; the only unsafe code is generated.
- `RunningProgramDialogViewModel : DialogViewModel<RunUnloadChoice>`: Leave, Stop and Cancel commands; Cancel is the
  default on close.

**`LaunchedProcess.StopAsync(timeout, ct)`.**
1. When the process has exited, return.
2. `sent = closeRequest.Send(process)`. When not sent, `Kill()`.
3. Otherwise, `WaitForExitAsync` with `timeout` (a linked CTS); on the timeout, `Kill()`.
4. Await the exit, then log 1090 with `closed` or `killed` and the elapsed time.

**`ProjectSessionViewModel`.**
- `Runner` (C-08), `HasLiveExecutableRun => Runner.HasLiveExecutableRun`.
- `Task StopAsync()`: `runCancellation?.Cancel()`, then `await Runner.StopAsync(CancellationToken.None)`.
- `void Kill()`: `runCancellation?.Cancel()`, then `Runner.Kill()`.
- `Stop()` is removed; its callers move to `StopAsync` (the `stop` command) or `Kill` (the exit path and the two
  tests that called it).
- `Dispose()`: as today (it still cancels and disposes `runCancellation`), then `Runner.Dispose()`, which kills the
  live run unless `Detach()` dropped it.

**`ShellProjectActions`.**
- `ConfirmUnloadAsync`:
  1. When `session.Runner.LiveProfileName is { } programName` (non-null exactly when `HasLiveExecutableRun` is true),
     `choice = await dialogs.ConfirmLeaveRunningAsync(programName)`;
     Cancel returns false.
  2. `ConfirmUnsavedChangesAsync` (today's body, made private); false returns false.
  3. Apply: Leave calls `session.Runner.Detach()`; Stop awaits `session.StopAsync()` inside
     `shell.StatusBar.BeginBusy(StoppingProgramText)`, a new `private const string StoppingProgramText =
     "Stopping the program…";` next to the class's `WaitingForBuildText`.
- `ConfirmExitCoreAsync` calls `ConfirmUnsavedChangesAsync` instead of `ConfirmUnloadAsync`, and replaces
  `shell.Session?.Stop()` with `shell.Session?.Kill()`.
- `StopCommandHandler.ExecuteAsync` returns `context.Session?.StopAsync() ?? Task.CompletedTask`.

**Seams.**
- Ports: `IEditorDialogs.ConfirmLeaveRunningAsync` (doubles: `RecordingDialogs` in Editor.UITests and `FakeDialogs` in
  Editor.Tests, which answer from a `Queue<RunUnloadChoice>` and return `Cancel` when it is empty), `ISettingsStore`
  (through the runner's `Func<RunSettings>`).
- OS: `kill(2)` and `CloseMainWindow`.

**First red test.** `LaunchedProcessStopTests.AProgramIgnoringSigtermIsKilledWithItsChildAfterTheTimeout` (Linux and
macOS): echo tool with `sleep,ignore-sigterm,spawn-child` and a 1-second timeout. With `StopAsync` stubbed as an
immediate `Kill` it fails on the outcome `killed after ≥ 1 s`; it asserts that both pids are gone.

**Cost.** Per stop: one syscall, then a wait of at most the timeout. Nothing per frame.

---

## C-10 · Console input and the external terminal (T123)

**Data owners.**
- The handle owns `CanWriteInput` (C-05).
- `RunStateTracker` publishes the current run's handle (`Input`, `InputChanged`).
- `OutputPanelViewModel` owns only `InputText`.
- `RunSettings.LinuxTerminal` owns the Linux terminal command.
- The script file is owned by the script: it deletes itself.
- `TerminalStart` (one member per OS) owns how a request is started, the script's extension and whether NetPrints
  follows the started process (`FollowsProgram`).
- `ProjectRunner` owns the hand-off: a handle whose `IsHandedOff` is true is detached at once (C-09 `Detach()`).

**Pattern.**
- The terminal launch is a strategy per OS, a smart enum `TerminalStart` (`internal abstract`, private nested sealed
  members exposed as `TerminalStart.Windows`, `MacOS`, `Linux`, and `Current`), each
  `ProcessStartInfo Create(ProcessStartRequest request, string? scriptPath, IReadOnlyList<string> terminalCommand)`:
  - `Windows`: the program itself, `ArgumentList` = the request's arguments, `WorkingDirectory`, `UseShellExecute`
    false, `CreateNoWindow` false, no redirection, removals then variables applied to `Environment`; `scriptPath` and
    `terminalCommand` unused.
  - `MacOS`: `open` with `-a`, `Terminal`, `scriptPath`; `terminalCommand` unused.
  - `Linux`: `terminalCommand[0]` with the rest of `terminalCommand`, then `/bin/sh`, then `scriptPath`, as
    `ArgumentList` (two appended arguments, so no launcher takes its single-argument path, ADR-0042).
  - Each member also has `bool FollowsProgram` (`Windows` true; `MacOS` and `Linux` false) and
    `string ScriptExtension` (`Windows` "", `MacOS` ".command", `Linux` ".sh").
- `TerminalScript.Build(ProcessStartRequest)` is a pure text builder (golden-tested).
- `LinuxTerminals.Candidates` is the ordered table `["xdg-terminal-exec", "--"]`, `["x-terminal-emulator", "-e"]`,
  `["ptyxis", "--"]`, `["gnome-terminal", "--"]`, `["kgx", "--"]`, `["konsole", "-e"]`, `["xfce4-terminal", "-x"]`,
  `["xterm", "-e"]`. `LinuxTerminals.Detect(Func<string, bool> onPath)` returns the first whose program `onPath` finds,
  or null. `LinuxTerminals.NotFoundMessage` is built from `Candidates` (text in contracts §3). `LinuxTerminals.OnPath`
  is the real probe: a file named `name` in any `PATH` entry.

Rejected:
- A pseudo-terminal in Output (R23: a much larger feature).
- `cmd /k` on Windows (it re-parses the arguments, the FR-056 reason).
- `export NAME=value` lines in the script: `export` cannot set names that are not shell identifiers, while
  `env -u A NAME=value prog args` handles any name and does the removals in the same step.
- Following the terminal process on Linux and macOS: its lifetime depends on the emulator (`gnome-terminal` returns at
  once, Debian's `x-terminal-emulator` wrapper adds `--wait`, `ptyxis --`, Konsole by default and `xterm` live with the
  window) and its exit code is not the program's.
- Appending only the script path: Debian's `xfce4-terminal.wrapper` turns one `-e` argument into a re-parsed
  `xfce4-terminal -e <string>`.
- A console helper on Windows and a `run.macTerminal` setting: later (ADR-0042).

**Types.**
- `ITerminalLauncher` / `TerminalLauncher(Func<RunSettings> settings, string scriptDirectory)`: starts one request in a
  window. Composition: `[Singleton<ITerminalLauncher>(Factory = "Create")]` with
  `internal static TerminalLauncher Create(ISettingsStore settings)` returning
  `new(() => settings.Get(RunSettings.Descriptor), Path.Combine(EditorDataPaths.Resolve().StateDirectory, "terminal"))`,
  so the container that builds `ProcessLauncher` (`[Singleton<IProcessLauncher>]`) can resolve it; `EditorDataPaths`
  is not registered. `Start(request, startId)`: on Windows, `TerminalStart.Windows.Create(request, null, [])`; on macOS
  and Linux, write the script, and on Linux take `CommandLineSplitter.Split(settings().LinuxTerminal)` when it has at
  least one element, else `LinuxTerminals.Detect(LinuxTerminals.OnPath)`, else throw
  `new TerminalNotFoundException(LinuxTerminals.NotFoundMessage)`; then `Process.Start` of the
  `TerminalStart.Current.Create(...)` result. A `Win32Exception` from `Process.Start` deletes the script and throws
  `TerminalNotFoundException` with `The terminal command '{0}' could not be started: {1}. Check run.linuxTerminal in
  Settings › Run.`
- `TerminalLauncher.Create` script directory:
  `OperatingSystem.IsMacOS() ? Path.Combine(Path.GetTempPath(), "NetPrints", "terminal") :
  Path.Combine(EditorDataPaths.Resolve().StateDirectory, "terminal")`.
  `TerminalLauncher.FollowsProgram => TerminalStart.Current.FollowsProgram`.
- `ProcessLauncher`'s `ExternalTerminal` branch: `terminals.Start(request, id)`, then `ProcessStarted`, no reads.
  - When `terminals.FollowsProgram`: `EnableRaisingEvents`, `ProcessExited` when that process exits; the returned
    `LaunchedProcess` has `CanWriteInput` false.
  - Otherwise: the handle's `IsHandedOff` is true and `CanWriteInput` false. No `ProcessExited` or `OutputReceived` is
    raised for that id, and the `Process` is disposed when it exits.
- `ProjectRunner.Start`: after `launcher.Start`, when the handle's `IsHandedOff`, call `Detach()`.
- `ProjectSessionViewModel.RunFlowAsync` step 7: when the handle returned by `Runner.Start` has `IsHandedOff`, report
  `Run.ExternalTerminalStarted` in the status bar.
- A `TerminalNotFoundException` reaches `ProjectSessionViewModel`, which writes its message
  with `tracker.WriteNotice` and reports it in the status bar, as a blocked run does.
- `TerminalScript`: request to `sh` text. Exactly:
  ```sh
  #!/bin/sh
  rm -f -- "$0"
  cd -- '<cwd>' || exit 1
  env -u '<removed>' … '<NAME>=<value>' … '<program>' '<arg>' …
  status=$?
  printf '\n[Process exited with code %s. Press Enter to close.]\n' "$status"
  read _
  ```
  Each `'` inside a value is written as `'\''`. The file is written to
  `<scriptDirectory>/<start id>-<8 random hex><TerminalStart.Current.ScriptExtension>` (`scriptDirectory` is
  `<EditorDataPaths.StateDirectory>/terminal` in production, the per-user temp folder `NetPrints/terminal` on macOS;
  the folder is created when missing) with mode 0700 (`File.SetUnixFileMode`). The random part
  (`RandomNumberGenerator.GetHexString(8, lowercase: true)`) keeps two editor instances, whose start ids both begin at
  1, from writing the same file.
- `LinuxTerminals`: the detection order of ADR-0042 and contracts/run-profiles.md §3 ("External terminal"), above.
- `OutputPanelViewModel` gains `InputText`, `IsInputAvailable` (`tracker.Input is { CanWriteInput: true }`),
  `SendInputCommand` (writes the line to `tracker.Input`, adds `> <text>` with `tracker.WriteNotice`, clears
  `InputText`) and `CloseInputCommand` (`tracker.Input?.CloseInput()`). It listens to `tracker.InputChanged` through
  the existing dispatcher queue.

**Seams.**
- Ports: `ITerminalLauncher` (used only by `ProcessLauncher`), `RunStateTracker.Input`.
- Data: the script text.
- In XAML, the input row is a `TextBox` with an Enter `KeyBinding` to `SendInputCommand` and two buttons, visible on
  `IsInputAvailable`. It is wired with no code-behind (avalonia-behaviors).

**First red test.** `TerminalScriptTests.EveryArgumentIsSingleQuoted` with a stub that joins with spaces: it fails on
the golden text for the argument `it's here`. Then `LinuxTerminalsTests.DetectFollowsTheOrder`: with the five-row table
of the first draft it fails on the first assertion (`xdg-terminal-exec`).

**Cost.**
- Per external run: one file write and one process start; on Linux at most eight `PATH` probes.
- Per keystroke: none (binding).
- Per send: one pipe write.

---

## C-11 · Project settings › Run (T121)

**Data owners.**
- The file owns the profiles. `LaunchSettingsEditor` owns nothing; it is a pure transform of the text.
- `RunSectionViewModel` owns the editing rows of the current snapshot and rebuilds them after each write.
- `ProjectRunner.Refresh()` owns the fact that the run target re-reads.

**Pattern.** Edits as pure text transforms through `JsonNode` (R22), and a view model per profile.

Rejected:
- Serializer round-trip (R22: loses unknown members and order).
- Patching text at offsets: the JSON escaping and the reformatting of nested objects make offsets fragile.
- One view model with an indexer over all fields: the `[i] value` preview and the per-profile read-only reasons are
  per-profile state.

**Types.**
- `LaunchSettingsEditor`, static, in Workspace:
  - `string SetField(string json, string profileName, string field, string? value)` (null removes the member);
  - `string SetEnvironment(string json, string profileName, IReadOnlyList<KeyValuePair<string, string>> variables)`.
  - It parses with `JsonNode.Parse(json, documentOptions: new() { AllowTrailingCommas = true })`, finds the profile
    through `LaunchSettings.Find` semantics (case-insensitive), and writes with `WriteIndented = true`, 2 spaces,
    `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, keeping the original's line ending (`\r\n` when the text has one)
    and its final newline.
  - A file with comments is never passed in, because the view model is read-only then.
- `RunSectionViewModel` (`public sealed partial class … : ObservableObject, IDisposable`) with an `internal`
  constructor `RunSectionViewModel(ProjectRunner runner, IFolderLauncher files, AtomicFileWriter writer)`: internal,
  because `IFolderLauncher` is internal (a public constructor would be CS0051). The file path is
  `runner.Settings.FilePath` when the file exists, else `LaunchSettings.RelativePath` under the project folder (the
  section never creates the file). Writes are UTF-8 without BOM bytes through `AtomicFileWriter`.
  - `Profiles` (`RunProfileEditorViewModel`), `IsReadOnly`, `ReadOnlyReason`, `HasFile`, `OpenFileCommand`
    (`files.Open(path)`).
  - A commit writes the file atomically, then calls `runner.Refresh()` and rebuilds. It listens to `runner.Changed`
    (a drop-down refresh may have read a newer file) and unsubscribes in `Dispose`.
- Construction path: `ProjectSettingsDocumentViewModel` (built by `ShellHost.OpenDocument` as
  `new ProjectSettingsDocumentViewModel(session, context)`) gains `public RunSectionViewModel Run { get; }`. Its public
  constructor chains to a new `internal ProjectSettingsDocumentViewModel(ProjectSessionViewModel session,
  EditorContext context, IFolderLauncher files)` with `new ShellFolderLauncher()`; that one builds
  `Run = new RunSectionViewModel(session.Runner, files, new AtomicFileWriter(new RealEditorFileSystem()))`. Tests use
  the internal constructor with a recording `IFolderLauncher`. The document disposes `Run` in its `Dispose(bool)`
  override, next to the project unsubscription. Nothing is registered in a container.
- `RunProfileEditorViewModel`:
  - `Name` and `Kind` (shown, not editable: renaming a profile or changing its kind is done in a text editor; the
    section adds and removes no profile), `IsEditable` (`Project` or `Executable` and the section not read-only),
    `ExecutablePath`, `Arguments`, `ArgumentPreview` (`[i] value` per `CommandLineSplitter.Split(Arguments)` entry),
    `WorkingDirectory`, `Environment`, `EnvironmentError`, `ShowWorkingDirectoryWarning` (true when
    `WorkingDirectory` is not empty and not `Path.IsPathRooted`).
  - `Environment` is one `string`, one `NAME=VALUE` per line in file order. On commit each non-empty line is split at
    its first `=`; a line with no `=` or an empty name sets `EnvironmentError` to the `Run.Section.EnvironmentInvalid`
    text and writes nothing.
  - `CommitCommand` (parameter: the field name, one of `executablePath`, `commandLineArgs`, `workingDirectory`,
    `environmentVariables`) writes that field through `LaunchSettingsEditor` (an empty text removes the member). Each
    `TextBox` invokes it on `LostFocus`; the single-line ones also on Enter (avalonia-behaviors). Never per keystroke.

**Seams.**
- Ports: `IFolderLauncher.Open(string path)` (new member; `ShellFolderLauncher` does nothing when `!File.Exists(path)`,
  else `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })`, as `Reveal` does for a folder; the two
  `RecordingFolders` doubles in Editor.Tests record it), the file writer.
- Signal: the runner's `Changed`, which updates the button.

**First red test.** `LaunchSettingsEditorTests.AnEditKeepsUnknownMembersAndOrder`, with a stub that re-serializes
through `JsonSerializer`: it fails on the byte comparison with `after.json`.

**Cost.** Per commit: one parse, one write, one re-read. Per keystroke: the preview's `Split`, O(n), only while the
Arguments field has focus.
