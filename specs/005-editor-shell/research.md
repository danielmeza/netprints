# Research: Editor Shell (P3a)

Each entry: **Decision**, **Rationale**, **Alternatives considered**. Entries that settle an architectural question
name their ADR. Web sources fetched for R2 are archived outside the repository (`.agent-archive/netprints-p3a/`,
not committed); the facts used are restated here.

## R1. Scope sources

- Roadmap P3a section (owner-approved 2026-09-25, with owner additions 2026-09-25 to 2026-09-28), the P3 "Carried
  over from P2" bullet, and the phases table.
- Constitution 1.2.3: I (Linux-first, CI Linux-only), II (UI-agnostic view models), III (extension-first), V (tests
  gate), VI (versioned, deterministic formats), VIII (one phase, one PR).
- UX audit `docs/research/2026-09-25-ux-audit/`: H1 (dirty state), H2 (two-level windows), H3 (round command
  buttons), H4 (keyboard), M3 (type and spacing system), M16 (undo feedback), L1 (window chrome, optional), L2
  (persisted layout).
- ADR-0004 (canvas popups), ADR-0006 (parallel desktop E2E), ADR-0007 (XAML practices) and the `avalonia-xaml`,
  `avalonia-behaviors` and `avalonia-styling` skills.
- `specs/004-catalog-cli/implementation-notes.md`: E-R15 and R15 (type-scoped search), FU-1 to FU-7 (carry-overs).
- Current code (surveyed 2026-10-01): `MainWindow` (launcher) and `ClassEditorWindow` (one per class, opened by
  `IWindowService.OpenClassEditor`); `MainEditorViewModel` (save, compile, run); `UndoRedoStack` with named
  `DelegateUndoableCommand`s and `Changed`/`Applied` events; `ClassGraph.IsDirty`/`MarkDirty`/`MarkClean` driven
  by `Applied`; `ShutdownCoordinator`; `JsonFileSettingsStore` (`<ApplicationData>/NetPrints/settings.json`);
  `IProjectProfile` with one built-in profile (`netprints.default`); `EditorStyles.axaml` with about 20 colour
  tokens and a 24-pixel header class; `GraphEditorView` code-behind with a global tunnel key handler (Ctrl+Space)
  and gesture handlers; XAML hygiene allowlists empty except one E2 entry (the Fluent palette in `EditorApp.axaml`);
  E2E scenarios in `tests/NetPrints.Desktop.E2ETests/Scenarios/` with `StepTimer` timings and `CheckpointAsync`
  tree dumps and screenshots, but nothing captured when a test times out.

## R2. Docking library (ADR-0018)

- **Decision**: adopt Dock.Avalonia 12.1.0.6 (`Dock.Avalonia`, `Dock.Model.Mvvm`, `Dock.Avalonia.Themes.Fluent`,
  `Dock.Serializer.SystemTextJson`), exact pin, behind an `IShell` seam with one adapter namespace
  (`NetPrints.Editor.Shell.Docking`); a spike gates it at the start of sub-phase C; fallback is a plain Avalonia
  layout (`Grid` + `GridSplitter` + `TabControl`, "open in new window" instead of floating).
- **Facts checked (2026-10-01)**: NuGet: Dock.Avalonia 12.1.0.6 published 2026-08-27, `net8.0`/`net10.0`, MIT,
  depends on Avalonia >= 12.1.1 (repo: 12.1.3). Stable Avalonia 12 releases since 12.0.0.1 (2026-04-09); 12.1.0
  (2026-07-29) moved to Avalonia 12.1; a separate `.v11` lane serves Avalonia 11. `Dock.Model.Mvvm` depends on
  CommunityToolkit.Mvvm >= 8.4.0 (repo: 8.4.2) and `Dock.Model`; ReactiveUI only via `Dock.Model.ReactiveUI`,
  Newtonsoft.Json only via `Dock.Serializer.Newtonsoft`; `Dock.Serializer.SystemTextJson` has no extra dependency on
  `net10.0`. Model: `Factory.CreateLayout()` → `IRootDock` with `DocumentDock`, `ToolDock`, `ProportionalDock`;
  view-model-first documents and tools. Serialization: `Save`/`Load` plus `DockState.Save/Restore` to re-attach
  app content; ids are app-defined; floating bounds and state persist; System.Text.Json source generation needs an
  assembly attribute. Floating: native windows by default, managed in-window floating as an option. Linux floating
  issues #1013, #229, #211, #169 are closed. The Dock repo has Avalonia.Headless test projects. One active
  maintainer, 10+ releases April–August 2026.
