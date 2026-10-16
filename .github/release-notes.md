## Unreleased

**Breaking (API):** the pin properties of eight built-in nodes are named after their stored pin key. Saved graphs and generated code are unchanged. There are no `[Obsolete]` shims; rename the calls:

- `IfElseNode.ExecutionPin`, `ForLoopNode.ExecutionPin`: `ExecPin`
- `ReturnNode.ReturnPin`: `ExecPin`
- `ExecutionEntryNode.InitialExecutionPin` (so `MethodEntryNode` and `ConstructorEntryNode`), `EventEntryNode.InitialExecutionPin`: `ExecPin`
- `DefaultNode.DefaultValuePin`: `DefaultPin`
- `ClassReturnNode.SuperTypePin`: `BaseTypePin`

`TypeOfNode.InputTypePin` keeps its name, because `TypePin` is its output pin.

**Breaking (API):** `CanSetPure` follows pin roles. A node can be made pure exactly when an output data pin has the `Result` or `OutParam` role, so the per-class overrides of `CallMethodNode`, `ConstructorNode`, `TernaryNode` and `ExplicitCastNode` are gone. `CallMethodNode.ReturnValuePins` lists the `Result` pins only.

**Added:** a call to a method with `out` or `ref` parameters has an output pin for each of them (keyed `out.data.<parameterName>`) that carries the value after the call, and a void method with an `out` parameter can be made pure. Saved graphs do not change. Node capabilities (`IPinListEditor`, `IOverloadableNode`, `IPinNameEditor`) are role interfaces the editor asks for.

## 0.3.0

**Changed:** the editor is one window. The launcher and the per-class windows are gone: a project opens in a single window with a project tree, tabbed graphs, an inspector, an Errors, Output and C# panel, a Variables panel, a menu bar and a command bar. Panes dock, tab and float, and a graph tab can float into its own window and dock back; **View › Reset layout** restores the default. Every action of the old windows is still reachable (class settings, members, event graphs, overriding a base method, variable getter, setter and type graphs, references, project settings, create and open project). One project per window; opening another unloads the current one.

**Added:** unsaved changes are marked. A class with changes not yet saved shows `*` on its tab, its row in the project tree and the window title, and the mark goes away when you save or undo back to the saved state. **Save** (Ctrl+S) writes the class you are editing and the status bar says how many files it saved; **Save all** (Ctrl+Shift+S) writes everything.

**Added:** the editor asks before it throws changes away. Closing the window, Exit, Close project, Open project and New project show an "unsaved changes" prompt with Save all, Don't save and Cancel; Exit also asks before stopping a program that is still running.

**Added:** backups and recovery. Thirty seconds after your last change the editor backs up each unsaved class in its own data folder (outside your project). If the editor is killed or crashes, opening the project offers to restore the backed-up files as unsaved changes, discard them, or close the dialog to decide later. Saving, undoing back to the saved state or choosing Don't save removes the backup.

**Added:** the status bar reports undo, redo, save, build results and whether the program is running. Adding and deleting nodes can now be undone. **Help › Keyboard shortcuts** lists every shortcut and **Help › About** shows the version.

**Added:** keyboard shortcuts: Save Ctrl+S, Save all Ctrl+Shift+S, Open Ctrl+O, New project Ctrl+Shift+N, Undo Ctrl+Z, Redo Ctrl+Y or Ctrl+Shift+Z, Compile F7 or Ctrl+Shift+B, Run F5, **Stop Shift+F5**, Next and previous tab Ctrl+Tab and Ctrl+Shift+Tab, Close tab Ctrl+W, Add node Ctrl+Space, Select all Ctrl+A, Delete, Rename F2, Frame selection F, Fit all Home or Shift+F. Single-key shortcuts never fire while you type in a text field.

**Added:** the start page. With no project open the editor shows recent projects (pin, unpin, remove and search; missing projects are marked), Open, New project, Samples and What's new. **New project** offers a Console app (with a `Program` class and an empty `Main`, so it builds and runs at once) and a Class library; it checks the name and folder before writing and cleans up after a failure. **Samples** copies HelloWorld to a folder you choose and opens the copy. **Help › Start page** opens it again.

