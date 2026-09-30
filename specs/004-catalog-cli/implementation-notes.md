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
