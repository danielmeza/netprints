## Unreleased

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
