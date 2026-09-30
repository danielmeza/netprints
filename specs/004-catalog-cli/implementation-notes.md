# Implementation Notes: P2 — Catalog tooling and Spectre CLI

## Decisions

- `SuppressDependenciesWhenPacking` on NetPrints.Annotations fixes NU5128; the dependency group it drops is empty, as in NetPrints.Sdk.
- `ExcludeAssets="runtime"` on the MSBL001 exclusions in NetPrints.Cli.Tests, same shape as NetPrints.Core.Tests.
- verify-packages.sh work was pulled forward from T075; only the targets-file check remains for T075.

## Checkpoint reports

### Checkpoint A

**Status**: ✓ Green

**Build**: Solution builds with 0 warnings (Release mode).
- `dotnet build -c Release -v q -tl:off --nologo`: 23 projects, 0 errors, 0 warnings.

**Test Suite**:
- Catalog.Tests (Release): 1 passed
- Cli.Tests (Release): 1 passed
- All projects build and tests pass cleanly.

- Whole suite: 975 tests, 965 passed, 10 skipped. E2E run: green.

**CI**: green run 36654324173 at 0b57512. The Release runs at 355905b and 0613745 failed and were fixed by 0613745 and 0b57512.

## Batch B1 (T008-T011) — API tracking

- T010: `dotnet format analyzers src/<P>/<P>.csproj --diagnostics RS0016 RS0037 --severity info` applied for every project. Entries: Core 1251, Extensibility 338, Reflection 169, Serialization 899, Catalog 0 (no public API yet). Core and Reflection moved to `PublicAPI.Shipped.txt` (`git diff v0.1.1` on them shows only the new `NetPrintsTrackPublicApi` property); the rest stay in Unshipped.
- T011: no other analyzer diagnostic (RS0026, RS0027, RS0041, ...) fires, so no API change was needed.
- T008: the test project references the analyzer package only for its path (`GeneratePathProperty`); `ExcludeAssets` does not keep its analyzer off the compilation, so a target removed that `Analyzer` item (replaced in Review B by a `PackageDownload`, see below).

## Batch B2 (T012-T016a) — experimental API opt-in

### Decisions

- T013: `Directory.Build.targets` sets `NoWarn` inside `<Target Name="NetPrintsExperimentalOptIn" BeforeTargets="CoreCompile">`. The property form in the task text (a top-level `<NoWarn>$(NoWarn);@(Items)</NoWarn>`) keeps the literal `@(Items)` at evaluation time; the reference is expanded only when the property is used as a task parameter such as Csc `DisabledWarnings`, so it would have produced a bogus `NoWarn` entry, not an empty one (corrected after Review B). The target form expands the items at build time. Limitation: evaluation-time readers (`dotnet msbuild -getProperty:NoWarn`) see no NPXE ids; design-time builds run the target and do see them.
- T013: observed compiler behaviour (ADR-0017 decision 3): the diagnostic is reported for uses inside the defining assembly too. `NetPrints.Core` needs `NPXE0003` (ClassTranslator, TranslationEnvironment) and `NetPrints.Extensibility` needs `NPXE0001`, `NPXE0002` and `NPXE0003`. Other opt-ins, each only for the ids the build showed: Editor 1+2, Desktop 2, TestExtension 1+2+3, Core.Tests 1+2+3, Editor.Tests 1+2, Editor.UITests 2.
- T014: `ExperimentalApiIds` is in namespace `NetPrints.Core`. `NPXE0004` and `CatalogProfiles` are declared now and marked in T044.
- T014: PublicApiAnalyzers writes experimental symbols as `[NPXE000n]Symbol` in the API files, and members of an experimental type carry the prefix too. The `dotnet format analyzers` fixer does not emit the prefix and adds nothing when the shipped file already lists the type. The `ExperimentalApiIds` entries came from the fixer (run with an emptied Shipped file, then Shipped restored); the `[NPXE0003]` lines and `*REMOVED*` lines for the Core emitters (shipped in v0.1.1 without the attribute) and the prefixed lines in Extensibility's Unshipped file were written by script from the analyzer's own RS0016/RS0017 messages, then the build was clean.
- T012: the literal gate allows the one `NPXE0004` duplicate (`AllowedDuplicateCode`): the generator compiles `Catalog/Engine/ExperimentalApis.cs` and cannot reference Core. The declaring files it lists do not exist yet (Catalog engine files arrive in later batches).
- T012: hygiene gates added to `SourceHygieneTests` (rewritten on `XDocument` in Review B): `TheOnlyNoWarnIsTheExperimentalOptInLine`, `ExperimentalOptInItemsLiveOnlyInProjectFiles` (no opt-in in props/targets, so nothing is inherited), `EveryExperimentalOptInIsADeclaredId`, `NoPragmaDisablesAnExperimentalDiagnostic`, `NoEditorConfigSeverityForAnExperimentalDiagnostic`; `NoUnlistedBuildWarningSuppressions` allows only the exact opt-in line.
- The in-test extension compiler (`ExtensionTestSupport.Compile(..., params string[] optIn)`) suppresses only the ids the caller passes through `WithSpecificDiagnosticOptions`, the compilation equivalent of an opt-in item (ADR-0017 Consequences); it is neither a NoWarn nor a pragma.
- T016a (owner-approved): `SolutionHygieneTests.EverySourceAndTestProjectIsInTheSolution`; `samples/`, `legacy/` and `docs/` are out of scope (samples load the Generator from `bin/`).

### Red/green evidence