**Added:** the editor restores where you were. The window position and size, the panel layout, the open graph tabs, the active tab and each graph's zoom and position come back after a restart; a graph that no longer exists is skipped, and an unreadable or newer state file falls back to the defaults.

**Added:** navigation. **Ctrl+Shift+P** opens the command palette and runs a command by name. **Ctrl+P** goes to anything: graphs, nodes (by the title shown on the canvas), variables and methods of the open project, and commands after a leading `>`. **Ctrl+click** on a connection jumps to the end farther from the click, and the connection's context menu offers Go to source and Go to target. **Alt+Left** and **Alt+Right** go back and forward through where you have been (go to, errors, connection jumps and tab changes). Hovering a connection shows its ends, its type and the documentation of the called method, and each graph shows breadcrumbs (project, class, graph) above the canvas.

**Added:** event graphs can be renamed (F2 or the inspector; a duplicate name is refused) and custom event entries have an inspector with their name and arguments: rename the event, add, remove, reorder, rename and retype arguments, each change one undo step. The generated method has the new name and parameters, and renaming a custom event updates the nodes that call it. Custom event arguments are saved in the graph file as an optional `arguments` property (see the graph file format guide). **An older editor drops custom event arguments when it saves the graph**, so do not open a graph with typed event arguments in an older version.

**Added:** F2 on a graph, method or variable in the project tree renames it in place: Enter commits (one undo step, a duplicate or invalid name is refused with the reason), Esc or leaving the box cancels. Opening an event graph lists its entries in the inspector (name, kind, argument count) and selecting one shows it. Changing the arguments of a custom event updates the nodes that call it in the same undo step: their pins follow the new signature and a removed argument's connection is dropped. Every panel has a View command (and so a palette entry), and **Go to source** and **Go to target** also run from the Go menu and the palette when the selected node has exactly one connection on that side.

**Changed:** a call that returns no value can no longer be pure. A graph file that stores `pure: true` on one loads it as impure with the warning `NPD010`. An unconnected `out` or `ref` argument is now reported as `NPT008` ("Connect a variable to …") instead of generating invalid C#. Setting an instance indexer now writes the index in the generated code. A custom event argument with an invalid or duplicate name in a graph file is repaired with the warning `NPD011` instead of failing the whole class, and the argument names are written once, in the pins.

**Added:** Light and System themes. **View › Theme** switches between Dark, Light and System (which follows the operating system); the choice is stored and applied at the next start. Colours, the type scale and the spacing come from shared tokens, so every pane, dialog and the graph canvas look right in both variants.