- **Unverified (the spike checks them)**: compiled-binding document templates, automation peers and ids on docked
  and floating content, floating under openbox, theming through the editor's tokens.
- **Rationale**: dock, float, tab groups and layout serialization are large to build and test; the library is
  maintained, MIT, on the repo's Avalonia line, and uses the repo's MVViewModel toolkit without ReactiveUI. The seam keeps
  Dock types out of feature view models, tests and the P3 public API, so a Dock regression or the fallback costs one
  adapter.
- **Alternatives**: plain `TabControl` + `GridSplitter` (kept as the fallback: no floating, own persistence);
  UniDock, Avalonia.UpDock, EmberDock (no verified Avalonia 12 support, smaller user bases); Actipro Docking
  (commercial). Adopting Dock without a seam (rejected: couples the P3 extension API to a single-maintainer
  library).

## R3. CI and test-infrastructure carry-overs (ADR-0019)

- **Decision**: the three CI items join P3a as User Story 1, sub-phase A: (a) Desktop E2E failure diagnostics,
  (b) the Linux test-project matrix with an aggregate "Build and test (Linux)" check, (c) the Windows CLI workflow
  with the UTF-8 `show --textconv` test. The four code items (FU-1 NPX008 host and transitive references, FU-2
  `ReflectionProvider` skipping unreadable references, FU-5 info-level analyzer backlog, FU-6 `ProjectCheck`
  generation skip) stay in P3; the roadmap's P3 bullet now lists only those.
