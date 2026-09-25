# Feature Specification: Core Refactor and Extension Points

**Feature Branch**: `003-core-refactor`

**Created**: 2026-09-25

**Status**: Draft

**Input**: Coordinator description: "Phase P1 of the NetPrints modernization, 'Core refactor and extension
points', as scoped in `.specify/memory/roadmap.md` (P1 section, including its additions: P0 review
follow-ups, method-local variables, the AvaloniaEdit C# code view, `[LoggerMessage]` logging and the D5
reference-pack documentation tooltips). Reuse the designs already agreed in the plan page
(serialization, extension points, composite reflection provider, event graphs)."

**Roadmap phase**: P1 (depends on P0, merged). **Done when** (roadmap): an old sample loads, is saved
as JSON and generates identical C#.

**Sources reused** (see [research.md](./research.md) §1 for the decision-by-decision citation):
roadmap P1; constitution 1.2.0; the plan page (archive
`.agent-archive/2026-09-25-session-c18f4e98/netprints-unreal-plan.html`, sections "Serialization",
"Extension points NetPrints must expose", "Catalog tooling", "MVVM split"); `specs/001-modernize-build/`
(research §c resolver, §k MVVM traps, §r test automation; PR #1 review follow-ups);
`specs/002-grid-rendering/research.md` (D8); `docs/research/2026-09-25-ux-audit/` (D4, D5, H7, L6).

## Clarifications

### Session 2026-09-25

No interactive user was available. Each question was resolved with the default most consistent with
the sources above; the owner can overturn any of them before implementation.

- Q: Where are node positions stored, given "positions separate from logic"? → A: In a separate `layout` section of the same class file (node id → position, sorted by id). One file per class keeps document stores and future editor hosts (VS Code custom editor) simple, and moving nodes changes only lines in that section.
- Q: What happens to the old XML files when a legacy project is saved? → A: They are left untouched. Saving writes the JSON project and class files next to them, and the open project switches to the JSON project file. Open accepts both formats; the old format is read-only (constitution: "legacy XML is import-only").
- Q: Which extensions are loaded, and may a project folder load code on its own? → A: Only extensions found in user-configured extension directories (settings or the `NETPRINTS_EXTENSION_PATH` variable) are loaded. A project only lists the extension ids it uses; opening a project never loads code from the project folder. Nodes of a missing extension are kept as-is and reported.
- Q: What does a project compile against? → A: A target framework declared by the project (default `net10.0`), resolved to the matching .NET reference pack. Old projects that reference .NET Framework assemblies are mapped to the reference pack of the project's target framework (`net10.0` for imported projects), with one warning. .NET Framework targets are no longer supported (constitution 1.2.0).
- Q: How is a method-local variable's type chosen? → A: With the existing type chooser (a single type, generic types included); local variables have no type graph. Names are unique within the method, including its parameters.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open an old project and keep working in a readable format (Priority: P1)

A NetPrints user opens a project saved by an earlier NetPrints version. It loads as before. When they
save, the project and its classes are written as readable, versioned, diff-friendly text documents,
and the generated C# is exactly the same as before. Reopening the saved project shows the same
graphs, positions and settings.

**Why this priority**: The phase's definition of done; every later host (CLI, VS Code, Unreal) and
reviewable graphs in git depend on it.

**Independent Test**: Open the checked-in legacy fixtures, save them, reopen the saved copies and
compare the generated C# of every class with the golden C# recorded from the pre-P1 code.

**Acceptance Scenarios**:

1. **Given** a legacy project (the HelloWorld sample and a fixture that uses every built-in node kind), **When** it is opened, saved and reopened, **Then** every class generates C# byte-identical to the golden output recorded before the refactor.
2. **Given** a saved project, **When** it is saved again without changes, **Then** the files are byte-identical.
3. **Given** a saved class, **When** only node positions change, **Then** only lines in the layout section of that file change.
4. **Given** a document with an older schema version, **When** it is opened, **Then** it is upgraded in memory and opens; **Given** a newer schema version than this build supports, **Then** the user sees a clear error and nothing is overwritten.
5. **Given** a class that uses nodes from an extension that is not installed, **When** it is opened and saved, **Then** those nodes are preserved unchanged and the user is told which extension is missing.

---

### User Story 2 - Compile, run and read documentation on any OS (Priority: P1)

A user on Linux, Windows or macOS compiles and runs a project against the .NET reference assemblies of
the project's target framework, and sees documentation tooltips for framework methods (for example
`Console.WriteLine`), which are missing on Linux today (UX audit D5).

