# Feature Specification: Editor Extension Host

**Feature Branch**: `006-editor-extension-host`

**Created**: 2026-10-15

**Status**: Draft

**Input**: Coordinator description: "Phase P3 of the NetPrints modernization, 'Editor extension host', as scoped in
`.specify/memory/roadmap.md` (P3 section). Core first: NetPrints' own analyzer and source-generator infrastructure and
the public contribution API with the extension host (plugin-loaded editor extensions contributing commands, panels,
inspector sections, settings pages and the other contribution kinds; `NetPrints.Desktop --profile`; the
`NetPrints.Extensibility` and `NetPrints.Serialization` packages for extension authors), then the features built on
them: settings pages, user-defined shortcuts, run profiles, theme presets, the C# target, NuGet packages, an extension
manager, samples from remote sources, the author conformance kit and a sample non-Unreal extension. The P2 and P3a
carry-overs join. The owner's decisions are recorded in the roadmap and in ADR-0020, ADR-0031 and ADR-0033 to ADR-0039;
this spec cites them and does not reopen them."

**Roadmap phase**: P3 (depends on P0, P1 and P3a; P3a merged 2026-10-15 as PR #12, `bf3496e9`). **Done when**
(roadmap): the phase's features work end to end and the docs are updated (guides, API reference, ADRs).

**Scope (owner, 2026-10-16)**: the extension manager and samples from remote sources are roadmap phase P3c's. Their
IDs (User Stories 13 and 14, FR-094 to FR-106, SC-013 and SC-014) stay below, marked "moved to P3c", and are not
reused; the boundary calls are in the Assumptions. Earlier the same day the owner had also moved the package service
and NuGet packages to P3c and the tail to P3b, then kept both in P3: User Story 12 (NuGet packages in the References
dialog; FR-089 to FR-093, SC-012) and User Stories 16 and 17 (inspector sections, value editors and graph rules; user
and extension themes; FR-109 to FR-115) are active (Clarifications, session 2026-10-16). P3 is about 5 weeks in agent
time.

**Version (owner, 2026-10-16)**: P3a ships as v0.3.0, so P3's release is 0.4.0. The hard renames, the `*REMOVED*`
entries, the release notes, the published package versions and `netprintsApi` 0.4 below name 0.4.0; before this date
they said 0.3.0, including the owner's hard-rename decision of 2026-10-08.

**Sources reused**: the roadmap's P3 section, including "Carried over from P2" and "Carried over from P3a"; the
deferrals of the P3a spec (`specs/005-editor-shell/spec.md`: Assumptions and the deferred lines after FR-089) and its
final notes; constitution 1.2.4; ADR-0010, ADR-0017, ADR-0020, ADR-0021, ADR-0023, ADR-0031 and ADR-0033 to ADR-0039;
`docs/research/2026-10-14-engine-integration/`; and the owner-decision records and audited research the roadmap names
(kept outside this repository): the attribute-driven plan and the SOLID plan (2026-10-08), the theme presets and the
details panel decisions (2026-10-13), and the run profile kinds, remote samples and attributes/language-version
research with their audits (2026-10-14).

## Clarifications

### Session 2026-10-15

No interactive user was available (owner instruction: work autonomously, decide every question and record it). Each
question was answered with the recommended option, the one most consistent with the constitution, the roadmap and the
ADRs; the reason follows each answer. Answers 3 and 4 are architectural and are recorded in
[ADR-0040](../../docs/adr/0040-editor-settings-pages-and-key-bindings.md).

- Q: The roadmap's P3 items add up to far more than the phases table's "~2.5 w"; what is in P3's scope, and what may
  be cut if it does not fit? → A: Every item of the roadmap's P3 section is in scope, ordered core first by story
  priority (P1 to P4). The only cuts allowed are the fallbacks the roadmap and the ADRs already name: B17's 3-day v1;
  the sample gallery's screenshots, `localized` and update badge to v1.1; B1 and C (user and extension themes) to P6;
  the model-driven property grid to P3b; S7 and SUG5, R6, N2 and N5 to P6. Each is taken only at the checkpoint before
  the work it cuts (B17's v1 and the gallery's v1.1 items at Checkpoint E, the tail items at Checkpoint F) and recorded
  in the implementation notes (the session 2026-10-16 answers below supersede the Checkpoint E and F cut lines). Items
  the roadmap marks "v1.1" (the `netprints
  package` and `netprints extension` command verbs other than `verify`) are out of P3. Reason: the owner placed these
  items in P3, and the named fallbacks are already-decided cut lines, so nothing is re-scoped silently.
- Q: How long do an extension's editor contributions live, and which copy wins when the same extension comes from a
  project and from the per-user extensions? → A: Two contribution groups. Extensions installed per user (B17) or found
  in user-configured directories load once at editor start; their application-level contributions (start page tiles,
  project templates, samples, themes, settings pages, commands that need no project) join the **app group**, and their
  project-level contributions (node libraries, commands that act on a project, panels, inspector sections, status-bar
  items, context-menu items) are added to each project's **session group**. Extensions a trusted project declares
  load with that project into its session group. A session group is removed as a whole when its project unloads.
  Changes to per-user extensions apply after a restart (no hot enable or disable). When a project declares an
  extension id that is also installed per user, the project's copy serves that session, the per-user copy adds
  nothing to it, and an info diagnostic names both versions. Reason: load contexts are not collectible yet (B17,
  ADR-0031's later step), per-session groups are already decided (N1), and the project's declaration is what its build
  uses, so the editor matches the build.
- Q: How do the typed options of ADR-0031 relate to the existing settings store? → A: `settings.json`, read and
  written by the existing settings store, stays the only persisted source of settings. Each section (built-in or
  extension) is a typed options class read from it with the source-generated JSON contexts and checked by
  source-generated `[OptionsValidator]` validators at composition; the configuration binding generator and
  `IConfiguration` providers are not used for settings. Settings pages read and write sections through the store,
  which notifies the typed options of a change. User key bindings are a `keyboard` section of the same file. Sections
  of extensions that are not loaded are preserved. Reason: one file and one parse path, the NPXE0002 API and file
  format unchanged, validation without reflection, and none of the array-merge and string-typing surprises of binding
  JSON through `IConfiguration` (ADR-0040; amends ADR-0031 decision 7).
- Q: Are settings pages generated from a section's description, or does each extension draw its own page? → A:
  Generated by default, custom when needed. A section without a custom view gets a page generated from its exported
  JSON schema (the AOP S10 slice): toggles, numeric boxes with ranges, text, drop-downs for enumerated strings, path
  pickers and string lists, with descriptions as help text and validator messages under the field. An extension may
  instead contribute its own page view through its view resources, under an experimental id, and the conformance kit
  scans that view like other contributed XAML. Reason: most extensions need only a few settings and should write no
  UI; the schema export is already planned for the settings pages; a custom view stays possible for engine paths and
  pickers (ADR-0040).
- Q: What does `NetPrints.Desktop --profile <id>` do? → A: It names a project profile (P1's `NetPrintsProfile`, the
  built-in one or one an extension contributes) for the session's start page: New project preselects that profile,
  and the template list and the samples gallery are filtered to it, with a "Show all" switch. It does not change
  which profile an existing project uses (its `.csproj` decides), and it is not remembered. An unknown id opens the
  start page unfiltered with a message naming the id and the known ids. Reason: a launcher (U3's Unreal plugin, a
  Godot shortcut) can start the editor ready for its engine without a second settings set; a VS Code-style "profile"
  (separate settings and extension sets) is a larger feature no owner decision asked for.

### Session 2026-10-16

The owner answered the two questions the coordinator asked after the spec was written.

- Q: Should the package work (the package service, the Packages tab, the extension manager and the remote samples,
  about 23 days) move to its own phase, as the coordinator recommends? → A: Yes. The owner: "continua con P3 como
  recomiendas" ("go on with P3 as you recommend"). It is roadmap phase P3c. User Stories 12 to 14, FR-089 to FR-106 and
  SC-012 to SC-014, with their edge cases and entities, moved there; their IDs are kept, marked "moved to P3c". This
  narrows three earlier answers. Answer 1: B17's 3-day v1 and the gallery's v1.1 items are no longer P3 cut lines, and
  Checkpoint E applies no scope rule. Answer 2: in P3 the per-user extensions are those in the configured extension
  directories; the per-user folder the extension manager installs into comes with P3c. Answer 5: `--profile` filters
  New project and the template list; the samples gallery filter moves to P3c with the sample manifest's `profile`.
- Q: Are the coordinator's process defaults (one spec and one PR, core first, the P3a batch and review process)
  confirmed? → A: Yes, by the same answer; the Planning Notes record them.
- Q: Should the tail (sub-phase G: inspector sections, value editors, graph rules, translator helpers, node kinds by
  convention, user and extension themes) stay in P3 behind the Checkpoint F scope rule? → A: No. The owner approved
  moving it to P3b. User Stories 16 and 17 and FR-109 to FR-115 moved there, with their IDs kept and marked "moved to
  P3b". No success criterion was tied only to them. FR-116 (the docs) and the 0.4.0 release preparation stay in P3.
  This narrows answer 1: the Checkpoint F scope rule no longer exists, and the P6 fallbacks it named for R6, N2, N5, S7,
  SUG5, B1 and C are no longer P3 cut lines (P3b decides their placement). The manifest `themes` field (FR-029) stays
  in P3, because it must exist before the packages publish; P3b consumes it.
- Q: Does each sub-phase end with its own review? → A: No. Reviews run at five checkpoints: A+B, C1+C2, D1+D2, E+F and
  the final review. Each checkpoint is one adversarial review batch plus one fix batch. The sub-phase gate reports stay
  in tasks.md. The implementers push and return without waiting for CI; the coordinator watches the PR's CI.
- Q: After the two moves above, what stays out of P3? → A: Only the extension manager and the remote samples. The
  owner, verbatim: "Lo de los paquetes nuget debe quedar, ademas las secciones del inspector, editores de valores y
  reglas del grafo y temas de usuario o extensiones debe quedar tambien lo que puedes sacar es el gestor de extensiones
  y los samples remotos." ("The NuGet packages must stay; the inspector sections, value editors and graph rules, and
  user or extension themes must stay too; what you can take out is the extension manager and the remote samples.")
  User Story 12 with FR-089 to FR-093 and SC-012, and User Stories 16 and 17 with FR-109 to FR-115, are active again;
  User Stories 13 and 14, FR-094 to FR-106, SC-013 and SC-014 stay in P3c. This reverses the tail answer above and
  narrows the first answer of this session: the package service and the Packages tab return with User Story 12, and
  P3c's extension manager and samples gallery reuse that service. No scope rule comes back: the tail and User Story 12
  are planned work with no cut line, and the P6 fallbacks of the 2026-10-15 answer 1 stay retired. The narrowed
  answers 2 and 5 still hold, because they depend on the extension manager and the sample manifest, which stay in P3c.
  The model-driven grid (ADR-0036 c) stays in P3b, with the member defaults and editor metadata it builds on. The
  reviews stay at five checkpoints: the plan folds the tail (sub-phase G) into the E+F checkpoint (E+F+G) and the
  NuGet packages (sub-phase H) into the final one.

## User Scenarios & Testing *(mandatory)*

The stories are ordered by priority. **MVP slice: User Stories 1–4**, core first: the analyzer and generator
foundation, the node model reshaped before the API freezes, the published packages, and extensions contributing to the
editor. Each later story is independently testable on top of the MVP. Priority P4 marks the tail (User Stories 16 and
17); the owner kept it in P3 with no scope rule (Clarifications, session 2026-10-16).

### User Story 1 - Analyzer and generator foundation (Priority: P1)

A NetPrints contributor declares services, smart enums and settings validation with attributes instead of hand-written
wiring, and the build enforces the repository's member layout rules. NetPrints' own analyzers and source generators
are projects in the solution, run in every build and have snapshot tests.

**Why this priority**: The owner made this the first P3 batch (2026-10-08). The declarative pins, the composition with
a container, the settings validation and the hygiene analyzers are all generated or checked by this infrastructure.

**Independent Test**: Build the solution: the generators emit the service registrations, the `CommandKeyId` enum and
the settings validators, and their snapshot tests pass. A source file with an attribute on its member's line, or with
two public members and no blank line between them, fails the build with an NPS diagnostic.

**Acceptance Scenarios**:

1. **Given** a member whose attribute is on the same line as the member, **When** the solution builds, **Then** the
   build fails with an NPS diagnostic naming the file and line; with the attribute on its own line it builds.
2. **Given** two properties, methods, events or public members with no blank line between them, **When** the solution
   builds, **Then** an NPS diagnostic fails the build; private fields grouped without blank lines build.
3. **Given** a service marked `[Singleton<T>]` or `[SessionScoped<T>]`, **When** the editor composes, **Then** the
   service comes from the generated registration, and a singleton that depends on a session-scoped service is a build
   error.
4. **Given** a command key, **When** the editor registers the command, **Then** the key is a `CommandKey` member whose
   behaviour lives in the member (no switch over keys), attributes name it through the generated `CommandKeyId` enum,
   and gesture strings are parsed only at the boundaries (key bindings, manifests, display).
5. **Given** a settings section with an invalid value, **When** the editor composes, **Then** the generated validator
   reports the key and the reason, the section keeps its defaults, and the editor starts.
6. **Given** a graph document holding a member this editor version does not know, **When** it is opened and saved,
   **Then** the unknown member is written back unchanged and in canonical order.

---

### User Story 2 - Node model reshaped before the API freezes (Priority: P1)

A node author, built-in or extension, declares a node's pins as typed properties. A pin's key follows a convention
(the property name without the `Pin` suffix), with an attribute only to customise it. `out` and `ref` parameters
become output pins, purity follows from pin roles, node capabilities are role interfaces, and node search is split into
providers owned by node kinds. Saved graphs do not change.

**Why this priority**: These change public shapes (pin properties, node capabilities, search providers). The owner
decided a hard rename in 0.4.0 with no compatibility shims (2026-10-08), so they must land before the packages publish.

**Independent Test**: Every fixture and sample graph loads and saves byte-identical; the generated C# goldens are
byte-identical except the recorded changes for `out` and `ref` pins; the pin-layout golden (S0) matches the declared
pins; node search matches its characterization goldens.

**Acceptance Scenarios**:

1. **Given** a node kind with typed pin properties, **When** it builds, **Then** the generator registers its pins with
   keys equal to the property name without `Pin`, and a `[Pin("…")]` attribute overrides a key.
2. **Given** a change that would alter a stored pin key (a renamed pin property with no override), **When** the
   solution builds, **Then** pin-key tracking fails the build naming the old and the new key.
3. **Given** a graph saved by the P3a editor, **When** the P3 editor loads and saves it, **Then** the file is
   byte-identical, although 8 pin properties were renamed (research R8 counted 13 renames and 2 overrides; B6 found 9 overrides, renamed 8 and kept 1).
4. **Given** a call node for a method with an `out` parameter, **When** it is placed, **Then** it has an output data
   pin carrying the parameter's value after the call, and an `out` or `ref` input that is not connected to a variable
   is a validation error.
5. **Given** a call to a method that returns `void` and has an `out` parameter, **When** the user makes it pure,
   **Then** it is allowed; a `void` method with no `out` or `ref` parameter cannot be made pure, and the Exception pin
   does not count as a result.
6. **Given** each node-search scenario of the P3a characterization goldens, **When** the search opens, **Then** the
   results and their order match the goldens, except the recorded order change of the This Variables and This Methods
   categories, and no node is created to open the search.
7. **Given** an unconnected `sbyte` or `decimal` input pin, **When** the user edits it, **Then** it shows the numeric
   editor and its value emits valid C#.

---

### User Story 3 - Published packages and a stable extension API (Priority: P1)

An extension author references `NetPrints.Extensibility.Abstractions`, `NetPrints.Extensibility` and
`NetPrints.Serialization` as NuGet packages instead of the repository's projects. Every contribution kind the built-in
editor uses is reachable through the same public API; parts that are still settling need an explicit opt-in.

**Why this priority**: It is the phase's headline. P3b, U1 to U3 and community extensions build on these packages, and
every shape-defining change must land before the publish gate.

**Independent Test**: Pack the packages locally, build the sample extension against the packed packages only (no
project references), and load it in the editor; package validation and the public API checks pass.

**Acceptance Scenarios**:

1. **Given** the locally packed packages, **When** an extension project references only them, **Then** it compiles,
   its manifest validates, and the editor loads it.
2. **Given** a change to a public type of a published package with no entry in its API tracking files, **When** the
   solution builds, **Then** the build fails and names the member.
3. **Given** an extension that registers editor contributions, **When** the command-line tool or a build loads it,
   **Then** it loads without error and its editor contributions are ignored.
4. **Given** an extension that registers its own services, **When** it loads, **Then** it gets its own service
   provider, and it can resolve host services only from the published allow-list.
5. **Given** an extension that uses an experimental API without opting in to its id, **When** it builds, **Then** a
   compile error names the NPXE id.
6. **Given** an extension label with no translation for the editor's language, **When** it is shown, **Then** its
   default text appears.
7. **Given** an extension that registers a document migration for its own node data, **When** a document saved with
   the older shape is opened, **Then** the migration runs and the node loads.

---

### User Story 4 - Extensions contribute to the editor (Priority: P1)

A user opens a project that declares an extension, or has an extension in their user extension directories, and its
commands, menu items, shortcuts, panels, context-menu items, tooltips, go-to results, start page tiles, project
templates, status-bar items and settings pages appear next to the built-in ones, marked with the extension's name.
Closing the project removes that project's contributions. A failing extension does not take the editor down.

**Why this priority**: It is the purpose of the phase: P3a's contribution registry is internal, and P3 opens it.

**Independent Test**: Open a fixture project that declares the test extension: an item of each contribution kind
appears; close the project: none remains; a throwing extension shows its diagnostics and the editor keeps working.

**Acceptance Scenarios**:

1. **Given** a trusted project that declares an extension contributing one item of every kind, **When** it opens,
   **Then** each item appears on its surface, and the command palette and the shortcuts sheet name the extension as
   its owner.
2. **Given** that project open, **When** it is closed or another project is opened, **Then** none of its extension's
   contributions remains, and the built-in ones are unchanged.
3. **Given** an extension whose registration throws, **When** the project opens, **Then** none of its contributions is
   added, Errors shows an extension diagnostic naming it, and the rest of the editor works.
4. **Given** an extension command whose handler throws, **When** the user runs it, **Then** the error dialog names the
   extension and the command, and the editor stays usable.
5. **Given** two extensions that register the same command id, or the same gesture in the same scope, **When** they
   load, **Then** the first registration wins, the second is reported as an extension diagnostic, and built-in
   contributions always register first.
6. **Given** `NetPrints.Desktop --profile godot` and the sample Godot extension in a user extension directory, **When**
   the editor starts, **Then** New project preselects the Godot profile and the template list is filtered to it (the
   samples gallery filter moved to P3c).
7. **Given** a project created from the sample extension's Godot template, **When** it opens, **Then** it builds and
   the run profile button lists its game and Godot editor profiles.

---

### User Story 5 - Settings pages (Priority: P2)

A user opens Settings and finds every editor setting, and the settings of every loaded extension, on searchable pages
with validation, applied at once where the setting allows it.

**Why this priority**: Extensions already declare settings (NPXE0002) that no view shows, and the theme presets and
user shortcuts need a home; P3a deferred user shortcuts to these pages.

**Independent Test**: Open Settings, change the theme and a setting of the test extension, restart: both values
persist; enter an invalid value: it is refused with a message.

**Acceptance Scenarios**:

1. **Given** the editor, **When** the user runs Settings (Ctrl+,), **Then** a Settings document opens with the pages
   General, Appearance, Keyboard shortcuts, Run and Extensions, plus one page per loaded extension that declares
   settings, listed under Extensions.
2. **Given** an extension section with no custom view, **When** its page opens, **Then** it shows a field per setting
   generated from the section's schema, with the setting's description as help text.
3. **Given** text in the Settings search box, **When** the user types, **Then** only the settings whose label or
   description matches stay visible, across all pages.
4. **Given** an invalid value, **When** the user leaves the field, **Then** the validator's message appears under it
   and the value is not saved; a valid value is saved at once.
5. **Given** a setting that needs a restart, **When** it changes, **Then** it is labelled as such and the "Restart to
   apply" bar appears.
6. **Given** `settings.json` holding the section of an extension that is not loaded, **When** the user changes another
   setting, **Then** that section is written back unchanged.

---

### User Story 6 - User keyboard shortcuts (Priority: P2)

A user changes, adds or removes the shortcut of any command, built-in or contributed by an extension, sees a conflict
before it happens, and can reset one or all shortcuts.

**Why this priority**: P3a bound every built-in command (H3, H4) and deferred user-defined shortcuts to P3's settings
pages; extension commands make the need larger.

**Independent Test**: Rebind Save to another gesture, resolve the conflict the page reports, restart: the new gesture
saves and the old one does not; reset all: the defaults are back.

**Acceptance Scenarios**:

1. **Given** the Keyboard shortcuts page, **When** it opens, **Then** it lists every command with its label, owner,
   scope, default gestures and current gestures, filterable by text or by recording a gesture.
2. **Given** a command, **When** the user records a new gesture for it, **Then** the gesture runs the command in that
   scope from then on, and the menus, tooltips, command palette and shortcuts sheet show it.
3. **Given** a recorded gesture that another command uses in an overlapping scope, **When** the user confirms,
   **Then** the page names the other command first; accepting removes the gesture from it, and cancelling changes
   nothing.
4. **Given** user overrides, **When** the editor restarts, **Then** they apply; an override for a command that is not
   loaded is kept and listed under "Not loaded".
5. **Given** a single-key or Shift-only gesture recorded for a command in the Global scope, **When** the user confirms,
   **Then** it is refused with the reason (ADR-0020 scope rules).
6. **Given** "Reset" on one command or "Reset all", **When** the user confirms, **Then** the defaults are back and the
   overrides are removed from the settings file.

---

### User Story 7 - Run profiles (Priority: P2)

A developer runs a project through a launch profile from `Properties/launchSettings.json`, the file `dotnet run`,
Visual Studio and Rider share: a `Project` profile, or an `Executable` profile that starts an engine or tool after the
build, whatever the project's output type. The editor and `netprints run` behave the same. A Debug or Release
selector, an input line for programs that read the console, and a "run in external terminal" option complete it.

**Why this priority**: Godot and UnrealSharp script projects are libraries, so Run is disabled for them today; the
owner decided the profile kinds on 2026-10-14 (ADR-0037).

**Independent Test**: Open a fixture with `Project`, `Executable` and `IISExpress` profiles: the split button lists
all three with `IISExpress` disabled and its reason; running the `Executable` profile starts the expected argv in the
expected folder; `netprints run -lp` starts the same process request.

**Acceptance Scenarios**:

1. **Given** a project with profiles, **When** it opens, **Then** the run profile button lists them in file order, the
   first one NetPrints can run is selected, and the user's selection is remembered per project.
2. **Given** an `Executable` profile whose `executablePath` uses `%GODOT4%` and whose arguments use `$(ProjectDir)`,
   **When** the user runs it, **Then** NetPrints builds, starts the absolute executable with the expanded argument list
   and working directory, and prints the resolved command line in Output on the profile's first run.
3. **Given** the same profile with `GODOT4` not set, **When** the user runs it, **Then** nothing starts and the message
   says the variable is not set.
4. **Given** a profile of a kind NetPrints cannot run (`IISExpress`, `Docker`, `WSL2`), **When** the list shows,
   **Then** it is disabled with the reason, and `netprints run -lp` with it exits 2.
5. **Given** a running program, **When** the user stops it, **Then** NetPrints asks it to close and kills its process
   tree after the stop timeout; unloading a project whose `Executable` run is alive asks whether to leave it running.
6. **Given** a program that reads standard input, **When** the user types a line in Output's input box, **Then** the
   program receives it; closing the input sends end of file.
7. **Given** the Release configuration selected, **When** the user builds or runs, **Then** both use Release, and the
   choice is remembered per project.

---

### User Story 8 - Theme presets (Priority: P2)

A user chooses a preset for Dark and one for Light (NetPrints Dark or High contrast; NetPrints Light or High contrast)
on the Appearance page and sees it applied at once. Every visual element of the editor is a theme
token, so presets can change any of them.

**Why this priority**: B0 of ADR-0035 (owner, 2026-10-13); the presets are embedded data and need the settings page.

**Independent Test**: Choose High contrast for Dark: the canvas turns `#000` and every node gets a solid border, nothing restarts; switch to Light: the Light preset applies and the Dark preset does not leak into it.

**Acceptance Scenarios**:

1. **Given** the Appearance page, **When** the user picks a Dark preset, **Then** it applies at once, only to the Dark
   variant, and is remembered.
2. **Given** each built-in preset, **When** the contrast tests run, **Then** every gated pair passes.
3. **Given** a settings file naming a preset that does not exist, **When** the editor starts, **Then** the variant's
   default preset applies, the problem is logged, and the other appearance settings are kept.
4. **Given** the High contrast preset, **When** it is selected, **Then** it is built on Semi's own high-contrast
   variant, and every NetPrints token has a value tuned for it.

---

### User Story 9 - Code that fits the project's C# version (Priority: P2)

A user whose project targets an older framework or pins an older `LangVersion` gets generated code that compiles for
it, the same bytes from the editor, `netprints regen` and `dotnet build`, and node search that hides what the project
cannot use, with the reason and a fix.

**Why this priority**: ADR-0038 (owner, 2026-10-14) puts the stable target types before the publish gate, and Godot
and Unity projects pin older versions.

**Independent Test**: For each workspace fixture (no `LangVersion`, `latest`, `preview`, `10.0`, an unknown `16.0`,
multi-target) the three writers produce identical bytes; a search for a node that needs a newer version says it is
hidden and offers "Show unavailable".

**Acceptance Scenarios**:

1. **Given** a project with `<LangVersion>latest</LangVersion>`, **When** code is generated, **Then** the version is the
   smaller of the framework's maximum and the editor's maximum, and an info message suggests pinning `<LangVersion>`.
2. **Given** a project that targets two frameworks, **When** code is generated, **Then** the lower version and the
   capabilities common to both apply.
3. **Given** a node that needs a newer C# version than the project's, **When** the user searches for it, **Then** it is
   hidden, the list says how many results the project's C# version hides, and "Show unavailable" shows them disabled
   with the reason.
4. **Given** an unavailable item with a quick fix, **When** the user applies "Set LangVersion", **Then** the project
   file's `<LangVersion>` is raised, at most to the framework's maximum.

---

### User Story 10 - Extension author kit (Priority: P2)

An extension author creates a project from `dotnet new netprints-extension`, tests it with the conformance kit, checks
the built package with `netprints extension verify`, and reuses an author CI workflow.

**Why this priority**: The packages are only useful with a way to know an extension behaves; NetPrintsUnreal needs it
before its first release.

**Independent Test**: Create an extension from the template with no edits: it builds, its kit tests pass, and
`netprints extension verify` on its package exits 0; break one rule per conformance check: each fails with its own
message.

**Acceptance Scenarios**:

1. **Given** a new folder, **When** the author runs `dotnet new netprints-extension`, **Then** the extension and its
   test project build and the conformance tests pass without edits.
2. **Given** an extension that breaks a conformance check, **When** its kit tests run, **Then** that check fails with a
   message naming the rule and the offending item.
3. **Given** an extension package built against an API the host does not have, **When** the author runs `netprints
   extension verify`, **Then** it exits non-zero and lists each missing member.
4. **Given** `NetPrints.TestExtension`, **When** the repository's tests run, **Then** it is tested only through the kit.

---

### User Story 11 - Carry-overs from P2 and P3a (Priority: P2)

A maintainer closes the defects and decisions earlier phases left for P3: extension reference checks, unreadable
references, generation on project load, the info-level analyzer backlog, two Windows command-line defects, and the
editor defects found at the end of P3a.

**Why this priority**: Recorded follow-ups that touch code P3 rewrites; leaving them makes P3's own work harder to
verify.

**Independent Test**: Each item has a test that fails before its fix (where it can run in CI), and the manual checks
are listed for the owner.

**Acceptance Scenarios**:

1. **Given** an extension whose private dependency conflicts with a host-provided or transitive reference, **When** it
   loads, **Then** NPX008 reports it.
2. **Given** a project with an unreadable reference, **When** the editor loads it, **Then** the other references load,
   and the unreadable one is logged and shown as a warning.
3. **Given** a catalog file checked out with CRLF line endings, **When** `netprints catalog --check` runs on Windows,
   **Then** it is not reported as stale.
4. **Given** `netprints git-install --command` with a Windows path, **When** git later runs the driver from `sh`,
   **Then** the path is intact.
5. **Given** a project opened in the editor or checked with `--check-project`, **When** loading ends, **Then** no
   generated file was written.

---

### User Story 12 - NuGet packages in the References dialog (Priority: P3)

A developer browses, adds, updates and removes NuGet packages from a Packages tab in the References dialog, with the
sources and credentials `dotnet restore` uses, and a failed change leaves the project files exactly as they were.

**Why this priority**: B7 (gap research 2026-10-06); it also builds the package service that P3c's extension manager
and samples gallery reuse. The owner kept it in P3 on 2026-10-16 (Clarifications).

**Independent Test**: In a Central Package Management fixture and a plain fixture, add and update a package from a
local feed: the files match what `dotnet package add` writes; make the restore fail: the files are byte-identical to
before.

**Acceptance Scenarios**:

1. **Given** the Packages tab, **When** the user searches a source, **Then** results show with versions, a prerelease
   switch, and deprecated and vulnerable badges.
2. **Given** a project that uses Central Package Management, **When** the user adds a package, **Then** the version goes
   to `Directory.Packages.props` (or a `VersionOverride` when the user picks one) and the reference to the project file,
   with comments and order preserved.
3. **Given** a change whose restore fails, **When** the failure is reported, **Then** the project and props files are
   restored to their exact bytes.
4. **Given** a package that graph nodes use, **When** the user removes it, **Then** the nodes that use it are listed
   first; proceeding keeps them as unresolved nodes.
5. **Given** no network, **When** the tab opens, **Then** installed packages and the global packages folder are still
   listed, marked offline.

---

### User Story 13 - Extension manager (moved to P3c)

Moved to roadmap phase P3c on 2026-10-16, with FR-094 to FR-098 and SC-013; it reuses User Story 12's package service.
P3 keeps loading per-user extensions from the configured extension directories and the extension package format that
the author kit packs and verifies (Assumptions).

---

### User Story 14 - Samples from remote sources (moved to P3c)

Moved to roadmap phase P3c on 2026-10-16, with FR-099 to FR-106 and SC-014; it reuses User Story 12's package service.
The start page keeps P3a's samples tile and bundled sample until P3c replaces them.

---

### User Story 15 - New projects from the command line (Priority: P3)

A developer creates a NetPrints project with `netprints new` or `dotnet new`, from the same templates the editor's New
project offers.

**Why this priority**: B15; small once templates carry files (N3).

**Independent Test**: `netprints new console --name Hello` and `dotnet new netprints-console -n Hello` produce the same
files as the editor's New project with the Console template, and the project builds and runs.

**Acceptance Scenarios**:

1. **Given** `netprints new --list`, **When** it runs, **Then** it lists the built-in templates and those of per-user
   extensions, with their profile.
2. **Given** `netprints new console --name Hello --output ./hello`, **When** it runs, **Then** the project is created
   and builds; an unknown template exits 2.

---

### User Story 16 - Inspector sections, value editors and graph rules (Priority: P4)

An extension author adds sections to the inspector for variables, methods and classes, contributes editors for pin and
default values of its types, adds graph rules whose diagnostics appear in Errors and in `netprints build`, and reaches
the translation context for helper members; built-in node kinds are declared by convention.

**Why this priority**: The additive tail of the SOLID plan (R6, N2, N5, S7 and SUG5); each is experimental. The owner
kept it in P3 on 2026-10-16 with no scope rule (Clarifications); the model-driven grid stays in P3b.

**Independent Test**: The test extension contributes an inspector section, a value editor for one of its types and a
graph rule: each appears, the rule's diagnostic shows in Errors and fails `netprints build`, and the extension's
conformance tests cover all three.

**Acceptance Scenarios**:

1. **Given** an extension inspector section that applies to variables, **When** a variable is selected, **Then** the
   section appears below the built-in ones and edits the model with undo.
2. **Given** an extension value editor for a type, **When** an unconnected pin or a default of that type is edited,
   **Then** the extension's editor is used; without the extension, the generic editor is.
3. **Given** a graph rule that flags a pattern, **When** the graph has it, **Then** Errors lists the diagnostic in the
   editor, and `netprints build` and `generate` report it with the same code.

---

### User Story 17 - Themes from users and extensions (Priority: P4)

A user duplicates a preset into a theme file, edits it, reloads it, and uses extensions that ship themes. Themes are
data and never run code.

**Why this priority**: B1 and C of ADR-0035; the owner wants community themes as plugins and kept both in P3 on
2026-10-16 (Clarifications). Distributing extension themes through the extension manager is P3c's.

**Independent Test**: Duplicate NetPrints Dark, change two tokens and a Semi key in the file, Reload: the editor shows
the change; a token with a bad value keeps the base value and is warned about; an extension's theme appears in the
preset lists while the extension is loaded.

**Acceptance Scenarios**:

1. **Given** "Duplicate theme", **When** the user runs it, **Then** a `*.nptheme.json` file with the active preset's
   values is written to the per-user themes folder and selected.
2. **Given** a theme file with an unknown token, a bad value, a newer schema or a failing contrast pair, **When** it is
   loaded, **Then** the unknown token is ignored with a warning, the bad value keeps the base value with a warning, the
   newer file is skipped with a message, and a contrast failure applies with a warning naming the pairs.
3. **Given** a loaded extension whose manifest lists themes, **When** the Appearance page opens, **Then** those themes
   are offered under the extension's name.

---

### Edge Cases

- An extension registers a contribution whose view fails to build at runtime: the surface shows a placeholder naming
  the extension, the failure is logged once, and the rest of the surface renders.
- The saved layout names a panel of an extension that is not loaded: the panel is dropped and the rest of the layout
  is restored (P3a rule); when the extension is loaded again, the panel opens at its default place.
- A user key binding names a command that no longer exists: the binding is kept in the file and listed under "Not
  loaded"; an invalid gesture string is reported on the Keyboard shortcuts page and ignored.
- A user binding takes a gesture an extension's default also claims in the same scope: the user binding wins, and the
  page marks the extension's default as overridden.
- `settings.json` cannot be read (invalid JSON): the editor starts with defaults, says so once, and keeps a copy of the
  unreadable file next to it before the next write.
- A `launchSettings.json` that is not valid JSON, or has two profile names differing only by case: the run profile
  button shows the error, Run is disabled for profiles, and `netprints run` exits 1 naming the file.
- An `Executable` profile points at a `.cmd` or `.bat` file on Windows: the run is refused with the reason (it would
  run through `cmd.exe`, which re-parses the arguments), and the message suggests making the interpreter the executable.
- A GUI-subsystem program (such as `UnrealEditor.exe`) has no standard output: the profile's "Don't capture output"
  option starts it detached from Output, which then shows only start and exit.
- An engine started by an `Executable` profile runs its own `dotnet build`: the MSBuild variables that NetPrints' own
  MSBuild locator set are not passed to it; values the user set before starting NetPrints are passed.
- The editor is started with `--profile` and a project path: the project opens and the profile is ignored, with an
  info message.
- A project's `LangVersion` names a version newer than the editor's compiler knows (`16.0`): the editor's maximum is
  used and an info message says so.
- A restore fails halfway through a package change, or the user cancels it: the project and props files are put back
  byte-identical, and the reflection provider is not reloaded.
- A package reference whose version comes from an MSBuild property, or sits in a condition the change cannot target:
  the Packages tab shows it read-only with the reason, and no edit touches it.
- Moved to P3c on 2026-10-16: a feed that ignores the package type filter, sample index and image limits, an extension
  disabled while documents use its nodes, and an extension package shown as Incompatible.

## Requirements *(mandatory)*

### Functional Requirements

**Analyzer and generator foundation (US1)**

- **FR-001**: The solution MUST hold NetPrints' own analyzer and source-generator projects (`netstandard2.0`, the
  constitution IV exception), referenced by every `src/` project that uses them, running in every build and in the
  editor, with no change to how a user's NetPrints project builds.
- **FR-002**: NPS layout rules MUST fail the build in `src/` when an attribute shares a line with its member or with
  another attribute, and when no blank line separates two properties, methods, events, public members or members with
  braces, a comment or attributes; consecutive private fields MAY stay grouped (owner decision 3, 2026-10-08).
- **FR-003**: Services MUST be declared with `[Singleton<T>]` and `[SessionScoped<T>]`, and a registration generator
  MUST emit factory registrations for MS.DI with no runtime assembly scanning (ADR-0031). A generator analyzer MUST
  report a singleton that depends on a session-scoped service, and the container MUST exist only at the root and per
  project session; class and graph contexts use generated typed factories, and nothing is resolved per row, node or
  pin.
- **FR-004**: The composition MUST move to the container in the steps and gates of ADR-0031: R5-0 baselines (M1 shell
  start, M2 graph scope, M4 trimming warnings) and the M5 spike before the first step, then app and shell (gate M1),
  project session (a test resolves every registration), class and graph (gate M2 with counting fakes). A step whose
  gate fails MUST stop the migration and be reported.
- **FR-005**: An internal `[SmartEnum]` generator MUST emit only the companion id enum and the `All` list; `CommandKey`
  MUST be an abstract class with private nested sealed members carrying per-member behaviour, with `CommandKeyId` for
  attributes; `CommandKey` and `CommandGesture` MUST implement span parsing and formatting directly, and gesture
  strings MUST be parsed only at boundaries (ADR-0034).
- **FR-006**: Settings sections MUST be validated by source-generated `[OptionsValidator]` validators that the
  composition calls itself at start; an invalid section MUST fall back to its defaults with a message naming the key
  (ADR-0040).
- **FR-007**: Every document record MUST preserve members it does not know on save, in canonical order, and `format`,
  `show` and the merge driver MUST treat them as opaque; this MUST land before any new document field ships
  (ADR-0038 decision 10, ADR-0039 decision 14). The editor's open and save keep them on every record that has an
  object in the model; inside a type, method, variable or value reference only `format`, `show` and the merge driver
  keep them, because the editor writes those references from the model's own types.
- **FR-008**: Every generator MUST have snapshot tests of its output and every analyzer rule a positive and a negative
  test; the docs MUST list each NetPrints diagnostic prefix (NPS, NPA, NPH and the rest) with its rules.

**Node model and search (US2)**

- **FR-009**: Data pins MUST be typed (`NodeInputDataPin<T>`, `NodeOutputDataPin<T>`), with `default(T)` defaults (except
  `IfElseNode.ConditionPin` and `ForLoopNode.MaxIndexPin`, which start unset to keep stored graphs byte-identical) and no
  generic-math constraint.
- **FR-010**: Node kinds MUST declare their pins through `[NodeKind]`, `[Pin]` and `[PinType]` and a convention
  generator: a pin's key is its property name without the `Pin` suffix unless `[Pin("…")]` overrides it; analyzers
  NPA001 to NPA009 MUST report misuse, with code fixes where the fix is mechanical.
- **FR-011**: Pin-key tracking (NPA010, NPA011), seeded from the S0 pin-layout golden, MUST fail the build when a
  stored pin key changes without a declared override, and MUST land before the renames.
- **FR-012**: The 8 pin property renames (research counted 13 renames and 2 overrides; B6 found 9 overrides, renamed 8 and kept 1)
  and the typed getters, the sealed node classes and the removed overrides MUST ship as a hard break in `NetPrints.Core` 0.4.0:
  `*REMOVED*` API entries and release notes, no `[Obsolete]` shims, and no change to any stored key.
- **FR-013**: Pins MUST carry a role (Result, OutParam, Exception, Target and the others the plan lists); a node MUST be
  allowed to be pure exactly when it has at least one output data pin with a result role; the Exception pin MUST NOT
  count (owner decision 13, 2026-10-08).
- **FR-014**: Every `out` and `ref` parameter of a call MUST also have an output data pin carrying its value after the
  call; an `out` or `ref` input MUST be connected to a variable, or validation reports it (owner decision 12,
  2026-10-08). Schema v1 stays additive; changed goldens are listed with the reason.
- **FR-015**: Node capabilities MUST be role interfaces (R2, ADR-0029), including `IMemberReferencingNode`, so search,
  navigation and the inspector ask for a role instead of a concrete node type.
- **FR-016**: Node search MUST be built from providers grouped by the node kind that owns them (SUG1), with
  `NodeSearchViewModel` and `SuggestionRowViewModel` replacing `SuggestionListViewModel` and `SuggestionItem`; nodes MUST
  be created through `Func<NodeGraph, Node>` factories by one creation service, and ranking MUST be one host-side
  ranker with a fixed category order (SUG2, SUG3, ADR-0028). Opening the search MUST NOT create a node.
- **FR-017**: Node search MUST return the same results in the same order as the P3a characterization goldens, except
  the recorded This Variables and This Methods order change, and MUST stay within P3a's budget of 30,000 rows.
- **FR-018**: A `PrimitiveKinds` table in Core MUST replace the scattered primitive lists (and with them the IL2057 trim warning of `NodePinViewModel`; the one at `TypedValueConverter.ParseEnum`
  stays, because enums are not in the table), and `sbyte` and `decimal` MUST be full primitives (unconnected
  defaults and the numeric editor), pinned by a golden test (ADR-0034).

**Published packages and the public API (US3)**

- **FR-019**: `NetPrints.Extensibility.Abstractions` (contracts only) MUST be split from `NetPrints.Extensibility`
  before the search provider contracts publish (R7, ADR-0030).
- **FR-020**: `NetPrints.Extensibility.Abstractions`, `NetPrints.Extensibility` and `NetPrints.Serialization` MUST become
  packable at version 0.4.0 together with the packages they depend on, with public API tracking files, package
  validation, a README, and XML docs on every public member; `ExtensionApi.Version` MUST change with every shipped API
  change, and the validation baseline MUST be set at the first publish.
- **FR-021**: The contribution kinds MUST be public descriptors in the Abstractions package: commands, panels, dashboard
  tiles, project templates, context-menu items, tooltip providers, go-to providers (P3a), plus status-bar items (C-15),
  settings pages, dialogs (R4), node search providers (SUG4), document migrations (N4), view resources and icon ids
  (N8, ADR-0021) and, in the tail, inspector sections, value editors and graph rules (FR-109, FR-110). The built-in
  editor MUST register all of its own contributions through the same public API, as the built-in editor extension; the
  built-in status-bar segments (error and warning counts, build state, run state, zoom, selection count) MUST be status-bar
  items.
- **FR-022**: Command handlers MUST receive a typed context built on `IProjectSession` and a graph-view abstraction,
  not editor view models (ADR-0020's R16 note), before `ICommandHandler` publishes; contribution descriptors MUST get
  typed contexts and an `AppliesTo` predicate, so a context-menu item can be hidden, not only disabled (R5a, R5b).
- **FR-023**: Editor contributions MUST be registered through an editor facet of the extension builder (N1); hosts
  without an editor (the CLI, the build) MUST provide a facet that accepts and ignores them.
- **FR-024**: An extension MUST be able to register its own services (`IExtensionBuilder.Services`, experimental) into a
  provider of its own; host services MUST be reachable only through an allow-list of host exports, and core services
  are not replaceable by extensions in P3.
- **FR-025**: Project templates MUST be able to carry files (N3): a file tree with replacement tokens (project name,
  namespace, profile) and optional `Properties/launchSettings.json`; the built-in Console and Library templates MUST use
  the same mechanism.
- **FR-026**: Extensions MUST be able to register document migrations for their own node data (`AddDocumentMigration`,
  N4), run on load before the node is created.
- **FR-027**: Localization infrastructure (L1, ADR-0027) MUST separate stable keys from display text: contribution
  labels are keys with a default text, display text comes from resources, `netprints.language` selects the language,
  menu ids are separate from labels, and Semi and Ursa follow the app culture (ADR-0023 amendment). A key with no
  translation MUST show its default text. UI text P3 adds MUST come from resources, and the XAML literal ratchet MUST
  NOT grow; moving the existing strings is P6 (L2).
- **FR-028**: `CSharpLanguageVersion` and a minimal `CSharpTarget` MUST be public and stable before the publish gate;
  `CSharpFeature` and `Requires` on node-search entries MUST be experimental under a new NPXE id (ADR-0038 decision 9).
- **FR-029**: The extension manifest MUST accept a `themes` field (a list of theme files) before the packages publish,
  with a `netprintsApi` minor version that declares it (ADR-0035). Offering those themes on the Appearance page is
  FR-115.
- **FR-030**: Every API that is still settling MUST carry `[Experimental]` with an NPXE id listed in the extension
  guide (ADR-0017); the plan assigns the ids.
- **FR-031**: After the publish gate, the hygiene analyzers NPH001 to NPH006 (S5) and the command analyzer (S6) MUST run
  at error severity in `src/`, replacing the hygiene tests they cover (ADR-0003 amendment; XAML rule E7 stays a test,
  ADR-0007 amendment).

**Extensions in the editor (US4)**

- **FR-032**: The editor MUST keep contributions in an app group and in one session group per open project, as the
  Clarifications define, and MUST remove a session group as a whole when its project unloads.
- **FR-033**: Every surface the registry generates (menus, command bar, palette, shortcuts sheet, context menus,
  tooltips, go-to results, start page, templates, status bar, settings) MUST show contributions from extensions next
  to the built-in ones, and the palette and the shortcuts sheet MUST show each item's owner.
- **FR-034**: An extension that throws while registering MUST contribute nothing, and Errors MUST show an extension
  diagnostic with its id; a handler, provider or view that throws later MUST be contained: the error dialog or a
  placeholder names the extension, the failure is logged, and the editor keeps working. For a view, its construction,
  and the layout passes that run through its holder, are contained (a failure there is reported once per view and the
  view is replaced by the placeholder); any other exception from an extension's view code, such as a later
  self-invalidated layout, attach, render or an event handler, reaches the editor's error dialog, which names the
  extension (display name and id) whose code threw, says the details are in the log and says the user can stop
  loading it by removing its folder from the extension folders on the Extensions page or, for a project's copy, by
  no longer trusting the project there (disabling an extension is FR-098, P3c). An exception from a binding is not
  one of these: Avalonia turns it into a binding error in the log, and it does not reach the dialog.
- **FR-035**: Registry issues (duplicate ids, gesture conflicts in a scope, a contribution naming an unknown id) MUST
  become extension diagnostics shown in Errors and logged; the first registration wins and built-in contributions
  always register first (ADR-0020).
- **FR-036**: `NetPrints.Desktop --profile <id>` MUST behave as the Clarifications define (answer 5, narrowed on
  2026-10-16: the samples gallery filter moved to P3c); with a project path as well, the project opens and the profile
  is ignored with an info message.
- **FR-037**: A sample non-Unreal extension for Godot MUST live in the repository, build only against the packed
  packages, pass the conformance kit, and contribute: a project profile `godot`; a Godot project template whose
  `launchSettings.json` holds
  `Executable` profiles for the game and the Godot editor (`%GODOT4%`, `--path .`), labelled with the stale-assembly
  note for Release builds; a command; a settings page; and a status-bar item. Its sample under `samples/<id>/` moved to
  P3c with FR-105. It needs no Godot installation to build or test, and it is not published to nuget.org in P3.
- **FR-038**: Project-declared extensions MUST keep loading in the CLI and the build without a trust check, as
  analyzers and build tasks do; the extension guide and ADR-0030 MUST say so.

**Settings pages (US5)**

- **FR-039**: A Settings document MUST open from the command `openSettings` (Ctrl+,, File menu, palette) and list
  pages from the settings page contribution kind: General, Appearance, Keyboard shortcuts, Run, Extensions, and one
  page per loaded extension section, listed under Extensions.
- **FR-040**: A section with no custom view MUST get a page generated from its exported JSON schema, as the
  Clarifications define; the schema of the built-in sections MUST be published as a file, golden-tested, and
  documented in a settings table (S10).
- **FR-041**: Search MUST filter settings by label and description across all pages.
- **FR-042**: A value MUST be validated when the field loses focus or the user presses Enter; an invalid value shows
  the validator's message under the field and is not saved; a valid value is saved at once and applied at once unless
  the setting is marked as needing a restart, which shows the "Restart to apply" bar.
- **FR-043**: Settings MUST persist only through the existing settings store in `settings.json` (ADR-0040): unknown
  sections and keys are preserved, and an unreadable file is copied aside before the next write.
- **FR-044**: The built-in pages MUST cover every existing user setting (theme and presets, extension directories,
  trusted projects, language, run options) and the new ones P3 adds; no built-in setting may exist only in the file.

**User keyboard shortcuts (US6)**

- **FR-045**: The Keyboard shortcuts page MUST list every registered command (built-in and contributed) with label,
  owner, scope, default gestures and current gestures, filterable by text and by a recorded gesture.
- **FR-046**: The user MUST be able to add, replace and remove gestures of any command, and reset one command or all
  commands; changes apply at once to key handling and to every surface that shows gestures.
- **FR-047**: Before a change takes effect, the page MUST name any command that uses the same gesture in an overlapping
  scope; accepting removes the gesture from that command, and cancelling changes nothing.
- **FR-048**: User bindings MUST follow ADR-0020's scope rules (no single-key or Shift-only gesture in Global, function
  keys exempt) and the macOS Ctrl-to-Cmd mapping.
- **FR-049**: User overrides MUST be stored as differences from the defaults in the `keyboard` section of
  `settings.json`; an override for a command that is not loaded MUST be kept and listed under "Not loaded"; an invalid
  gesture string MUST be reported and ignored.
- **FR-050**: Precedence MUST be: user override, then built-in default, then extension default in load order.

**Run profiles, configuration and console (US7)**

- **FR-051**: NetPrints MUST read `Properties/launchSettings.json` and support the `Project` and `Executable` kinds
  natively, applying each profile itself with an explicit argument list and working directory, the same way in the
  editor and the CLI (ADR-0037 decision 1); the kinds are an internal table in P3.
- **FR-052**: The default profile MUST be the first one in file order whose kind NetPrints can run; `commandName` is
  case-sensitive, profile names are not, and names that differ only by case are an error. Other kinds MUST be listed
  disabled with the reason and never delegated to `dotnet run`. `<name>.run.json` files and VB's `My Project/` are
  ignored.
- **FR-053**: The selected profile and the "Run in external terminal" and "Don't capture output" options MUST be stored
  per user and per project in the session state, never in `launchSettings.json`.
- **FR-054**: `%NAME%` MUST be expanded from the environment and `$(Name)` from captured project properties
  (`ProjectDir`, `TargetDir`, `TargetPath`, `OutDir`, `Configuration`, `TargetFramework` and names added through
  `AddProjectProperty`), then the result split with the Windows `CommandLineToArgvW` rules on every OS and passed as
  an argument list; a join and split round-trip test MUST pin it.
- **FR-055**: Paths MUST follow ADR-0037 decision 6: a relative `workingDirectory` from the project folder, an absent
  one is the project folder, an `executablePath` with a separator relative to the working directory, a bare name from
  `PATH` (with `.exe` and `.com` on Windows); the OS MUST receive an absolute path, and a macOS `.app` folder resolves
  to `Contents/MacOS/<binary>` or is reported.
- **FR-056**: An unset `%NAME%` in `executablePath` or `workingDirectory`, an uncaptured property or a missing executable
  MUST stop the run before the build starts, with a message naming it; in arguments and environment it is a warning
  and the text stays literal. A `.cmd` or `.bat` target on Windows MUST be refused with the reason.
- **FR-057**: The profile's environment and `DOTNET_LAUNCH_PROFILE` MUST reach the program only (`dotnet run -e` for
  `Project`, the process request for `Executable`); the MSBuild variables NetPrints' own MSBuild locator set MUST NOT
  reach an `Executable` program, and values the user set before NetPrints started MUST.
