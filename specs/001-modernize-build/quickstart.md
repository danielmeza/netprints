# Quickstart: Build, Test and Verify P0 on Linux

## 1. Prerequisites

- .NET 10 SDK 10.0.100 or later (`dotnet --version`). `global.json` rolls forward to the newest
  installed 10.0 feature band.
- To run the desktop editor (not needed for tests): a Linux desktop session (X11 or Wayland with
  XWayland) and fontconfig. Headless tests need no display, no fonts and no fontconfig (verified in a
  clean `mcr.microsoft.com/dotnet/sdk:10.0` container).

## 2. Build and test (SC-001: two commands)

```bash
dotnet build NetPrints.sln -c Release
dotnet test --solution NetPrints.sln -c Release --no-build
```

Expected:
- The build finishes with **0 errors**. Known warnings: Fody `OnInputTypeChanged` (Core, until
  P1) and Roslyn analyzer warnings in the moved reflection code. The editor, the desktop app and the
  test projects build with warnings as errors.
- Tests report `failed: 0` for both test projects: `NetPrintsUnitTests` (11 tests) and
  `NetPrints.Editor.Tests` (reflection, view-model and host tests) and `NetPrints.Editor.UITests`
  (headless UI tests). All three use xUnit v3.
- `--report-xunit-trx --results-directory TestResults` writes `.trx` files, as CI does.

Individual projects:

```bash
dotnet test --project NetPrintsUnitTests -c Release
dotnet test --project NetPrints.Editor.Tests -c Release
dotnet test --project NetPrints.Editor.UITests -c Release
```

## 3. CLI smoke

```bash
dotnet run --project NetPrintsCLI -c Release -- --version     # prints "NetPrintsCLI <version>" (exit code 1 is expected until P2)
dotnet run --project NetPrintsCLI -c Release -- -p samples/HelloWorld/HelloWorld.netpp -r
# → "Compilation succeeded." then the program prints "Hello, World!" (uses the runtime-assembly fallback on Linux)
```

The compiled output goes to `samples/HelloWorld/Compiled_HelloWorld/`, which is git-ignored.

## 4. Run the editor

```bash
dotnet run --project NetPrints.Desktop -c Release -- samples/HelloWorld/HelloWorld.netpp
```

The main window opens with project "HelloWorld" loaded (PAR-05).

## 5. UI tests (replace the manual parity walkthrough)

Every PAR item is exercised through the UI by automated tests; nothing needs a hand check.

**Headless** (no display server; part of section 2):

```bash
dotnet test --project NetPrints.Editor.UITests
```

- Snapshots compare with `NetPrints.Editor.UITests/Snapshots/Baselines/*.png`. After an
  intended visual change, regenerate them and review the images before committing:
  `NETPRINTS_UPDATE_SNAPSHOTS=1 dotnet test --project NetPrints.Editor.UITests -- --filter-class "*SnapshotTests"`.
- Artifacts (actual and diff images, flow screenshots, per-test tree dumps and screenshots) go to
  `NETPRINTS_UI_ARTIFACTS` (default: `ui-artifacts/` in the test output folder).

**Desktop E2E** (Linux with X11 tools; the real editor, real input):

```bash
sudo apt-get install xvfb openbox xdotool imagemagick x11-utils libgtk-3-0t64 adwaita-icon-theme
NETPRINTS_E2E=1 dotnet test --project NetPrints.Desktop.E2ETests
```

- The tests start their own Xvfb on a free display (100 and up) with openbox, and run the editor
  there with `NETPRINTS_AUTOMATION=1` (read-only automation pipe), no D-Bus session (GTK file
  dialogs on that display) and a private home. They never touch your desktop display.
- Without `NETPRINTS_E2E=1` the E2E tests are skipped (as in the `build-test` CI job).
- Screenshots of each flow step and diagnostics are written to `NETPRINTS_UI_ARTIFACTS/e2e/<test>/`.
- A run takes about 5 minutes.

## 6. CI

Push the branch or open a PR against `master`: the **CI** workflow (`.github/workflows/ci.yml`,
see `contracts/ci-workflow.md`) runs on `ubuntu-latest`: the `build-test` job (build, all tests
without a display, CLI smoke; artifacts `test-results` and `ui-headless`) and the `e2e` job
(desktop E2E on Xvfb; artifact `e2e-results`). There are no Windows jobs. The VS extension
workflow arrives in P4.