- T012 red (before targets/ids existed): `ExperimentalApiTests` 3 of 6 failed (no NPXE error without opt-in); `SourceHygieneTests` 2 of 13 failed (`TheOnlyNoWarnIsTheExperimentalOptInLine`, `EveryExperimentalOptInIsADeclaredId`). Green after T013/T014.
- Negative checks of the gates (temporary, reverted): an opt-in `NPXE0009`, a `#pragma warning disable NPXE0001`, a `dotnet_diagnostic.NPXE0001.severity` globalconfig line, an opt-in item in a `.props` and a `<NoWarn>` in another `.props` each failed the matching gate (7 failures).
- T016a: the test was written before the check and shown red by removing the `NetPrints.Testing.Ui.csproj` entry from `NetPrints.slnx` (1 of 1 failed); entry restored, green.
- The full suite found 3 failures the filtered runs missed (in-test extension sources use the experimental emitter API); fixed with the suppression above. A later run hit a flake in the new probe test (it referenced every loaded assembly, including a temporary extension assembly another test deletes); it now references the trusted platform assemblies plus Core and Extensibility.

### Checkpoint B

**Status**: green (SC-011), after Review B (T017, T018)

- `dotnet build -c Release`: 23 projects, 0 errors, 0 warnings. `dotnet format NetPrints.slnx --verify-no-changes`: clean.
- Whole suite (Release, no E2E env): 1015 tests, 1005 passed, 10 skipped, 0 failed.
- E2E (`NETPRINTS_E2E=1`, `--fail-skips on`): 9 tests, 9 passed, 0 skipped.
- Only `<NoWarn>` in the repository: the `Directory.Build.targets` opt-in line.
- CI: green run 36664521413 at 9280856 after a rerun. The first attempt failed only in `Test (Editor UI, headless)`: `SnapshotTests.Inspectors` ('inspector-class' 0.893% of pixels differ, max 0.5%), a rendering flake in code this batch does not touch (the same test passed locally in the whole suite and on 854066f); the rerun passed.

### Review A (T006, Opus) — PR #9 review 5360487391

0 blocking findings, 4 Low and 2 Nit, all fixed in T007:
- Low: both SmokeTests asserted a constant; now they pin the CLI assembly (and InternalsVisibleTo) and the Catalog assembly load, and the classes are sealed.
- Low: Checkpoint A lacked whole-suite totals, E2E result and the CI run; added.
- Low: `SuppressDependenciesWhenPacking`, the MSBL001 exclusions and the verify-packages.sh pull-forward are recorded under Decisions.
- Nit: redundant `Version="5.0.0"` dropped in Annotations (`VersionOverride` alone restores 5.0.0).
- Nit: Directory.Build.props comment now says six packages.

Heads-up for sub-phases D/E: any netstandard2.0 dependency of the generator must be bundled into `analyzers/dotnet/cs`, because `SuppressDependenciesWhenPacking` silently drops nuspec dependencies.

### Review B (T017, Opus) — PR #9 review 5361050457

11 findings (2 High, 4 Medium, 4 Low, 1 Nit), all fixed in T018 (commits 919f1c8 and 9280856):

1. High, help URL 404: `UrlFormat` had a `/docs/` segment the site (`routeBasePath: '/'`) does not serve. Fixed in `ExperimentalApiIds`, `PublicAPI.Unshipped.txt`, contract §5 and research.md. AP-T02 now checks the URL maps to an existing `docs/**/*.md` page with an `## API stability` heading (red first: the mapped path `docs/docs/guide/extensions.md` did not exist).
2. High, unmarked public API around experimental types: a reflection test (`EveryPublicSymbolThatMentionsAnExperimentalTypeIsExperimentalWithTheSameId`) checks that every public symbol in Core and Extensibility whose signature (base type, interfaces, parameters, return, property, field, event, generic arguments) mentions an `[Experimental]` type carries that id. Red on the old code with 18 offenders (`TranslationEnvironment` members and constructor, `ExtensionSettingsDescriptor<T>`, `JsonFileSettingsStore`, `ExtensionRegistry.HostChannels/FindHostChannel/Settings/ClassEmitters/MemberEmitters`, `NullHostChannel`, `InMemoryHostChannel` and their members). Decision: none of these became `internal`. `NullHostChannel`, `InMemoryHostChannel` and `JsonFileSettingsStore` are public API in the 003 contracts (extension-points.md §6-§7) and the test kit and embedding hosts use them (TestExtension uses `InMemoryHostChannel`), so each is marked with its id (host channel, host channel, settings); `ExtensionSettingsDescriptor<T>`, `NetPrintsSettings.Descriptor` and the registry members are marked with their ids. `TranslationEnvironment` stays stable as a type; its constructor, `Deconstruct` (now explicit, so it can be marked) and the two emitter properties carry `NPXE0003` (`[method:]`/`[property:]` targets on the positional record). Editor.UITests gained the `NPXE0001` opt-in the build then showed. API files: for Core the shipped `TranslationEnvironment` lines got `*REMOVED*` plus `[NPXE0003]` lines in Unshipped; for Extensibility the unshipped lines were prefixed in place; both from the analyzer's own RS0016/RS0017 messages, then the build was clean.
3. Medium, break of the shipped Core emitters: documented in the guide's "API stability" section, ADR-0017 Consequences and a new "Unreleased" section in `.github/release-notes.md` (the repo has no changelog; v0.1.x releases used the release-notes template plus GitHub generated notes).
4. Medium, only NPXE ids in the opt-in target: an `<Error>` guard in `Directory.Build.targets` rejects any item not matching `^NPXE\d{4}$`. `ExperimentalOptInTargetTests` builds a temp project against the real targets file: red before (a `CS8602` item built), green after; an `NPXE0003` item still builds.
5. Medium, regex gates: the opt-in and warning-bar gates now parse build files with `XDocument` and match elements by local name (`BuildFileRules`). Red evidence: a temporary `probe-tmp/Probe.csproj` holding `<NetPrintsExperimentalOptIn Condition="true" Include="CS8602" />`, `Include='IDISP001'`, a conditioned `<NoWarn Condition=...>` and `<NoWarn >` passed all 13 old gates; the same probe failed `EveryExperimentalOptInIsADeclaredId`, `TheOnlyNoWarnIsTheExperimentalOptInLine` and `NoUnlistedBuildWarningSuppressions` with the new gates. Probe deleted. `BuildFileRulesTests` keeps every variant as a permanent case.
6. Medium, blanket test opt-in: `ExtensionTestSupport.Compile(..., params string[] optIn)`; the two emitter sources pass `ExperimentalApiIds.Emitters`. `ATestExtensionMustListTheExperimentalIdsItUses` shows a source without its id, or with another id, fails to compile (it could not pass with the old blanket list). ADR-0017 Consequences record the mechanism.
7. Low, other analyzer config files: `GlobalAnalyzerConfigFiles` and `EditorConfigFiles` items in any build file fail `NoUnlistedBuildWarningSuppressions` (probe and unit cases).
8. Low, analyzer removal: `DropPublicApiAnalyzerFromTests` and the `PackageReference` are gone; `NetPrints.Core.Tests` uses `PackageDownload Version="[$(PublicApiAnalyzersVersion)]"` and the path `$(NuGetPackageRoot)microsoft.codeanalysis.publicapianalyzers/$(PublicApiAnalyzersVersion)`; the version property lives in `Directory.Packages.props` and feeds the `PackageVersion`. `NoUnlistedBuildWarningSuppressions` now rejects any `<Analyzer Remove>` item.
9. Low, wrong T013 reasoning: corrected above, with the evaluation-time limitation recorded.
10. Low, NPXE0004 duplicate: `IsAllowedDuplicate` allows exactly two declarations, one in `NetPrints.Core/ExperimentalApiIds.cs` and one in `NetPrints.Catalog/Engine/ExperimentalApis.cs`; `declaredCodes` tracks file and code. Unit cases: a third declaration, a repeat in one file and a wrong file each fail.
11. Nit, redundant pragma test: probe first: `tests/probe-tmp/P1.cs` with `# pragma warning disable NPXE0001` and `P2.cs` with a bare `#pragma warning disable` were both rejected by `NoUnlistedSuppressions` (and passed the old regex test), so `NoPragmaDisablesAnExperimentalDiagnostic` was dropped and ADR-0017 decision 5 says so. Probe deleted.

