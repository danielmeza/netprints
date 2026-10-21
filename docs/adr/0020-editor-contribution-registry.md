# 0020: One contribution registry drives the editor's commands, panels and menus

## Status

Accepted (2026-10-01, P3a spec `specs/005-editor-shell/`, research R5) and implemented in P3a. Amended 2026-10-01 after batches
B1 to B4; see "Changes made in implementation". Amended 2026-10-06 by ADR-0021: descriptors name icons by icon id (`IconId`), not
by icon-kind name. The registry is also the source of the shortcut table in [the commands guide](../guide/editor/commands-and-shortcuts.md),
which a test compares with the registry.

## Context

The editor's actions today are view-model commands that each window binds by hand. The class window binds three
shortcuts (Delete, Ctrl+Z and Ctrl+Y), the round command buttons are hand-placed, and there is no menu (UX audit H3
and H4). P3a adds a menu bar, a command bar, a command palette, a shortcuts sheet, go-to-anything, context menus,
connection tooltips, a start page with tiles and project templates. If each of these lists actions on its own, they
drift apart. P3 must also let extensions contribute commands, panels, inspector sections and settings pages, and the
roadmap requires the built-in editor to use the same points first (owner, 2026-09-25).

## Decision

- **One registry** (`IContributionRegistry`, namespace `NetPrints.Editor.Contributions`) accepts seven kinds:
  commands, panels, dashboard tiles, project templates, context-menu items, tooltip providers and go-to providers.
  Every built-in contribution is registered through it at composition time by feature modules
  (`FileContributions`, `EditContributions`, `BuildContributions`, `NavigationContributions`, …). The registry is
  frozen once the shell starts; P3a has no dynamic add or remove. (P3 replaces this with groups; see Amendment 2.)
- **Descriptors are UI-toolkit free** (constitution II): ids and labels are strings, icons are icon-kind names,
  shortcuts are gesture strings (`"Ctrl+Shift+B"`), and menu locations are a path, a group and an order. Execution
  goes through a handler that receives a `CommandContext`: the shell services, the active document and the
  selection. The handler returns `CanExecute` and `ExecuteAsync`. Only the view layer turns these into Avalonia
  `KeyBinding`s, `MenuItem`s and icons. That lets P3 move the descriptors into a public package without changing
  their shape. `CommandContext` is the exception (Review B R16): it carries the concrete `ProjectSessionViewModel`
  and `NodeGraphViewModel`, which belong to the editor, not to a public surface. P3 replaces them with interfaces
  (`IProjectSession`, a graph-view abstraction) before `ICommandHandler` is published; P3a keeps the concrete types
  because no extension can implement a handler yet.
- **Every surface is generated from the registry:**
  - the menu bar, the command bar, the command palette and the keyboard shortcuts sheet;
  - the shell's key bindings, through two custom behaviors that materialize the registered gestures:
    `CommandKeyBindingsBehavior` (window `KeyBindings` for `Global`) and `ScopedCommandKeysBehavior` (a tunnel
    handler for `Graph` and `ProjectTree`) (ADR-0007 D11, option 5);
  - the context menus of nodes, pins, connections, tree items and the canvas;
  - connection and pin tooltips;
  - go-to-anything results;
  - the start page tiles and the "New project" template list.
  No view lists actions of its own.
- **Shortcut scopes.** A command declares a scope: `Global` (shell-wide), `Graph` (the canvas has focus) or
  `ProjectTree`; a command may name several (`CommandScope` is a flags enum, and `Global`, which is 0, overlaps every
  scope). Single-key and Shift-only gestures (`F`, `Home`, `Delete`, `Shift+A`) are allowed only in non-global scopes;
  function keys F1 to F24 are exempt by design, so F5, F7 and Shift+F5 are `Global` and fire in text fields too. A
  function-key command that acts on a selection (F2 rename) must therefore be scoped. A focused
  text input always gets its own editing keys first. The canvas-scope bindings reach Nodify through a tunnel-routed
  behavior, so Nodify's own handling cannot swallow them (`avalonia-behaviors` D9).
- **Ids and conflicts.** Ids are namespaced (`netprints.command.save`, `netprints.panel.errors`). A duplicate id, or
  two commands with the same gesture in the same scope, is reported (logged, and listed in
  `IContributionRegistry.Issues`), and the first registration wins, the rule extensions already follow for catalog
  ids (ADR-0010). P3 maps these issues to NPX diagnostics when extensions contribute.
- **Owner.** Each contribution records its owner (`netprints` for built-ins, the extension id in P3), so P3 can show
  where an item came from and unload an extension's contributions as a group.

## Consequences

- One test can compare the registry with every surface (spec SC-003). A command that appears in a menu but not in
  the palette, or that has no test, is visible in review.
- Adding a built-in action means adding one descriptor and one unit-tested handler, not editing several views.
- P3 opens the registry to extensions, adds settings pages and inspector sections as new kinds, and adds
  user-defined shortcuts on top of the declared defaults. The P3a descriptors must not need a breaking change for
  that, which is why they avoid UI types.
- The generated key bindings replace the hand-written `KeyBinding`s and the global key handler in
  `GraphEditorView` (Ctrl+Space). That code-behind shrinks to gesture and viewport mechanics.

## Changes made in implementation

Recorded together with their tests (contracts/contributions.md carries the same members):