- **FR-058**: The first run of each `Executable` profile in a session MUST print the resolved command line in Output;
  there is no new trust prompt (ADR-0037 decision 5).
- **FR-059**: Stop MUST ask the program to close (its main window, or SIGTERM) and kill its process tree after the stop
  timeout (setting `run.stopTimeoutSeconds`, default 5); unloading a project with a live `Executable` run MUST ask
  whether to leave it running.
- **FR-060**: Project settings MUST get a Run section (profile list, kind, executable path, arguments with an argument
  preview, working directory, environment variables; other kinds read-only with "Open file"), with the warning that
  `dotnet run` resolves a relative `workingDirectory` from `Properties/`.
- **FR-061**: The command bar MUST show a run profile split button next to Run and Stop when the project has profiles
  or is an executable, with the commands `selectRunProfile` and `editRunProfiles`; Run MUST be enabled when the
  selected profile's kind can run the project, not only for `OutputType` Exe.
- **FR-062**: `netprints run` MUST use the default profile and accept `-lp|--launch-profile <name>` and
  `--no-launch-profile`; `-- <args>` replace the profile's arguments while its environment and working directory still
  apply; an unknown profile or a kind NetPrints cannot run exits 2, a profile that cannot be resolved exits 1
  (ADR-0015).
- **FR-063**: A Debug/Release configuration selector MUST sit next to the run profile button (B23); build and run use
  the selection, which is remembered per user and per project; the CLI keeps its `-c` option.