**Why this priority**: Removes the last hard-coded Windows paths (constitution I) and restores a
parity item (PAR-39 documentation tooltip) on Linux.

**Independent Test**: On Linux CI, compile and run the HelloWorld sample against the resolved reference
pack; hover the `WriteLine` node and read its summary; repeat with a fake SDK layout and with a
configured reference-pack directory.

**Acceptance Scenarios**:

1. **Given** an installed .NET SDK, **When** a project targeting `net10.0` compiles, **Then** it compiles against the `net10.0` reference pack (not the runtime directory) and the program runs.
2. **Given** a node that calls a framework method, **When** the user hovers it, **Then** the tooltip shows the method's documentation summary on Linux.
3. **Given** a machine with only the .NET runtime (no reference packs) and no configured location, **When** a project compiles, **Then** it falls back to the runtime assemblies with a warning, as in P0.
4. **Given** a configured reference-pack location, **When** a project compiles, **Then** that location is used first.
5. **Given** an old project with .NET Framework references, **When** it compiles, **Then** it uses the current .NET reference pack and reports one warning explaining the mapping.

---

### User Story 3 - Extend NetPrints without forking it (Priority: P1)

An extension author (for example the future NetPrintsUnreal) ships an extension package that adds node
kinds with their own C# translation, adds attributes/modifiers to generated classes and members,
contributes a precomputed type catalog, a project profile and its own settings, and can talk to the
application that launched the editor. The user installs it by placing it in an extension directory;
NetPrints loads it in isolation and uses its contributions in the editor, the CLI and serialization.

**Why this priority**: Constitution III (extension-first, no forks); every U-phase and P2/P3 builds on
these seams.

**Independent Test**: Build the in-repo test extension, point the extension path at it, and verify
each contribution end-to-end (node saved/loaded/translated, emitted attribute in C#, catalog type found
in search, profile applied, settings round-trip, host message handled) plus failure isolation.

**Acceptance Scenarios**:

1. **Given** the test extension in an extension directory, **When** the editor starts, **Then** its node kind appears in node search, and a graph using it saves, reloads and translates with the extension's C#.
2. **Given** an extension class/member emitter, **When** a class is translated, **Then** the emitted attributes, modifiers, usings and base types appear in a deterministic order, and without the extension the C# is unchanged.
3. **Given** an extension type catalog, **When** the reflection provider is built, **Then** catalog types are found alongside the project's live types, and the live provider does not enumerate the catalogued assemblies.
4. **Given** an extension that fails to load (bad manifest, incompatible API version, missing dependency, exception while registering), **When** the editor starts, **Then** the failure is reported and logged, and all other extensions and the editor still work.
5. **Given** a host message "types changed", **When** it arrives through the host channel, **Then** the editor reloads its types.
6. **Given** extension settings, **When** they are changed and the editor restarts, **Then** they are read back; settings sections of extensions that are not installed are preserved.

---

### User Story 4 - Several events in one graph (Priority: P2)

A user builds an event graph for a class that contains several entry points (for example "OnStart"
and "OnTick", or overrides of base-class virtual methods). Each entry point becomes its own method in
the generated C#.

**Why this priority**: Core concept needed by Blueprint-style graphs (BeginPlay/Tick in U2) and useful
outside Unreal (handlers, lifecycle overrides); plan page "Event graphs".

**Independent Test**: Create an event graph with two custom events and one override, compile and run
a program that calls them, and compare the generated C# with a snapshot.

**Acceptance Scenarios**:

1. **Given** an event graph with entries A and B, **When** the class is translated, **Then** two methods A and B are generated, each containing only the nodes reachable from its entry.
2. **Given** an entry that overrides a virtual method of the base type, **When** translated, **Then** the method is emitted with `override` and the base signature.
3. **Given** a data connection from a node reachable only from entry A into a node reachable only from entry B, **When** translated, **Then** an error names both nodes and no C# is emitted for B.
4. **Given** two entries with the same name, or an entry named like an existing method, **When** the user names it, **Then** the name is rejected with a message.

---

### User Story 5 - Method-local variables (Priority: P2)

A user declares variables that belong to one method or constructor, reads and writes them with
getter/setter nodes like class variables, and sees them in the Variables panel in a "Method: <name>"
group next to the "Class" group.

**Why this priority**: Owner idea (roadmap 2026-09-25); avoids polluting classes with fields used by a
single method.

**Independent Test**: Add a local counter to `Main`, set and read it in a loop, compile and run, and
check that the variable is declared at the top of the generated method.

**Acceptance Scenarios**:

