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
| Job ids | `build-test`; `e2e` (desktop E2E on Xvfb: apt `xvfb openbox xdotool imagemagick x11-utils libgtk-3-0t64 adwaita-icon-theme`, `NETPRINTS_E2E=1`) | `build-test` stable; `e2e` may change |
| SDK | `actions/setup-dotnet@v6`, `dotnet-version: 10.0.x`; `global.json` governs the feature band | may change |
| Build | `dotnet build NetPrints.slnx -c Release` | may change |
| NuGet cache | `actions/cache@v6` on `~/.nuget/packages`, key = hash of `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `**/*.csproj` | may change |
| Tests | `dotnet test --solution NetPrints.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults` (Microsoft.Testing.Platform mode from `global.json`) | may change |
| CLI smoke | `NetPrints.Cli --version` output contains `NetPrints.Cli` | may change |
| Artifacts | `test-results` (`TestResults/**/*.trx`), uploaded `if: always()`; `ui-headless` (snapshot actual/diff images, diagnostics); `e2e-results` (E2E TRX, screenshots, diagnostics) | `test-results` stable name; others may change |
| Permissions | `contents: read` | stable |
| Concurrency | group `ci-${{ github.ref }}`, `cancel-in-progress: true` | may change |
| Result | a job fails on any build error, test failure or CLI smoke failure | **stable** |

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