- **FR-064**: While a program runs with captured output, Output MUST offer an input line that writes a line to the
  program's standard input and a control that closes it (B5); "Run in external terminal" MUST start the program in a
  new terminal window of the OS, with the terminal command configurable on Linux.
- **FR-065**: The sample Godot template's profiles MUST work with no core code for Godot (ADR-0037 decision 12).

**Theme presets (US8)**

- **FR-066**: The theme token catalogue MUST live in product code with the contrast math, covering every visual element
  (ADR-0035 decision 1), including wire opacity and thickness per variant and the inspector tokens ADR-0036 needs; the
  docs MUST list the tokens from the catalogue.
- **FR-067**: Presets MUST be applied as a per-variant overlay in the application's Dark and Light theme dictionaries,
  so a Dark preset never affects Light; a test MUST pin the overlay precedence.
- **FR-068**: The embedded presets MUST be NetPrints Dark, NetPrints Light and High contrast (on Semi's high-contrast variants with tuned NetPrints tokens; in System mode the tuned tokens apply on the plain variant, ADR-0035 Amendment 2); each MUST pass the contrast gates on the exact ratio. (Owner, 2026-10-18: the Classic Dark preset is removed; it only mimicked the legacy editor.)
- **FR-069**: The Appearance page MUST offer the mode (Dark, Light, System) and the `darkPreset` and `lightPreset`
  choices, applied at once; the preset settings MUST be strings parsed at the boundary, and an unknown value falls back
  to the variant's default with a log entry (ADR-0035 decision 7).

**C# target (US9)**

- **FR-070**: Each project MUST have one `CSharpTarget` computed from the evaluated `LangVersion`,
  `MaxSupportedLangVersion` and target frameworks, carried by the project snapshot and reached through `Project` and
  `IProjectSession`; generated code MUST depend on its language version only (ADR-0038).
- **FR-071**: `latest`, `preview` and `default` MUST resolve to the smaller of the framework's maximum and the newest
  version NetPrints knows (`CSharpLanguageVersion.MaxKnown`), with an info hint to pin `<LangVersion>`; a multi-targeted project MUST use the minimum version and
  the common capabilities; a version newer than the framework's default is allowed with an info message.
- **FR-072**: The language version MUST travel in the generate request, so the editor, `netprints regen` and the SDK's
  generate target write identical bytes for the same project.
- **FR-073**: Node search MUST hide entries whose requirements the target does not meet, say how many are hidden, and
  offer "Show unavailable"; short lists MUST show unavailable items disabled with the reason and the quick fixes.
- **FR-074**: Quick fixes MUST offer raising `<LangVersion>` up to the framework's maximum (the project body wins over
  an imported props file, and the fix says so) and a PolySharp hint when only types are missing; a missing type is a
  warning, not an error.
