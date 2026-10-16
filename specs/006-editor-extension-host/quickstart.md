# Quickstart: validating P3 end to end

This runs every active story's main flow (US1–US11, US15) against a local build. The behaviour is in the
[spec](./spec.md), the surfaces in [contracts/](./contracts/), the formats in [data-model.md](./data-model.md). Most
steps are also automated tests; this guide is the manual and checkpoint walk-through.

## Setup (every session)

Never use the owner's display (`DISPLAY=:1`). Isolate all per-user files:

```bash
export NETPRINTS_STATE_DIR="$(mktemp -d)"   # state, backups, logs
export XDG_CONFIG_HOME="$(mktemp -d)"       # settings.json lives in $XDG_CONFIG_HOME/NetPrints/
pgrep -af Xvfb                              # pick a free display number, then:
Xvfb :NN -screen 0 1600x1000x24 & export DISPLAY=:NN
```

Work on copies of fixtures and samples in a temporary folder; never save into `samples/`. Kill your Xvfb at the end.

## 0. Build, test, pack

```bash
dotnet build -v q -tl:off --nologo
dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8
NETPRINTS_E2E=1 dotnet test --project tests/NetPrints.Desktop.E2ETests -c Release --no-build --no-progress --no-ansi -- --fail-skips on
scripts/pack-local.sh
dotnet build extensions/NetPrints.Godot -c Release
dotnet test --project extensions/NetPrints.Godot/tests/NetPrints.Godot.Tests -c Release --no-progress --no-ansi
```

Expected: 0 failures; `local-packages/` holds every package of [extension-api.md](contracts/extension-api.md) §1 at the
local version `scripts/pack-local.sh --print-version` prints; package validation and the public API checks pass.

## 1. Analyzer and generator foundation (US1)

1. In a scratch copy of the repository, put an attribute on its member's line in a `src/` file and build: the build
   fails with NPS001 naming the file and line. Remove two blank lines between properties: NPS002. Group two private
   fields: builds.
2. Open the generated sources of `NetPrints.Editor` (`obj/.../generated/`): `AddEditorServices()`, the scope factories,
   `CommandKeyId` and the options validators are there; `dotnet test --project tests/NetPrints.Analyzers.Tests` passes.
3. Make a `[Singleton<…>]` service take a `[SessionScoped<…>]` one: NPI001 fails the build.
4. Write `{"extensions":{"netprints.editor":{"theme":42}}}` to `$XDG_CONFIG_HOME/NetPrints/settings.json`, start the
   editor: it starts, the message names `netprints.editor.theme`, and the theme is the default.
5. Add an unknown member to a node of a copied graph file, open and save it in the editor: the member is still there,
   after the known members.

## 2. Node model (US2)

1. `tests/NetPrints.Core.Tests/bin/Release/net10.0/NetPrints.Core.Tests -class '*PinLayout*'`: the S0 golden matches.
2. Open a graph saved by the P3a editor (the fixtures), save it: `git diff --exit-code` on the copy is empty.
3. Place a call to `int.TryParse(string, out int)`: it has a `result` output pin and an `out` data pin. Leave the `out`
   input unconnected and build: a validation error. Make a `void` method with an `out` parameter pure: allowed; a `void`
   method without one: not offered.
4. Open node search from an exec output, an `int` input and the canvas: the rows match the P3a goldens (the test
   `NodeSearchGoldenTests` is the reference); Esc leaves the graph unchanged.
5. Give an unconnected `sbyte` and a `decimal` input a value: the numeric editor shows; build: valid C#.

## 3. Packages and the stable API (US3)

1. `dotnet build extensions/NetPrints.Godot` with `local-packages/` as the only NetPrints source: builds with no project
   reference. `netprints extension verify extensions/NetPrints.Godot/src/NetPrints.Godot/bin/Release/*.nupkg` exits 0.
2. Add a public member to `NetPrints.Extensibility.Abstractions` without an API file entry: the build fails naming it.
3. `netprints build` a copy of the fixture project that declares the test extension: no error; editor contributions
   are ignored.
4. Remove the `NPXE0005` opt-in from the Godot extension and build: the compiler reports NPXE0005.
5. Start the editor with `netprints.language` set to a culture with no translation: extension labels show their default
   text.
6. Open the fixture graph whose `Fx` node has `kindVersion` 0: the extension's migration runs and the node loads.

## 4. Extensions in the editor (US4)

1. Open the fixture project that declares `NetPrints.TestExtension` and trust it: one item of each kind is visible
   (command in its menu and the palette with the owner, panel, tile, template, context-menu item, tooltip, go-to
   result, status-bar item, settings page, dialog).
2. Close the project, open another: none of them remains.
3. Open the fixture whose extension throws in `Register`: Errors shows NPX005 naming it; the editor works. Run the
   fixture command whose handler throws: the error dialog names the extension and the command.
4. Two fixture extensions register the same command id and the same gesture: Errors shows NPX004 and NPX009; the first
   wins.
5. Put the Godot extension's built folder in `netprints.extensionPaths`, start
   `NetPrints.Desktop --profile godot`: New project preselects Godot and the template list is filtered (Show all
   restores it). `--profile nope`: unfiltered, with a message listing the known ids.
6. Create a project from the Godot template, open it: it builds, and the run profile button lists the game and Godot
   editor profiles.

## 5. Settings pages (US5)

1. Ctrl+,: the Settings document opens with General, Appearance, Keyboard shortcuts, Run, Extensions, and the test
   extension's page under Extensions.