1. **Given** a method with a local variable `count` of type `int`, **When** translated, **Then** `int count = default(int);` is declared at the top of that method only, and getter/setter nodes read and assign it.
2. **Given** the Variables panel with a method graph open, **Then** it shows the class variables under "Class" and that method's locals under "Method: <name>"; creating, renaming, retyping and removing a local is undoable.
3. **Given** a local named like a parameter or another local, **Then** the name is rejected.
4. **Given** a saved class with locals, **When** reopened, **Then** the locals and their nodes are restored.

---

### User Story 6 - A real C# code view with diagnostics linked to nodes (Priority: P2)

A user reads the generated C# in a code view with syntax highlighting, line numbers, folding and a
monospace font, with no wrapping. Compiler errors and warnings for the generated code appear live as
squiggles and as rows in the error list (severity, code, message, class and method); double-clicking a
row opens the graph and selects the node that produced the code. Hovering a symbol shows its signature
and documentation. The view is read-only.

**Why this priority**: The "learn C#" hook and the compile feedback loop (UX audit D4, H7, L6;
roadmap P1 code view). Builds on US1–US2 (identifiers, documentation).

**Independent Test**: Headless UI tests open a class with a deliberate type error, assert the
highlighted code view, the squiggle, the error row and navigation to the node; snapshot baselines are
regenerated and reviewed.

**Acceptance Scenarios**:

1. **Given** a class, **When** its code view is shown, **Then** keywords, types and strings are highlighted, lines are numbered, long lines scroll horizontally, and types/members can be folded.
2. **Given** a graph whose generated code does not compile, **When** the user edits it, **Then** within about 2 s of the last edit a squiggle marks the span and the error list shows the diagnostic with the originating class, method and node.
3. **Given** a diagnostic row, **When** the user double-clicks it, **Then** the graph opens with that node selected and in view.
4. **Given** a compile (Compile/Run buttons), **Then** its errors use the same structured rows.
5. **Given** the pointer over a framework method name in the code view, **Then** its signature and summary are shown.

---

### User Story 7 - Maintainable core and editor internals (Priority: P3)

A maintainer works on a core without IL weaving, with nullable annotations and warnings-as-errors
everywhere, structured logging, view models that depend only on narrow services, gestures implemented
as commands, explicit composition without test hooks, and an automated gate that keeps UI types out
of view models. These are the P0 review follow-ups (roadmap P1).

**Why this priority**: No user-visible change, but it lowers the cost of P2–P8 and closes review
debt; constitution tech constraints ("no IL weaving") and principle II.

**Independent Test**: The build has 0 warnings with warnings-as-errors in every project; the
architecture gate test fails on a checked-in violating fixture and passes on the code; all existing
tests stay green.

**Acceptance Scenarios**:

1. **Given** the solution, **When** it builds, **Then** no project uses Fody, every project has nullable enabled and warnings as errors, and the build has 0 warnings.
2. **Given** model changes made by core code (not the editor), **Then** the editor's views update as before (same change notifications as the Fody build).
3. **Given** a view model that references an Avalonia or Nodify type, or a child view model that references its parent editor, **Then** the architecture gate test fails.
4. **Given** an unhandled UI exception or an extension load failure, **Then** it is logged through source-generated log methods and still shown to the user.

---

### Edge Cases

- A JSON document is truncated or invalid: the user sees an error with the file and position; nothing is overwritten; the rest of the project still loads.
- A class file listed by the project is missing: the project opens without it and reports it (today: exception).
- Two nodes in a legacy file have the same name: stable ids are still unique and deterministic.
- A legacy file uses a node kind, reference kind or value type the importer does not know: the import fails with a message naming it (no silent data loss).
- The file changes on disk while open (e.g. `git checkout`): the storage layer reports the change to subscribers; P1 does not act on it in the editor (reload and conflict prompts belong to the P3a document lifecycle).
- The reference pack for the requested target is missing but another major version exists: the running runtime's major version pack is used with a warning; if none exists, the P0 runtime-directory fallback applies.
- An extension is present twice (two directories): the first by search order wins; the duplicate is reported.
- An extension node kind id collides with a built-in or another extension: the later registration is rejected and reported.
- An event graph with no entries, or an entry with nothing connected: an empty method is generated (same as an empty method graph).
- A local variable is removed while getter/setter nodes use it: the nodes are removed with it, undoably (same as class variables).
- The code view is shown for a class with no diagnostics: no squiggles; the error list shows only compile errors.

## Requirements *(mandatory)*

### Functional Requirements

**Serialization (US1)**