- **FR-075**: Node translators MUST reach the target through a default-implemented `Target` on the execution
  translation context, and literal formatting MUST follow the version.

**Extension author kit (US10)**

- **FR-076**: `NetPrints.Extensibility.Testing` MUST ship `ExtensionTest<TExtension>` (declarative test state,
  `RunAsync`), an `ExtensionHarness` for a real or folder-based extension, and a conformance suite with the 12 checks of
  the roadmap (manifest, packaging, type identity, pure and repeatable registration, no issues, node round trip,
  translation compiles, deterministic emitters, settings, host channel lifecycle, coexistence with the built-ins and a
  noisy neighbour, disposal), plus an optional xUnit adapter.
- **FR-077**: The conformance suite MUST also check service providers, graph rules and editor contributions, and scan
  contributed views for literal colours and sizes a theme cannot reach (ADR-0010 amendment).
- **FR-078**: `NetPrints.TestExtension` MUST be tested only through the kit, and each conformance check MUST have a
  fixture extension that fails it.
- **FR-079**: A `dotnet new netprints-extension` template MUST create an extension project and its test project that
  build and pass the kit with no edits; `dotnet pack` of the extension project MUST produce the extension package of
  FR-080.
- **FR-080**: `netprints extension verify <folder|package>` MUST run the checks that need no test code (manifest,
  packaging, type identity, API compatibility: every host member the extension references exists in the host's API
  version) and write a report as text or `--format json`, with ADR-0015 exit codes. An extension package is a `.nupkg`
  of package type `NetPrintsExtension` holding the folder the loader reads; installing it from a feed or a file is
  P3c's (FR-095).