Deviations from the task: none of the marked types was made internal (finding 2, reason above). Finding 6 has no red run of its own: its test needs the new overload, and the old blanket list could never fail it. Findings 7 and 8 were shown red by the same probe (its `GlobalAnalyzerConfigFiles`, `EditorConfigFiles` and `<Analyzer Remove>` items passed the old gates).

## Batch C1 (T019-T022) — Spectre CLI skeleton, project resolution, build

### Decisions

- Every CLI type is `internal` (tests see it through the existing `InternalsVisibleTo`), so nothing enters `PublicAPI.Unshipped.txt`; `NetPrints.Cli` is not an API-tracked project.
- Commands are listed once in `CliCommandCatalog.All` (name plus a registration action); `CliApplication.ConfigureCommands` walks it, and CL-T01 iterates it, so a command added later gets the `--help` check and `ValidateExamples` for free.
- `CliEnvironment` is a class over `currentDirectory`, a `getVariable` function and the stderr `TextWriter` (`FromProcess()` for the real one). Internal-error and usage messages go to `environment.Error`; project-resolution errors and results go to the injected `IAnsiConsole` (stdout), as in P1.
- The project system is injected as `Lazy<IProjectSystem>`: resolving `IProjectSystem` eagerly would load Microsoft.Build before `IMsBuildRegistration.EnsureRegistered` ran. `CliServices.CreateProjectSystem` and `MsBuildRegistrationAdapter.EnsureRegistered` keep the `NoInlining` split.
- `UseStrictParsing()`: without it Spectre silently moves unknown options into the remaining arguments (found by CL-T02: `build --no-such-option` exited 2 only because the test's directory had no project).
- Output is written with `IAnsiConsole.Profile.Out.Writer` (`WriteLineRaw`), not `console.WriteLine`: the latter wraps at the console width, which breaks paths and diagnostics when stdout is redirected (width 80).
- Console settings (`CliServices.ConsoleSettings`): `AnsiSupport.No` and `ColorSystemSupport.NoColors` when the output is redirected or `NO_COLOR` is non-empty, otherwise detection.
- `TypeRegistrar` owns the `ServiceProvider`s it builds and is disposed by `CliApplication.RunAsync` (IDISP004/005/007).
- `TypeResolver` is not disposable: it wraps a provider the registrar owns.
- The pre-parse rejects the P1 flags only in the leading options (before the command name or `--`); after the command name they are ordinary unknown options, so `run <project> -- -r` will forward `-r`.
- T022: the old `CliBuildTests` cases `SuccessfulRunPropagatesTheChildsNonZeroExitCode` and `SuccessfulRunWithZeroChildExitCodeReturnsExitCode0` exercise the run path, which `BuildCommand` does not have; they move to `RunCommandTests` in T023 (CL-T05). The project reference from `NetPrints.Core.Tests` to `NetPrints.Cli` existed only for `CliBuildTests` and is removed with the file.
- T022: the messages changed from P1's `Compiling ...`/`Compilation succeeded.`/`Compilation failed with N errors:` to the contract's `Build succeeded.` / `Build failed with N error(s).` (no header line).

### Deviations

- CI: the "CLI smoke" step now asserts exit 0 (was 2) and "CLI sample compile and run" runs `build samples/HelloWorld/HelloWorld.csproj` and greps `Build succeeded.`, because P1's `-p/-r` flags are rejected from C1 on and CI must stay green until T026 rewrites both steps (`run` arrives in C2).
- `scripts/verify-packages.sh` step 4 ran the installed tool as `netprints -p <csproj> -r` (the CI "Packages (local feed)" job failed on the P1-flag message at c35dc4f); it now runs `netprints build <csproj>` and greps `Build succeeded.`. T075 or T026 may switch it to `run` once C2 lands.
- The tests and the code were written in one pass per task file rather than committed per task: T019-T022 are one commit, because T019's suite needs the build command (T022) to satisfy `ThereIsAtLeastTheBuildCommand` and `--help` over the catalog.
- `samples/HelloWorld/Compiled_HelloWorld/` (ignored, dated 2026-09-25, a P1 leftover) makes a local `netprints build samples/HelloWorld` fail with CS0101; a clean copy builds. Not touched (run output), CI checks out clean.
- Commit 0613745's message lost `$(TargetPath)` to shell expansion.

### Red/green evidence

- Red for T019, T021 and T022 (tests first): with the three test files and `Support/CliTestHost.cs` written and `CommandLineParser` already replaced by `Spectre.Console.Cli` in the csproj, `dotnet build tests/NetPrints.Cli.Tests` failed (10 errors: the old `Program.cs` no longer compiled, and `CliApplication`, `CliCommandCatalog`, `CliServices`, `ProjectLocator`, `IMsBuildRegistration`, `ExitCodes` did not exist).
- Green after T020-T022 code: first run 36 of 37 passed; the failure `UnknownCommandOptionOrValueExitsWithUsage(build --no-such-option)` (no message on stderr) was real red for strict parsing, green after `UseStrictParsing()`. Cli.Tests: 37 passed.
- The whole-suite run found `SourceHygieneTests.NoNullForgivingOperator` failing on three `Path.GetDirectoryName(...)!` in the new tests; fixed without `!`.

### Checkpoint C1

- `dotnet build -c Release`: 0 warnings, 0 errors. `dotnet format NetPrints.slnx --verify-no-changes`: clean.
- Whole suite (Release): 1047 tests, after the fix 0 failed (Core.Tests 566 passed, Cli.Tests 37 passed); E2E (`--fail-skips on`): 9 of 9 passed.

### Batch C2 (T023-T026) — run, migrate, CI

### Decisions

- `ProjectCommandBase.ExecuteProjectAsync` now receives the `CommandContext` (`run` reads `Remaining.Raw`) and exposes `Environment` as a protected property, so subclasses do not capture the constructor parameter a second time.
- `run` appends `--` plus the arguments after `--` to `GetRunCommand`'s arguments only when there are any (`dotnet run ... --no-build -- a b`); the child's stdout goes to the console output, its stderr to `CliEnvironment.Error`, and its exit code is the command's. The two run cases dropped from `CliBuildTests` are ported into `RunCommandTests`.
- `migrate` does not derive from `ProjectCommandBase` (it takes several paths). A `.csproj` argument, or no argument, resolves the project (`ProjectLocator`), checks the SDK (exit 3) and reads `ProjectSnapshot.GraphFiles`; a directory is searched recursively for `*.netpc.json`, skipping `bin` and `obj`; a `.netpc.json` file is read directly; another file or a missing path exits 2.
- Each graph is read through the `IDocumentFormat` that `DocumentFormatRegistry.Find` resolves, from a `FileSystemDocumentStore` (`watch: false`) rooted at the graph's directory with the file name as id. A `DocumentVersionException` prints `<path>: schema <found> is not supported (this tool supports <supported>)`; any other read failure prints `<path>: unreadable: <reason>`; both exit 1 after the remaining graphs are reported, with the last line `N of M graph(s) could not be read.` (the contract defines the last line only for success).
- The registry uses the built-in node converters only (no extension node converters yet); C3 wires the project's extension folders in (see Batch C3).
- `NetPrints.Cli` references `NetPrints.Serialization` directly (it was not transitive).
- T026: CI "CLI smoke" also runs `--help`; "CLI sample compile and run" runs `run samples/HelloWorld/HelloWorld.csproj` and greps `Hello, World!`; `scripts/verify-packages.sh` step 4 runs the installed tool as `netprints run` and greps `Hello, World!`.

### Red/green evidence

- Red (T023, tests first): `RunCommandTests` and `MigrateCommandTests` written with the `FakeProjectSystem.LoadAsync`/`GraphFiles` support, commands not registered: 16 of 17 failed (unknown command, exit 2). Green after T024/T025: 17 of 17 passed.
- Manual: `migrate samples/HelloWorld/HelloWorld.Program.netpc.json` reports schema 1; `run` of a clean copy of the sample prints `Hello, World!`, exit 0.

### Batch C3 (T027-T031) — generate, regen --check, CI graph check

### Decisions

- `GeneratedFileResult` gains `UpToDate` (last, defaulted parameter): true when the output already held the rendered bytes before the call, false for a missing or different file and for any error. `Written` stays "the file was rewritten", so Write reports stale as `Written` and fresh as `UpToDate`, Check reports stale as neither. `GenerateAsync(request, mode, ct)` is a new overload; the two-argument one calls it with `Write`, so `Generator/Program.cs` is unchanged. `NetPrints.Generation` and `NetPrints.Workspace` are not API-tracked.
- `generate` output: diagnostics as canonical lines, `generated: <path>` per rewritten file, `stale: <path>` per stale file (paths relative to the project directory); last line `N generated file(s) up to date, M written.`, or `M stale.` when `--check` found stale files. Exit 1 on any error diagnostic or stale file; a failing extension folder prints its `NPX` lines, exits 1 and writes nothing. `--graph` must name a graph of the project, otherwise exit 2.
- Found by CL-T08: `IProjectSystem.LoadAsync` opens the project in `MSBuildWorkspace`, whose design-time build runs the SDK's `NetPrintsGenerate` target (BeforeTargets CoreCompile), so `generate --check` silently rewrote stale files (and `Touch`ed fresh ones) before the command looked at them. `ProjectSystemOptions` gets `GenerateOnLoad` (default true); false passes the global property `_NetPrintsSkipGenerate=true` to the workspace (named `NetPrintsSkipGenerate` until Review C part 2 made it internal), and `NetPrints.Sdk.targets` skips the target when it is `true`. The CLI's project system sets it to false (`build` and `run` generate through `dotnet build`, unaffected). Pinned by `GenerateRequestFactoryTests.LoadingWithGenerateOnLoadOffLeavesAStaleGeneratedFileAlone`.
- CL-T13 builds a temp copy of HelloWorld against the in-repo SDK, parses the `netprints.generate.rsp` the target wrote and compares it field by field with `FromSnapshot` of the loaded project (records with list members do not compare structurally).
- The command tests reuse `CliTestHost` through `RunRealAsync` (real `CliServices.CreateDefault()` with the host's console and environment) and a `SampleCopy` fixture; `Cli.Tests` references `NetPrints.Testing` (`LocalSdkLayout`) and builds `NetPrints.Generator` and `NetPrints.TestExtension` first without referencing them. The tests compare content and the output lines. (This note first said mtimes were not a usable "nothing written" signal because the SDK target touches outputs; that stopped being true once `GenerateOnLoad: false` landed, and `CheckNamesTheStaleFileExitsOneAndWritesNothing` now also asserts the mtime is unchanged.)
- CL-T11 (`run` on a temp copy of HelloWorld through the real tool) was already green when written: it pins the C2 behaviour with the real SDK rather than driving new code.
- Migrate now loads the extension folders of the projects it reads (`GraphCodeGenerator.LoadExtensions`) and builds its `JsonDocumentFormat` from the registry's node converters; a failing extension exits 1 with the `NPX` lines. The C2 open item is closed, but correct it: an unknown `$kind` is not "unreadable", `NodeListConverter` keeps it as an `UnknownNodeDocument`, so the report never failed on extension nodes; the wiring matters once a migration writes graphs. Graphs given as files or directories have no project, so only the built-in converters apply to them.
- T030: CI step "Graph checks" runs `regen --check samples/HelloWorld` after the sample run.

### Red/green evidence

- Red (T027): `GenerationModeTests` and `GenerateRequestFactoryTests` written first: `dotnet build tests/NetPrints.Core.Tests` failed (`GenerationMode`, `GenerateRequestFactory`, `UpToDate` missing). Green after T028: 8 of 8 (then 9 with the `GenerateOnLoad` test).
- Red (T029): `GenerateCommandTests` and `HelloWorldCliTests` with no `generate` command: 8 of 9 failed (unknown command). After the command: 4 of 9 still failed until `GenerateOnLoad` (stale files were rewritten by the load); then 66 of 66 Cli.Tests.
- Red/green for `GenerateOnLoad`: with the option in place but the target condition reverted the new Core test failed; with the condition, it passes.
- Red/green for migrate: `AProjectsBrokenExtensionFolderExits1WithItsDiagnostic` failed before the wiring; `AProjectsExtensionNodesAreReadThroughItsExtensionFolders` passed both before and after (see above).

### Checkpoint C3

- `dotnet build NetPrints.slnx -c Release`: 0 warnings, 0 errors. `dotnet format NetPrints.slnx --verify-no-changes`: exit 0.
- Whole suite (Release): 1087 tests, 0 failed, 10 skipped (the headless-UI capability skips); E2E (`--fail-skips on`): 9 of 9 passed. Cli.Tests 68 passed.

### Checkpoint C

**Status**: ✓ Green

**Build**: Solution builds with 0 warnings (Release mode).
- `dotnet build -c Release -v q -tl:off --nologo`: 23 projects, 0 errors, 0 warnings.

**Test Suite**:
- Main suite (Release): 1087 tests, 1077 passed, 0 failed, 10 skipped (5m 04s)
- E2E (`NETPRINTS_E2E=1`, `--fail-skips on`): 9 tests, 9 passed, 0 failed, 0 skipped (2m 37s)

**CI**: Green; PR #9 awaits final review (T035).

**Documentation**: New `docs/guide/cli.md` with global options, all four commands, exit codes, CI recipes, and removed P1 flags. Updated `docs/guide/install.md`, `docs/guide/projects.md`, `README.md`, and `.github/release-notes.md`.

**SC-001 status (command coverage)**: ✓ Four of nine commands shipped and documented (`build`, `run`, `generate`/`regen`, `migrate`). Full coverage deferred to Checkpoint F (T098).

**SC-002 status (run and regen parts)**: reopened by Review C, closed again in Review C part 2 on the evidence below.
- As first written this said "Closed" on the CI "Graph checks" step. That step ran after the sample build, whose `NetPrintsGenerate` target rewrites a stale `.g.cs`, so it could never fail (Review C, High). The `run` half (T026) stood; the regen half did not.

**Open items at the time of Checkpoint C** (this section first said "None"): restore failure exited 0, a project load failure exited 4, `run` mangled `--` arguments and buffered the program's output, Ctrl+C exited 4, exit-2/3 messages went to stdout, the CI graph gate could not fail, the tool and SDK versions were never compared, and the docs and contract disagreed with the code. All are closed in Review C parts 1 and 2 below. Still open, by design: the commands of sub-phases D-F (`catalog`, `format`, `show`, `merge`, `git-install`, SC-001 at Checkpoint F) and the final PR review (T035).

### Review C fix batch, part 1 (T036, CLI runtime behaviour)

Decisions, one per Review C comment:
- `run --` forwarding: `CliApplication.RunAsync` cuts the argv at the first `--` before Spectre sees it and registers the tail as `ForwardedArguments`; `RunCommand` reads that holder instead of `Remaining.Raw`. Cases pinned: `""`, `-`, `--=`, an argument with spaces, a second `--` in the tail.
- `run` buffering: new internal `IProgramRunner`/`ProgramRunner`. In production the child inherits stdin, stdout and stderr (prompts, ordering, colours, Ctrl+C); given streams, it redirects and copies with `CopyToAsync`. The test host's `CapturingProgramRunner` does that and writes to its console, so the forwarding tests keep asserting output. `IProcessRunner` stays for builds.
- Restore failure: `snapshot.Messages` errors are printed to stderr (`file(line,col): code: message`) and `generate`/`migrate` return 1 before generating.
- Malformed csproj: `MsBuildProjectSystem` already wraps `InvalidProjectFileException` in `ProjectSystemException` (`NPW003`), so no new code was added; the CLI now catches `ProjectSystemException` (project commands and migrate), prints `<project>: <message>` to stderr and returns 1 (3 for `NoSdkRegistered`). 4 stays for real bugs.
- `--verbose` with no command is dropped before Spectre, so `--verbose --help` and `--verbose --version` exit 0 and a bare `--verbose` equals bare `netprints` (help, exit 0).
- `CommandRuntimeException`: Spectre gives no structural distinction, so DI and command-creation faults are recognised by the fixed message prefixes of Spectre 0.55 (`Could not resolve type`, `Could not create`, `Could not find converter`, `Could not get settings type`) and map to 4; conversion, validation and missing-value errors stay 2. A test registers a command with an unresolvable dependency.
- Ctrl+C: `OperationCanceledException` with the token cancelled returns 130 silently (`ExitCodes.Canceled`); an OCE without a cancellation request is still an internal error. Part 2 documents 130.
- Streams: every exit-2 and exit-3 message (unknown project, no SDK, `--graph` rejection, migrate path errors) goes to stderr.
- `--graph`: matching uses `GraphPathComparison.Default` (case-insensitive on Windows and macOS); relative values resolve against the current directory (part 2 fixes the guide); `GenerateSettings.Validate` rejects an empty value and Spectre's `__default_command` token with exit 2. `--check` help now says "Write no generated file".
- `migrate` walk: reparse points (symlinks, junctions) are not followed; a directory that cannot be listed prints `<dir>: unreadable: <reason>`, counts as a failure and exits 1. `.git` and `node_modules` are not skipped (not asked).
- `migrate` exit code: `CollectAsync` returns a `CollectResult(ExitCode, Message)` instead of the caller comparing message strings.
- Missing `schemaVersion`: the `unreadable: Missing 'schemaVersion'.` line is kept and now pinned by a test; part 2 aligns the contract.
- `TypeRegistrar` is `IAsyncDisposable`; `CliApplication` uses `await using` (also for the probe provider).
- Broken-manifest test writes `ExtensionManifest.FileName` and asserts `NPX001` (`InvalidManifest`).

Deviations: the manual smoke used a scratch console app that echoes its arguments (not HelloWorld, which ignores them); HelloWorld is covered by the `SampleCopy` tests. Symlink and unreadable-directory tests skip on Windows (and as root).

### Review C fix batch, part 2 (T036, CI, versions, scripts, docs)

Decisions, one per remaining Review C comment:
- **CI gate that could not fail (High).** In `ci.yml`, "Graph checks" now runs right after the solution build and before any step that builds a sample; a "Generated files unchanged" step after the sample run does `git diff --exit-code -- '*.netpc.g.cs'`. Evidence below.
- **Version skew (Medium).** The package's `build/NetPrints.Sdk.props` sets `NetPrintsSdkVersion` from its version folder (`<packages>/netprints.sdk/<version>/build/`), not for `NetPrintsUseLocalSdk=true`. `CliServices` asks the project system for it (`ExtraProperties`), `GenerateCommand` compares it with `ToolVersion` (the informational version, registered by `CliApplication` with `TryAddSingleton` so tests inject their own) ignoring `+metadata`: `generate` warns on stderr and continues, `generate --check` prints an error naming both versions and how to align them and exits 1. Rule recorded as Amendment 1 of ADR-0015. Path-derived rather than pack-time substituted so the props file stays one file shared by the samples and the package; it depends on NuGet's folder layout, which is fixed.
- **`NetPrintsSkipGenerate` (Low).** Renamed `_NetPrintsSkipGenerate` in `MsBuildProjectSystem` and the targets; a new `_NetPrintsReportSkippedGenerate` target logs a low-importance message (`-v:d`) when generation is skipped. A project that still sets the old public-looking property now regenerates.
- **Load-time default (Low).** `LoadingWithTheDefaultOptionsRegeneratesAStaleGeneratedFile` is the mirror of the `GenerateOnLoad: false` test (the editor relies on the default).
- **`verify-packages.sh` (Low).** Both `|| true` are gone; exit codes are captured and asserted 0 for `--version`, `dotnet run` and `netprints run`, and `netprints regen --check "$APP"` runs with the packed tool against the `PackageReference` project.
- **Docs.** `docs/guide/cli.md`: `--graph` is relative to the current directory; stray `regen` line removed; "Removed 0.1 flags" with the exact message, exit 2 and the mapping; exit 130, the stdout/stderr split, `run -- <args>` forwarding and the version rule added. `.github/release-notes.md`: a Breaking entry (flag mapping, exit-code changes) and a New entry; the CLI is not "New". `contracts/cli.md`: the `unreadable: Missing 'schemaVersion'.` line, 130, the stream split, the version rule, `--graph` resolution and the migrate walk. `CliCommandCatalog`: examples use `samples/HelloWorld/HelloWorld.Program.netpc.json`; the run description mentions `--` and the generate description mentions `regen`, since Spectre's help shows neither in the usage line. `CliHelpExamplesTests` checks that example paths exist and that the help mentions them. The rest of the `--check` wording (thread 1005): "Write no generated file" and the `obj/` note are in the guide and the contract.

Red/green:
- Red: `GenerateCommandTests` version cases (2 failed: no warning, exit 0 for `--check`), `SdkVersionPropertyTests` (2 failed), `GenerateRequestFactoryTests` skip-property cases (2 failed: the old public property still skipped, no skip message). Green after the code: Core.Tests 9 of 9, Cli.Tests 35 of 35 for those classes. The default-load mirror test passed on the existing code (it pins the default against a future flip). `CliHelpExamplesTests` red on the old catalog (2 of 6), green after.
- `scripts/pack-local.sh` then `scripts/verify-packages.sh local-packages <version>`: all checks passed, including the new `regen --check` with the packed tool (the packed SDK's version folder equals the tool's version).

**SC-002 evidence (closed here).** A scratch git repository holding `samples/` (HelloWorld with the in-repo SDK, `src` symlinked), Release CLI:
1. A `.g.cs` with `// stale` appended and committed. The new CI order: `netprints regen --check samples/HelloWorld` prints `stale: HelloWorld.Program.netpc.g.cs`, `1 stale.`, exit **1**. The step fails.
2. The old order (sample `run` first, then `regen --check`): `run` exits 0 and prints `Hello, World!`, then `regen --check` prints `1 generated file(s) up to date, 0 written.`, exit **0**. This is the defect.
3. After the sample run with the stale file committed and the graph touched, `git diff --exit-code -- '*.netpc.g.cs'` prints the one-line diff and exits **1**, so a build that rewrote a committed file fails the new step. (Without the touch MSBuild's incremental check leaves a newer stale file alone, which is why `regen --check` runs first and the diff is the second line of defence.)

Deviations: the version is read from the package folder name rather than substituted at pack time (above). `git diff` proof used a scratch repository, not the CI runner (the step itself is proven only by running the same commands).

### Review C summary

26 comments in review 5362322402 (2 High, 5 Medium, 13 Low, 6 Nit); all fixed: runtime behaviour in 3f8469d (part 1), CI gate, version rule, SDK property, tests, script, docs and these notes in part 2. Lessons: a gate needs a run that proves it can fail; an SDK build target that rewrites files must run after, not before, a check of those files; a note that says "None" for open items needs a list to be true.

## Sub-phase D

### Batch D1 (T037-T040, model, writer, reader, schema)

Decisions:
- Model type names carry the `Catalog` prefix (`CatalogTypeRef`, `CatalogNodeHint`, `CatalogObsoleteInfo`, `CatalogTypedValue`, `CatalogTypeKind`, ...) so they never clash with Roslyn's `TypeKind` or the Core specifier types; data-model.md's short names map one to one.
- Optional flags (`generic`, `isEnum`, `isInterface`, `params`, `error`) are plain `bool` (false = omitted); optional collections are `IReadOnlyList<T>?`. Records compare lists by reference, so round trips are asserted on the re-written text, not with `Assert.Equal` on records.
- The writer writes the model in the order it is given; sorting (by name, id, rendered name) belongs to the builder (D3). Assemblies are written multi-line (only parameters, type references, node hints and obsolete records are inline, as the contract lists); every parameter is one line inside a multi-line `parameters` array, string arrays (`modifiers`, `genericParameters`, `keywords`) are inline, `enumMembers` and `interfaces` are one per line. An empty obsolete record is written `{}`.
- Escaping: `"`, `\\`, and every char below U+0020 (`\n`, `\r`, `\t` short, the rest `\u00xx` lower-case); U+007F to U+009F and everything non-ASCII are written as is.
- No `!` anywhere; `LowerCaseEnumConverter<T>` (net10.0 only) reads and writes enums as lower-case names because the shared model cannot carry System.Text.Json attributes. `CatalogJsonContext` sets camel case, `WhenWritingNull` and `RespectNullableAnnotations`; the generated schema replaces the exporter's output for enums with a lower-case `enum` list, strips `null` from `type` and pins `schemaVersion` to `const 1`.
- The reader checks `schemaVersion` on the JSON element first (missing or non-number is NPC102, above 1 is NPC101), then deserializes; it also rejects an empty `assemblies` list and duplicate type ids (NPC102). `Types` is normalized to an empty list because the generated deserializer leaves it null when the property is absent.
- `CatalogDiagnosticCodes` constants are named after the meaning (`UnsupportedSchemaVersion`, `MalformedCatalog`, ...); `ExperimentalApis.cs` (the second `NPXE0004` declaration) arrives with T044, so no experimental type exists yet and no D1 file needs the opt-in.
- API tracking: `dotnet format analyzers --diagnostics RS0016 RS0037` filled `PublicAPI.Unshipped.txt` for the model; `CatalogSchema` was added by hand because the fixer reported nothing for it.
- `eng/validate-schemas.sh` loops over `SCHEMAS` (schema file and instance glob); a schema with no tracked instance fails. `scripts/build-docs.sh` compares both schemas after the copy.
- The Annotations project already compiles the shared sources (Model, Engine, `CanonicalCatalogWriter`, Emit) for netstandard2.0; that build is the proof that they stay compatible.

Red/green: T037 tests were written before any model type existed (red = compile errors CS0246 on `CatalogDocument`), committed, then green with T038/T039 (24 tests). T040's `CatalogSchemaTests` were red on `CatalogSchema` missing, then green (35 tests in `NetPrints.Catalog.Tests`); the sourcemeta CLI lint caught `enum_with_type` on the first schema, fixed in `CatalogSchema`.

### Batch D2 (T041-T043, attribute injection, fixture library, glob and profile reader)

Decisions:
- T041: `AttributeSources.Source` is a C# 7.3 string constant (classic constructors, no `?`, no `#nullable`, `global::` names) and the generator registers it plus `AddEmbeddedAttributeDefinition()` in post-initialization output under the hint name `NetPrintsAttributes.g.cs`. `CatalogGenerator` is public (`[Generator(LanguageNames.CSharp)]`). The test project references `NetPrints.Annotations` with `Aliases="Annotations"` and adds `Microsoft.CodeAnalysis.CSharp` (central 5.9.0; the generator itself stays on 5.0.0); `GeneratorTestHost` compiles in-memory sources against the running framework's reference set, runs the generator through `CSharpGeneratorDriver`, and is the base for the later AN tests.
- T041 (AN-T01): the "no NetPrints assembly reference" check reads the emitted PE's `AssemblyReferences`; the InternalsVisibleTo case compiles a second assembly that references the first (which grants it IVT) and asserts no compile error, so the `[Embedded]` types are not imported.
- T042: the fixture is `IsPackable=false` like every test project (the release workflow packs the whole solution); CT-T12 must pack it explicitly with `-p:IsPackable=true`. `MinVerSkip=true` keeps version 1.0.0. The project builds warning-free under the repository analyzers without any suppression. It sits in a `/tests/Fixtures/` solution folder.
- T042: the test project records the fixture's output folder as `AssemblyMetadata("CatalogFixtureLibOutput")` (same pattern as `PublicApiAnalyzersPath`), so `FixtureLibrary` needs no configuration guessing; `Profiles/**` is copied to the output. `ExposeAttribute` has constructor parameter `flags` and a get-only `Flags` property; how a profile's argument `name` matches (constructor parameter or property) is settled in T045.
- T042: fixture coverage: generic class with constraints, generic method, nested type of a generic type, covariant interface, record struct with operators and implicit/explicit conversions, abstract base with virtual and abstract members, static class with `ref`/`out`/`in`/`params`, default values of several kinds, extension methods (plain and generic), obsolete method, error-obsolete method, obsolete type and obsolete enum member, a nested enum, `[NetPrintsType]`/`[NetPrintsNode]`/`[NetPrintsIgnore]` uses and an internal `[NetPrintsNode]` method (NPC004 case). The missing-assembly edge case (NPC005) needs a second assembly and is built by `MissingDependencyTests` (T044), not by this fixture.
- T043: `Glob.IsMatch` is ordinal; `*` matches any run including dots, `?` matches one character, every other character (brackets, plus, backtick) is literal; iterative matcher, no regex. `Glob.AnyMatch(null or empty)` is false.
- T043: `CatalogProfile` is a positional record with the data-model fields; enums `CatalogProfileBase`, `CatalogObsoleteMode`, `CatalogAttributeRuleKind`; `CatalogArgumentMatch` names its comparison fields `EqualsValue`/`ContainsValue` (a property called `Equals` would hide `object.Equals`). It is not `[Experimental]` yet: that lands with `ExperimentalApis.cs` in T045 as tasks.md lists.
- T043: `ProfileJson.Parse` reads through `MiniJson`, a strict recursive reader (depth 64, `\uXXXX`, no comments, no trailing commas) that builds plain values, so it compiles into the generator. Every defect is `CatalogFormatException` NPC003: malformed text, root not an object, `schemaVersion` not a positive integer or above 1 (message names both versions), missing or invalid `id`, the reserved ids `public-api`/`annotated`, unknown `base`/`obsolete`/`rule` values, wrong property types, an argument without exactly one of `name`/`position` and exactly one of `equals`/`contains`, a negative `position`. Unknown properties (including `$schema`) are ignored so a later additive field does not break older readers.
- `CatalogFormatException` moved from `Json/` to `Engine/` so the shared sources (and the generator) can throw it; the type and its namespace are unchanged.
- API tracking: `dotnet format analyzers ... --diagnostics RS0016 RS0037 --severity info` reported nothing while the project had RS0016 errors (it needs a compiling workspace), so the new entries were taken from the analyzer's own RS0016 messages and appended to `PublicAPI.Unshipped.txt`; the build is clean.

Red/green: T041 was red on CS0234 (`NetPrints.Annotations.CatalogGenerator` missing), then 3 AN-T01 tests green (one intermediate failure was a test defect: the `InternalsVisibleTo` line was placed before a `using`). T042 `FixtureLibraryTests` was red on CS0103 (`FixtureLibrary` missing), then 3 green. T043 `GlobTests` and `ProfileJsonTests` were red on CS0103/CS0246 (`Glob`, `ProfileJson`, `CatalogProfile` missing), then 99 tests green in `NetPrints.Catalog.Tests` (one intermediate failure was a test defect: a doubled backslash in a raw string).

## Governance proposals
