# Contract: built-in commands, menus and shortcuts

Product surface for US4 and US7 (FR-030 to FR-036, FR-060 to FR-063). Every row is one registered command
(ADR-0020). "Bar" means it appears on the command bar. A test asserts that the registry matches this table, and the
`commands-and-shortcuts.md` guide's table is generated from the registry and checked by a test.

## 1. Menus

Menu order: **File, Edit, View, Go, Build, Help**. Groups inside a menu are separated by a divider.

| Id (`netprints.command.`…) | Label | Menu › group | Default shortcut | Scope | Bar |
|---|---|---|---|---|---|
| `newProject` | New project… | File › project | Ctrl+Shift+N | Global | |
| `openProject` | Open folder or project… | File › project | Ctrl+O | Global | |
| `closeProject` | Close project | File › project | — | Global | |
| `newClass` | New class | File › class | — | Global | |
| `addExistingClass` | Add existing class… | File › class | — | Global | |
| `save` | Save | File › save | Ctrl+S | Global | yes (1) |
| `saveAll` | Save all | File › save | Ctrl+Shift+S | Global | |
| `projectSettings` | Project settings | File › project-settings | — | Global | |
| `references` | References… | File › project-settings | — | Global | yes (7) |
| `exit` | Exit | File › exit | — | Global | Closes the main window; the window-close path (also Alt+F4) asks the unload prompt, once. Order: wait for a build, ask to stop a running program, ask the unload prompt, then stop the program. Don't save deletes the backups of the listed files only once the project is replaced or closed |
| `undo` | Undo `<action>` | Edit › history | Ctrl+Z | Global | yes (4) |
| `redo` | Redo `<action>` | Edit › history | Ctrl+Y, Ctrl+Shift+Z | Global | yes (5) |
| `delete` | Delete | Edit › selection | Delete | Graph, ProjectTree | |
| `rename` | Rename | Edit › selection | F2 | Graph, ProjectTree | |
| `selectAll` | Select all | Edit › selection | Ctrl+A | Graph | |
| `cancel` | Cancel | (no menu) | Esc | Graph | Enabled only while a node search or Get/Set popup is open; otherwise Esc reaches Nodify (cancels a drag). |
| `nodeSearch` | Add node… | Edit › nodes | Ctrl+Space | Graph | |
| `classSettings` | Class settings | Edit › class | — | Global | yes (6) |
| `addMethod` | Add method | Edit › class | — | Global | |
| `addConstructor` | Add constructor | Edit › class | — | Global | |
| `addVariable` | Add variable | Edit › class | — | Global | |
| `addEventGraph` | Add event graph | Edit › class | — | Global | |
| `overrideMethod` | Override method… | Edit › class | — | Global | Asks for a base method (the method chooser) and opens the override; undo removes it. |
| `frameSelection` | Frame selection | View › viewport | F | Graph | |
| `fitAll` | Fit all | View › viewport | Home, Shift+F | Graph | |
| `showPanel.<panel>` | Project, Inspector, Variables, Errors, Output, C# | View › panels | — | Global | |
| `floatDocument` | Float tab | View › layout | — | Global | |
| `dockDocument` | Dock tab | View › layout | — | Global | |
| `resetLayout` | Reset layout | View › layout | — | Global | |
| `theme.dark` / `theme.light` / `theme.system` | Dark / Light / System | View › Theme | — | Global | |
| `commandPalette` | Command palette… | View › find | Ctrl+Shift+P | Global | |
| `goToAnything` | Go to anything… | Go › find | Ctrl+P | Global | |
| `navigateBack` | Back | Go › history | Alt+Left | Global | |
| `navigateForward` | Forward | Go › history | Alt+Right | Global | |
| `goToSource` | Go to source | Go › connection | — (Ctrl+click on a connection) | Graph | Parameter: a connection; without one, the single selected node's only incoming connection, else disabled |
| `goToTarget` | Go to target | Go › connection | — | Graph | Parameter: a connection; without one, the single selected node's only outgoing connection, else disabled |
| `nextTab` | Next tab | Go › tabs | Ctrl+Tab | Global | |
| `previousTab` | Previous tab | Go › tabs | Ctrl+Shift+Tab | Global | |
| `closeTab` | Close tab | Go › tabs | Ctrl+W | Global | |
| `compile` | Compile | Build › build | F7, Ctrl+Shift+B | Global | yes (2, error badge) |
| `run` | Run | Build › run | F5 | Global | yes (3, toggles with Stop) |
| `stop` | Stop | Build › run | Shift+F5 | Global | yes (3) |
| `keyboardShortcuts` | Keyboard shortcuts | Help › help | — | Global | |
| `startPage` | Start page | Help › help | — | Global | |
| `about` | About NetPrints | Help › about | — | Global | |

Rules:

- Labels with `<action>` are dynamic (`ICommandHandler.DynamicLabel`): `Undo Add node`, or plain `Undo` when nothing
  can be undone, in which case the command is disabled.
- The command bar is at most 40 px high. Each button shows an icon and a label, and its tooltip is
  `"<Label> (<first shortcut>)"`. Compile shows an error-count badge after a compile with errors. Run and Stop
  share one slot, which shows Stop while the program runs.
- Graph-scope shortcuts act only while the graph canvas has focus, and never while a text input inside a node or
  the inspector has focus. Opening or activating a graph puts focus in its canvas (SC-004: keyboard-only).
- Shortcuts are written `Ctrl+…`; on macOS the view layer binds `Ctrl` to Cmd (Meta). The conversion is unit tested
  with a platform flag; the real keys are checked by hand (CI is Linux only). Tooltips still show the text as written.

## 2. Context menus (FR-011, FR-062)

| Target | Items (command ids) |
|---|---|
| Tree: class | open class graph, `rename`, add method, add constructor, add variable, add event graph, `overrideMethod`, `classSettings`, `delete` |
| Tree: method, constructor, event graph | open, `rename` (event graphs and methods), `delete` |
| Tree: variable | `rename`, `delete` |
| Connection | `goToSource`, `goToTarget` |
| Canvas | `nodeSearch`, `selectAll`, `fitAll` |

The tree's open item is the command `openGraph` (label "Open", `Enter` in the `ProjectTree` scope, no menu): it opens
the graph of the selected class, method, constructor or event graph. An item is shown only when its command can run for
the selected row; the menu opens on the selected row, and a right-click selects the row first.

Add-member commands (`addMethod`, `addConstructor`, `addVariable`, `addEventGraph`, `overrideMethod`, in the table above) act on the
tree selection's class, else the active document's class.

## 3. Status messages

- Undo or redo: `Undid: <action>` or `Redid: <action>`, shown for 4 s.
- Save: `Saved <n> file(s)`. Compile: `Build succeeded`, or `Build failed: <n> error(s)`. Run: `Running…`, then
  `Exited with code <n>`.
- A backup failure: `Backups are failing; see the log`, shown once per session.

## 4. Busy indicator

The status bar shows an indeterminate progress bar with a text while a long operation runs, 150 ms after it starts so that
fast ones do not flash it (`StatusBarViewModel.BeginBusy`): "Loading project…" (open, create), "Loading references…" (reflection
reload) and "Preparing graphs…" (the overload warm-up that runs after each reload, off the UI thread, so the first node
search and the first graph open do not wait for reflection). A build shows "Building…" at once, from the build state. Scopes
nest; the newest text wins.
