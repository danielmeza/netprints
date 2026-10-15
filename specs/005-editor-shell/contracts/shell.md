# Contract: shell layout, documents, panels and automation

FR-010 to FR-018, FR-040 to FR-044, FR-065, FR-070 to FR-074 and FR-101. This is the surface that the E2E and
headless tests drive.

## 1. Default layout

```text
┌ Menu: File Edit View Go Build Help ─────────────────────────────────────────────┐
├ Command bar (≤ 40 px): Save │ Compile [n] │ Run/Stop │ Undo │ Redo │ Class settings │ References ┤
├──────────────┬──────────────────────────────────────────────┬─────────────────────┤
│ Project      │ [Main*] [EventGraph] [Start] …   (tabs)      │ Inspector (60%)     │
│ (tree)       │ Project › Program › Main  (breadcrumbs)      ├─────────────────────┤
│  20% width   │ graph canvas                                 │ Variables (40%)     │
│              │                                              │  22% width          │
├──────────────┴──────────────────────────────────────────────┴─────────────────────┤
│ [Errors] [Output] [C#]   (bottom panel, 25% height)                                │
├ Status bar: message │ build state │ backup warning ───────────────────────────────┤
```

- The **Reset layout** command restores exactly this layout: the tree on the left, the inspector above the Variables
  panel on the right (two tool docks in a vertical split, so bringing the Inspector forward never hides Variables), the
  three bottom tabs with Errors active, and every panel visible.
- A floated pane whose window is closed with the OS close button docks back to its default position. A floated
  graph tab closes as a tab does.
- With no project open, the document area shows the **Start** page. The tree, inspector and bottom panels stay in
  place, and show an empty state.

## 2. Documents

| Document | Id form | Title | Notes |
|---|---|---|---|
| Graph | `graph:<classPath>#<graphKey>` | graph name, with `*` while its class file is unsaved | `graphKey` is `method:<id>`, `ctor:<id>`, `event:<id>`, `getter:<variableId>`, `setter:<variableId>`, `type:<variableId>` or `class` and never contains `#`; `classPath` may (the id splits on the last `#`) |
| Start page | `start` | Start | shown with no project open, and by `startPage` |
| Project settings | `project-settings` | Project settings | replaces the launcher's settings pane |

## 3. Panels

| Id | Title | Default dock | Content |
|---|---|---|---|
| `netprints.panel.projectTree` | Project | Left | project › classes › Methods, Constructors, Variables, Event graphs |
| `netprints.panel.inspector` | Inspector | Right | class, method, constructor, variable or event-entry inspector for the latest selection, in the tree or in the active graph (a variable getter or setter node shows its variable, a call to a method of a project class shows that method, any other node or none shows the member that owns the graph) |
| `netprints.panel.variables` | Variables | Right, below the Inspector (its own tool dock, 40% of the right column) | member variables of the active document's class (add, remove, rename through the row's inspector, getter, setter and type graph buttons) and, while the active document is a method, constructor or accessor graph, that graph's local variables (add, remove, retype, rename) |
| `netprints.panel.errors` | Errors | Bottom | the diagnostics of the whole project (live analysis plus the last build), grouped by class, each row labelled with its class; activating a row opens, or activates, the graph of its node and selects the node, and a row with no graph key opens its class graph (registered as a navigation) |
| `netprints.panel.output` | Output | Bottom | build output, then the program's stdout and stderr; cleared at each compile or run; a run brings it forward once, when the program starts, and later lines never switch the bottom panel back |
| `netprints.panel.csharp` | C# | Bottom | generated C# of the active graph's class (read-only `CodeView`) |

The Variables panel restores what the class window's variables list offered (FR-017). It is an `ActiveClassPanelViewModel`
like the C# panel: it follows the active document's class and, through the document's graph, the method whose locals it
lists. Tapping a member row selects the variable in the tree, which shows its inspector (with the name field) in the Inspector.

**Panel buttons and the registry.** A panel button that has a registry command runs that command through the `CommandInvoker`,
with no logic of its own: the Variables panel's Add variable runs `addVariable`. A row-local action with no registry
equivalent stays a view-model command: Add local variable, a variable row's remove and select, and the variable inspector's
Open, Add and Remove getter or setter and Open type graph buttons (inspector elements, listed in the former-actions table of `FormerActionsReachableTests`).

## 4. Window title

- With a project open: `"<active document title> – <project name>[*] – NetPrints"`.
- With no project open: `"NetPrints"`.
- The `*` appears after the project name while any file is unsaved.

## 5. Dialogs

| Dialog | Buttons | Result |
|---|---|---|
| Unsaved changes | Save all (default), Don't save, Cancel (Esc) | `Save`, `Discard`, `Cancel` |
| Recover unsaved work | One checkbox per backed-up file (checked: restore, unchecked: discard its backup; unchecked at the start when the backup is older than its file); Apply (default), Discard all; no Esc binding | `Restore` with the checked paths, `Discard`, `Later` (the zero value: the window was closed, the project opens unchanged and the backups are kept) |
| New project | Create (enabled when the input is valid), Cancel | the created project path |
| Stop running program? (on exit while running) | Stop and exit, Cancel | `IEditorDialogs.ConfirmAsync`: `true` is Stop and exit, `false` is Cancel |

Every dialog follows ADR-0007: a `DialogViewModel<TResult>` with `DialogCloseBehavior`, opened through `IWindowService`.

## 6. Automation ids

New constants go in `AutomationIds` (enforced by E4), grouped as `Shell.*`, `StartPage.*`, `CommandBar.*`, `Menu.*`,
`Palette.*`, `GoTo.*`, `Breadcrumbs.*`, `Inspector.EventEntry.*` and `Dialogs.Unsaved.*` / `Dialogs.Recover.*`.

- A menu item's and a command bar button's automation id is derived from its command id
  (`Menu.netprints.command.save`), so tests address commands without new constants.
- A tree item's automation id is `Tree.<kind>.<name>`, and a document tab's is `Tab.<document id>`.
- The E2E tests find panels by panel id, tabs by document id, and commands by command id. They never rely on the
  docking library's own element names.

## 7. Event graph and entry inspector

- **Event graph:** a Name field, also renamed inline in the tree with F2. A duplicate name is refused with
  "An event graph named '<name>' already exists".
- **Custom event entry:**
  - Name: unique among the class's methods and entries (P1 FR-027). A clash is refused with
    "'<name>' is already used by <kind> '<name>'".
  - Arguments: a list of name and type, with add, remove, move up and move down. The type is picked through the
    existing type picker.
  - Each edit is one undo step labelled "Rename event", "Add argument", "Change argument type" and so on.
- **Override entry:** the name, and the base signature shown read-only, with a note "Signature comes from
  <BaseType>.<Method>".
