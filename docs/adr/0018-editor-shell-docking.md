# 0018: The editor shell docks with Dock.Avalonia, behind a shell seam

## Status

Accepted (2026-10-01, P3a spec `specs/005-editor-shell/`, research R2). Spike gate passed on 2026-10-01 (batch C1, T030-T032): all five
checks pass, so the shell is built on Dock.Avalonia 12.1.0.6. Check 2 needed a workaround (below); results and evidence are in
`specs/005-editor-shell/implementation-notes.md`, "Spike".

Spike outcome, amendments to the decisions below:

- Layout persistence uses Dock's reflection-mode `DockSerializer` (`Dock.Serializer.SystemTextJson`), not its source-generated
  one: the generator emits `JsonSerializer.Deserialize(ReadOnlySpan<byte>, Type, JsonSerializerOptions?)` and
  `SerializeToElement(object?, Type, JsonSerializerOptions?)` in `ObjectPayloadConverter`, which `RS0030` bans in `src/`, and no
  suppression or per-file severity is allowed. The adapter calls no banned overload itself. Dockable subclasses are public (an
  internal one is written without its `$type` discriminator and fails to load).
- A loaded layout is re-attached by id before it is initialised: `Context` is set from the app, a dockable the app no longer knows
  is removed. `DockState` has nothing to restore for the MVVM `Document` and `Tool` (they are not `IDocumentContent`).
- `DockControl` is created with `InitializeFactory="True"` and without `InitializeLayout`: attaching a control created with
  `InitializeLayout` closes the layout's floating windows. The adapter calls `InitLayout` again once the control is attached,
  which also opens the floating windows of a loaded layout.
- Floating windows resolve view models through `Application.DataTemplates`, not the `DockControl`'s own `DataTemplates`. The
  adapter's `x:DataType` templates are registered there.
- Native floating works under Xvfb + openbox; managed floating is not needed on Linux.

## Context

P3a replaces the launcher window and the one-window-per-class model (UX audit H2) with a single shell: a project
tree, tabbed graph documents, an inspector and a bottom panel (Errors / Output / C#). The owner asked for dockable,
floatable and tabbed panes with serialized layouts, built on Dock.Avalonia (`wieslawsoltes/Dock`), provided it works
with Avalonia 12, CommunityToolkit.Mvvm, the headless tests and the Desktop E2E setup (Xvfb + openbox, ADR-0006).

Facts checked on 2026-10-01 (sources in research R2):

- Dock 12.1.0.6 (2026-08-27, MIT) targets `net10.0` and needs Avalonia >= 12.1.1; the repo pins 12.1.3. Dock has
  shipped stable Avalonia 12 releases since 12.0.0.1 (2026-04-09), with about ten releases since then.
- `Dock.Model.Mvvm` builds on CommunityToolkit.Mvvm (>= 8.4.0; the repo has 8.4.2) and pulls in no ReactiveUI.
  ReactiveUI comes only with `Dock.Model.ReactiveUI`, and Newtonsoft.Json only with `Dock.Serializer.Newtonsoft`.
  `Dock.Serializer.SystemTextJson` adds no dependency on `net10.0`.
- Documents and tools are view-model-first. Layouts serialize with the content re-attached by the app
  (`DockState`), and floating window bounds and states are kept.
- The Dock repo runs its own Avalonia.Headless test projects. Nothing documents compiled-binding templates,
  automation peers on tabs and panes, or behaviour under openbox, so those need a spike.
- One active maintainer, frequent four-part releases (`12.1.0.x`).

## Decision

- **Adopt Dock.Avalonia** for the shell layout. Reference exactly `Dock.Avalonia`, `Dock.Model.Mvvm`,
  `Dock.Avalonia.Themes.Fluent` and `Dock.Serializer.SystemTextJson`, all at `12.1.0.6` with an exact central pin
  (`[12.1.0.6]`). Do not reference `Dock.Model.ReactiveUI`, `Dock.Serializer.Newtonsoft` or the other serializers.
  Upgrades are deliberate PRs that keep the shell's headless and E2E tests green.
- **Shell seam.** Dock types stay inside one adapter namespace, `NetPrints.Editor.Shell.Docking`. Feature view
  models (graph documents, the project tree, the inspector, the bottom panels) and the contribution registry
  (ADR-0020) never reference Dock. They talk to an `IShell` service (open or activate a document, show or hide a
  panel, the active document, reset the layout). The adapter wraps each registered panel and each open document in
  a Dock dockable whose content is the NetPrints view model, and maps views by `DataTemplate x:DataType` (ADR-0007
  D6), not by Dock's naming-convention locator. The P3 extension API therefore exposes no Dock type.
- **Layout persistence.** The adapter saves Dock's layout with the System.Text.Json serializer inside a NetPrints
  envelope (`schemaVersion`, `dockLayout`) in the per-user state store (research R7). On restore, panels and
  documents are matched by stable ids: a registered panel id (`netprints.panel.*`), or a document id derived from
  the graph's identity. Unknown panels and documents whose graph no longer exists are dropped. A layout that fails
  to load is logged and replaced by the default layout. **View › Reset layout** always restores the default.
- **Floating** uses Dock's native floating windows. If the spike shows that native floating is unreliable under
  openbox, the shell switches to Dock's managed (in-window) floating on Linux only. That is a setting, not a
  fallback.
- **Spike gate.** The first task of the shell sub-phase is a throwaway spike that must show all of the following:
  1. a document template with `x:DataType` and compiled bindings;
  2. a layout round trip that re-creates documents through the app;
  3. automation ids on content inside docked, tabbed and floating panes, reachable from a headless test and from
     the E2E automation pipe;
  4. floating and re-docking a pane under Xvfb + openbox;
  5. Light and Dark theming through the editor's tokens.
  It records the results in `implementation-notes.md`.
- **Fallback.** If check 1, 2 or 3 fails and the spike cannot fix it, the shell is built on plain Avalonia instead:
  a `Grid` with `GridSplitter`s, a `TabControl` for documents, collapsible side and bottom panels, and pane sizes
  and visibility persisted in the same envelope with `schemaVersion` bumped. "Open in new window" replaces
  floating, through `IWindowService`. Features and tests are unaffected because they depend only on `IShell`.

## Consequences

- Docking, floating, tab groups and layout serialization come from a maintained library instead of new code, which
  also covers most of UX audit L2.
- The seam costs one adapter and one indirection (`IShell`), but bounds the cost of a Dock regression, an upgrade
  break or the fallback to that adapter, and keeps Dock out of the P3 public API.
- A single active maintainer and fast releases are a supply risk. The exact pin, the seam and the fallback contain
  it, so a Dock upgrade is never forced by another package.
- E2E tests drive the shell through commands and automation ids, not through drag gestures on Dock's chrome. Drag
  docking itself is Dock's responsibility and is checked by hand once per release.
