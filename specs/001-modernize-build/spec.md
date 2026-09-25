# Feature Specification: Modernize Build and Migrate Editor to Avalonia

**Feature Branch**: `001-modernize-build`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Modernize the NetPrints build so the core library, CLI and core tests build and pass on Linux with the .NET 10 SDK: global.json, Central Package Management, Directory.Build.props, Core multi-targeting netstandard2.0+net10.0, Roslyn 4.x, removal of unused dependencies, modern test framework, GitHub Actions CI on Linux (and Windows where useful). The WPF editor and VSIX remain Windows-only and must not break but are not modernized in this phase."

**Scope change (2026-09-24, from the user via the coordinator)**: P0 now also replaces the WPF
editor with an Avalonia editor at feature parity, so that the whole solution builds and all tests
pass on Linux. CI is Linux-only. The legacy VSIX leaves the solution build until P4.

**Scope update (2026-09-24, constitution 1.2.0)**: Visual Studio / .NET Framework hosting is out
of scope. Every project targets `net10.0` only and uses the latest stable dependencies (Roslyn 5.x,
Avalonia 12 + Nodify.Avalonia 2.0). Statements below about `netstandard2.0`, compiler services 4.x
and Avalonia 11 are superseded; see research.md "Scope update".

**Roadmap phase**: P0. **Done when**: the whole solution builds and every test (11 existing core
tests + new reflection/editor tests) passes on Linux in CI, and every item of the Editor Parity
Inventory below is verified.

## Clarifications

### Session 2026-09-24

No interactive user was available for the questions; each was resolved with the default most
consistent with the constitution, the roadmap and the coordinator's instructions.

- Q: Which operating systems does CI run on? → A: Main CI runs on Linux only (`ubuntu-latest`), in a workflow named `CI` (`.github/workflows/ci.yml`). The Visual Studio extension will get its own Windows workflow chained after `CI` (`workflow_run`), created in P4, not P0.
- Q: Which Avalonia major version does the editor use? → A: (superseded by the 1.2.0 scope update: Avalonia 12.1.3 + Nodify.Avalonia 2.0.0) Avalonia 11.x (11.3.22). Avalonia 12 ships only `net8.0`/`net10.0` assemblies; the P4 Visual Studio host runs on .NET Framework and needs `netstandard2.0` assemblies.
- Q: Which graph-canvas control provides pan/zoom, selection, dragging and connections? → A: Nodify.Avalonia 1.0.2, the last release built for Avalonia 11. It was verified on Linux in a headless test harness. Its risks are recorded in research.md.
- Q: How are the editor's user-interface tests run on Linux? → A: (superseded in the PR #1 review follow-up: xUnit v3 + Avalonia.Headless.XUnit in a separate UI test assembly) With the same test framework as the core tests (MSTest 4 on Microsoft.Testing.Platform), using Avalonia's headless platform with the Skia renderer. No display server is needed.
- Q: How do compile, run and type reflection work on Linux, where the .NET Framework reference assemblies used by existing projects do not exist? → A: Framework references that cannot be found fall back to the running .NET 10 runtime's assemblies. Executables built this way are launched through the `dotnet` host. The project file format does not change. Full reference-pack resolution stays in P1.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build and test everything on Linux (Priority: P1)

A contributor on Linux with only the .NET 10 SDK clones the repository and builds and tests the
whole solution with two documented commands; all tests pass without a display server.

**Why this priority**: Foundation for every later phase; the phase's definition of done.

**Independent Test**: In a clean Linux container with the .NET 10 SDK, run the commands in
`quickstart.md`; the build has 0 errors and every test passes.

**Acceptance Scenarios**:

1. **Given** a fresh clone on Linux, **When** the solution is built, **Then** every project in the solution (core library for both targets, reflection library, editor library, desktop app, CLI, both test projects) builds with 0 errors.
2. **Given** a successful build, **When** tests run, **Then** the 11 existing core tests and all new tests pass (0 failed, 0 skipped without reason).
3. **Given** a successful build, **When** the CLI runs with `--version`, **Then** it prints its name and version.
4. **Given** a newer .NET 10 feature-band SDK, **When** the build runs, **Then** it is accepted without edits.

---

### User Story 2 - Edit, compile and run a NetPrints project in the new editor (Priority: P1)

