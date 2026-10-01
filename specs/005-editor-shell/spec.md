# Feature Specification: Editor Shell

**Feature Branch**: `005-editor-shell`

**Created**: 2026-10-01

**Status**: Draft

**Input**: Coordinator description: "Phase P3a of the NetPrints modernization, 'Editor shell', as scoped in
`.specify/memory/roadmap.md` (P3a section): one window with a project tree, tabbed graphs, an inspector and a
bottom panel (Errors / Output / C#) replacing the launcher and the per-class windows (UX audit H2); a command
registry feeding a command bar, a menu and keyboard shortcuts (H3, H4), which is also the P3 contribution point;
document lifecycle (H1: dirty flag, close prompt, autosave or backup); design tokens and Nodify theme overrides
(M3, L1); persisted layout, tabs, zoom and window position (L2); undo feedback (M16); docking on Dock.Avalonia
(decide in the plan, with a fallback); a start dashboard; internal contribution points; the type-scoped search
decision deferred from P2 (E-R15); Xaml.Behaviors across the editor; navigation basics; the event graph and entry
inspector; screenshot editor guides. Three CI and test-infrastructure carry-overs from P2 join as an early story:
Desktop E2E timeout diagnostics, a test-project matrix for the Linux build-and-test job, and a Windows CLI leg with
a UTF-8 `show --textconv` test."

**Roadmap phase**: P3a (depends on P0 and P1; P2 merged as PR #9, `3a6eafc`). **Done when** (roadmap, owner
decisions 2026-09-28): the phase's features work end to end and the docs are updated (guides, API reference, ADRs),
including the screenshot editor guides.

**Sources reused** (see [research.md](./research.md) R1): roadmap P3a, the P3 "Carried over from P2" bullet and the
phases table; constitution 1.2.3; `docs/research/2026-09-25-ux-audit/` findings H1–H4, M3, M16, L1 and L2;
ADR-0004, ADR-0006 and ADR-0007; the `avalonia-xaml`, `avalonia-behaviors` and `avalonia-styling` skills;
`specs/004-catalog-cli/implementation-notes.md` (E-R15 and the final review's FU-1 to FU-7).

## Clarifications

### Session 2026-10-01

No interactive user was available (owner instruction: decide every question and record it). Each question was
answered with the default most consistent with the constitution, the roadmap and the UX audit. Architectural
answers have an ADR; the rest are in research.md.

- Q: Does type-scoped search (a search opened from a pin of a type) respect the catalog that covers the type's
  assembly? → A: Yes. When an embedded or extension catalog covers the assembly, the search lists only the members
  that catalog lists, so omitted and `[NetPrintsIgnore]` members are hidden; the live provider answers only for
  assemblies no catalog covers. If the covering catalog does not list the type at all, the search shows no members
  and says which catalog hides them. Binding existing nodes through `GetTypeFromSpecifier` is unchanged, so a graph
  that already uses a hidden member still loads, builds and runs (research R10).
- Q: What is the unit of "unsaved changes", and when does the editor prompt? → A: The unit is a file: each class
  graph file and the project file. A tab shows `*` when the file behind it is unsaved, and the window title shows
  `*` when any file is unsaved. Closing a tab never prompts, because the class stays loaded with the project. The
  prompt (Save all / Don't save / Cancel, listing the unsaved files) appears whenever the project would be unloaded:
  closing the window, Exit, Close project, opening or creating another project (research R6).
- Q: Autosave or backup? → A: Backup only. A file with unsaved changes is backed up to the per-user data folder
  within 30 seconds of its last change, never next to the project, so nothing reaches the user's repository. The
  next time the project is opened after a crash, the editor offers to restore the backups. The original files are
  written only by an explicit save; autosaving originals is not offered in P3a (research R6).
- Q: Which P2 carry-overs join P3a? → A: The three CI and test-infrastructure items (Desktop E2E timeout
  diagnostics, the test-project matrix with an ADR, the Windows CLI leg with the UTF-8 `show --textconv` test), as
  User Story 1, done first because P3a adds many Desktop E2E scenarios. The four code items stay in P3: NPX008 host
  and transitive references, the live `ReflectionProvider` skipping unreadable references, `ProjectCheck`
  generation skip, and the info-level analyzer backlog (ADR-0019, research R3).
- Q: Is the docking library adopted? → A: Yes, Dock.Avalonia 12.1.0.6, behind a shell seam and gated by a spike at
  the start of the shell work, with a plain Avalonia layout as the fallback; no feature or test depends on the
  library directly (ADR-0018, research R2).

Further defaults decided in the same session (each recorded in research.md):

- One project per editor window; opening another project replaces the current one after the unsaved-changes
  prompt. Floating a graph tab replaces "open class in its own window" (R4).
- Per-user state (layout, recent projects, per-project sessions, backups) lives in the per-user application-data
  folder next to the existing `settings.json`, never in the project directory; editor preferences (theme) use the
  existing settings store (R7).
- Project templates: "Console app" and "Class library" are built in; the UnrealSharp template is contributed by the
  NetPrintsUnreal extension (U1) through the same contribution point; Unity comes later (R8).
- Keyboard shortcuts: P3a binds every command that exists or that P3a adds, plus select all, frame selection and
  fit all. Node clipboard (copy, cut, paste, duplicate), arrow-key nudge and Tab-to-search stay in P6; user-defined
  shortcuts wait for the P3 settings pages (R5).
- Undo feedback: action names in the Edit menu and tooltips plus a status message; the undo history list stays in
  P6 (R5).
- Visual system: Dark stays the default, Light is supported and "System" follows the OS. The custom title bar
  (UX audit L1) is deferred to P6; P3a keeps native window decorations and gives the title the project name, the
  active document and the unsaved marker (R11).
- The connection-navigation mouse gesture is Ctrl+click; the mouse back and forward buttons are not mapped to the
  navigation history in P3a, because a mouse button already toggles "faint" (R9).

Coverage scan (clarify pass, same session): the categories left Partial after specify were resolved with these
defaults, applied to the sections named:

- Backup lifecycle: backups older than 30 days, and backups whose project path no longer exists, are deleted when
  the editor starts (FR-024, R6).
- Privacy of per-user files: backups and state files hold project content and paths, so on Linux and macOS they are
  created readable by the user only (FR-052, R7).
- Localization: the UI stays English-only in P3a; strings are kept in one place per view so a later phase can
  localize them (Assumptions).

Every other category was Clear: performance is out of scope by owner decision (FR-104, P8), there is no
authentication or network access, and observability is the existing logging plus the E2E diagnostics of US1.

## User Scenarios & Testing *(mandatory)*

The stories are ordered by priority. **MVP slice: User Stories 1–4** (CI diagnostics first, then the single window,
safe saving, and commands with shortcuts). Each later story is independently testable on top of the MVP.

### User Story 1 - Diagnosable, faster CI for the shell work (Priority: P1)

A maintainer whose pull request fails a Desktop E2E test can tell why from the CI artifacts alone: which step was
running, how long each step took, what the editor's UI looked like, and what the editor logged. The Linux test job
is split so each test project runs in parallel, and a Windows job checks the command-line tool on Windows.

**Why this priority**: P3a rewrites most of the editor's UI and adds many Desktop E2E scenarios. The known flake
(`EditCompileAndRun` timing out) cannot be diagnosed today, and the 13-minute Linux job slows every batch. This
infrastructure lands first so the rest of P3a is built on it.

**Independent Test**: Force a timeout in an E2E scenario through a test-only switch; the run's artifacts hold the
step timings, a UI dump, a screenshot and the editor log for that test. Open any pull request: the Linux tests run
as parallel per-project jobs under one aggregate check, and the Windows job runs the CLI tests.

**Acceptance Scenarios**:

1. **Given** a Desktop E2E scenario that exceeds its time budget, **When** it is cancelled, **Then** the failure
   message names the step that was running and its elapsed time, and the run's artifacts hold, in a folder named
   after the test: the step timings so far, a dump of the editor's UI tree (automation id, name, type, visibility,
   enabled state and bounds of each element, and the focused element), a screenshot of the whole display, the
   tail of the editor's log and standard error, and the state and output of the program the editor launched.
2. **Given** a Desktop E2E scenario that fails an assertion or whose editor exits unexpectedly, **When** the test
   ends, **Then** the same diagnostics are captured; when the editor is no longer running, the dump records its exit
   code instead of the UI tree.
3. **Given** a pull request, **When** CI runs, **Then** each test project runs in its own parallel job, the
   repository checks (solution build, graph and format checks, CLI smoke, sample compile and run, generated files
   unchanged) run in one job, and an aggregate check that keeps the name "Build and test (Linux)" passes only when
   every one of them passed.
4. **Given** a pull request, **When** CI runs, **Then** a Windows job builds the command-line tool and runs the CLI
   test project, including a test that runs `netprints show --textconv` on a graph with non-ASCII names and checks
   that the output bytes are UTF-8 without a byte order mark.
5. **Given** a change that makes the tool write non-UTF-8 output on Windows, **When** CI runs, **Then** the Windows
   job fails and names the test.

---

### User Story 2 - Work in one window (Priority: P1)

A developer opens a project and works in one window: a project tree on the left, graphs as tabs in the centre, the
inspector on the right and a bottom panel with Errors, Output and C#. Panes can be moved, tabbed together, floated
into their own window and docked back.

**Why this priority**: The launcher plus one maximized window per class (UX audit H2) is the largest day-to-day
friction, and the P3 extension host needs panels and documents to contribute to. Every later story builds on it.

**Independent Test**: Open the HelloWorld sample, open two method graphs as tabs, compile with an error, activate
the error, fix it, run, and read the program output, without any second OS window opening; float a graph tab,
edit in it, and dock it back.

**Acceptance Scenarios**:

1. **Given** the editor is started on a project, **When** it opens, **Then** one window shows the project tree
   (the project, its classes, and per class its methods, constructors, variables and event graphs), the document
   area, the inspector and the bottom panel with Errors, Output and C# tabs, plus a menu, a command bar and a status
   bar.
2. **Given** an open project, **When** the user opens a method, constructor or event graph from the tree
   (double-click or Enter), **Then** its graph opens as a tab; opening a graph that is already open activates its
   tab; tabs can be reordered, and closed with their close button, a middle-click or Ctrl+W.
3. **Given** a selection in the tree or in a graph, **When** it changes, **Then** the inspector shows the selected
   class, method, constructor, variable or event entry.
4. **Given** a project with a compile error, **When** the user compiles, **Then** the Errors tab lists it; activating
   the error opens its graph's tab and selects the node.
5. **Given** a project that compiles, **When** the user runs it, **Then** the Output tab shows the build output and
   the program's output; the C# tab always shows the generated C# of the active graph's class.
6. **Given** the default layout, **When** the user drags a pane to another side, tabs it with another pane, floats
   it or docks it back (or uses the matching View menu commands), **Then** the pane keeps its content and state;
   closing a pane hides it and the View menu shows it again; **View › Reset layout** restores the default layout.
7. **Given** a graph tab floated into its own window, **When** the user edits, undoes and saves there, **Then** it
   behaves exactly as when docked.
8. **Given** an open project, **Then** every action the class window and the launcher offered (class settings,
   adding and removing methods, constructors, variables and event graphs, references, project settings) is reachable
   from the shell's menu, command bar, tree context menu or inspector.

---

### User Story 3 - Never lose work (Priority: P1)

The editor shows what is unsaved, asks before discarding it, and recovers it after a crash.

**Why this priority**: UX audit H1: today closing the editor silently discards changes and nothing is backed up.
Silent data loss is the most damaging defect a single-window editor can have.

**Independent Test**: Edit a graph and see `*` on its tab and in the title; close the window and choose Cancel,
then Save all; edit again, kill the editor process after the backup interval, restart, and restore the change.

**Acceptance Scenarios**:

1. **Given** a saved project, **When** the user changes a graph, **Then** the graph's tab, its class in the tree and
   the window title show `*`; saving removes the marker; undoing back to the saved state also removes it.
2. **Given** unsaved changes, **When** the user closes the window or chooses Exit, **Then** a prompt lists the
   unsaved files with Save all, Don't save and Cancel; Cancel keeps the editor open with the changes; Save all saves
   and closes; a save that fails shows the error and keeps the editor open.
3. **Given** unsaved changes, **When** the user closes the project, opens another project or creates a new one,
   **Then** the same prompt appears first.
4. **Given** unsaved changes in a class, **When** the user closes one of its graph tabs, **Then** no prompt appears
   and the changes stay in the project, still marked in the tree and the title.
5. **Given** a file left unsaved for longer than the backup interval, **When** the editor process is killed and the
   project is opened again, **Then** the editor offers to restore it; Restore loads the backed-up content, marked as
   unsaved; Discard deletes the backups.
6. **Given** a backed-up file, **When** the user saves it, or chooses Don't save, **Then** its backup is deleted;
   no backup is ever written inside the project directory.

---

### User Story 4 - Commands, menus and shortcuts (Priority: P1)

Every action is a named command, shown in a menu with its shortcut, in a compact command bar with icons, and runnable
from the keyboard. Undo and redo say what they will undo.

**Why this priority**: UX audit H3 and H4: the 100-pixel round text buttons are the dominant visual, and only
Delete, Ctrl+Z and Ctrl+Y work from the keyboard. The command registry is also the contribution point that P3 opens
to extensions.

**Independent Test**: Use only the keyboard to save, compile, run, undo and redo; open Help › Keyboard shortcuts
and see every command; register a test command and see it in the menu, the command palette and the shortcuts list.

**Acceptance Scenarios**:

1. **Given** an open project, **Then** a command bar of at most 40 pixels shows icon-and-label buttons for Save,
   Compile (with an error-count badge after a compile), Run or Stop (showing whether the program runs), Undo, Redo,
   Class settings and References, each with a tooltip naming the command and its shortcut.
2. **Given** the menu bar (File, Edit, View, Go, Build, Help), **Then** every command is reachable from a menu
   item that shows its shortcut, and a command that cannot run is shown disabled.
3. **Given** the default shortcuts (FR-034), **When** the user presses one where it applies, **Then** its command
   runs; single-letter shortcuts act only while the graph canvas has focus, never while typing in a text field.
4. **Given** an undoable action ("Add node"), **Then** the Edit menu shows "Undo Add node", the Undo button's
   tooltip names it, and after undoing the status bar briefly shows "Undid: Add node"; with nothing to undo, Undo is
   disabled; the same applies to Redo.
5. **Given** Help › Keyboard shortcuts, **Then** a sheet lists every command with its shortcut, grouped as in the
   menu, built from the registered commands.
6. **Given** a contribution registered at startup (a command, a panel, a dashboard tile, a project template, a
   context-menu item, a tooltip provider or a go-to provider), **Then** it appears in every surface of its kind
   exactly as a built-in one does; two contributions with the same id, or two commands with the same shortcut in
   the same scope, are reported and the first registered wins.

---

### User Story 5 - Start dashboard (Priority: P2)

With no project open, the editor shows a start page: recent projects (pin, search, remove), open a folder or a
`.csproj`, create a project from a template, open a sample, and what is new in this version.

**Why this priority**: It replaces the launcher window that User Story 2 removes, and is the first screen every user
sees. The MVP can open projects from the File menu, so it is P2.

**Independent Test**: Start the editor with no arguments, create a console project from its template, close it, and
reopen it from the recent list; pin it, search for it, remove it from the list.

**Acceptance Scenarios**:

1. **Given** the editor starts with no project argument, or the user closes the project, **Then** the start page
   shows the recent projects (pinned first, then most recently opened), "Open folder or project", "New project",
   the samples and "What's new".
2. **Given** a recent project, **When** the user pins, unpins or removes it, or types in the search box, **Then**
   the list updates and the change survives a restart; removing never deletes files; a project whose path no longer
   exists is shown as unavailable with a remove action.
3. **Given** "New project", **When** the user chooses a template (Console app, Class library, or one contributed by
   an extension), a name and a folder, **Then** the project is created from that template's project profile, opened,
   and added to the recent list; a name that is invalid or a folder that is not empty is rejected with a message
   before anything is written.
4. **Given** a sample, **When** the user opens it, **Then** the sample is copied to a folder the user chooses and
   the copy is opened; the bundled sample is never modified.
5. **Given** "What's new", **Then** it shows the release notes of the running version and a link to the full
   release notes.

---

### User Story 6 - Pick up where you left off (Priority: P2)

The editor remembers the window, the layout, the open tabs and each graph's zoom and position, and restores them.

**Why this priority**: UX audit L2. Re-opening and re-arranging the same graphs after every restart is daily
friction, but nothing is lost without it.

**Independent Test**: Rearrange panes, open three graphs, zoom and pan one of them, move and resize the window,
restart: everything is restored. Corrupt the state file: the editor starts with the default layout.

**Acceptance Scenarios**:

1. **Given** a changed layout, window position, size and maximized state, **When** the editor restarts, **Then**
   they are restored; a saved position that is off every current screen is replaced by a centred window on the
   primary screen.
2. **Given** open graph tabs with an active one, and graphs with changed zoom and position, **When** the project is
   reopened, **Then** the same tabs open in the same order with the same one active, and each graph shows the same
   zoom and position.
3. **Given** a session that names a graph that no longer exists, or a panel no longer registered, **When** it is
   restored, **Then** that entry is skipped and the rest is restored.
4. **Given** a state file that is unreadable or from a newer version, **When** the editor starts, **Then** it uses
   the defaults, logs the reason and never fails to start.

---

### User Story 7 - Find and navigate (Priority: P2)

A command palette and "go to anything" find commands, graphs, nodes, variables and methods by typing; connections
can be followed to their other end; a history goes back and forward; breadcrumbs show where you are.

**Why this priority**: Owner ideas (2026-09-25) that make a single-window editor fast to move around in; they need
the shell (User Story 2) and the registry (User Story 4) first.

**Independent Test**: Press Ctrl+Shift+P and run "Compile" by typing; press Ctrl+P, type a node title and land on
it; Ctrl+click a connection to jump to its other end, then Alt+← to return to the same view.

**Acceptance Scenarios**:

1. **Given** Ctrl+Shift+P, **When** the user types, **Then** the palette lists matching commands with their
   shortcuts and menu path; Enter runs the selected one; a disabled command is shown but cannot run.
2. **Given** Ctrl+P, **When** the user types, **Then** matching graphs, nodes, variables, methods and (after a
   leading `>`) commands are listed, grouped by kind; Enter opens the graph and centres and selects the item.
3. **Given** a connection, **When** the user Ctrl+clicks it, **Then** the view moves to the end farther from the
   click and selects that node; the connection's context menu offers "Go to source" and "Go to target" naming the
   node and pin.
4. **Given** navigation by go-to, by an error, by a connection or by switching tabs, **When** the user presses
   Alt+← or Alt+→ (or the Back and Forward commands), **Then** the previous or next view is restored: the graph,
   its zoom and position, and the selection.
5. **Given** the pointer resting on a connection, **Then** a tooltip shows `Node.pin → Node.pin`, the data type (or
   "execution"), and the documentation of the members on both ends when available.
6. **Given** an open graph, **Then** breadcrumbs above it show Project › Class › Graph, and choosing a segment
   reveals it in the project tree.

---

### User Story 8 - Event graphs and entries in the inspector (Priority: P2)

Event graphs can be renamed, and selecting an event entry shows its properties: its name and arguments, editable
for custom events and read-only for overrides.

**Why this priority**: Owner report (2026-09-27): P1 names event graphs `EventGraph`, `EventGraph1`, … and entries
`CustomEvent1`, … with no way to rename them or give a custom event arguments.

**Independent Test**: Rename an event graph inline and in the inspector; select a custom event entry, rename it, add
two arguments, compile, and see the method and its parameters in the generated C#.

**Acceptance Scenarios**:

1. **Given** an event graph, **When** the user renames it inline in the tree (F2) or in the inspector, **Then** the
   tree, its tab and the breadcrumbs show the new name; the name is not emitted in the C#.
2. **Given** an event entry selected on the canvas, or its graph opened, **Then** the inspector shows its name, its
   kind (custom event or override) and its arguments.
3. **Given** a custom event entry, **When** the user renames it to a name used by another method or entry of the
   class, **Then** the rename is refused with a message (P1 FR-027); a unique name is applied and appears in the
   generated C#.
4. **Given** a custom event entry, **When** the user adds, renames, retypes, reorders or removes an argument,
   **Then** the entry's output pins and the generated method signature follow, and each change is one undoable
   action.
5. **Given** an override entry, **Then** its name and signature come from the base method and are shown read-only.

---

### User Story 9 - A consistent look (Priority: P3)

The editor uses one type ramp, one spacing scale and one set of colour tokens, in Dark and Light, including the node
canvas.

**Why this priority**: UX audit M3: headers are 24-pixel centred text, sizes and margins are ad hoc, and the canvas
brushes are Dark-only. It matters for polish, not function.

**Independent Test**: Switch View › Theme between Dark, Light and System and see every pane and the canvas follow;
the hygiene tests report no colour literal in any view and every token defined in both variants.

**Acceptance Scenarios**:

1. **Given** the shell, **Then** panel headers, labels, body text and captions use the type ramp (Caption 12,
   Body 14, Subtitle 16, Title 20), spacing comes from the scale (4, 8, 12, 16, 24), and inspector labels share one
   column width.
2. **Given** View › Theme, **When** the user picks Dark, Light or System, **Then** every pane, the command bar, the
   graph canvas, the nodes and the connections switch, and the choice survives a restart.
3. **Given** the views, **Then** no view contains a colour literal and every colour token has a Dark and a Light
   value.

---

### User Story 10 - Type-scoped search respects catalogs (Priority: P3)

A search opened from a pin of a type in a library that ships a catalog shows the same members the library's catalog
offers everywhere else.

**Why this priority**: Deferred from P2 (E-R15): today a type-scoped search lists members the library author
excluded. It affects only libraries with catalogs, and existing graphs keep working either way.

**Independent Test**: In a project referencing the annotated fixture library, drag from a pin of a cataloged type
and open search: only the catalog's members appear; do the same for a type from a library without a catalog: all
its public members appear.

**Acceptance Scenarios**:

1. **Given** a type whose assembly is covered by an embedded or extension catalog, **When** the user opens search
   from a pin of that type, **Then** only the members that catalog lists appear: none it omits and none marked
   `[NetPrintsIgnore]`.
2. **Given** a type whose assembly no catalog covers, **When** the user opens search from its pin, **Then** its
   public members appear as today.
3. **Given** a covered assembly whose catalog does not list the type, **When** the user opens search from its pin,
   **Then** no members appear and the search says which catalog hides them.
4. **Given** a graph that already uses a member the catalog hides, **When** it is opened, built and run, **Then** it
   behaves as before.

---

### Edge Cases

- The editor is started on a project path that does not exist or is not a project: the start page opens with an
  error message naming the path.
- A graph file of the open project is changed on disk by another program: P3a does not detect it; saving
  overwrites it (follow-up, research R14).
- A backup exists but the original file is newer than the backup: recovery still lists it, marked "older than the
  file on disk", and Discard is the default action.
- A backup cannot be written (disk full, no permission): editing continues, the status bar warns once, and the
  failure is logged.
- Two editor instances open the same project: the last one to unload it writes the session state; backups are kept
  per file, so the last instance to back up a file wins, as with saving.
- A floated pane's window is closed with the OS close button: the pane docks back instead of being lost; a floated
  graph tab closes like a tab.
- The saved layout names a panel from an extension that is no longer installed: that panel is dropped and the rest
  of the layout is restored.
- A command's shortcut conflicts with a text field (Ctrl+A, Delete, F2): the text field wins while it has focus.
- A shortcut that Nodify or the graph canvas handles itself (Delete, Ctrl+Space): the shell routes it to the same
  command, so the behaviour is identical from the menu and the keyboard.
- Compile or run while a save is in progress, or exit while compiling or running: the running work is cancelled or
  completed first, and exit waits for it or stops the program after asking.
- The recent list holds more than 20 projects: the oldest unpinned ones drop off; pinned ones are never dropped.
- A project template contributed by an extension fails while creating the project: the partly created folder is
  removed and the error is shown.
- An E2E diagnostics capture itself fails (no display, editor hung): the failure records what could not be
  captured and the original failure is still reported.
- On macOS, `Ctrl+` shortcuts are expected to map to Cmd; this is not verified in CI (Linux only).

## Requirements *(mandatory)*

### Functional Requirements

**CI and test infrastructure (US1)**

- **FR-001**: Every Desktop E2E scenario MUST, when it times out, fails or loses its editor process, write to a
  per-test folder under the test results: the step timings so far (including the step that was running and its
  elapsed time), a UI tree dump of every open editor window, a screenshot of the whole display, the tail of the
  editor's log and standard error, and the state of the program the editor last launched (running or its exit
  code, and the tails of its output and error streams). The failure message MUST name the running step.
- **FR-002**: The diagnostics capture MUST be covered by a test that forces a timeout through a test-only switch and
  checks every artifact exists; a capture that itself fails MUST not hide the original failure.
- **FR-003**: CI MUST upload the E2E diagnostics as an artifact of the run on failure.
- **FR-004**: The Linux test job MUST be split into one parallel job per test project plus one job for the
  repository checks, with an aggregate check named "Build and test (Linux)" that passes only when all of them
  passed; each job MUST upload its own test results and coverage (ADR-0019).
- **FR-005**: A Windows job MUST build the command-line tool and run the CLI test project on every pull request
  that changes the tool or a library it uses; a CLI test that genuinely cannot run on Windows MUST skip with a
  stated reason.
- **FR-006**: A CLI test MUST run `netprints show --textconv` as a separate process on a graph whose class, member
  and node names contain non-ASCII characters, and check that standard output is UTF-8 without a byte order mark and
  contains those names; it MUST run on Linux and Windows.

**Shell layout (US2)**

- **FR-010**: The editor MUST present one main window per open project, containing a menu bar, a command bar, a
  project tree, a document area with tabs, an inspector, a bottom panel with Errors, Output and C# tabs, and a status
  bar. The separate launcher window and the per-class editor windows MUST be removed.
- **FR-011**: The project tree MUST show the project, its classes, and per class its methods, constructors,
  variables and event graphs, with the actions the class window offered (open, add, remove, rename where the model
  supports it) in its context menu and inspector.
- **FR-012**: Opening a graph MUST open it as a document tab or activate its existing tab; tabs MUST support
  reorder, close (button, middle-click, Ctrl+W), next and previous (Ctrl+Tab, Ctrl+Shift+Tab).
- **FR-013**: The inspector MUST show the current selection of the active tree or graph: class, method,
  constructor, variable or event entry.
- **FR-014**: The Errors tab MUST list the compile diagnostics, and activating one MUST open its graph and select
  its node; the Output tab MUST show build output and the running program's output; the C# tab MUST show the
  generated C# of the active graph's class.
- **FR-015**: Panes MUST be dockable to any side, tabbable together, floatable into their own window and dockable
  back, by mouse and by View menu commands; closing a pane MUST hide it, and the View menu MUST list every panel to
  show it again; **View › Reset layout** MUST restore the default layout.
- **FR-016**: A graph tab MUST be floatable into its own window and keep full editing there (this replaces the
  per-class window).
- **FR-017**: Every action of the former launcher and class window MUST remain reachable (class settings, members,
  event graphs, references, project settings, create and open project).
- **FR-018**: The editor MUST hold one project per window; opening or creating another project MUST unload the
  current one through the unsaved-changes prompt (FR-022).

**Document lifecycle (US3)**

- **FR-020**: The editor MUST track unsaved changes per file (each class graph file and the project file). A file is
  unsaved after any change to it, and saved again after a successful save or when undo returns it to its saved
  state.
- **FR-021**: An unsaved file MUST be marked with `*` on its graph tabs and its tree node, and the window title MUST
  show the project name with `*` while any file is unsaved, followed by the active graph's name.
- **FR-022**: Before the project is unloaded (closing the window, Exit, Close project, opening or creating another
  project) with unsaved files, the editor MUST prompt with the list of unsaved files and Save all, Don't save and
  Cancel; Cancel and a failed save MUST keep the project open with its changes. Closing a tab MUST NOT prompt.
- **FR-023**: Save MUST save the active graph's file; Save all MUST save every unsaved file.
- **FR-024**: Every unsaved file MUST be backed up within 30 seconds of its last change to a per-user location
  outside the project directory; a save or Don't save MUST delete that file's backups. At startup the editor MUST
  delete backups older than 30 days and backups whose project path no longer exists.
- **FR-025**: Opening a project that has backups MUST offer recovery listing each backed-up file (with a note when
  the file on disk is newer than its backup): Restore loads the backup content as unsaved changes; Discard deletes
  the backups.
- **FR-026**: A backup that cannot be written MUST NOT interrupt editing; the editor MUST warn once in the status
  bar and log the error.

**Commands, menus, shortcuts and contributions (US4)**

- **FR-030**: Every editor action MUST be a registered command with a stable id, a label, an optional icon, an
  optional default shortcut, a menu location and an enabled state; menus, the command bar, the command palette, the
  shortcuts sheet and the keyboard all run the same command.
- **FR-031**: The menu bar MUST offer File, Edit, View, Go, Build and Help, showing each command's shortcut and
  disabled state.
- **FR-032**: The command bar MUST be at most 40 pixels high and show icon-and-label buttons for Save, Compile (with
  an error-count badge after a compile), Run or Stop (reflecting whether the program runs), Undo, Redo, Class
  settings and References, each with a tooltip "Name (Shortcut)". The 100-pixel round command buttons MUST be
  removed.
- **FR-033**: A running program MUST be stoppable with Stop.
- **FR-034**: The default shortcuts MUST be: Ctrl+S save; Ctrl+Shift+S save all; Ctrl+O open project; Ctrl+Shift+N new project; F7 and
  Ctrl+Shift+B compile; F5 run; Shift+F5 stop; Ctrl+Z undo; Ctrl+Y and Ctrl+Shift+Z redo; Delete delete selection;
  F2 rename; Ctrl+A select all; F frame selection; Home and Shift+F fit all; Esc cancel the current popup or
  operation; Ctrl+Space node search; Ctrl+W close tab; Ctrl+Tab and Ctrl+Shift+Tab next and previous tab;
  Ctrl+Shift+P command palette; Ctrl+P go to anything; Alt+Left and Alt+Right back and forward. Single-key shortcuts
  (F, Home, Delete, F2) MUST act only where they apply and never while a text field has focus.
- **FR-035**: Undo and Redo MUST show the name of the action they will undo or redo in the Edit menu and in their
  tooltips, be disabled when there is nothing to undo or redo, and show "Undid: <action>" or "Redid: <action>" in
  the status bar for a few seconds.
- **FR-036**: Help › Keyboard shortcuts MUST list every command with its shortcut, grouped as in the menu, built
  from the registry.
- **FR-037**: One editor-internal registry MUST accept these contribution kinds: commands, panels, dashboard tiles,
  project templates, context-menu items, tooltip providers and go-to providers; the built-in editor MUST register
  all of its own through it (ADR-0020). Duplicate ids and conflicting shortcuts in the same scope MUST be reported
  and the first registration MUST win.

**Start dashboard (US5)**

- **FR-040**: With no project open, the document area MUST show the start page: recent projects, open folder or
  project, new project, samples and what's new.
- **FR-041**: The recent list MUST support pin, unpin, remove and search by name or path, keep pinned entries first
  and then the most recent, keep at most 20 unpinned entries, mark entries whose path no longer exists, and survive
  restarts.
- **FR-042**: New project MUST create a project from a registered project template (built-ins: Console app and
  Class library, using the default project profile), a name and an empty or new folder, validate the input before
  writing, remove a partly created folder on failure, open the project and add it to the recent list.
- **FR-043**: Opening a sample MUST copy it to a user-chosen folder and open the copy; bundled samples MUST NOT be
  modified.
- **FR-044**: What's new MUST show the running version's release notes bundled with the editor and a link to the
  full release notes.

**Persistence (US6)**

- **FR-050**: The editor MUST persist per user: the window's position, size and maximized state; the panel layout;
  the theme; and the recent list. It MUST persist per project and per user: the open tabs, their order, the active
  tab, and each graph's zoom and position.
- **FR-051**: Restoring MUST skip entries whose graph or panel no longer exists, move a window that would be off
  every screen to the centre of the primary screen, and fall back to defaults (with a log entry) when the state is
  unreadable or from a newer version; the editor MUST never fail to start because of saved state.
- **FR-052**: State files MUST be versioned JSON stored in the per-user application-data folder, never in the
  project directory. On Linux and macOS, state and backup files and their folders MUST be readable by the user only.

**Navigation (US7)**

- **FR-060**: A command palette (Ctrl+Shift+P) MUST filter all registered commands by typed text and run the chosen
  one, showing shortcuts and disabled state.
- **FR-061**: Go to anything (Ctrl+P) MUST search graphs, nodes, variables and methods of the open project, and
  commands after a leading `>`, through the registered go-to providers, grouping results by kind and navigating to
  the chosen item.
- **FR-062**: Ctrl+click on a connection MUST navigate to the end farther from the click; the connection context
  menu MUST offer Go to source and Go to target.
- **FR-063**: Navigations (go-to, errors, connections, tab switches) MUST be recorded in a back and forward history
  of views (graph, zoom, position, selection) available through Alt+Left, Alt+Right and the Go menu.
- **FR-064**: Hovering a connection MUST show a tooltip with `Node.pin → Node.pin`, the data type or "execution",
  and the member documentation of both ends when available, through the registered tooltip providers.
- **FR-065**: Each graph document MUST show breadcrumbs Project › Class › Graph whose segments reveal the item in the
  project tree.

**Event graphs and entries (US8)**

- **FR-070**: Event graphs MUST be renameable inline in the tree and in the inspector; the name MUST stay out of the
  generated C#.
- **FR-071**: Selecting an event entry or opening its graph MUST show the entry in the inspector: name, kind and
  arguments.
- **FR-072**: A custom event entry's name MUST be editable, unique among the class's methods and entries (P1
  FR-027); a duplicate MUST be refused with a message.
- **FR-073**: A custom event entry's arguments (name and type) MUST be addable, editable, reorderable and removable,
  each change one undoable action that updates the entry's pins and the generated signature.
- **FR-074**: An override entry's name and signature MUST come from the base method and be read-only.

**Visual system (US9)**

- **FR-080**: The editor MUST define a type ramp (Caption 12, Body 14, Subtitle 16, Title 20), a spacing scale (4, 8,
  12, 16, 24) and colour tokens with Dark and Light values, and every view MUST use them; panel headers MUST be
  left-aligned and semibold; inspector labels MUST share one column width.
- **FR-081**: The graph canvas, nodes, pins and connections MUST take their colours from the same tokens through
  theme overrides, so they follow the theme.
- **FR-082**: View › Theme MUST offer Dark (default), Light and System; the choice MUST persist.
- **FR-083**: The window title MUST carry the product name, the project name with its unsaved marker and the active
  graph; native window decorations stay on every platform.

**Type-scoped search (US10)**

- **FR-090**: A search scoped to a type MUST take that type's members from the catalog that covers the type's
  assembly when one does (embedded or extension), hiding members the catalog omits and members marked
  `[NetPrintsIgnore]`; for assemblies no catalog covers, it MUST use the live provider.
- **FR-091**: When the covering catalog does not list the type, the scoped search MUST show no members and a
  message naming the catalog.
- **FR-092**: Resolving members of existing nodes MUST be unchanged, so graphs that use hidden members still load,
  build and run.

**Cross-cutting**

- **FR-100**: New and changed views MUST follow ADR-0007 and the `avalonia-*` skills: bindings, commands, behaviors
  and styles; code-behind only for gestures, viewport math and editor interop. Every remaining code-behind event
  handler MUST be either migrated or listed with a reason in a shrink-only allowlist checked by the hygiene tests,
  and the existing XAML hygiene allowlists MUST end empty.
- **FR-101**: Every new interactive element MUST have an automation id from `AutomationIds`, an accessible name when
  icon-only, and be reachable by keyboard.
- **FR-102**: Every story MUST have Desktop E2E coverage of its main flow, one scenario per class (ADR-0006); the
  per-class-window scenario MUST be replaced by a float and re-dock scenario.
- **FR-103**: The docs MUST gain screenshot-based editor guides (the shell layout and docking, the start page,
  saving and recovery, commands and shortcuts, navigation, and the inspector for event graphs), with screenshots
  produced by a repeatable scripted run; the ADRs and contributor docs MUST be updated.
- **FR-104**: P3a MUST NOT include performance work (owner decision 2026-09-25: P8).

### Key Entities

- **Shell**: the single editor window for one project; owns the layout, the documents, the panels and the active
  selection.
- **Document**: an open graph (method, constructor, event graph or class graph) or another document-area page (the
  start page, project settings); identified by the graph's identity.
- **Panel**: a dockable tool pane (project tree, inspector, Errors, Output, C#) identified by a stable id.
- **Command**: a named action with an id, label, icon, default shortcut, menu location and enabled state.
- **Contribution**: a registered command, panel, dashboard tile, project template, context-menu item, tooltip
  provider or go-to provider, with an id and an owner (built-in now, extensions in P3).
- **Unsaved file**: a class graph file or the project file whose content differs from what is on disk.
- **Backup**: a per-user copy of an unsaved file's content, with the original path and the time it was written.
- **Layout**: the arrangement of panels and documents, versioned and per user.
- **Session**: per project and per user: open tabs, active tab, and each graph's zoom and position.
- **Recent project**: a path with its display name, last opened time and pinned flag.
- **Navigation entry**: a graph, zoom, position and selection recorded in the back and forward history.
- **Design token**: a named size, spacing or colour (with Dark and Light values) used by every view.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the Desktop E2E suite, the main flow (open the sample, open two graphs, compile, fix an error, run,
  read the output) completes with exactly one editor window open apart from dialogs.
- **SC-002**: Every one of the five ways to unload a project with unsaved changes (window close, Exit, Close
  project, Open project, New project) shows the prompt in automated tests, and after a forced kill following the
  backup interval, recovery restores content byte-identical to the pre-kill unsaved content.
- **SC-003**: Every built-in editor action is a registered command (a test compares the registry with the list of
  surfaces); 100% of them appear in a menu and in the command palette; the default shortcuts of FR-034 each run
  their command in tests; 0 shortcut conflicts are reported for the built-ins.
- **SC-004**: A user can save, compile, run, stop, undo, redo, switch tabs, find a node and go back without the
  mouse (keyboard-only E2E scenario).
- **SC-005**: After a restart, the layout, open tabs, active tab, each graph's zoom and position, the window bounds
  and the theme equal what they were before (compared in tests); a corrupt or newer state file starts the editor
  with defaults in 100% of tests.
- **SC-006**: A forced E2E timeout produces every diagnostic of FR-001 (timings, UI dump, screenshot, logs, launched
  program state) in the CI artifacts; the longest Linux pull-request job takes at most 60% of the pre-split "Build and test (Linux)" job's
  time on the same commit; the Windows CLI job passes with the UTF-8 test.
- **SC-007**: In the project fixtures, a type-scoped search for a cataloged type lists 0 members outside its catalog
  (0 unannotated, 0 `[NetPrintsIgnore]`), a type from an uncovered assembly lists the same members as before, and a
  graph that uses a hidden member builds and runs with unchanged output.
- **SC-008**: Every colour token resolves to a value in both Dark and Light (a headless test enumerates them), no
  view contains a colour literal, and every XAML hygiene allowlist is empty; the code-behind allowlist contains only
  entries with a gesture, viewport or interop reason.
- **SC-009**: A custom event renamed and given two arguments in the inspector produces the renamed method with both
  parameters in the generated C#, and each change undoes in one step.
- **SC-010**: The docs site builds with 0 broken links and contains the six editor guides, each with at least one
  screenshot produced by the scripted run, plus ADR-0018 to ADR-0020.

## Assumptions

- Keyboard shortcuts, menu names, file names and labels named in this spec are product surface, not implementation
  choices.
- The UI-free core is unchanged except where a story needs model support: renaming event graphs and entries, entry
  arguments, and a saved-state marker on the undo stack.
- The contribution registry is internal in P3a; P3 makes it public for extensions and adds settings pages and
  user-defined shortcuts. Its shape must allow that without a breaking change.
- The UnrealSharp project template ships with NetPrintsUnreal (U1), not in this repository; P3a proves the
  template contribution point with a test template.
- Only Linux runs the editor in CI (constitution I); the Windows job covers the command-line tool only. macOS and
  Windows editor behaviour (Cmd mapping, floating windows) is checked by hand.
- The Velopack update notice, the custom title bar (L1), the undo history list, node clipboard and nudge, Tab-to-search,
  context menus beyond the ones named here, and detecting external file changes are later phases (research R14).
- The four code carry-overs from P2 (NPX008 host and transitive references, `ReflectionProvider` skipping
  unreadable references, `ProjectCheck` generation skip, the info-level analyzer backlog) stay in P3.
- The UI text stays English-only; localization is not in P3a.
- No tags, releases, NuGet pushes, wiki edits or external submissions happen in this phase's PR.