- `CommandScope` is `[Flags]` (`Global = 0`, `Graph = 1`, `ProjectTree = 2`): delete and rename act in the canvas
  and the tree. A multi-scope command overlaps every scope it names.
- Function keys F1 to F24 may be single-key `Global` gestures (`CommandGesture.IsFunctionKey`); a Shift-only gesture
  on any other key counts as single-key for that rule (`CommandGesture.IsPlain`).
- Key names come from a UI-free table in `CommandGesture` (letters, digits, F1 to F24, named keys; `Esc`, `Del`,
  `Ins`, `Return`, `PgUp`, `PgDn` and `Backspace` are aliases, normalised before conflict detection). An unknown key
  is an unparseable gesture. The view layer maps `Ctrl` to Command (Meta) on macOS.
- `InvalidDescriptor` also covers a missing handler or factory, a blank title, display name or profile id, and an
  invalid `CommandId` in a context-menu item. The labels of the four add-member commands became rows of
  contracts/commands.md.
- `IProjectActions`, reached through `IShell.ProjectActions`, holds the project flows, and
  `ConfirmUnloadAsync` has two callers: `UnloadingCommandHandler` for open, new and close, and the window-close
  path for exit and the OS close. `exit` only closes the window, so each path prompts once (Review B R6).
- Handlers have no `CanExecuteChanged`. The enabled state is read at invocation. Visible surfaces re-query on one
  combined notification, `ICommandContextProvider.CommandStatesChanged`, raised when the session, its build and run
  state, the project's compile state, an undo history, the graph selection or the active graph or document changes
  (Review B R4). Run and Compile are also disabled while the session builds (`IsBuilding`).
- `CommandContext` also carries the active graph view model (`ActiveGraph`), and `ICommandContextProvider` builds it
  per invocation.
- The class editor window gets its key bindings from the registry (`CommandInvoker` with the two behaviors above)
  instead of its hand-written Delete, Ctrl+Z and Ctrl+Y bindings, and `GraphEditorView`'s Ctrl+Space handler is the
  `nodeSearch` command. Graph gestures act only while the canvas has focus, and opening a graph focuses its canvas
  (`FocusOnDataContextBehavior` in `GraphEditorView`, Review B R5).

## Amendment 1: the contract an extension sees (P3, sub-phases C1 and C2)

The registry's descriptors became the public editor contract of `NetPrints.Extensibility.Abstractions` (NPXE0005,
[ADR-0030](0030-extensibility-split-and-editor-facet.md)). What changed for authors:

- **Typed contexts.** A handler or factory receives the context of its own surface (`ICommandContext`, `IPanelContext`,
  `IStartPageContext`, `IStatusBarContext`, `ISettingsPageContext`, `IDialogContext`), each with the `IServiceProvider` of
  the extension's own services and the session or shell members that surface needs, instead of one shared `CommandContext`.
  `IProjectSession`, `IGraphEditor` and `IEditorShell` are interfaces; the editor's view models implement them and no extension
  sees a view model type.
- **Groups and order.** A menu entry and a context-menu entry carry a `Group` and an `Order`; entries of one group sit together
  and a separator divides groups. `MenuPlacement` names the path and an optional submenu.
- **`AppliesTo`.** Context-menu items and status bar items take a predicate on their typed context, so one contribution shows
  only where it makes sense.
- **More kinds.** Status bar items, dialogs, tooltip providers, go-to providers, settings pages (a page without a view model is
  generated from its section's schema) and view resources join commands, panels, start page tiles and context-menu items.
  Project templates (with files) and node-data migrations go through `IExtensionBuilder`, because the builder is also what the
  CLI and the generator see.
- **Label keys and menu ids.** Labels are `LocalizedText`: a key plus the invariant text. When an author gives no key,
  `ConventionKeys` derives one from the id (`Command.<id>.Label`, `Panel.<id>.Title`, `Menu.<path>`), so a localisation file can
  override any label and an extension ships its own resources through `AddStringResources`.
- **Factories through DI.** A panel, dialog, tile, status bar item or settings page is created by a factory that receives its
  typed context; the extension resolves its view model from its own `IServiceProvider` (`IExtensionBuilder.Services`, NPXE0008)
  instead of constructing it, so a view model's dependencies are the extension's, not the editor's.
- **Invalid descriptors.** A duplicate id keeps the first and an invalid descriptor is reported and ignored; neither stops
  the editor or the other contributions. The builder's own contributions (templates, migrations) must also carry the
  manifest prefix (NPX013).

## Amendment 2: groups (P3, sub-phase D1)

The registry keeps an app group (the built-ins, then the per-user extensions in load order) and one session group per open
project. `Freeze()` ends the initial registration of the app group; after it, adding or removing a session group raises
`Changed` with a new immutable snapshot, and every surface rebuilds from it (research R14). A session group can shadow an
app-group owner: when a project declares its own copy of an extension the user directories already loaded, the app group's
items of that id are hidden while the project is open, so the project's copy serves the session (spec Clarifications,
T112 DD-6).

Known limit: Avalonia resolves every `avares://` asset (images, fonts) by assembly name and returns the first assembly of
that name it saw. While a per-user copy with the same assembly name is loaded, a project copy's assets therefore resolve
to the first-loaded copy, while its documents (templates, styles, icons) and its view models are the project copy's. The
editor does not replace Avalonia's global asset loader. When a project copy shadows a per-user copy whose version or
content hash differs, the editor logs one warning (event 1046) that names both copies and says the assets come from the
per-user copy.