A NetPrints user on Linux, Windows or macOS starts the desktop editor, creates or opens a project,
edits classes, methods, constructors and variables in node graphs, compiles, and runs the result.
Everything the WPF editor could do is available (see Editor Parity Inventory).

**Why this priority**: Replacing the editor must not lose functionality; it is the main
user-facing outcome of P0.

**Independent Test**: Follow the parity walkthrough in `quickstart.md` on Linux with the checked-in
sample project. Every PAR item is exercised and behaves as described. Automated tests cover the
items marked "auto" in the inventory.

**Acceptance Scenarios**: one per parity item, PAR-01 … PAR-60 (table below).
Representative end-to-end scenarios:

1. **Given** the sample project, **When** the user opens it, opens class `Program`, opens method `Main`, adds an "If Else" node through the node search, connects it, and saves, **Then** the saved files reload with the new node and connection.
2. **Given** the sample project, **When** the user presses Run, **Then** the project compiles and the program prints `Hello, World!`.
3. **Given** a method graph, **When** the user drags from an output data pin and releases on empty canvas, **Then** the node search opens filtered for that pin's type, and the chosen node is created at that point and connected.

---

### User Story 3 - Automatic verification on every change (Priority: P2)

A maintainer sees one Linux CI workflow named `CI` on every push and pull request to `master`. It
builds the solution, runs all tests (including headless UI tests), publishes test results and
fails on any break.

**Why this priority**: Constitution principle V; required to show the done criterion.

**Independent Test**: Push the branch; `CI` passes; breaking any test makes it fail.

**Acceptance Scenarios**:

1. **Given** a pull request, **When** `CI` runs, **Then** one Linux job builds, tests, smoke-runs the CLI and uploads test-result files.
2. **Given** a failing core, reflection, view-model or headless UI test, **When** `CI` runs, **Then** the job fails.
3. **Given** the obsolete Travis configuration, **When** P0 merges, **Then** it is removed and the README badge points to `CI`.
4. **Given** the P4 plan to chain a Windows extension workflow after `CI`, **When** P0 merges, **Then** the workflow file name (`ci.yml`) and name (`CI`) are stable and documented as a contract.

---

### User Story 4 - One place for build settings and dependency versions (Priority: P3)

A maintainer changes a dependency version or shared build setting in one place. The repository
no longer carries unused dependencies, IL-weaving in the editor, or Windows-only UI frameworks.

**Why this priority**: Lowers the cost of every later phase; principle VIII (remove dead
dependencies).

**Independent Test**: Search the repository. There are no inline package versions in SDK-style
projects, no Gapotchenko.FX, System.Management, MahApps, MvvmLight, WPF or WinForms references
in built projects, and no Fody in any project except the core library (which P1 removes).

**Acceptance Scenarios**:

1. **Given** the repository, **When** SDK-style project files are searched for version numbers, **Then** none are found.
2. **Given** the repository, **When** the solution is inspected, **Then** no built project references WPF, WinForms, MahApps, MvvmLight, Gapotchenko.FX or System.Management.
3. **Given** the core library, **When** it is built, **Then** it produces a `net10.0` output using the latest compiler services (5.x).

---

### User Story 5 - Headless-testable type reflection (Priority: P3)

A developer tests type discovery (methods, properties, constructors, overloads, documentation)
without any UI. The reflection code lives in its own UI-free library.

**Why this priority**: Constitution principle II. It is also a prerequisite for the P1 composite
provider and the P2 catalog.

**Independent Test**: Reflection tests run in the editor test project with no UI session, against
the .NET runtime's assemblies on Linux.

**Acceptance Scenarios**:

1. **Given** the runtime assembly set, **When** the reflection provider is created, **Then** non-static types, static methods and instance methods of `string` are returned, and no exception is raised for `ref readonly` parameters.
2. **Given** the reflection library, **When** its references are inspected, **Then** it references no UI framework.

---

### Edge Cases

