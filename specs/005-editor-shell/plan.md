# Implementation Plan: Editor Shell

**Branch**: `005-editor-shell` | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-editor-shell/spec.md` (roadmap phase P3a).

## Summary

P3a replaces the launcher and the per-class windows with one editor shell, and makes the editor's actions,
lifecycle and look consistent.

- **Shell.** A `ShellWindow` hosts:
  - a project tree;
  - tabbed graph documents with breadcrumbs;
  - an inspector;
  - a bottom panel with Errors, Output and C#;
  - a menu bar, a command bar of at most 40 px, and a status bar.
  The layout docks with Dock.Avalonia 12.1.0.6 behind an `IShell` seam. A spike gates the library, and a plain
  Avalonia layout is the fallback (ADR-0018).
- **Commands.** Every action is a command in one contribution registry: commands, panels, dashboard tiles, project
  templates, context-menu items, tooltip providers and go-to providers. The registry generates the menus, the
  command bar, the key bindings, the command palette and the shortcuts sheet (ADR-0020). It is the point that P3
  opens to extensions.
- **Lifecycle.**
  - Unsaved state is tracked per file, with a saved marker on the undo stack.
  - One unload prompt covers the five ways a project is unloaded.
  - Backups go to the per-user data folder, with crash recovery.
- **Start page and persistence.**
  - A start page replaces the launcher: recent projects, templates, samples and what's new.
  - Per-user state restores the window, the layout, the tabs and each graph's viewport.
- **Navigation.** A command palette, go-to-anything, connection jumps with a back and forward history, connection
  tooltips and breadcrumbs.
- **Event graphs.** Event graphs can be renamed, and custom event entries gain an inspector with arguments.
- **Visual system.** Type, spacing and colour tokens in Dark and Light, including Nodify theme overrides.
- **Type-scoped search** now respects the catalog that covers a type's assembly (research R10).
- **XAML.** A new enforced rule, E7, holds the remaining code-behind to justified gestures.
- **First, CI.** Sub-phase A makes CI diagnosable and faster: Desktop E2E failure diagnostics, a per-test-project
  matrix with a stable aggregate check, and a Windows CLI workflow with a UTF-8 `show --textconv` test
  (ADR-0019).
- **Decisions:** ADR-0018, ADR-0019, ADR-0020, and research R1–R16.

## Technical Context

**Language/Version**: C# (latest), .NET 10 SDK (`global.json`, `rollForward: latestFeature`), `net10.0` everywhere.

**Primary Dependencies**:
- Existing: Avalonia 12.1.3 (Desktop, Fluent, Headless), Nodify.Avalonia 2.0.0, CommunityToolkit.Mvvm 8.4.2,
  Xaml.Behaviors.Interactions(.Custom, .DragAndDrop) 12.0.7, Material.Icons.Avalonia 3.0.2, AvaloniaEdit 12.0.0,
  DynamicData 9.4.33, Microsoft.Extensions.* 10.0.12, System.Text.Json source generation.
- **New:** `Dock.Avalonia`, `Dock.Model.Mvvm` and `Dock.Avalonia.Themes.Fluent`,
  all exactly `[12.1.0.6]` (no Dock serializer package, ADR-0018). They bring in `Dock.Model`, `Dock.Settings` and `Dock.Controls.Recycling.Model`
  transitively. No ReactiveUI and no Newtonsoft.Json (research R2).

**Storage**:
- Graph and project files (unchanged format; schema v1 gains one optional property for custom-event arguments).
- Per-user JSON state and backups under `<ApplicationData>/NetPrints/` (contracts/state-files.md).

**Testing**:
- xUnit v3 (3.2.2) on Microsoft.Testing.Platform.
- View-model tests in `NetPrints.Editor.Tests` and headless wiring tests in `NetPrints.Editor.UITests`
  (`[AvaloniaFact]`).
- Desktop E2E in `NetPrints.Desktop.E2ETests`: Xvfb + openbox, one class per scenario (ADR-0006).
- Repository hygiene (`XamlHygieneTests` E1–E8, `CiWorkflowTests`) in `NetPrints.Core.Tests`.

**Target Platform**: The desktop editor runs on Linux, Windows and macOS. CI runs the editor on Linux only. A
separate Windows workflow runs the CLI tests.

**Project Type**: Desktop application (Avalonia editor library plus desktop host), with CI workflow changes.

**Performance Goals**: None (FR-104; P8 owns performance). The only time target is the CI wall time of SC-006.

**Constraints**: See "Standing constraints" below.

**Scale/Scope**: 10 user stories; about 30 new or replaced views; 3 ADRs. `tasks.md` (batch S2) has 114 tasks in 8
sub-phases, with a review and a reserved fix batch per sub-phase.

## Standing constraints (every batch)

**Code**
- `net10.0` only. Analyzers stay at error (ADR-0003). The hygiene tests forbid: `!` null-forgiving, suppressions
  outside the ADR-0003 ledger, `#nullable disable`, sync-over-async, and `<NoWarn>`. Fix causes, never suppress.
  Never change analyzer packages, severities or `.editorconfig` to get green.