**Changed:** every icon is a vector and every glyph comes from one family, so icons stay sharp at 150% and 200% display scaling; the NetPrints mark is drawn as a vector too (start page and About). Controls show a focus ring when you tab to them, hover and pressed states are consistent, and the Errors, Output, project tree, node search, command palette, go to anything and inspector panes show an empty state with an icon and one sentence instead of a blank area. The Unsaved changes, Confirm, Keyboard shortcuts, Trust, Issues, Recover, About and Error dialogs share one layout (title, icon, body, buttons in the platform's order, Enter for the default, Esc for cancel) and size to their content.

**Changed:** choosing a method to override and changing the overload of a call use one readable list: methods are grouped under their declaring type, shown as `void WriteLine(string format, object arg0)` with no namespaces, and a filter box narrows the rows as you type. **Override method** dims the methods the class already has. A call node has an overloads button that opens the same list under it.

**Changed:** the graph canvas. Pins and wires are coloured by data type, and a pin is an outline until it is connected, then filled. Pin rows are clean (no background of their own, so the node body shows through), execution pins are squares and type pins have their own shape. An unconnected Target pin on a member of the graph's own class or a base class says "self". Nodes use the header role colours and kind glyphs, and node headers and pins meet the contrast ratios in both themes.

**Added:** each recent project on the start page has an icon tile. Setting Pure on a call can be undone and redone. A search scoped to a type follows the catalogs: it lists the members of the catalog that covers the type's assembly, hides the members that catalog omits, and says which catalog does not list the type when it has no entry for it; graphs that already use a hidden member still load, build and run.

**Added:** logs and per-user folders. Each run of the editor writes a log file (the last 20 runs are kept), and an exception that ends the editor is logged before it exits. **Help › Open logs folder** opens the folder. The editor's own files and your backups now live in one folder per user: `%LOCALAPPDATA%\NetPrints` on Windows, `$XDG_STATE_HOME/NetPrints` or `~/.local/state/NetPrints` on Linux and `~/Library/Application Support/NetPrints` on macOS, with the logs in a `logs` folder beside them (`~/Library/Logs/NetPrints` on macOS). The first start moves the files from the old folder; if the move fails the old folder stays and the reason is in the log. `NETPRINTS_STATE_DIR` and `NETPRINTS_LOG_DIR` choose other folders.

**Changed:** the Variables panel is compact: each variable is one row with a coloured dot for its type family, its name and its type, and the **+** in a section header adds a variable. Select a row to edit the variable in the Inspector. F2 or a double click on the name renames it in place, Delete or the **-** on the selected row removes it, and dragging the name onto the canvas places a getter or setter. Rename and Delete also run from the Variables panel.

**Changed:** document tabs are compact (the strip is 28 px high) with a smaller close button and one label baseline, and the line under the strip is drawn the same way in every dock state and at every display scale, with no gap or doubled line under the selected tab. The breadcrumbs float over the top left of the graph canvas instead of taking a bar of their own; only the segments take the pointer, so panning, selecting and right-click search work under them. Switching the theme says which theme it applied, and the theme commands are named in the command palette (**Theme › System (follow OS)**).

**Fixed:** the execution wire follows a node that was rewired by inserting a node into a connection. Docking a floated pane back no longer duplicates its entry in the layout, and a pane left without a home after a dock operation is homed once, after the operation. The selected document tab joins its document.

**Added:** guides for the editor, with screenshots: [the editor window](https://danielmeza.github.io/netprints/guide/editor/shell), [the start page](https://danielmeza.github.io/netprints/guide/editor/start-page), [saving and recovery](https://danielmeza.github.io/netprints/guide/editor/saving-and-recovery), [commands and shortcuts](https://danielmeza.github.io/netprints/guide/editor/commands-and-shortcuts), [navigation](https://danielmeza.github.io/netprints/guide/editor/navigation) and [event graphs](https://danielmeza.github.io/netprints/guide/editor/event-graphs). The [projects guide](https://danielmeza.github.io/netprints/guide/projects) now explains how a `Properties/launchSettings.json` gives **Run** (F5) and `netprints run` arguments and environment variables.

## Downloads

| Platform | File |
|---|---|
| Linux x64 | `NetPrints-<version>-linux-x64.tar.gz` |
| Windows x64 | `NetPrints-<version>-win-x64.zip` |
| macOS Apple silicon | `NetPrints-<version>-osx-arm64.tar.gz` |

The editor archives are self-contained: they run without a .NET runtime. Opening, building and running
NetPrints projects needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), which also
provides the runtime your compiled programs need.

Packages: `dotnet tool install -g NetPrints.Cli` (command `netprints`) and
`<PackageReference Include="NetPrints.Sdk" Version="…" PrivateAssets="all" />`.

**These builds are not code-signed.** Windows SmartScreen shows "Windows protected your PC": choose
*More info* → *Run anyway*. On macOS, open it once, then *System Settings* → *Privacy & Security* →
*Open Anyway* (or run `xattr -dr com.apple.quarantine NetPrints-<version>-osx-arm64`).

Verify a download: `sha256sum -c --ignore-missing SHA256SUMS.txt` and `gh attestation verify <file> -R danielmeza/netprints`.