- A bare `dotnet build` at the repository root builds `NetPrints.sln`, which contains only Linux-buildable projects. The legacy VSIX is not in the solution.
- A project saved by the WPF editor, with default `.NETFramework/v4.5` references, is opened on Linux: it loads, reflection and compile use the runtime-assembly fallback, and the file is not rewritten unless the user saves.
- The same project is opened on Windows with .NET Framework reference assemblies installed: the original reference paths are used, so behavior is unchanged.
- An assembly reference points to a missing file: reflection and compile skip it and report it in the compile errors. The editor must not crash. (The WPF editor threw.)
- The node search over the full runtime assembly set holds more than 100,000 entries. It must open and filter within the success-criteria limits.
- An exception during load or compile is shown in an error dialog. The editor stays usable.
- The CLI returns exit code 1 for `--help`/`--version`. This is existing behavior; the P2 redesign changes it.
- Fody weaver warnings (`OnInputTypeChanged` signature) remain in the core library until Fody is removed in P1.
- The WPF editor's "remove class" button never worked: it checked for the wrong data context. The Avalonia editor implements the intended behavior (PAR-11).

## Requirements *(mandatory)*

### Functional Requirements

**Build foundation**

- **FR-001**: The repository MUST pin the .NET 10 SDK with `rollForward: latestFeature`.
- **FR-002**: Shared build settings (language version, deterministic build, CI build flag, nullable default, warnings policy) MUST be declared once.
- **FR-003**: Package versions for all SDK-style projects MUST be declared once centrally, with transitive pinning enabled so known-vulnerable transitive packages are lifted.
- **FR-004**: The core library MUST build for `net10.0`, using the latest stable compiler services (5.x).
- **FR-005**: The CLI MUST target `net10.0` only and keep its current command-line behavior.
- **FR-006**: Unused or replaced dependencies MUST be removed: Gapotchenko.FX (its `HashCode` polyfill is built into net10.0), System.Management, the WinForms reference, MahApps, MvvmLight, PropertyChanged.Fody in the editor, and the MSTest 1.x and preview test SDK packages.
- **FR-007**: The core library MUST keep building with Fody at a version that works on the .NET 10 SDK, with no change in behavior. Fody is removed in P1.

**Cross-platform compile and reflection (minimal; full resolution in P1)**

- **FR-008**: Framework references whose files do not exist MUST resolve to the running .NET runtime's assemblies for both compilation and reflection. Existing paths MUST keep resolving as before. Project files MUST NOT change format.
- **FR-009**: Missing assembly references MUST be skipped, not thrown, and MUST be reported to the user.
- **FR-010**: Running a compiled executable project MUST work on Linux. When the executable was built against the .NET runtime assemblies, it MUST start through the `dotnet` host with a generated runtime configuration file.

**Reflection library**

- **FR-011**: The reflection provider, its interface, its memoizing wrapper, the documentation helper, the operator specifiers and the converters MUST move from the editor into a UI-free library targeting `net10.0`. The move changes behavior only where compiler services 4.x require it (for example, `ref readonly` parameters).

**Avalonia editor**

- **FR-012**: The WPF editor MUST be replaced by an Avalonia 12.x editor split into an editor library (views and view models) and a desktop application targeting `net10.0`. The WPF editor and its test project are removed from the repository.
- **FR-013**: View models MUST NOT use UI-toolkit types (brushes, dispatchers, UI points). They use plain value types and injected services for dialogs, file and folder pickers, clipboard, UI-thread dispatch, the reflection host and messaging.
- **FR-014**: Every item in the Editor Parity Inventory MUST be implemented. Where the WPF behavior was defective (PAR-11), the intended behavior MUST be implemented instead.
- **FR-015**: The editor MUST use a dark Fluent theme with an emerald accent, Material icons, and platform file and folder pickers.
- **FR-016**: The desktop app MUST open a project passed as its only command-line argument.

**Tests and CI**