- View model types are named `<Name>ViewModel`, never `<Name>VM` (owner decision 2026-10-01, ADR-0007 amendment;
  enforced by the `NoTypeNameEndsInVM` hygiene test from sub-phase A on).
- XAML over code-behind (ADR-0007, the `avalonia-xaml`, `avalonia-behaviors` and `avalonia-styling` skills):
  - load the skills before touching `.axaml`/`.axaml.cs`;
  - every UI action is a registered command with a unit test (ADR-0020);
  - code-behind only for gestures, viewport math and editor interop, each listed in the E7 allowlist with a reason;
  - colours only as theme tokens via `DynamicResource`;
  - automation ids only from `AutomationIds`.
- View models hold no Avalonia or Dock types (constitution II). Dock types live only in
  `NetPrints.Editor.Shell.Docking`.
- An identifier used in more than one place is a named constant (command ids, panel ids, document id prefixes,
  automation ids, state file names, environment variable names).
- Every public API in `src/` has XML docs. `[LoggerMessage]` logging. `TimeProvider` is injected (debounce,
  backup age, status-message expiry). JSON goes through source-generated contexts only.
- Code comments stay short. Rationale goes to commit messages and `specs/005-editor-shell/implementation-notes.md`
  ("Decision: …").

**Tests**
- Red/green: pin each invariant before changing or removing code. This covers the unload paths, the saved marker,
  the registry rules, the state fallbacks and type-scoped search. Every replaced window's scenarios must be green
  on the shell before the old window is deleted.
- Tests are xUnit v3 and pass `TestContext.Current.CancellationToken`. UI tests go through page objects and
  `AutomationIds`, with no sleeps. E2E tests set `NETPRINTS_STATE_DIR` per lease, so workers never share state.
- **Docs and fixtures.** A guide whose content is generated (the shortcut table) has a staleness test. Golden
  fixtures stay byte-identical unless a task says otherwise.

**Workflow**
- Never leave changes under `samples/`. Never touch `samples/HelloWorld/Compiled_HelloWorld/`. The bundled sample
  excludes it.
- No publishing: no tags, no NuGet push, no wiki edits, no external submissions. One branch (`005-editor-shell`) and
  one PR (draft until Checkpoint H and the final review).
- **Batch sizing** (owner rules): 3–6 units per implementation batch: 2–4 related tasks when they are design-heavy,
  up to 6 when they are light or mechanical. A design-heavy task (the spike, the Dock adapter, the registry, the
  lifecycle services) counts as 2 units. Every sub-phase ends with an Opus review of its whole diff and a reserved
  Sonnet fix batch, green on CI before the next sub-phase. One final, lighter Opus review checks integration before
  merge.
- **Builds and tests per batch:** `dotnet build -v q -tl:off --nologo`. Tests: build first, then
  `dotnet test --no-build --no-progress --no-ansi`. Run the whole suite plus the E2E run once at the end of a batch
  (AGENTS.md). Each batch pushes and waits for its own CI.
- **Governance files.** `.specify/memory/*` is not edited by implementers. Proposals go in implementation-notes.md.
  The roadmap edits in this spec PR were made by the spec batch on the coordinator's instruction; batch S2 applied
  constitution 1.2.4 and updated the roadmap's phases table the same way.

## Constitution Check

*Gate before Phase 0 and re-checked after Phase 1 design: PASS. The one deviation (Complexity Tracking) was resolved
by constitution 1.2.4 in batch S2.*

