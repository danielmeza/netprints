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
- T008: the test project references the analyzer package only for its path (`GeneratePathProperty`); `ExcludeAssets` does not keep its analyzer off the compilation, so a target removes that `Analyzer` item.

## Batch B2 (T012-T016a) — experimental API opt-in

### Decisions

- T013: `Directory.Build.targets` sets `NoWarn` inside `<Target Name="NetPrintsExperimentalOptIn" BeforeTargets="CoreCompile">`. Item references are not expanded by property evaluation outside a target, so the property form in the task text would have produced an empty opt-in.
- T013: observed compiler behaviour (ADR-0017 decision 3): the diagnostic is reported for uses inside the defining assembly too. `NetPrints.Core` needs `NPXE0003` (ClassTranslator, TranslationEnvironment) and `NetPrints.Extensibility` needs `NPXE0001`, `NPXE0002` and `NPXE0003`. Other opt-ins, each only for the ids the build showed: Editor 1+2, Desktop 2, TestExtension 1+2+3, Core.Tests 1+2+3, Editor.Tests 1+2, Editor.UITests 2.
- T014: `ExperimentalApiIds` is in namespace `NetPrints.Core`. `NPXE0004` and `CatalogProfiles` are declared now and marked in T044.
- T014: PublicApiAnalyzers writes experimental symbols as `[NPXE000n]Symbol` in the API files, and members of an experimental type carry the prefix too. The `dotnet format analyzers` fixer does not emit the prefix and adds nothing when the shipped file already lists the type. The `ExperimentalApiIds` entries came from the fixer (run with an emptied Shipped file, then Shipped restored); the `[NPXE0003]` lines and `*REMOVED*` lines for the Core emitters (shipped in v0.1.1 without the attribute) and the prefixed lines in Extensibility's Unshipped file were written by script from the analyzer's own RS0016/RS0017 messages, then the build was clean.
- T012: the literal gate allows the one `NPXE0004` duplicate (`AllowedDuplicateCode`): the generator compiles `Catalog/Engine/ExperimentalApis.cs` and cannot reference Core. The declaring files it lists do not exist yet (Catalog engine files arrive in later batches).
- T012: hygiene gates added to `SourceHygieneTests`: `TheOnlyNoWarnIsTheExperimentalOptInLine`, `ExperimentalOptInItemsLiveOnlyInProjectFiles` (no opt-in in props/targets, so nothing is inherited), `EveryExperimentalOptInIsADeclaredId`, `NoPragmaDisablesAnExperimentalDiagnostic`, `NoEditorConfigSeverityForAnExperimentalDiagnostic`; `NoUnlistedBuildWarningSuppressions` allows only the exact opt-in line.
- The in-test extension compiler (`ExtensionTestSupport.Compile`) suppresses the three ids through `WithSpecificDiagnosticOptions`, the compilation equivalent of an opt-in item; it is neither a NoWarn nor a pragma.
- T016a (owner-approved): `SolutionHygieneTests.EverySourceAndTestProjectIsInTheSolution`; `samples/`, `legacy/` and `docs/` are out of scope (samples load the Generator from `bin/`).

### Red/green evidence

- T012 red (before targets/ids existed): `ExperimentalApiTests` 3 of 6 failed (no NPXE error without opt-in); `SourceHygieneTests` 2 of 13 failed (`TheOnlyNoWarnIsTheExperimentalOptInLine`, `EveryExperimentalOptInIsADeclaredId`). Green after T013/T014.
- Negative checks of the gates (temporary, reverted): an opt-in `NPXE0009`, a `#pragma warning disable NPXE0001`, a `dotnet_diagnostic.NPXE0001.severity` globalconfig line, an opt-in item in a `.props` and a `<NoWarn>` in another `.props` each failed the matching gate (7 failures).
- T016a: the test was written before the check and shown red by removing the `NetPrints.Testing.Ui.csproj` entry from `NetPrints.slnx` (1 of 1 failed); entry restored, green.
- The full suite found 3 failures the filtered runs missed (in-test extension sources use the experimental emitter API); fixed with the suppression above. A later run hit a flake in the new probe test (it referenced every loaded assembly, including a temporary extension assembly another test deletes); it now references the trusted platform assemblies plus Core and Extensibility.

### Checkpoint B

**Status**: green (SC-011)

- `dotnet build -c Release`: 23 projects, 0 errors, 0 warnings. `dotnet format NetPrints.slnx --verify-no-changes`: clean (after ordering the new usings and attribute indentation).
- Whole suite (Release, no E2E env): 994 tests, 984 passed, 10 skipped, 0 failed.
- E2E (`NETPRINTS_E2E=1`, `--fail-skips on`): 9 tests, 9 passed, 0 skipped.
- Only `<NoWarn>` in the repository: the `Directory.Build.targets` opt-in line.

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

## Governance proposals