- **FR-081**: A reusable author CI workflow MUST build an extension, run its kit tests and `verify`, and be used by the
  sample Godot extension's CI.

**Carry-overs (US11)**

- **FR-082**: NPX008 MUST also check host-provided and transitive references (FU-1).
- **FR-083**: The live reflection provider MUST skip an unreadable or bad reference, log it once and show it as a
  warning, instead of failing the whole reload (FU-2).
- **FR-084**: The info-level analyzer backlog in `src/` (FU-5; 210 hits at the P3a final run) MUST be triaged: each rule
  is fixed in code or recorded in ADR-0003 with its reason, and the end state is 0 unrecorded info-level hits in
  `src/`.
- **FR-085**: Loading a project in the editor, and Desktop `--check-project`, MUST NOT write generated files, as the
  CLI's check does not (FU-6); generation runs on save and build.
- **FR-086**: `netprints catalog --check` MUST NOT report a catalog as stale for line-ending differences alone, and the
  repository MUST declare `.npcat.json` files as LF in `.gitattributes`; a Windows CI test MUST pin it.
- **FR-087**: `netprints git-install --command` MUST write a command that `sh` runs with the path intact on Windows; a
  Windows CI test MUST pin it.
- **FR-088**: The P3a open items MUST be closed: the Dock `IsPinned` binding warning at runtime is gone (the startup
  warnings test covers it; Dock#1191 does not fix it, and an upstream issue awaits the owner's OK), a hover on the
  middle of a cable hits it at the fit zoom (a test pins it), the start-page guide images that carry the build version
  are regenerated before the release, and the `ShellMainFlow` E2E test is watched (a new failure is fixed in the batch
  that sees it). If a Dock release that includes wieslawsoltes/Dock#1191 ships during P3, Dock MUST be bumped from
  12.1.0.6 and the workaround that fills empty capability objects removed, with the capability binding warnings still
  absent (the same startup warnings test).

**NuGet packages (US12)**

- **FR-089**: One package service (`NetPrints.Packages`) MUST serve the Packages tab, shaped so that P3c's extension
  manager and samples gallery reuse it: sources and credentials from the `nuget.config` hierarchy and the NuGet
  credential plugins, search and versions, a prerelease switch, a 5-minute cache, and an offline fallback to the global
  packages folder. No secret MUST be stored in NetPrints state, and source URLs MUST be redacted in logs. A spike MUST
  first pin the NuGet library version and the credential plugin hooks.
- **FR-090**: Package changes MUST be project edits (`AddPackageReference`, `SetPackageVersion`,
  `RemovePackageReference`) aware of Central Package Management, `VersionOverride`, version ranges and conditional items,
  preserving comments and order; `NetPrints.Sdk` can be updated but not removed. A reference the edits cannot change
  safely (a version from an MSBuild property, a condition the change cannot target) MUST be shown read-only with the
  reason.
- **FR-091**: Applying a change MUST write the files, restore through the project load, and on failure or cancellation
  put the project and props files back byte-identical; on success the reflection provider is refreshed and the graphs
  re-validated.
- **FR-092**: Removing a package MUST first list the nodes that use it; if the user proceeds they stay as unresolved
  nodes.
- **FR-093**: The Packages tab MUST have Installed, Browse and Updates tabs, a source drop-down, a version picker with a
  compatibility filter, deprecated and vulnerable badges, and transitive packages shown read-only.

**Extension manager (US13), samples from remote sources (US14): moved to P3c**

These IDs moved to roadmap phase P3c on 2026-10-16 and are not reused in P3.

- **FR-094**: Moved to P3c (the Extensions dialog).
- **FR-095**: Moved to P3c (installing extension packages into the per-user folder; the package format itself stays in
  P3, FR-080).
- **FR-096**: Moved to P3c (install integrity checks).
- **FR-097**: Moved to P3c (the install trust prompt and the trust revoke list).
- **FR-098**: Moved to P3c (enable, disable and uninstall after a restart).
- **FR-099**: Moved to P3c (the sample package).
- **FR-100**: Moved to P3c (sample sources).
- **FR-101**: Moved to P3c (the sample manifest).
- **FR-102**: Moved to P3c (index and image safety).
- **FR-103**: Moved to P3c (the samples gallery).
- **FR-104**: Moved to P3c (the sample install prompt and open).
- **FR-105**: Moved to P3c (samples shipped by extensions).
- **FR-106**: Moved to P3c (no bundled sample).

**New projects from the command line (US15)**

- **FR-107**: `netprints new` MUST list and create projects from the project template registry (built-in templates and
  those of per-user extensions), with `--name`, `--output` and `--list`; an unknown template exits 2.
- **FR-108**: A `NetPrints.Templates` package MUST provide `dotnet new` templates `netprints-console`,
  `netprints-library` and `netprints-extension`, generated from the same files as the registry's templates; a test MUST
  compare their output with the editor's New project.

**Tail: inspector sections, value editors, graph rules (US16)**

- **FR-109**: Extensions MUST be able to contribute inspector sections for variables, methods and classes, and editors
  for pin and default values (R6), through the value-editor contract of ADR-0036 shared by pins, variable and parameter
  defaults and grid rows; the built-in inspectors MUST register the same way. All of it is experimental.
- **FR-110**: Extensions MUST be able to contribute graph rules (N2, ADR-0032) whose diagnostics appear in Errors and in
  `netprints build` and `generate` with the same codes.
- **FR-111**: Node translators MUST be able to add usings and helper members through the translation context (N5).
- **FR-112**: Built-in fixed node kinds MUST be declared by convention (S7: inferred document, translator and
  `On<Pin>` dispatch, generated registration) with their search entries as generated metadata (SUG5, ADR-0026). The
  model-driven grid with contributed editors (ADR-0036 c) is P3b's.

**Tail: themes from users and extensions (US17)**

- **FR-113**: User theme files (`*.nptheme.json` in the per-user themes folder, ADR-0033) MUST follow ADR-0035 decision
  8 (schema version, id, label, base, a sparse token map, alias table, size cap; no URLs, fonts or images) with Reload
  themes and Duplicate theme; contrast failures warn and apply.
- **FR-114**: A theme MAY set Semi resources; each key MUST be checked against the resources loaded at runtime (unknown
  key: warning) and each value parsed by the type of the resource it replaces; NetPrints MUST NOT keep a list of Semi
  keys, and the theming guide links to Semi's own documentation.
- **FR-115**: Themes listed in a loaded extension's manifest `themes` field MUST be offered on the Appearance page under
  the extension's name, with the same validation; a theme never runs code or loads XAML. Distributing them through the
  extension manager is P3c's.

**Docs, release and CI (all stories)**

- **FR-116**: The docs MUST be updated: an extension authoring guide built on the packages and the kit, the settings
  and keyboard shortcuts guide, the theming guide (with user and extension themes), the run profiles guide, the NuGet
  packages guide, the CLI reference for the new commands, the API reference of the published packages, the ADRs the
  plan lists, and release notes for the 0.4.0 hard break (the extension manager and samples guides moved to P3c).
- **FR-117**: The trimming ratchet (`eng/trim-warnings.txt`) MUST NOT go up; the node creation service removes the two
  warnings of reflection-based node creation.
- **FR-118**: New Desktop E2E scenarios MUST follow ADR-0006 (one class per scenario, page objects, no sleeps) and run
  in the existing UI shards; tests MUST go in the existing test projects unless the plan justifies a new one (the kit's
  own tests excepted).

### Key Entities

- **Extension**: an assembly plus `netprints-extension.json` (id, name, version, assembly, `netprintsApi`, `dependsOn`,
  `themes`), loaded from a trusted project or a user extension directory (`netprints.extensionPaths`,
  `NETPRINTS_EXTENSION_PATH`); the per-user folder the extension manager installs into comes with P3c.
- **Extension package**: a `.nupkg` of type `NetPrintsExtension` holding an extension folder, packed from the extension
  template and read by `netprints extension verify`; installing it per user by id and version comes with P3c.
- **Contribution**: a descriptor of one of the public kinds (FR-021) with an id, a label key, an owner and an optional
  `AppliesTo` predicate.
- **Contribution group**: the app group or a project's session group; removed as a whole with its project.
- **Settings section**: a typed options class with a key in `settings.json`, an exported schema and a validator; owned
  by the editor or an extension.
- **Settings page**: a contributed page generated from a section's schema or drawn by a custom view.
- **Key binding override**: a command id, a scope and gestures added or removed by the user, stored in the `keyboard`
  section.
- **Run profile**: a named entry of `launchSettings.json` with a kind (`Project`, `Executable` or an unsupported kind),
  executable path, arguments, working directory and environment.
- **Run options**: per user and per project: the selected profile, configuration, external terminal and output capture.
- **Theme preset**: an embedded, named set of token values for the Dark or Light variant; **theme file**: a user or
  extension `*.nptheme.json` over a base.
- **C# target**: a project's language version (as specified and as resolved), target frameworks and capabilities.
- **Conformance check**: one of the kit's checks, with a rule id, a message and a fixture that fails it.
- **Verify report**: the result of `netprints extension verify`: checks run, failures and missing API members.
- **Pin role**: the role of a node pin (Result, OutParam, Exception, Target, …) that purity and capabilities use.
- **Package source**: a `nuget.config` feed with its credentials kept by NuGet, never by NetPrints (per-user sample
  index sources are P3c's).
- **Package reference**: a declared `PackageReference` with its version (in the project, or in
  `Directory.Packages.props` under Central Package Management, or a `VersionOverride`), editable or read-only with the
  reason; **package change**: an add, update or remove applied as one unit and rolled back byte-identical.
- Moved to P3c on 2026-10-16: **sample** and **sample index**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 0 NPS, NPA and NPH violations in `src/` at the end of the phase; every analyzer rule has at least one
  passing and one failing test, and every generator has a snapshot test.
- **SC-002**: 100% of the fixture and sample graphs load and save byte-identical across the pin renames; the generated
  C# goldens are byte-identical except the changes listed for `out` and `ref` pins; node search matches 100% of the P3a
  characterization goldens apart from the one recorded order change.
- **SC-003**: The sample Godot extension, built only against the locally packed packages, loads in the editor and
  passes every conformance check of the kit (the roadmap's 12 and the checks FR-077 adds); package validation and the
  public API checks pass for every packable project; 100% of public members of the published packages have XML docs.
- **SC-004**: An extension contributing one item of every public contribution kind shows each item on its surface, and
  after its project unloads 0 of its items remain (automated test over every kind); 100% of registration and handler
  failures in the failure fixtures leave the editor usable and produce a diagnostic naming the extension.
- **SC-005**: Shell start (M1) and opening a graph (M2) are at most 10% slower than their R5-0 baselines, measured in
  the perf job.
- **SC-006**: 100% of user settings are reachable from a settings page; changed settings and key bindings equal their
  values after a restart in automated tests; an unreadable settings file starts the editor with defaults in 100% of
  tests and keeps the copy.
- **SC-007**: A keyboard-only E2E scenario rebinds a built-in command and an extension command, resolves a conflict,
  restarts and runs both through their new gestures.
- **SC-008**: For each run profile fixture (`Project`; `Executable` with `%NAME%`, `$(Name)`, relative and absolute
  paths, a bare name on `PATH`), the editor and `netprints run` build the identical process request (program, argument
  list, working directory, environment); the argument round-trip test covers at least 30 cases, including quotes,
  backslashes, spaces and empty arguments.
- **SC-009**: Each built-in preset passes 100% of the contrast gates, measured on the exact ratio; switching presets needs 0 restarts; a
  Dark preset changes 0 Light token values (overlay test).
- **SC-010**: For all 6 C# target workspace fixtures, the editor, `netprints regen` and the SDK generate target write
  byte-identical files.
- **SC-011**: A project created from `dotnet new netprints-extension` builds and passes the kit with 0 edits; each
  conformance check of the kit (the roadmap's 12 and the checks FR-077 adds) has a fixture that fails it with a message
  naming the rule.
- **SC-012**: Each package change in the Central Package Management and plain fixtures matches what `dotnet package
  add` writes for the same change; 100% of forced restore failures and cancellations leave the project and props files
  byte-identical.
- **SC-013**: Moved to P3c on 2026-10-16 (the install rejection table).
- **SC-014**: Moved to P3c on 2026-10-16 (no bundled sample; a sample installed from a folder source opens and runs).
- **SC-015**: The Windows CLI leg passes the CRLF catalog check test and the `git-install` path test; 0 unrecorded
  info-level analyzer hits remain in `src/`.
- **SC-016**: No CI job takes longer than 15 minutes, and the trimming warning count is at most its value at the start
  of the phase.
- **SC-017**: The docs site builds with 0 broken links and contains the guides of FR-116 and every ADR the plan names.

## Assumptions

- Commands, menu names, settings keys, file names, package names, diagnostic prefixes and public type names named in
  this spec are product surface for users or extension authors, not implementation choices.
- Extensions still load into non-collectible load contexts; hot enable and disable, collectible contexts and the unload
  test come later (ADR-0031's later step).
- Only Linux runs the editor in CI (constitution I); the Windows job covers the command-line tool. Windows and macOS
  editor behaviour (the session log on Windows, the two-DPI check, Cmd mapping, external terminals) is checked by hand
  and listed for the owner.
- No tag, release, NuGet push, wiki edit or external submission happens in this phase's PR; packages are packed and
  validated locally and in CI, and the release (v0.4.0) needs the owner's approval.
- The schema stays v1 and additive until the version cut (roadmap), so document changes in P3 need no migrations of
  NetPrints' own documents.
- The UI text stays English; P3 builds the localization infrastructure (L1) only.
- The P3a performance and UI-test infrastructure (the perf job, the four UI shards, the E2E diagnostics) is reused as
  it is.
- The roadmap's "anything functional left beyond P0 parity" has no known item: P0 merged at parity with its 60-item
  inventory, and P3a replaced the old windows. A parity gap found during P3 is recorded as a follow-up, not added to
  P3's scope.
- Boundary calls of the P3c split (2026-10-16, revised the same day: the package service, NuGet packages and the tail
  stay in P3). The extension manager and the remote samples moved out; each remaining story keeps the minimum it needs
  from the host:
  - **Per-user extensions (Clarifications answer 2, FR-032, US4, US15)**: the app group loads, at editor start,
    the extensions in the configured extension directories (`netprints.extensionPaths`, `NETPRINTS_EXTENSION_PATH`),
    which the editor already reads; the Settings page for extension directories (FR-044) is how a user adds one. The
    managed per-user folder `<data>/extensions/<id>/<version>/`, its enabled or disabled state and the "Incompatible"
    display are P3c's.
  - **Published packages (US3)**: the packages are packed locally and consumed through `dotnet restore` like any NuGet
    package; US3 does not use the `NetPrints.Packages` service.
  - **Package service (US12, FR-089)**: P3 builds it for the Packages tab (sources, credentials, search, versions,
    metadata, cache, offline fallback, installed and transitive packages). P3c's extension manager and samples gallery
    reuse it and add what only they need: searches by package type, download, verify and extract into the per-user
    folders, and the sample index sources. The spike on whether feeds accept the custom package types stays in P3c.
  - **Author kit (US10)**: the extension package format (a `NetPrintsExtension` `.nupkg` holding the loader's folder)
    stays in P3, because the template packs it and `netprints extension verify` reads it (FR-079, FR-080). Installing
    it, and the spike on whether remote feeds accept the custom package type, are P3c's; if that spike changes the
    format, P3c changes the template and `verify` with it.
  - **Sample Godot extension (FR-037)**: it ships everything except its sample under `samples/<id>/`, which needs the
    sample manifest and extension samples (FR-101, FR-105) and moved to P3c.
  - **`--profile` (Clarifications answer 5, FR-036)**: it filters New project and the template list; P3a's bundled
    samples carry no profile, so the samples gallery filter comes with the sample manifest's `profile` in P3c.
  - **Start page samples**: P3a's samples tile and its bundled HelloWorld sample stay as they are until P3c replaces
    them (FR-106).
  - **Extension themes (US17, ADR-0035 C)**: P3 offers the themes of loaded extensions on the Appearance page
    (FR-115); their distribution through the extension manager's Browse tab is P3c's.
  - **Trust**: P3 keeps the per-project trust prompt and the trusted projects setting (FR-044); the per-publisher install
    trust and its revoke list are P3c's (FR-097).
  - **Restart to apply**: the bar stays in P3 for settings that need a restart (FR-042); P3c reuses it for extension
    changes.

## Out of Scope

- **PR #19** (the owner's visual feedback on the merged P3a shell: wire follow, redock duplicates, document tabs, the
  compact Variables panel, the theme palette) is its own follow-up PR, not part of P3.
- **P3b**: the declaration kinds registry and the SmartEnum kind example; member defaults (ADR-0036 a), editor metadata
  (b), and the model-driven grid (c); user attributes and the plugin attribute
  contracts (ADR-0039); declaration-only partial members; emission styles with requirements, NPT010, NPT011, the target
  probes, the CLI target line and the style matrix (ADR-0038's P3b slice); re-enabling Const; parameter modifiers.
  The tail that moved to P3b earlier on 2026-10-16 is back in P3 (Clarifications); only the model-driven grid stays
  here.
- **P6**: moving existing strings into resources (L2) and the first translation (L3); node clipboard, nudge,
  Tab-to-search, the undo history list, the Self node, keyboard access to every connection (GR-1), mouse back and
  forward, "show all members", the custom title bar, the Publish dialog, live instance inspection.
- **P3c** (split from P3 on 2026-10-16): the extension manager and its per-user folder, samples from remote sources
  (User Stories 13 and 14, FR-094 to FR-106, SC-013 and SC-014), the sample Godot extension's own sample, and the
  samples gallery's `--profile` filter, all on P3's package service.
- **P7** structured code generation; **P8** performance work, ReadyToRun publishing and the shared warmed reflection
  provider; **P4** VSIX; **P5** VS Code and browser build.
- **U1 to U3**: the UnrealSharp template and profiles, the Unreal flags section, struct editors, and a public
  `AddRunProfileKind` (added when U3 or NetPrintsUnity needs it, ADR-0037 decision 4).
- **Later**: hot enable and disable of extensions, extension seams for core services, project-scope and "dev" extension
  folders, theme-only packages without an assembly, a hosted theme gallery, the nightly job co-loading published
  extensions (once two published extensions exist), dual-type sample and template packages for `dotnet new install`,
  `netprints check` for the C# target, `ICSharpTargetProvider` (NetPrintsUnity), a per-user run environment, detecting
  external file changes, autosaving original files, the multi-project workspace, the Velopack update notice, the logo
  redesign, and the `netprints package` and `netprints extension` verbs other than `verify` (v1.1, after P3c).

## Planning Notes

### Process (coordinator defaults, 2026-10-15; confirmed by the owner on 2026-10-16)

- One spec, one branch, one PR, with sub-phases and checkpoints as in P3a.
- Core first: the analyzer and generator infrastructure and the public contribution API with the extension host land
  before the features built on them (settings pages, user shortcuts, run profiles, theme presets, the conformance
  kit).
- The P3a execution process applies: implementation batches of 2 to 4 tasks run one after another; each sub-phase
  checkpoint gets an adversarial review and a fix batch; the PR gets a light final review; tests go in the existing
  test projects. Since 2026-10-16 the reviews run at five checkpoints (A+B, C1+C2, D1+D2, E+F with the tail G, and
  the final review, which also reviews the NuGet sub-phase H), implementers push and return without waiting for CI,
  and the coordinator watches the PR's CI (plan "Process").
- PR #19 is not part of P3.

### Sub-phases (outline for the plan)

The plan refines this outline into ten sub-phases: C splits into C1 (public contracts) and C2 (facets and the publish
gate), D into D1 (the host in the editor) and D2 (the author kit and the Godot sample), F also holds the editor half of
the C# target (US9), and the NuGet packages (US12) are sub-phase H. plan.md's sub-phase table is the one tasks.md
follows.

- **A, foundation**: S1 (analyzer and generator projects, NPS rules, DI attributes and registration generator,
  `[SmartEnum]` and `CommandKey`, `CommandGesture` parsing, options validators); R5-0 baselines and the M5 spike;
  unknown document members preserved; the P2 code carry-overs (FR-082 to FR-087). Checkpoint A.
- **B, node model and search (Core 0.4.0)**: SUG1; SUG2 and SUG3; S2a, S2b, S4, S3; R2; pin roles, `out` and `ref`
  pins and purity; `PrimitiveKinds`. Checkpoint B.
- **C, public contracts and the publish gate**: R7; SUG4 with L1; the C# target types (US9); R5a and R5b with the app,
  shell and session composition steps; N1 and N8 with per-extension services; R4; N3 and N4; the status-bar kind;
  the manifest `themes` field; API tracking and package validation; then S5 and S6. Checkpoint C (publish gate).
- **D, extension host in the editor**: contribution groups and failure isolation; `--profile`; settings pages and
  S10; user shortcuts; theme presets (B0); the P3a editor items (FR-088); the kit, template, `verify` and author
  workflow; the sample Godot extension. Checkpoint D.
- **E, run and build**: run profiles in the editor and CLI; the configuration selector; console input and external
  terminal. Checkpoint E.
- **F, command-line templates**: `netprints new` and the `dotnet new` templates (US15). The extension manager and the
  remote samples that F held moved to P3c on 2026-10-16; the package service and the Packages tab are sub-phase H.
  Checkpoint F has no scope rule any more.
- **G, tail**: R6 and the value-editor contract; N2; N5; S7 and SUG5; B1 and C. Checkpoint G; reviewed with E and F.
- **H, NuGet packages (US12)**: the NuGet spike; the package service; package reference edits; apply, restore and
  roll back; removal impact; the Packages tab. Checkpoint H; reviewed at the final checkpoint.
- **Release preparation** (held by G before 2026-10-16): regenerated guide images, the 0.4.0 release notes, the
  quickstart run, the CI budget and docs checks; it runs just before the final review.

### ADRs

- Written with this spec: [ADR-0040](../../docs/adr/0040-editor-settings-pages-and-key-bindings.md) (settings
  pages, typed sections and user key bindings; amends ADR-0031 decision 7).
- For the plan and the sub-phases (reserved numbers): 0024 analyzer and generator infrastructure, 0025 declarative node
  pins, 0026 node kinds by convention, 0027 localization, 0028 node search providers and creation, 0029 node
  capabilities, 0030 the Extensibility split and the editor facet, 0032 graph rules. Amendments: ADR-0003 (hygiene rules
  as analyzers), ADR-0007 (E7 stays a test), ADR-0010 (conformance checks for providers, rules and contributions),
  ADR-0020 (typed contexts, contribution groups, `AppliesTo`, new kinds, label keys, menu ids, factories through DI),
  ADR-0021 (extension icon ids), ADR-0023 (Semi and Ursa locale), ADR-0026 (`PrepareFromSearchAsync`, offers from typed
  pins), ADR-0035 (B1 and C in P3, C's distribution in P3c). ADR-0026 and ADR-0032 are written in sub-phase G. The
  plan adds one new ADR, 0042 (the NuGet package service and package reference edits), written in sub-phase H.

### Decisions taken while specifying

Each was decided autonomously from the roadmap and the ADRs; the reason follows.

- The sample non-Unreal extension is a Godot extension (FR-037): the roadmap already gives the sample extension the
  Godot template and its `Executable` profiles, and Godot needs no core code (ADR-0037). The SmartEnum kind example
  needs P3b's declaration kinds, so it joins the sample extension in P3b.
- The roadmap's `netprints-verify` tool is the `netprints extension verify` command (FR-080): one CLI, ADR-0015 exit
  codes, and the extension manager (P3c) can show its report later. The nightly co-loading job waits for published
  extensions to co-load.
- Loading a project never writes generated files (FR-085, FU-6): P3a made opening a project side-effect free (backups
  outside the project, never building on open), and the CLI already skips generation.
- `.cmd` and `.bat` targets are refused rather than warned about (FR-056): `cmd.exe` re-parses the arguments, which
  breaks the exact argument list of FR-054 and opens argument injection.
- The stop timeout is a setting with a 5-second default (FR-059), so engines that save on exit can be given longer.
- Run options other than the profile itself (profile choice, configuration, external terminal, output capture) are per
  user and never written to `launchSettings.json` (FR-053), which stays a file shared with other tools.
- MSBuild variables are scrubbed only when NetPrints' own locator set them (FR-057): user-set values are intentional.
- One `NetPrints.Templates` package carries the `dotnet new` templates for projects and extensions (FR-108), generated
  from the registry's files, so the editor, `netprints new` and `dotnet new` cannot drift.
- Command ids follow the P3a table's `netprints.command.<camelCase>` style: `openSettings`, `keyboardShortcuts`,
  `selectRunProfile`, `editRunProfiles`, `manageExtensions` (the roadmap's `run.selectProfile`, `run.editProfiles` and
  `extensions.manage`; `manageExtensions` ships with P3c).
- Key binding precedence puts built-in defaults before extension defaults (FR-050), matching ADR-0020's "built-ins
  register first".

### Sizing

The item sizes in the roadmap's P3 section added up to far more than the phases table's "~2.5 w". The extension
manager and the remote samples, about 12 days, moved to P3c on 2026-10-16. What remains still exceeds it: the
attribute-driven core path is about 25 units, run profiles about 7 days and the package service with the Packages tab
about 11 days, before the SOLID shape work, themes, the C# target, the kit and the tail. The plan re-estimates P3 per
sub-phase and proposes the phases table update, which the coordinator makes with the owner's OK. Plan units are human
half-days and overstate calendar time by about 5 times; the roadmap states P3 as about 5 weeks in agent time (about 4
before the tail and the NuGet packages came back).
