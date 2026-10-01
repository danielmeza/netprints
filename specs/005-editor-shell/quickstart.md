# Quickstart: validating P3a end to end

This runs every story's main flow against a local build. The behaviour is in the [spec](./spec.md), the surfaces
are in [contracts/](./contracts/), and the formats are in [data-model.md](./data-model.md). Never use the owner's
display (`DISPLAY=:1`). For manual checks, start your own Xvfb (see AGENTS.md "Displays") and set
`NETPRINTS_STATE_DIR` to a temporary folder, so your runs never touch real per-user state.

## 0. Build and test

```bash
dotnet build -v q -tl:off --nologo
dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8
NETPRINTS_E2E=1 dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release --no-build --no-progress --no-ansi -- --fail-skips on
```

Expected: 0 failures in both runs. The E2E run includes the new shell scenarios (research R13).

## 1. CI and diagnostics (US1, contracts/ci.md)

- Run `NETPRINTS_E2E=1 tests/NetPrints.Desktop.E2ETests/bin/Release/net10.0/NetPrints.Desktop.E2ETests -class '*E2EDiagnosticsTests'`
  (the test sets `NETPRINTS_E2E_FORCE_TIMEOUT` for its own editor).
  `TestResults/e2e-diagnostics/<class>/` should hold `summary.md`, `timings.md`, `ui-tree.json`, `display.png`,
  `editor.log`, `process.txt` and `run-state.json`.
- On the PR, check the Actions view:
  - `Repository checks (Linux)` and five `Test (…)` legs run in parallel, and `Build and test (Linux)` is green
    only after all of them;
  - `CLI (Windows)` is green, and its TRX lists `ShowTextconvEncodingTests` as passed;
  - the longest Linux leg takes at most 60% of the old job's time (SC-006; P2's last run took about 13 min).

## 2. One window (US2, contracts/shell.md)

1. Start the editor on a copy of `samples/HelloWorld/HelloWorld.csproj`. One window opens, with the tree, an
   empty document area, the inspector and the Errors / Output / C# tabs.
2. Open `Main` from the tree, then a second graph: two tabs. Break a connection, press F7: Errors lists the error.
   Activate the error: its node is selected. Undo, press F5: Output shows `Hello, World!`.
3. Choose View › Float tab on `Main`: it opens in its own window. Edit it, then choose Dock tab. Choose View ›
   Reset layout: the default layout is back.

## 3. Never lose work (US3)

1. Edit a graph: `*` appears on the tab, on the class in the tree, and in the title. Undo it: the `*` disappears.
2. Edit again and close the window: the prompt lists the file. Cancel keeps the editor open. Close again and pick
   Save all: the editor closes and the file is saved.
3. Edit, wait 35 s, then `kill -9` the editor. Restart on the same project: the recovery prompt appears. Restore
   brings the edit back, still unsaved. The backups are under `$NETPRINTS_STATE_DIR/backups/`.

## 4. Commands and shortcuts (US4, contracts/commands.md)

- Keyboard only: Ctrl+S, F7, F5, Shift+F5, Ctrl+Z, Ctrl+Shift+Z, Ctrl+Tab, Ctrl+W.
- The Edit menu shows `Undo <action>`. After an undo, the status bar shows `Undid: <action>`.
- Help › Keyboard shortcuts lists every row of contracts/commands.md §1.

## 5. Start page (US5)

Start with no arguments: the start page shows. Create a Console app in an empty temporary folder: it opens and
appears in Recent. Close the project, pin it, search for it, then remove it. Open the HelloWorld sample into a
temporary folder: `git status samples/` stays clean.

## 6. Persistence (US6, contracts/state-files.md)

Rearrange the panes, open three graphs, zoom one of them, then move and resize the window. Restart: all of it is
restored. Write `{` into `state/layout.json` and restart: the editor uses the default layout and logs a warning.

## 7. Navigation (US7)

Use Ctrl+Shift+P, then type "comp" and Enter: it compiles. Use Ctrl+P, then type a node title: the graph opens
with the node selected. Ctrl+click a connection: the view moves to its other end. Alt+Left: you are back at the
same view. Hover a connection: the tooltip shows `Node.pin → Node.pin` and the type.

## 8. Event graphs and entries (US8)

Rename an event graph with F2. Select a custom event: rename it to a method's name and it is refused; give it a
unique name. Add two arguments, then compile: the C# tab shows the method with both parameters. Each step undoes
on its own. An override entry's signature is read-only.

## 9. Look (US9)

Use View › Theme › Light, then System, then Dark: every pane and the canvas follow. Then run
`tests/NetPrints.Core.Tests/bin/Debug/net10.0/NetPrints.Core.Tests -class '*XamlHygieneTests'`: it is green, with
all allowlists empty and the E7 code-behind allowlist justified.

## 10. Type-scoped search (US10)

In a project that references the annotated fixture library (`tests/Fixtures/Catalog/CatalogFixtureLib`), drag from
a pin of a cataloged type and open search: only the catalog's members are listed. For a type that is not
cataloged, the search lists nothing and names the catalog. For a type from an uncovered library, every public
member is listed. A graph that already uses a hidden member still builds and runs.

## 11. Docs

`scripts/build-docs.sh` succeeds with 0 broken links, and the six pages under `docs/guide/editor/` show their
screenshots. `scripts/guide-screenshots.sh` regenerates them, and an unchanged UI gives visually identical images.