- **FR-017**: All tests MUST use xUnit v3 on Microsoft.Testing.Platform (owner decision in the PR #1 review follow-up; originally MSTest 4). The editor test project MUST contain reflection tests, view-model tests and headless UI smoke tests (start the app, open the sample project, open a method graph, create a node via search, connect pins, save and reload). All of these MUST run on Linux without a display.
- **FR-018**: A sample project (`samples/HelloWorld`) MUST be checked in. Tests MUST verify that it loads, compiles on Linux and prints `Hello, World!` when run.
- **FR-019**: Main CI MUST be a single Linux workflow named `CI` in `.github/workflows/ci.yml`. It runs on push and pull request to `master`, builds the solution, runs all tests, smoke-runs the CLI and uploads test results. P0 MUST NOT add Windows jobs or the VSIX workflow.
- **FR-020**: The legacy VSIX project MUST be removed from the solution build. Its source MUST stay in the repository, marked as pending P4 rework.
- **FR-021**: The obsolete Travis configuration MUST be removed. The README MUST describe the new projects, target frameworks, build commands and CI badge.

### Key Entities

- **Build topology**: projects, target frameworks, dependencies, CI coverage (see `data-model.md`).
- **Editor view-model graph**: main editor → class editor → graph → node → pin; suggestion list; member variable; compilation reference (see `data-model.md`).
- **Editor services**: dialog, file/folder picker, clipboard, UI dispatcher, reflection host, messenger (see `contracts/editor-services.md`).
- **CI workflow contract**: workflow file/name that P4 chains to (see `contracts/ci-workflow.md`).

## Editor Parity Inventory

This inventory is derived from the WPF editor source as of commit `e54082c` (`NetPrintsEditor/**`).
"Verify" = auto (automated test) or manual (quickstart walkthrough). Each item is an
acceptance scenario of User Story 2.

| ID | Area | Behavior to preserve (Given/When/Then condensed) | Verify |
|----|------|--------------------------------------------------|--------|
| PAR-01 | Main window | Title shows the project name; app icon; opens at about 800×600 | manual |
| PAR-02 | Main window | "Project" pane with Create / Open / Save Project; Save is enabled only when a project is open; the Project and Settings panes are mutually exclusive toggles | auto+manual |
| PAR-03 | Project | Create Project makes project "MyProject" / namespace "MyNamespace" with default references and prompts a save dialog (`*.netpp`, suggested name). Cancelling restores the previous project. The project name is taken from the chosen file name | auto |
| PAR-04 | Project | Open Project uses a `*.netpp` picker and loads in the background with an indeterminate progress overlay. On failure an error dialog shows the exception, which is also copied to the clipboard | auto+manual |
| PAR-05 | Project | A single command-line argument opens that project at startup | auto |
| PAR-06 | Project | Save Project prompts for a path if none is set, then saves the project and all classes | auto |
| PAR-07 | Settings | Settings pane with Output (compilation output flags) and Binary type (library/executable) choosers, disabled when no project is open, with tooltips | auto |
| PAR-08 | References | References button, enabled when a project is open, opens the References dialog | auto |
| PAR-09 | Compile | Compile button, enabled when the project can compile (tooltip even when disabled). Compiles in the background; errors list and status message update | auto |
| PAR-10 | Run | Run button, enabled for executable projects that output binaries: compile, then run on success (on Linux through the `dotnet` host) | auto |
| PAR-11 | Classes | Class list: each entry opens its class editor window, reusing an open window (brought to front, restored if minimized). The remove button closes that window and removes the class (fixes the WPF defect) | auto |
| PAR-12 | Classes | New Class creates a uniquely named class (`MyClass`, `MyClass2`, … — Core `NetPrintsUtil.GetUniqueName` numbering) in the default namespace | auto |
| PAR-13 | Classes | Existing Class adds a `*.netpc` file (copied into the project folder); an error dialog appears on failure | auto |
| PAR-14 | Main window | Closing the main window closes all class editor windows | auto |
| PAR-15 | Reflection | The reflection provider reloads when a project opens, when references change, and after a compilation finishes; the type list for type pickers is refreshed | auto |
| PAR-16 | References dialog | Lists references with text and tooltip | auto |
| PAR-17 | References dialog | Add assembly (file picker); duplicates are ignored (full path, case-insensitive); an error dialog appears on failure | auto |
| PAR-18 | References dialog | Add source directory (folder picker); duplicates are ignored; an error dialog appears on failure | auto |
| PAR-19 | References dialog | Include/Exclude toggle for source-directory references; disabled for assemblies | auto |
| PAR-20 | References dialog | Remove reference | auto |
| PAR-21 | References dialog | Close button | manual |
| PAR-22 | Class window | Title is the class name; icon; opens maximized | manual |
| PAR-23 | Class window | Toolbar: Compile, Run, Class (shows class inspector and opens the class graph), Save (saves the whole project), with tooltips and enablement as in PAR-09/10 | auto |
| PAR-24 | Methods | Methods list: name (trimmed), remove button (clears the inspector and graph if they show it), single click shows the method inspector, double click opens the graph | auto |
| PAR-25 | Methods | Create Method: unique `Method#` name; entry and return nodes placed on the grid and connected; opens the graph | auto |
| PAR-26 | Methods | "Override a method" chooser lists overridable methods; picking one creates and opens the override; the chooser resets | auto |
| PAR-27 | Constructors | Constructors list with the same remove, inspector and open behavior as methods | auto |
| PAR-28 | Constructors | Create Constructor (public; entry node on the grid; opens the graph) | auto |
| PAR-29 | Variables | Variable rows: remove (undoable), click shows the variable inspector, Add/Remove getter and setter (undoable), double click on Get/Set opens that graph, "Open type graph" | auto |
| PAR-30 | Variables | Create Variable: unique `Variable#`, type `object`, undoable | auto |
| PAR-31 | Layout | Resizable splitters between the left lists, the graph, the error list and the inspector | manual |
| PAR-32 | Compile output | Compile error list and status line ("Ready", "Compiling...", "Build succeeded", "Build failed with N error(s)") | auto |
| PAR-33 | Inspector | The right pane switches between class, variable and method inspectors | auto |
| PAR-34 | Class inspector | Name and Namespace (update as typed), Visibility, Sealed/Abstract/Static/Partial, read-only generated C# preview refreshed about every 1 s | auto |
| PAR-35 | Method inspector | Name (read-only for constructors), Visibility, Sealed/Abstract/Static/Virtual/Override/Async (hidden for constructors) | auto |
| PAR-36 | Variable inspector | Name, Visibility, ReadOnly/Const/Static/New | auto |
| PAR-37 | Keyboard | Delete removes the selected nodes except method entry, class return and the main return node; Ctrl+Z undoes; Ctrl+Y redoes | auto |
| PAR-38 | Canvas | Grid background with 28-px cells; graph name shown as a large watermark | manual |
| PAR-39 | Nodes | Header color by node kind (entry, return, call method, call static, constructor, make delegate, type, getter, setter, make array, throw, ternary, default), selected border highlight, shadow, documentation tooltip, label; reroute nodes compact | auto+manual |
| PAR-40 | Nodes | Overload chooser (call/constructor overloads; make-array size mode); changing it is undoable | auto |
| PAR-41 | Nodes | "Pure" checkbox on nodes that allow it | auto |
| PAR-42 | Nodes | +/- buttons with tooltips: left (array elements, method arguments, return types, class interfaces), right (method generic arguments) | auto |
| PAR-43 | Pins | Pin shapes: exec = square, data = circle, type = triangle. Color by pin kind, dimmed when unconnected; hover outline; tooltip; default-value indicator for explicit defaults | auto+manual |
| PAR-44 | Pins | Inline editors for unconnected inputs: text with default-value watermark, enum chooser, boolean checkbox; middle-click clears the value | auto+manual |
| PAR-45 | Pins | Editable pin names where allowed; input pins aligned left, outputs right | auto |
| PAR-46 | Linking | Drag from pin to pin connects only compatible pins (type check with subclass and implicit-cast rules); a live preview cable follows the pointer and snaps to a compatible target | auto+manual |
| PAR-47 | Linking | Releasing a pin drag on empty canvas opens the node search filtered for that pin; the chosen node is placed there and auto-connected | auto |
| PAR-48 | Cables | Bezier cables colored by pin kind; highlight on hover; mouse back button (XButton1) toggles "faint" on a cable or pin; middle-click on a cable or pin disconnects it; double click on a cable inserts a reroute node midway | auto+manual |
| PAR-49 | Selection | Click selects a node; left-drag on empty canvas box-selects; click on empty canvas deselects | auto+manual |
| PAR-50 | Dragging | Dragging moves all selected nodes, compensating for zoom, and snaps them to the grid on release | auto+manual |
| PAR-51 | Viewport | Right-drag pans (with a move cursor); the wheel zooms by ×1.3 around the pointer, clamped to 0.3–1.0; the view resets when another graph opens | manual |
| PAR-52 | Node search | Right-click (no drag) on the canvas opens a 700×300 search popup at the pointer. Results are grouped by category (NetPrints, This Variables/Methods, Static Methods/Variables, Pin Variables/Methods, Pin Static Methods, Types, Generic Types, Generic Static Methods) with icons per kind. Search is multi-term and case-insensitive. The search box is cleared and focused on open; clicking an item creates the node at the pointer and closes the popup | auto+manual |
| PAR-53 | Node search | Built-in nodes per graph kind: method graph (For Loop, If Else, Construct New Object, Type Of, Explicit Cast, Return, Make Array, Literal, Type, Make Array Type, Throw, Await, Ternary, Default); constructor graph (same, minus Return and Await); class graph (Type, Make Array Type) | auto |
| PAR-54 | Node search | Special items: Construct → Select Type, then the first constructor; Literal and Type → Select Type; Make Delegate → Select Method; property/field → Get/Set chooser | auto |
| PAR-55 | Get/Set chooser | Popup at the pointer or drop point; Get and Set are enabled according to visibility; closes when the pointer leaves; creates a getter or setter node | auto |
| PAR-56 | Drag & drop | Dragging a method or constructor from the list onto the graph creates a call or constructor node at the drop point | auto+manual |
| PAR-57 | Drag & drop | Dragging a variable from the list onto the graph opens the Get/Set chooser | auto+manual |
| PAR-58 | Dialogs | Select Type: editable chooser of non-static types (defaults to `object`) and a Select button | auto |
| PAR-59 | Dialogs | Select Method: method chooser (first item preselected) and a Select button | auto |
| PAR-60 | Undo/redo | Undo pairs: Add↔Remove variable, Add↔Remove getter/setter, overload change restores the previous overload, Remove method has a no-op undo; a new action clears redo | auto |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On Linux, a contributor goes from fresh clone to a passing full test run with 2 documented commands.
- **SC-002**: 100% of tests pass in Linux CI (11 existing core tests + all new tests), with 0 skipped without a documented reason.
- **SC-003**: Linux CI completes in under 15 minutes on a standard hosted runner.
- **SC-004**: 60 of 60 parity items are verified (auto or manual), and the result is recorded in the PR.
- **SC-005**: On a typical developer machine, with the full runtime assembly set, the node search opens in under 2 s after a project is loaded (cold path: the first search of a graph), and narrowing results by typing updates in under 300 ms per keystroke.
  - Measured (owner-accepted target, 2026-09-25): 1.46–1.59 s first search and 11–25 ms per keystroke (Release, developer machine).
  - The CI shared runner takes 3.4 s. The CI test asserts only a 3× regression bound (6 s / 900 ms).
  - Meeting 2 s on slower machines such as the CI runner is scheduled for the performance phase P8 (roadmap).
- **SC-006**: The desktop editor starts to its main window in under 5 s on a typical developer machine.
- **SC-007**: 0 package version numbers in SDK-style project files. 0 references to WPF, WinForms, MahApps, MvvmLight, Gapotchenko.FX or System.Management in built projects.
- **SC-008**: The sample project compiles and prints `Hello, World!` on Linux, both in the editor (Run) and in an automated test.
- **SC-009**: Two consecutive builds of the core library from the same commit produce identical outputs.

## Assumptions

- Contributors and CI use the .NET 10 SDK (10.0.100+). Users of the desktop app on Linux need the .NET 10 runtime, and the usual X11 or Wayland desktop libraries to run the UI.
- Compiling on Linux targets the .NET 10 runtime's assemblies. Windows users with .NET Framework reference assemblies keep the old targets. Proper reference-pack and target selection is P1.
- The VSIX is not built in P0. Its source stays, marked pending P4, and it will not compile against the new editor until P4.
- Out of scope, kept in later phases: serialization abstraction and JSON (P1), extension points, plugin loading, profiles and UI contributions (P1/P3), event graphs (P1), catalog tooling and the Spectre CLI (P2), the VSIX and its chained Windows workflow (P4), VS Code, browser and sidecar (P5). P0 must not block them: view models stay UI-agnostic, and reflection stays UI-free.
- Governance: the roadmap currently places the Avalonia editor in P3, and constitution 1.0.0 predates the Linux-only CI rule and the VSIX-workflow exception. The amendments the user requested could not be applied by the spec agent, because governance files are protected. They are listed in plan.md → "Pending governance amendments" and need the user's approval before implementation starts.