2. Type "timeout" in search: only `run.stopTimeoutSeconds` stays. Enter `0`, press Enter: the validator's message, not
   saved. Enter `10`: saved; `settings.json` shows it.
3. Change `netprints.language`: "Requires restart" and the "Restart to apply" bar.
4. Put a section for an unknown extension in `settings.json`, change any setting: that section is written back unchanged.
5. Write invalid JSON to `settings.json`, start: defaults, one message, and `settings.json.unreadable-*` next to it.

## 6. Keyboard shortcuts (US6)

1. Keyboard shortcuts page: filter by "save" and by recording Ctrl+S.
2. Record Ctrl+Shift+S for Save: the page names Save As first; accept. Restart (same `XDG_CONFIG_HOME`): Ctrl+Shift+S
   saves, Ctrl+S does not, menus and the palette show the new gesture.
3. Record `F` for a Global command: refused with the reason. Record a gesture for the test extension's command; remove
   the extension, restart: the binding is under "Not loaded".
4. Reset all: `keyboard.bindings` is empty and the defaults are back. The E2E `RebindShortcuts` covers SC-007.

## 7. Run profiles (US7)

1. Open the run-profiles fixture (`Project`, `Executable` with `%ECHO_TOOL%` and `$(TargetDir)`, `IISExpress`): three
   entries, `IISExpress` disabled with its reason; the first runnable is selected.
2. Unset `ECHO_TOOL`, Run: nothing starts, NPR005 names the variable. Set it, Run: Output prints the resolved command
   line once; the echo tool prints its argv, working directory and `DOTNET_LAUNCH_PROFILE`.
3. Type a line in Output's input box: the tool echoes it; Close input: it prints EOF and exits.
4. Start the long-running profile, Stop: closed, or its tree killed after `run.stopTimeoutSeconds`. Start it again and
   close the project: "Leave it running?".
5. Select Release, Build: Release output; reopen the project: Release is still selected.
6. `netprints run <copy> -lp Executable -- a "b c"`: the same request with the replaced arguments;
   `-lp IISExpress` exits 2; `-lp nope` exits 2.
7. Windows and macOS (owner, manual): "Run in external terminal" opens a terminal window; Stop on Windows closes the
   main window first.

## 8. Theme presets (US8)

1. Appearance › Dark preset: Classic Dark. The canvas is `#000`, pins are triangles, nothing restarts. Switch to
   Light: NetPrints Light, with no Classic Dark value.
2. High contrast for Dark: Semi's high-contrast variant with NetPrints' tuned tokens.
3. Write `"darkPreset": "nope"`: the default Dark preset, a log entry, the other appearance keys kept.

## 9. C# target (US9)

1. For each workspace fixture (no `LangVersion`, `latest`, `preview`, `10.0`, `16.0`, multi-target):
   `netprints regen`, `dotnet build` (SDK target) and the editor's save write identical bytes (the test
   `CSharpTargetWriterIdentityTests`).
2. In the `10.0` fixture, search for a node that needs C# 11: hidden, "N results hidden by the project's C# version",
   "Show unavailable" shows it disabled with the reason. Apply "Set LangVersion": `<LangVersion>` is raised, at most to
   the framework's maximum.

## 10. Extension author kit (US10)

```bash
dotnet new install local-packages/NetPrints.Templates.*.nupkg --debug:custom-hive "$(mktemp -d)"
dotnet new netprints-extension -n Acme.Hello -o "$(mktemp -d)/acme" --debug:custom-hive <same hive>
```

1. Build and test the new extension and its test project with `local-packages/` as the feed: the 16 kit checks pass;
   `dotnet pack` produces a `NetPrintsExtension` package; `netprints extension verify` on it exits 0.
2. Break one rule per check (the fixtures under `tests/Fixtures/Extensions/Conformance/` do it): each fails with its
   NPK code and the offending item.
3. Verify a package built against a member the host lacks: exit 1, the member listed.
4. The PR's checks include the Godot extension job, which calls the reusable `netprints-extension.yml` workflow.

## 11. Carry-overs (US11)

1. The `Fx` fixture with a private dependency older than a transitive host reference: NPX008.
2. A project with an unreadable reference: the other references load; NPW009 is shown once.
3. Open a project and `NetPrints.Desktop --check-project <copy>`: no generated file is written (compare the tree).
4. Windows CLI leg: the CRLF catalog check test and the `git-install` path test pass (SC-015).
5. `dotnet format analyzers NetPrints.slnx --severity info --verify-no-changes`: 0 unrecorded hits in `src/`.
6. The startup warnings test shows no Dock `IsPinned` or capability "Value is null" warning; hovering the exact middle
   of a cable at the fit zoom shows its tooltip.

## 12. New projects from the command line (US15)

```bash
netprints new --list
netprints new console --name Hello --output "$(mktemp -d)/hello"
netprints new nope --name X        # exits 2
```

The created project builds and runs; its files equal `dotnet new netprints-console -n Hello` and the editor's New
project with the Console template (the test `TemplateParityTests`).

## 13. Tail (US16, US17): moved to P3b

Nothing to run in P3. P3b's quickstart covers inspector sections, value editors, graph rules, user theme files and
extension themes.

## 14. Docs and release readiness

`scripts/build-docs.sh`: 0 broken links (SC-017); the guides of FR-116 and the ADRs of the plan are present; the guide
images that carry the build version are regenerated (`scripts/guide-screenshots.sh`). No tag, push to NuGet or release
happens without the owner's approval.