- **E2E diagnostics design**: a `FailureCapture` in `tests/NetPrints.Desktop.E2ETests/Hosting/` runs from the test
  base's failure path (exception, assertion, the `CancelAfter` timeout of ADR-0006, or the editor process exiting).
  It writes `TestResults/e2e-diagnostics/<TestClass>/`: `timings.md` (the `StepTimer` entries plus the open step
  and its elapsed time), `ui-tree.json` (every window's automation tree via the existing automation pipe: id, name,
  type, bounds, visible, enabled, focused), `display.png` (the whole Xvfb display, so GTK dialogs and floating
  windows are included), `editor.log` (the editor's log file and stderr tail, last 400 lines), and `process.txt`
  (alive or exit code), and `run-state.json` (the last launched program's state, exit code and output tails,
  asked through the automation pipe, which issue #11 showed was missing). Each part is captured independently with its own short timeout, and a capture error is
  written into `capture-errors.txt` instead of masking the test failure. A test-only environment switch
  (`NETPRINTS_E2E_FORCE_TIMEOUT=<step>`) makes a step hang so a test can prove the capture. The CI `e2e-results`
  artifact already uploads `TestResults/`.
- **`EditCompileAndRun` flake** (issue #11: the editor took over four minutes to start and the run step timed out
  after 120 s without telling whether the program ran): no speculative fix. The scenario is rewritten for the shell in sub-phase C anyway;
  the diagnostics are expected to explain any recurrence, and a fix lands then with the evidence in
  implementation-notes.md.
- **Rationale**: P3a adds roughly ten E2E scenarios; without diagnostics every flake costs a re-run and guesswork,
  and the 13-minute serial job is paid by every batch.
- **Alternatives**: build once and pass outputs between jobs as an artifact (rejected: hundreds of megabytes per
  run and slower upload/download than a cached per-leg build); `dotnet test --solution` with parallel modules in
  one job (rejected: same runner, little gain, and it lost the per-module zero-test handling, see `ci.yml`); a
  Windows leg inside `CI` (rejected: the constitution keeps `CI` Linux-only; a separate workflow is the smaller
  deviation); running only a Windows-tagged subset of CLI tests (rejected: hides real Windows breakage that
  constitution I forbids).

## R4. Shell structure

- **Decision**: one `ShellWindow` per open project replaces `MainWindow` and `ClassEditorWindow`. `ShellViewModel` owns the
  project session (`ProjectSessionViewModel`), the `IShell` service, the documents and the panels. The class window's
  content splits into the **Project tree** panel (classes and their methods, constructors, variables and event
  graphs, with the add, remove and open actions), **graph documents** (one per method, constructor, event graph or
  class graph), and the **Inspector** panel (class, method, variable and event-entry inspectors). The launcher's
  settings pane becomes a **Project settings** document; References stays a dialog. Bottom panels: **Errors**
  (existing error list), **Output** (build output and the running program's standard output and error, which today
  go to the log), **C#** (existing `CodeView`, following the active document's class). A status bar shows
  messages, compile state and the undo feedback.
- **Undo scope**: each class keeps its own undo stack, as today; Undo and Redo act on the active document's class
  (or the tree selection's class when the tree has focus).
- **Rationale**: matches the UX audit's suggested fix and every reference editor (Unreal, Unity Shader Graph,
  Godot); keeps the existing view models (inspectors, graph editor, error list, code view) and moves them into panels
  instead of rewriting them.
- **Alternatives**: several projects per window (rejected: P3a has no multi-project model and the build, catalogs
  and extensions are per project); keeping per-class windows as an option (replaced by floating a graph tab, which
  gives the same multi-monitor use).
- **E2E impact**: `MinimizeAndRestoreClassWindowTests` is replaced by `FloatAndRedockGraphTests`; the shared
  `SmokeScenarios` flows (create project, edit-compile-run, add references, pan, drag from lists) are rewritten
  against the shell's automation ids.

## R5. Commands, menus, shortcuts and contributions (ADR-0020)

- **Decision**: one `IContributionRegistry` with seven kinds; every surface generated from it; UI-free descriptors;
  shortcut scopes `Global`, `Graph`, `ProjectTree`; duplicates and gesture conflicts reported, first wins.
- **Shortcut set (FR-034)**: commands that exist or that P3a adds, plus select all, frame selection and fit all
  (thin wrappers over Nodify's editor commands). Deferred to P6: node clipboard (copy, cut, paste, duplicate),
  arrow-key nudge, Tab or Space to open search at the cursor (Ctrl+Space keeps working). User-defined shortcuts
  wait for the P3 settings pages.
- **Undo feedback (M16)**: Edit menu labels `Undo <name>`/`Redo <name>` from `IUndoableCommand.Name`, the same
  text in the command bar tooltips, disabled states from `CanUndo`/`CanRedo`, and a status message for 4 seconds.
  The history list is P6.
- **Run and Stop**: the run starts the program through `IProcessLauncher`; Stop cancels the run's token and kills
  the process tree. Exit while a program runs asks to stop it.
- **Rationale**: H3/H4 fixes and the P3 contribution point come from the same mechanism; descriptors without UI
  types satisfy constitution II and can move to a public package in P3.
- **Alternatives**: hand-written menus and `KeyBinding`s per view (rejected: drifts, untestable as a whole);
  Avalonia `NativeMenu` (rejected for the main menu: not shown on Linux window managers; it can mirror the menu on
  macOS later).

## R6. Document lifecycle (H1)

- **Decision**:
  - Unit of unsaved state: the file (class graph file, project file).
  - `ClassGraph.IsDirty` stays the model flag. `UndoRedoStack` gains a saved-state marker (the undo depth and the
    identity of the top command at save time), so undoing back to it marks the class clean. Edits outside the undo
    stack (inspector edits that are not undoable) mark it dirty unconditionally.
  - The project file is unsaved only if a project-level change is pending. If the current references and project
    settings flows write the file immediately, they keep doing so and the project file never shows as unsaved (to
    be checked in sub-phase D).
  - The prompt runs from one place, `ShellViewModel.ConfirmUnloadAsync`, called by window close (through
    `ShutdownCoordinator`), Exit, Close project, Open project and New project.
  - Backups: a per-file debounce of 30 seconds after the last change writes the file's serialized content to
    `<ApplicationData>/NetPrints/backups/<project-key>/<relative-path>.bak.json` plus `manifest.json` (original path,
    written time, file hash). `<project-key>` is the first 16 hex characters of the SHA-256 of the project file's
    full path. A save or Don't save deletes that file's backup; a project with no remaining backups loses its
    folder. At startup, backups older than 30 days or whose project no longer exists are deleted.
  - Recovery runs when a project is opened and its backup folder is not empty.
  - Writes are atomic (temporary file, then rename) and user-only on Unix (0600 files, 0700 folders).
- **Rationale**: no silent data loss, nothing written into the user's repository (backups next to graphs would show
  up in `git status` and in the build), and no surprise overwrite of originals.
- **Alternatives**: `.bak` next to each graph (rejected: pollutes the repository and the SDK's item globs);
  autosaving originals (rejected for P3a: surprises users who rely on git to review changes; a P3 setting can add
  it); prompting on tab close (rejected: closing a tab loses nothing).

## R7. Per-user state store (L2)

- **Decision**: `IEditorStateStore` (editor-internal) with files under `<ApplicationData>/NetPrints/state/`:
  `window.json`, `layout.json` (the ADR-0018 envelope), `recent.json`, and `sessions/<project-key>.json` (open
  tabs, active tab, per-graph viewport location and zoom). Each file carries `schemaVersion: 1`; a newer or
  unreadable file is ignored with a warning log and defaults are used. The theme is an editor preference in the
  existing `JsonFileSettingsStore` (`settings.json`) under the editor's own settings id, through the P1 settings
  API. Writes are atomic and user-only on Unix. State is saved on project unload and on exit, plus the layout when it
  changes (debounced).
- **Rationale**: preferences (what the user chose) and state (where the user left off) have different lifetimes;
  the P1 settings store already handles preferences; state is per-machine and must never enter a repository.
- **Alternatives**: a `.netprints/` folder in the project (rejected: needs `.gitignore` handling and leaks layout
  into repositories); one big state file (rejected: concurrent instances and partial corruption would lose
  everything).

## R8. Start dashboard

- **Decision**: the start page is a document-area page shown when no project is open, built from registered
  dashboard tiles: Recent, Open, New project, Samples, What's new.
  - Recent list: at most 20 unpinned entries, pinned first. Search matches name and path (case-insensitive
    substring). Missing paths are shown as unavailable.
  - Project templates are contributions: built-ins **Console app** and **Class library** both use the
    `netprints.default` profile, with output type `Exe` and `Library`. UnrealSharp arrives with NetPrintsUnreal
    (U1) as an extension contribution, and Unity later. Creating a project validates the name (a valid C#
    identifier for the namespace and a valid file name) and requires an empty or new folder. A partly created
    folder is removed on failure.
  - Samples are the repository's `samples/HelloWorld` (the `.csproj` and the graph and generated files, excluding
    `bin/`, `obj/` and `Compiled_HelloWorld/`), bundled with the desktop app as content and copied to a chosen folder
    before opening.
  - What's new is a bundled `WhatsNew.md` for the running minor version, rendered as headings and bullet lists
    without a Markdown library, with a link to the GitHub releases page.
- **Rationale**: replaces the launcher with the reference-editor pattern (VS, Rider, Unity Hub); tiles and
  templates are contribution kinds so P3 extensions add their own.
- **Alternatives**: keeping the launcher window (rejected by H2); a Markdown rendering dependency (rejected: one
  short page does not justify it); downloading samples (rejected: no network access in the editor).

## R9. Navigation basics

- **Decision**: command palette over the registry; go-to-anything over registered `IGoToProvider`s (built-ins:
  graphs, nodes, variables, methods; commands after `>`), ranked by prefix match, then word-start match, then
  substring, then name. Connection navigation: Ctrl+click goes to the end farther from the click point; the
  connection context menu offers Go to source and Go to target. A `NavigationHistory` (at most 50 entries) records
  graph, viewport and selection before each navigation, available through Alt+Left, Alt+Right and the Go menu.
  Connection tooltips come from `ITooltipProvider`s; the built-in one shows `Node.pin → Node.pin`, the data type or
  "execution", and the XML documentation of the members on both ends. Breadcrumbs: Project › Class › Graph.
- **Mouse buttons**: XButton1/XButton2 stay as they are (one toggles "faint"); mapping them to history is a P6
  gesture review item.
- **Alternatives**: fuzzy scoring libraries (rejected: one ranking function is enough; P8 owns search speed).

## R10. Type-scoped search and embedded catalogs (E-R15)

- **Decision**: a type-scoped query (`WithType(T)`) asks the composite provider which source covers T's assembly.
  If an embedded or extension catalog covers it, the members come from that catalog only, with the catalog's own
  rules (omitted and `[NetPrintsIgnore]` members absent). If the catalog does not list T, the result is empty with
  a "hidden by catalog <id>" reason that the search view shows. If no catalog covers the assembly, the live provider
  answers as today. Inherited members come from whichever source covers the base type's assembly. Project types
  (the user's own classes) are always live. `GetTypeFromSpecifier` and member binding of existing nodes do not
  change.
- **Rationale**: one answer for "what does this library offer to graphs" everywhere (global search, scoped
  search, the CLI catalog); the library author's `[NetPrintsIgnore]` and profile choices are the contract P2
  introduced; binding stays tolerant, so no existing graph breaks.
- **Alternatives**: keep the live answer (rejected: contradicts the catalog the user sees elsewhere and E-R15's
  finding); a "show all members" toggle in scoped search (deferred to P6 if users ask: it reintroduces the
  inconsistency on demand); warn on existing nodes that use hidden members (deferred: a P6 diagnostics item).

## R11. Visual system (M3, L1)

- **Decision**:
  - Tokens in `EditorStyles.axaml`:
    - type ramp: `Font.Caption` 12, `Font.Body` 14, `Font.Subtitle` 16, `Font.Title` 20;
    - spacing: `Space.XS` 4, `Space.S` 8, `Space.M` 12, `Space.L` 16, `Space.XL` 24, as `Thickness` and `double`
      resources;
    - colours as `Area.Role` tokens with Dark and Light values in `ThemeDictionaries`;
    - an `Inspector.LabelColumnWidth` token.
  - The Fluent palette in `EditorApp.axaml` moves into theme dictionaries, which empties the last E2 allowlist
    entry.
  - Nodify gets `ControlTheme` overrides based on its defaults (`BasedOn`) for `NodifyEditor`, `Node`,
    `Connection`, `Connector` and `ItemContainer`, using `DynamicResource` tokens.
  - `NodeKindBrushConverter` is replaced by style classes per node kind (avalonia-styling D5 known debt).
  - View › Theme sets `RequestedThemeVariant` (Dark default, Light, System).
  - L1's custom title bar is deferred to P6; P3a sets the title text only.
- **Rationale**: the audit's suggested fix; Light support is the only way to prove every colour is a token (SC-008).
- **Alternatives**: Dark only (rejected: leaves tokens unprovable and the D5 debt in place); a custom title bar now
  (rejected: platform-specific hit-testing that Linux CI cannot verify).

## R12. Xaml.Behaviors adoption and the code-behind allowlist

- **Decision**: every remaining `*.axaml.cs` event handler is either migrated (prebuilt behavior first, then a custom
  behavior, ADR-0007 D11) or kept with a reason. A new enforced rule **E7** in `XamlHygieneTests` lists the
  methods in each view's code-behind that handle events (`object? sender, …EventArgs e` signatures and overrides of
  `On*`). They must appear in a shrink-only `CodeBehindAllowlist` with a reason (gesture, viewport math, editor
  interop, focus plumbing). ADR-0007 gets an amendment note and the `avalonia-xaml` skill documents E7.
  `GraphEditorView`'s global Ctrl+Space handler moves to the registry's key bindings (ADR-0020).
- **Rationale**: the roadmap's done-when ("allowlists empty or only justified gestures") becomes a build-time
  check instead of a review judgement.
- **Alternatives**: a review checklist only (rejected: not enforced, and the existing E rules show allowlists work).

## R13. Testing and guide screenshots

- **Decision**:
  - View-model unit tests (`NetPrints.Editor.Tests`) for every command handler, the registry, the lifecycle, the state store,
    navigation and recent projects.
  - Headless tests (`NetPrints.Editor.UITests`) for the wiring: generated menus and key bindings, the docking adapter
    with layout round trips, the theme tokens in both variants, and the dialogs.
  - Desktop E2E scenarios, one class each (ADR-0006): `ShellMainFlowTests`, `UnsavedChangesPromptTests`,
    `CrashRecoveryTests`, `FloatAndRedockGraphTests`, `ResetLayoutTests`, `RestoreSessionTests`,
    `StartPageNewProjectTests`, `CommandPaletteTests`, `GoToAnythingTests`, `KeyboardOnlyTests` and
    `EventEntryInspectorTests`, plus the rewritten shared smoke flows.
  - Guide screenshots come from `GuideScreenshotTests`, an opt-in E2E class (`NETPRINTS_GUIDE_SHOTS=1`) that drives
    the sample through each guide's states and writes PNGs to `website/static/img/guide/editor/`. They are run by
    `scripts/guide-screenshots.sh`, committed, and regenerated when the UI changes.
- **Rationale**: one place produces reproducible screenshots; CI does not regenerate them (rendering under
  `llvmpipe` is stable, but committed images change only on purpose).
- **Alternatives**: manual screenshots (rejected: drift and not reproducible); a CI job that diffs the screenshots
  (deferred: P6 visual diff).

## R14. Deferred and follow-ups (not P3a)

- P6: custom title bar (L1); undo history list; node clipboard, duplicate, nudge, Tab-to-search; mouse back and
  forward buttons; context menus beyond navigation and the tree; a "show all members" toggle and hidden-member
  warnings (R10); external file-change detection; visual diff of graphs.
- P3: public contribution API and extension contributions; settings pages and user-defined shortcuts; inspector
  sections from extensions; the four P2 code carry-overs (FU-1, FU-2, FU-5, FU-6).
- P8: any performance work (search ranking speed, large-graph rendering, E2E sharding across runners).
- Later: the Velopack update notice on the start page; macOS native menu mirroring; localization.

## R15. Documentation

- **Decision**: new guide section `docs/guide/editor/` with six pages:
  - `shell.md` (layout, panels, docking and reset);
  - `start-page.md`;
  - `saving-and-recovery.md`;
  - `commands-and-shortcuts.md` (with the shortcut table generated from the registry by a test that fails when the
    page is stale);
  - `navigation.md`;
  - `event-graphs.md`.
  Updates to `docs/guide/projects.md` (create and open through the start page), `docs/contributing/` (E2E
  diagnostics and the CI matrix), the ADR index (0018–0020), ADR-0007's amendment note (E7), and the `avalonia-*`
  skills.
- **Rationale**: the roadmap's P3a done-when and the owner's editor-guides decision (2026-09-28).

## R16. View model naming (owner decision 2026-10-01)

- **Decision**: view model types end in `ViewModel`. The 24 `*VM` types on master are renamed first, in one
  mechanical commit at the start of sub-phase A, before any new shell code: types, file names, `x:DataType`,
  bindings and casts in XAML, tests, docs, skills and ADR mentions. In the editor's own namespaces, a rename that
  would collide with an existing type is resolved case by case, and the resolution is recorded in
  implementation-notes.md. A `SourceHygieneTests` check, `NoTypeNameEndsInVM`, scans every C# type declaration in
  `src/` and `tests/` (classes, records, structs and interfaces, including generic ones such as
  `DialogViewModel<TResult>`) and fails on a name ending in `VM`. `MVVM` and other names where `VM` is not a suffix
  of a type name are unaffected. The rule is recorded as an amendment to ADR-0007, which owns the editor's
  view-layer conventions. The `avalonia-*` skills mention it in the same batch.
- **Rationale**: an owner decision. Abbreviated suffixes hide intent. Renaming before the shell work means every
  new P3a type follows the rule and nothing is renamed twice.
- **Alternatives**: rename gradually as files are touched (rejected: mixed naming for the whole phase, and P3a
  replaces most of those views anyway); record the rule only in the plan's conventions (rejected: the rule
  outlives P3a, so it belongs in the ADR that later phases read).

Names of current types in this spec's documents already use the new form (for example `MainEditorViewModel`,
`DialogViewModel<TResult>`).
