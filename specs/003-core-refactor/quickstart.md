# Quickstart: Validate P1 on Linux

Prerequisites: .NET 10 SDK (brings `packs/Microsoft.NETCore.App.Ref`), the repository after the
reorganization PR (plan.md), and for E2E the P0 tools (Xvfb, openbox, xdotool; see
`specs/001-modernize-build/quickstart.md` §5).

## 1. Build and test (two commands, unchanged)

```bash
dotnet build NetPrints.slnx -c Release          # 0 warnings, warnings are errors everywhere (SC-007)
dotnet test --solution NetPrints.slnx -c Release --no-build
```

Focused suites:

```bash
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*Serialization*"   # DF-T01…T18
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*Extensibility*"   # EX-T01…T13
dotnet test --project tests/NetPrints.Core.Tests -c Release -- --filter-namespace "*References*"      # RC-T01…T05
dotnet test --project tests/NetPrints.Editor.Tests -c Release                                          # ED-T02…T14 (VM level)
dotnet test --project tests/NetPrints.Editor.UITests -c Release                                        # ED-T01, T03, T05–T07, T09 (headless)
```

Golden files (C#, documents, notification map) and UI baselines are regenerated only on purpose:
`NETPRINTS_UPDATE_SNAPSHOTS=1 dotnet test …`, then review the diff before committing.

## 2. US1 — legacy project → JSON, identical C#

```bash
cp -r tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld /tmp/hw && cd /tmp/hw
dotnet run --project <repo>/src/NetPrints.Cli -c Release -- -p HelloWorld.netpp -r     # loads legacy, prints Hello, World!
dotnet run --project <repo>/src/NetPrints.Desktop -c Release -- /tmp/hw/HelloWorld.netpp
# In the editor: Save. Expect HelloWorld.netpp.json + HelloWorld.Program.netpc.json next to the untouched legacy files.
dotnet run --project <repo>/src/NetPrints.Cli -c Release -- -p HelloWorld.netpp.json -r # same output
```

Expected: the JSON matches `contracts/document-format.md` §1.7 (expanded whitespace); saving again
changes nothing (`git diff --no-index` empty); moving a node changes only `layout` lines.

## 3. US2 — reference packs and documentation

- Editor: open `samples/HelloWorld/HelloWorld.netpp.json`, open `Main`, hover the `WriteLine` node:
  the tooltip shows "Writes the specified string value, followed by the current line terminator…".
- `NETPRINTS_REFERENCE_PACKS=/nonexistent dotnet run --project src/NetPrints.Cli -- -p samples/HelloWorld/HelloWorld.netpp.json`
  → warning `NPR005`, then compiles with the installed pack.

## 4. US3 — test extension

```bash
dotnet build tests/NetPrints.TestExtension -c Release
export NETPRINTS_EXTENSION_PATH=$PWD/tests/NetPrints.TestExtension/bin/Release/extensions
dotnet run --project src/NetPrints.Desktop -c Release
```

Expected: node search in a method graph shows the "Test Extension" category; the generated C# of a
class shows the test emitter's attribute; no error dialog. Rename the manifest's `netprintsApi` to
`2.0`: the startup dialog lists `NPX002` and the editor works.

## 5. US4–US6

- Event graph: class `Program` → Event graphs → New; add "Custom Event" `OnStart` and `OnTick`; the code
  view shows two methods.
- Local variable: open `Main`, Variables panel → "Method: Main" → New; set type `int`; the code view
  shows `int Variable0 = default(int);` at the top of `Main`.
- Code view: highlighted, numbered, foldable; break a connection so an argument is missing → squiggle and
  error row within ~2 s; double-click the row → the node is selected and in view.

## 6. E2E once at the end

`NETPRINTS_E2E=1 dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release` on a private Xvfb
display (≥ :140, `NETPRINTS_E2E_DISPLAY_START`), as in P0.1.