| Principle | Check | Result |
|---|---|---|
| I. Cross-platform, Linux-first | Every new UI is Avalonia-only. State paths come from `SpecialFolder.ApplicationData`. No Windows APIs. The Unix permission setting is guarded by OS checks. The editor CI stays Linux. The Windows CLI workflow adds coverage and replaces nothing. | Pass (constitution 1.2.4 allows the path-filtered Windows CLI workflow; see Complexity Tracking) |
| II. UI-agnostic core | Core changes are UI-free: the event graph rename and entry arguments. Registry descriptors, `IShell`, the state and lifecycle services are UI-free. Dock types are confined to one adapter namespace. | Pass |
| III. Extension-first | Panels, commands, tiles, templates, context menus, tooltips and go-to providers are contribution kinds that the built-in editor uses (ADR-0020). UnrealSharp's template is contributed by its extension, not hard-coded. | Pass |
| IV. Single target framework | No new projects. Dock packages ship `net10.0`. | Pass |
| V. Tests gate every change | Each story has view-model, headless and E2E obligations (research R13). E2E failures become diagnosable (US1). CI keeps every test project gated through the aggregate check. | Pass |
| VI. Readable, deterministic output | Graph output is unchanged except the optional arguments property, which is canonical and ordered. The state files are versioned (`schemaVersion`) with defined fallbacks. | Pass |
| VII. Abstractions over concretions for I/O | `IEditorStateStore`, `BackupService` (through an I/O abstraction), and `IShell` over Dock. Documents still go through `IDocumentFormat`. | Pass |
| VIII. Simplicity, incremental delivery | One PR. Later-phase items are recorded (research R14). The exact pin, the seam and the fallback bound the new dependency. | Pass |
| Tech constraints | CommunityToolkit.Mvvm (no ReactiveUI through Dock), Avalonia 12.x, System.Text.Json source generation, `[LoggerMessage]`, no Fody. | Pass |

## Project Structure

### Documentation (this feature)

```text
specs/005-editor-shell/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── tasks.md                     # batch S2 (speckit-tasks), then speckit-analyze
├── implementation-notes.md      # created by batch S2; decisions and checkpoint reports
├── checklists/requirements.md
└── contracts/commands.md, contributions.md, shell.md, state-files.md, ci.md
docs/adr/0018-…, 0019-…, 0020-…  (committed with this spec)
```

### Source Code (new = added by P3a, changed = modified, removed = deleted)

```text
src/NetPrints.Editor/
├── Shell/                    # new: ShellWindow.axaml, ShellViewModel, ProjectSessionViewModel, IShell, DocumentId,
│                             #   DocumentViewModel, GraphDocumentViewModel, PanelViewModel, StatusBarViewModel,
│                             #   TitleFormatter, ConfirmUnload flow
├── Shell/Docking/            # new: DockShellAdapter, ShellDockFactory, LayoutSerializer (only Dock references)
├── Contributions/            # new: IContributionRegistry, ContributionRegistry, descriptors, CommandContext,
│                             #   ContributionIssue, BuiltIn/{File,Edit,View,Go,Build,Help}Contributions.cs
├── Commands/                 # new: handlers per menu; CommandPalette/ (view model + view); KeyboardShortcutsSheet/
├── Behaviors/                # changed: + CommandKeyBindingsBehavior, ScopedCommandKeysBehavior (tunnel, canvas)
├── ProjectTree/              # new: ProjectTreePanelViewModel + view (content moved from ClassEditorWindow)
├── Lifecycle/                # new: UnsavedChangesTracker, BackupService, RecoveryService, UnsavedChangesDialog,
│                             #   RecoverDialog
├── State/                    # new: EditorDataPaths, AtomicFileWriter, IEditorStateStore, JsonEditorStateStore,
│                             #   StateJsonContext, RecentProjects, SessionState, WindowStateService, EditorSettings (theme)
├── StartPage/                # new: StartPageViewModel, tiles, NewProjectDialog, ProjectTemplateService, Samples, WhatsNew.md
├── Navigation/               # new: GoToAnythingViewModel, built-in IGoToProviders, NavigationHistory, ConnectionNavigator,
│                             #   ConnectionTooltipProvider, BreadcrumbsViewModel
├── Output/                   # new: OutputPanelViewModel + view
├── Events/                   # changed: EventGraph rename; EventEntryInspectorViewModel, EventArgumentViewModel (new)
├── UndoRedo/UndoRedoStack.cs # changed: MarkSaved, IsAtSavedState
├── Search/                   # changed: scoped-search empty state ("hidden by catalog")
├── Main/                     # removed: MainWindow.*; MainEditorViewModel split into ShellViewModel + command handlers
├── ClassEditor/              # removed: ClassEditorWindow.* (lists → ProjectTree, inspectors → Inspector panel)
├── Hosting/                  # changed: IWindowService (no OpenClassEditor), EditorComposition (registry, shell),
│                             #   ShutdownCoordinator (unload prompt), RunStateTracker (new), automation `runState`
├── EditorStyles.axaml, EditorApp.axaml   # changed: tokens (type, spacing, colours Dark/Light), Nodify ControlThemes
└── AutomationIds.cs          # changed: Shell.*, StartPage.*, Menu.*, CommandBar.*, Palette.*, GoTo.*, …
src/NetPrints.Desktop/        # changed: Program.cs (no-arg → start page, NETPRINTS_STATE_DIR), bundled sample content
src/NetPrints.Core/           # changed: EventGraph rename, EventEntryNode arguments (+ generation of the signature)
src/NetPrints.Serialization/  # changed: arguments mapping (schema v1, optional property)
src/NetPrints.Reflection/Catalogs/CompositeReflectionProvider.cs   # changed: type-scoped routing (R10)
schemas/netpc.v1.schema.json  # changed: optional custom-event arguments
tests/NetPrints.Editor.Tests/         # + Contributions/, Shell/, Lifecycle/, State/, StartPage/, Navigation/, Events/;
                                      #   Reflection/ type-scoped routing tests
tests/NetPrints.Editor.UITests/       # + ShellWiring, RegistrySurface, DockLayoutRoundTrip, ThemeTokens, dialogs
tests/NetPrints.Desktop.E2ETests/     # + Hosting/FailureCapture.cs, E2EDiagnosticsTests; Scenarios/ new classes (R13);
                                      #   MinimizeAndRestoreClassWindow → FloatAndRedockGraph; GuideScreenshotTests
tests/NetPrints.Testing.Ui/           # changed: SmokeScenarios and page objects for the shell
tests/NetPrints.Core.Tests/           # + XamlHygieneTests E7 and E8, CiWorkflowTests
tests/NetPrints.Cli.Tests/            # + ShowTextconvEncodingTests; Windows skips with reasons where needed
.github/workflows/ci.yml (changed), cli-windows.yml (new)
docs/guide/editor/*.md (new, 6 pages), website/static/img/guide/editor/*.png (new), scripts/guide-screenshots.sh (new)
docs/guide/projects.md, docs/contributing/testing.md (new), docs/adr/0007 (amendment note), .claude/skills/avalonia-*/SKILL.md (E7)
Directory.Packages.props            # changed: Dock packages
```

