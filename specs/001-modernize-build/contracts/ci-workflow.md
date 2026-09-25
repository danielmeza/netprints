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
| Job ids | `build-test`; `e2e` (desktop E2E on Xvfb: apt `xvfb openbox xdotool imagemagick x11-utils libgtk-3-0t64 adwaita-icon-theme`, `NETPRINTS_E2E=1`). `e2e` has `needs: build-test` and reuses its Release `bin/` output (`e2e-build` artifact) instead of restoring or building again | `build-test` stable; `e2e` may change |
| SDK | `actions/setup-dotnet@v6`, `dotnet-version: 10.0.x`; `global.json` governs the feature band. `e2e` still installs it, only to host the prebuilt test executable | may change |
| Build | `dotnet build NetPrints.sln -c Release` (`build-test` only; `e2e` builds nothing) | may change |
| NuGet cache | `actions/cache@v6` on `~/.nuget/packages`, key = hash of `Directory.Packages.props`, `Directory.Build.props`, `global.json`, `**/*.csproj` (`build-test` only; `e2e` does not restore) | may change |
| Tests | `build-test`: `dotnet test --solution NetPrints.sln -c Release --no-build --report-xunit-trx --results-directory TestResults` (Microsoft.Testing.Platform mode from `global.json`) | may change |
| E2E tests | `e2e` downloads and extracts `e2e-build`, then runs the test host directly (no `dotnet test`, since nothing was restored): `./NetPrints.Desktop.E2ETests -trx <file> -failSkips` (xunit.v3 in-process runner's own CLI, not the MTP `--report-xunit-trx`/`--results-directory` flags `dotnet test` translates) | may change |
| CLI smoke | `NetPrintsCLI --version` output contains `NetPrintsCLI` | may change |
| Artifacts | `test-results` (`TestResults/**/*.trx`), uploaded `if: always()`; `ui-headless` (snapshot actual/diff images, diagnostics); `e2e-build` (tarred Release `bin/` of `NetPrints.Desktop.E2ETests` and `NetPrints.Desktop`, produced by `build-test`, consumed by `e2e`); `e2e-results` (E2E TRX, screenshots, diagnostics) | `test-results` stable name; others may change |
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
