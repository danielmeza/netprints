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

## Deviations

- Commit 0613745's message lost `$(TargetPath)` to shell expansion.

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

## Governance proposals