**Structure Decision**: Follow ADR-0001's layout. No new projects: the shell, the registry and the state live in
`NetPrints.Editor` by feature folder. Dock is referenced by `NetPrints.Editor` only. The public extension package
for contributions is P3's decision.

## Implementation sub-phases (drives tasks.md)

| Sub-phase | Stories | Content | Checkpoint |
|---|---|---|---|
| A. Naming, CI and test infrastructure | US1, FR-105 | **first, the mechanical `*VM` → `*ViewModel` rename** (25 types, about 125 `.cs`/`.axaml` files, `x:DataType` and bindings, tests, docs and ADR mentions; no PublicAPI files, the editor is not API-tracked) **plus the `NoTypeNameEndsInVM` hygiene test**, as its own commit before any other P3a code; E2E `FailureCapture` + forced-timeout test + launched-program state through the automation pipe; CI matrix + aggregate + `CiWorkflowTests`; `cli-windows.yml` + `ShowTextconvEncodingTests` + Windows skips; contributing/testing.md | A: SC-006, FR-105 |
| B. Registry and commands core | US4 (core) | registry + validation + issues; `ProjectSessionViewModel` extracted from `MainEditorViewModel`; descriptors and handlers for every existing action (save, compile, run, new **Stop**, undo/redo with dynamic labels, delete, rename, select all, frame, fit); key-binding behaviors (global + tunnel canvas); `UndoRedoStack` saved marker | B: registry tests, handler tests |
| C. Shell | US2 (+ US4 surfaces) | **Dock spike gate** (ADR-0018 five checks, results in notes); `IShell` + Dock adapter (or fallback); `ShellWindow` with menu bar, command bar, status bar; panels: project tree, inspector, Errors, Output, C#; graph documents; project settings document; smoke flows rewritten; `FloatAndRedockGraph`, `ResetLayout`; remove `MainWindow` and `ClassEditorWindow` last | C: SC-001 |
| D. Lifecycle and feedback | US3, US4 (rest) | unsaved tracking per file; title and tab markers; unload prompt on the five paths; backups + pruning + permissions; recovery; undo feedback; Help › Keyboard shortcuts; `KeyboardOnly` E2E | D: SC-002, SC-003, SC-004 |
| E. Start page and persistence | US5, US6 | state store + contracts; recent list; project templates (console, library) + template contribution test; bundled sample; what's new; window, layout and session restore; corrupt/newer state fallbacks | E: SC-005 |
| F. Navigation and event inspector | US7, US8 | palette; go-to-anything + providers; connection navigation + history; tooltip providers; breadcrumbs; core rename and arguments + schema; event-entry inspector; generated signature | F: SC-009, SC-004 (navigation keys) |
| G. Look, search and hygiene | US9, US10, FR-100 | tokens + Light/Dark + theme command; Nodify ControlThemes; `NodeKindBrushConverter` → style classes; E7 + allowlist, E8 (no `FontSize` literals) + ADR-0007 note; remaining code-behind migrated; type-scoped catalog routing + empty state; `ThemeSwitchTests`, `TypeScopedSearchTests`; visual polish (FR-084–FR-089): icon ids on one family (ADR-0021) + `THIRD-PARTY-NOTICES.md`, product mark, node-header roles and glyphs, pin and selection tokens, focus, density and motion tokens, `Font.Mono`, empty-state control, dialog shell, high-DPI snapshots, contact sheet | G: SC-007, SC-008, SC-011 |
| H. Docs and polish | all | six editor guides + `GuideScreenshotTests` + script; projects guide; shortcut-table staleness test; docs build; quickstart run; full suite + E2E; final review | H: SC-010, all SC |

