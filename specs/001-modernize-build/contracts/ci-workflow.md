# Contract: Main CI workflow (`CI`)

Consumers: every PR (merge gate), the README badge, and the **P4 Visual Studio extension workflow**,
which chains after this one. Changing any item marked **stable** requires updating P4's workflow
and this contract.

| Item | Value | Stability |
|------|-------|-----------|
| File | `.github/workflows/ci.yml` | **stable** |
| Workflow `name:` | `CI` | **stable** (P4 uses `workflow_run: workflows: ["CI"]`) |
| Triggers | `push` → `master`, `pull_request` → `master`, `workflow_dispatch` | stable |
| Runner | `ubuntu-latest` only (no Windows or macOS jobs) | stable (constitution: main CI is Linux-only) |
| Job id | `build-test` | stable |
| SDK | `actions/setup-dotnet@v6`, `dotnet-version: 10.0.x`; `global.json` governs the feature band | may change |
| Build | `dotnet build NetPrints.sln -c Release` | may change |
| NuGet cache | `actions/cache@v6` on `~/.nuget/packages`, key = hash of `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `**/*.csproj` | may change |
| Tests | `dotnet test --solution NetPrints.sln -c Release --no-build --report-xunit-trx --results-directory TestResults` (Microsoft.Testing.Platform mode from `global.json`) | may change |
| CLI smoke | `NetPrintsCLI --version` output contains `NetPrintsCLI` | may change |
| Artifact | `test-results` (`TestResults/**/*.trx`), uploaded `if: always()` | stable name |
| Permissions | `contents: read` | stable |
| Concurrency | group `ci-${{ github.ref }}`, `cancel-in-progress: true` | may change |
| Result | the job fails on any build error, test failure or CLI smoke failure | **stable** |

## P4 consumer sketch (not created in P0)

```yaml
name: VSIX
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [master]
jobs:
  vsix:
    if: ${{ github.event.workflow_run.conclusion == 'success' }}
    runs-on: windows-latest
    # plus a path filter on the VS extension and the libraries it consumes
    # (workflow_run has no `paths:`, so it compares changed files in a first step)
```
