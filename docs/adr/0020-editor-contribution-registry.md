# 0020: One contribution registry drives the editor's commands, panels and menus

## Status

Accepted (2026-10-01, P3a spec `specs/005-editor-shell/`, research R5).

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
  frozen once the shell starts; P3a has no dynamic add or remove.
- **Descriptors are UI-toolkit free** (constitution II): ids and labels are strings, icons are icon-kind names,
  shortcuts are gesture strings (`"Ctrl+Shift+B"`), and menu locations are a path, a group and an order. Execution
  goes through a handler that receives a `CommandContext`: the shell services, the active document and the
  selection. The handler returns `CanExecute` and `ExecuteAsync`. Only the view layer turns these into Avalonia
  `KeyBinding`s, `MenuItem`s and icons. That lets P3 move the descriptors into a public package without changing
  their shape.
- **Every surface is generated from the registry:**
  - the menu bar, the command bar, the command palette and the keyboard shortcuts sheet;
  - the shell's key bindings, through one custom behavior that materializes the registered gestures (ADR-0007
    D11, option 5);
  - the context menus of nodes, pins, connections, tree items and the canvas;
  - connection and pin tooltips;
  - go-to-anything results;
  - the start page tiles and the "New project" template list.
  No view lists actions of its own.
- **Shortcut scopes.** A command declares a scope: `Global` (shell-wide), `Graph` (the canvas has focus) or
  `ProjectTree`. Single-key gestures (`F`, `Home`, `Delete`, `F2`) are allowed only in non-global scopes. A focused
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