**Order rationale.** A gives every later E2E change diagnostics. B lets C generate menus and bindings from the
registry from the start. C removes the old windows only after the shell carries every flow. D through G are
independent of each other on top of C, and are ordered by story priority. H shoots screenshots once the UI is
stable. **MVP** = A–D (US1–US4).

**Spike outcome handling.** If the spike fails checks 1–3 (ADR-0018), C switches to the fallback adapter. The
coordinator rewrites the Dock-specific text of the remaining tasks (T033, T064) before batch C2 starts, and nothing
outside `Shell/Docking` changes. The outcome is recorded in implementation-notes.md (task T032).

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| A separate Windows workflow (`cli-windows.yml`) besides the Linux-only `CI` | Constitution I requires the tools to work on Windows, and the UTF-8 textconv contract can only fail there | The constitution's development workflow names the VS extension workflow as the "single" Windows exception. A separate, path-filtered workflow that does not replace `CI` is the smallest deviation. The PATCH amendment below was accepted as constitution 1.2.4 (batch S2). Folding it into `CI` would break the Linux-only gate wording more. |
| New dependency family (Dock.*) with an exact pin | Docking, floating, tab groups and layout serialization (US2, US6) | Building them on plain Avalonia is the larger and riskier piece of work. It remains the fallback behind the seam (ADR-0018). |
| `IShell` seam and an adapter over Dock | Keeps Dock out of feature view models, tests and the P3 public API, and makes the fallback cheap | Direct Dock use would couple the extension API to a single-maintainer library |
| New enforced hygiene rule E7 with an allowlist | Makes the roadmap's "code-behind only for justified gestures" checkable | A review checklist is not enforced. The E1–E6 allowlists show the pattern works. |
| Optional property in graph schema v1 (custom-event arguments) | US8 needs arguments, and the owner keeps the graph schema at v1 until the version cut | A schema v2 bump now contradicts the owner decision. The property is optional, and v1 readers ignore unknown properties (System.Text.Json default). An older editor drops the arguments on save, which the release notes state. |

## Governance proposals

- **Constitution PATCH 1.2.3 → 1.2.4: accepted.** Applied in batch S2 on the coordinator's instruction, for the
  owner to confirm on PR #12. Development Workflow: "Other workflows may add Windows or macOS legs that extend `CI`
  for a component, path-filtered (for example the CLI on Windows); they never replace the Linux `CI` gate." Reason:
  ADR-0019.
- **Roadmap.** Edited by the spec batches, on the coordinator's instruction:
  - phases table: P2 merged (PR #9, `3a6eafc`) and released as `v0.2.0`, P3a in progress (spec on PR #12);
  - the P3 carry-over bullet trimmed to the four code items;
  - the P3a section names the three CI items it took and the decisions taken.

## Follow-ups (recorded, not in P3a)

See research R14:
- **P6:** custom title bar; undo history; node clipboard, nudge and Tab-to-search; mouse navigation buttons; more
  context menus; scoped-search "show all" and hidden-member warnings; external file-change detection; visual graph
  diff.
- **P3:** public contribution API; settings pages and user shortcuts; FU-1, FU-2, FU-5 and FU-6.
- **P8:** performance and E2E sharding.
- **Later:** the Velopack update notice; a macOS native menu; localization.
