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

## 5. Parity walkthrough (manual items of the Editor Parity Inventory)

Record the result for each PAR ID in the PR description (SC-004). Suggested order:

1. **Main window** (PAR-01..08, 14): check the title and icon. Toggle the Project and Settings panes
   (only one is open at a time). In Settings, change Output and Binary type. Open References and
   close it again (PAR-21).
2. **Project lifecycle** (PAR-03, 04, 06): Create Project → cancel the dialog → the previous project
   is still open. Create Project → save to a temporary folder. Open Project → pick
   `samples/HelloWorld/HelloWorld.netpp` → the progress overlay appears, then the project loads.
   Open a corrupt `.netpp` → an error dialog appears and the exception is on the clipboard.
3. **References** (PAR-16..20): add an assembly (for example any `.dll` from `~/.dotnet/shared`). Add
   the same one again → no duplicate. Add a source folder → toggle Include/Exclude. Remove both.
4. **Classes** (PAR-11..13): New Class twice (`MyClass`, `MyClass2`). Open one, minimize it, click it
   again → it is restored and activated. Remove it → its window closes. Existing Class → pick a
   `.netpc` file.
5. **Class window** (PAR-22..37): the window is maximized and titled with the class name. Create a
   Method, a Constructor and a Variable. Override `ToString`. Single-click and double-click list
   entries (inspector and graph). Resize the splitters (PAR-31). Edit the class, method and
   variable inspectors and watch the generated code update within about 1 s. Add and remove a
   getter and setter, and undo/redo with Ctrl+Z/Ctrl+Y. Press Delete on selected nodes (entry and
   return nodes are kept).
6. **Canvas** (PAR-38..57): check the grid and watermark (38). Check node colors, the selected
   border and tooltips (39). Change an overload and undo (40). Toggle Pure (41). Use the +/-
   buttons on the method entry and return nodes (42). Check pin shapes, hover outline and the
   default-value indicator (43). Edit an unconnected value and middle-click to clear it (44).
   Rename an editable pin (45). Drag pin to pin, including incompatible targets (46). Drop a pin
   drag on empty canvas → filtered search → the node is created and connected (47). On a cable:
   hover, mouse back button (faint), middle-click (disconnect), double-click (reroute) (48).
   Click, box-select and click empty canvas (49). Drag several nodes → they snap to the grid (50).
   Right-drag pans, the wheel zooms within 0.3–1.0 around the pointer, and the view resets when
   switching graphs (51). Right-click opens search: categories, icons, multi-word filtering, focus
   (52). Check the built-in node lists in method, constructor and class graphs (53). Construct,
   Literal, Type and Make Delegate open their dialogs (54, 58, 59). A property item opens the
   Get/Set chooser (55). Drag a method, a constructor and a variable from the lists onto the
   canvas (56, 57).
7. **Compile & run** (PAR-09, 10, 23, 32): press Run in the class window → the status goes
   "Compiling..." then "Build succeeded", and `Hello, World!` appears in the terminal that started
   the editor. Break the graph (disconnect a required pin) → Compile → the error list fills and the
   status reads "Build failed with N error(s)".

## 6. CI

Push the branch or open a PR against `master`: the **CI** workflow (`.github/workflows/ci.yml`,
see `contracts/ci-workflow.md`) runs on `ubuntu-latest` and uploads the `test-results` artifact.
There are no Windows jobs. The VS extension workflow arrives in P4.