- **FR-001**: Projects and classes MUST be saved as JSON documents with an explicit schema version, through a format abstraction and a storage abstraction, so other formats and storage locations can be added without changing callers (constitution VII).
- **FR-002**: Saved documents MUST be deterministic and diff-friendly: stable key and element order, fixed indentation and line endings, no timestamps or machine-specific paths, node positions in a separate layout section.
- **FR-003**: Documents MUST be free of object-reference cycles: nodes and pins are identified by stable ids and connections are an edge list.
- **FR-004**: Legacy XML projects and classes MUST be importable (read-only) and produce the same model as today's loader.
- **FR-005**: Documents with an older schema version MUST be migrated in memory before mapping; a newer version MUST be rejected with a clear message.
- **FR-006**: Node kinds MUST be serializable polymorphically, including node kinds registered by extensions; nodes of unknown kinds MUST round-trip unchanged.
- **FR-007**: Saving a legacy project MUST write JSON files next to the legacy files, leave the legacy files untouched and switch the open project to the JSON file; opening MUST accept both formats.
- **FR-008**: For every class of the legacy fixtures, C# generated after import and after a JSON round trip MUST be byte-identical to the golden C# recorded from the pre-P1 code.
- **FR-009**: The storage abstraction MUST report external changes to stored documents; file saves MUST be atomic (no half-written file on failure).
- **FR-010**: The checked-in sample MUST be converted to JSON; the legacy files become test fixtures.

**References and documentation (US2)**

- **FR-011**: A project MUST declare its target framework (default `net10.0`); compilation and reflection MUST use the matching .NET reference pack, found through configured locations first, then the SDK installations and package caches of this machine.
- **FR-012**: If no matching pack is found, the pack of the running .NET major version and then the running runtime's assemblies MUST be used, each with a warning (P0 behavior preserved as the last fallback).
- **FR-013**: Legacy .NET Framework references MUST be mapped to the resolved pack with one warning; hard-coded Windows paths MUST be removed from all projects (constitution I).
- **FR-014**: Documentation MUST be read from the XML files that ship next to the reference assemblies, so tooltips show framework documentation on every OS (D5).
- **FR-015**: Executables compiled against a pack MUST run through the `dotnet` host with a runtime configuration for that framework.

**Extension points (US3)**

- **FR-016**: Extensions MUST be able to contribute node kinds (model, C# translation, serialization, search entries), class and member emitters, type catalogs, project profiles, host channels and settings through documented interfaces, with no change to NetPrints code (constitution III).
- **FR-017**: Extensions MUST be described by a manifest (id, version, assembly, NetPrints API version, dependencies) and loaded in an isolated load context that shares the NetPrints contract assemblies with the host.
- **FR-018**: Loading MUST be deterministic (dependency order, then id) and failure-isolated: one extension's failure never prevents others or the editor from starting; every failure is reported.
- **FR-019**: Extensions MUST only be loaded from user-configured extension directories; projects record the extension ids they use and never cause code to load by themselves.
- **FR-020**: The reflection layer MUST combine precomputed type catalogs with the live compiler-based provider behind the existing provider interface; assemblies covered by a catalog are not enumerated by the live provider (they stay referenced so user sources still compile).
- **FR-021**: Built-in node kinds MUST be registered through the same node-library mechanism extensions use.
- **FR-022**: Class and member emitters MUST run in a deterministic order, and with no emitters the generated C# MUST be unchanged.
- **FR-023**: A project MUST reference a project profile (default profile built in) that supplies the output layout, default target framework and references, and the base classes and class templates offered.
- **FR-024**: A host-channel abstraction MUST let the editor receive and send messages from/to the launching application; P1 ships a no-op and an in-memory implementation and handles "types changed" and "focus document".
- **FR-025**: Each extension MUST get its own settings section (user scope, and project scope in the project document); unknown sections MUST be preserved.

**Event graphs (US4)**

- **FR-026**: A class MUST support event graphs with any number of entry points; each entry MUST translate to its own method (custom event or override of a base virtual method), in a deterministic order.
- **FR-027**: Data flow between nodes of different entries MUST be reported as an error; entry names MUST be unique among the class's methods and entries.
- **FR-028**: The editor MUST list, create, open and remove event graphs, and offer custom-event and override entries in node search inside event graphs.

**Method-local variables (US5)**

- **FR-029**: Method and constructor graphs MUST own local variables (name, type) with getter and setter nodes; locals MUST be declared at the top of the generated method and named without collisions.
- **FR-030**: The Variables panel MUST show two groups, "Class" and "Method: <name>" (for the opened method or constructor graph); create, rename, retype and remove MUST be undoable; locals MUST be available in node search and by drag and drop in that graph.

**C# code view (US6)**

- **FR-031**: The generated C# preview MUST be a read-only code view with C# highlighting following the theme, line numbers, folding of types and members, no wrapping with horizontal scrolling, and a bundled monospace font.
- **FR-032**: Compiler diagnostics for the generated code MUST be computed off the UI thread after edits (debounced), shown as squiggles and as structured error-list rows (severity, id, message, class, method, node); compile errors MUST use the same rows.
- **FR-033**: Every generated statement MUST be traceable to the node that produced it, without changing the generated C# (FR-008).
- **FR-034**: Double-clicking a diagnostic row MUST open the graph and select and reveal the node.
- **FR-035**: Hovering a symbol in the code view MUST show its signature and documentation summary.

**Maintainability (US7)**

- **FR-036**: The core model MUST raise the same property-change notifications without IL weaving (source-generated MVVM); Fody MUST be removed from the repository.
- **FR-037**: Every project MUST build with nullable enabled and warnings as errors.
- **FR-038**: Child view models MUST depend only on narrow services, never on their parent editor; the parent reacts to model collection changes (P0 review).
- **FR-039**: An automated architecture gate MUST fail when a view model uses UI-toolkit types or a child view model references its parent, and MUST be shown to fail on a checked-in violating fixture.
- **FR-040**: Canvas gestures (connection completed, disconnect, split/reroute) MUST be implemented as view-model commands instead of view code-behind.
- **FR-041**: The editor composition MUST take all services explicitly, with no test-only hooks in production code.
- **FR-042**: Logging MUST use source-generated log methods (the app-level error handler, extension loading, reflection reload and all new logging); UI-framework log output (e.g. the grid shader fallback) MUST be forwarded to the same logging pipeline, and the desktop app MUST write logs to the console.

### Key Entities

- **Project document / class document**: versioned, cycle-free text documents for a project and each class (header, graphs, variables, locals, layout, extension settings). See data-model.md.
- **Node kind**: a registered kind of node (id, model type, translation, serialization, search entries), built in or from an extension.
- **Extension**: manifest + assembly + its contributions (node library, emitters, catalogs, profiles, host channel, settings).
- **Reference pack**: the set of reference assemblies and documentation files for a target framework.
- **Event graph / entry point**: a graph with several entry nodes, each producing one method.
- **Local variable**: name and type owned by a method or constructor graph.
- **Code diagnostic**: severity, id, message, source span, and the class, graph and node it maps to.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of classes in the legacy fixtures (HelloWorld and the all-node-kinds fixture) generate byte-identical C# after import and after a JSON round trip.
- **SC-002**: Saving an unchanged project twice produces byte-identical files; moving one node changes only layout lines.
- **SC-003**: On Linux CI, the HelloWorld sample compiles against the reference pack and prints `Hello, World!`, and the `Console.WriteLine` tooltip shows its documentation summary.
- **SC-004**: The test extension's six kinds of contribution work end-to-end, and each of the four failure modes leaves the editor usable, in automated tests.
- **SC-005**: Opening a project, including extension loading and reference resolution, takes at most 10% longer than on `master` for the HelloWorld sample (measured and recorded; no new performance work — P8).
- **SC-006**: Diagnostics appear within 2 s after the last edit for the sample classes on a typical developer machine.
- **SC-007**: 0 build warnings with warnings-as-errors in every project; 0 references to Fody.
- **SC-008**: All existing tests (P0, P0.1) stay green; the architecture gate is demonstrated to fail on its fixture.

## Assumptions

- The JSON files use the extensions `.netpp.json` (project) and `.netpc.json` (class), as assumed by the P4/P5 notes in the roadmap and plan page.
- Node ids of imported legacy nodes are derived deterministically from the graph and node names; new nodes get random ids once and keep them.
- Hover quick info is implemented directly on the compiler APIs NetPrints already uses; RoslynPad.Editor.Avalonia is evaluated and not adopted in P1 (research.md R4).
- The code view stays where the preview is today (class inspector); the bottom panel and docking are P3a.
- Automatic reload on external change and conflict prompts beyond "warn if unsaved" are P3a (document lifecycle).
- Out of scope, deferred per roadmap: catalog generation tooling, the annotations generator and the Spectre CLI including `migrate` (P2); editor shell, docking, dirty-state UX (P3a); plugin UI contributions, settings pages and `--profile` (P3); structured codegen and block scopes (P7); performance work including `MetadataReference` caching and moving translation off the UI thread (P8). The CLI keeps its P0 behavior but reads both formats.
- Superseded follow-up: "bind the grid to `ViewportTransform`" is dropped; P0.1 replaced the grid and chose typed property sync instead (P0.1 research D8).
