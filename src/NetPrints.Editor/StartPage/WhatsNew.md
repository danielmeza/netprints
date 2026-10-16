## The editor shell

- The editor is one window per project: a project tree, tabbed graphs, an inspector, a Variables panel and an Errors, Output and C# panel. Panes dock, tab and float, and View > Reset layout restores the default.
- A start page shows your recent projects, a sample and these notes whenever no project is open.
- New project creates a console app or a class library from a template, in a location and name you choose. A console app starts with a Program class and an empty Main method, so it builds and runs at once.
- A sample opens in one click: it is copied to your projects folder, and you can change that folder first.
- Drop a .csproj file or a project folder on the window to open it.
- The editor can reopen the last project when it starts: tick the box on the start page.
- Recent projects can be pinned, searched and removed, and they are still there after a restart.
- The window, the panel layout and the open tabs of each project are restored when you start the editor again.
- Light and System themes: View > Theme switches between Dark, Light and System, which follows your operating system.

## Saving and recovery

- A class with unsaved changes shows * on its tab, in the project tree and in the window title. Save (Ctrl+S) writes the class you are editing and Save all (Ctrl+Shift+S) writes everything.
- The editor asks before it throws changes away: closing the window, Exit, Close project, Open project and New project offer Save all, Don't save and Cancel.
- Thirty seconds after your last change the editor backs up each unsaved class. If the editor is killed or crashes, opening the project offers to restore the backups.
- Each run writes a log file; Help > Open logs folder shows it. Your editor settings and backups now live in per-user folders, and an older folder is moved there on the first start.

## Commands and navigation

- Ctrl+Shift+P opens the command palette and runs a command by name. Help > Keyboard shortcuts lists every shortcut.
- Ctrl+P goes to anything: graphs, nodes, variables and methods of the open project, and commands after a leading >.
- Ctrl+click on a connection jumps to the end farther from the click, and its context menu offers Go to source and Go to target.
- Alt+Left and Alt+Right go back and forward through the places you have been. Hovering a connection shows its ends and its type.
- Breadcrumbs above each graph show the project, the class and the graph; click one to find it in the project tree.
- F2 renames a graph, method or variable in the project tree in place.

## Event graphs and overrides

- Event graphs can be renamed, and a custom event has an inspector for its name and arguments: add, remove, reorder, rename and retype arguments, each change one undo step. The generated method follows, and so do the nodes that call it.
- Override method lists the base methods under their declaring type with a filter box, and dims the ones the class already overrides.

## Look

- Every icon is a vector, so icons stay sharp at 150% and 200% display scaling.
- Pins and wires on the canvas are coloured by data type, and node headers use the same colours in both themes.
- Panes with nothing to show say so with an icon and one sentence.
- Document tabs are compact, and the line under the tab strip stays clean at every display scale.
- Breadcrumbs float over the top left of the graph canvas, and the canvas keeps responding under them.
- The Variables panel is one compact row per variable: F2 or a double click renames it in place, Delete removes it, and dragging the name onto the canvas places a getter or setter.
- Fixed: the execution wire follows a node rewired by inserting a node into a connection, and docking a floated pane back no longer duplicates it.

## Guides

- [The editor window](https://danielmeza.github.io/netprints/guide/editor/shell)
- [The start page](https://danielmeza.github.io/netprints/guide/editor/start-page)
- [Saving and recovery](https://danielmeza.github.io/netprints/guide/editor/saving-and-recovery)
- [Commands and shortcuts](https://danielmeza.github.io/netprints/guide/editor/commands-and-shortcuts)
- [Navigation](https://danielmeza.github.io/netprints/guide/editor/navigation)
- [Event graphs](https://danielmeza.github.io/netprints/guide/editor/event-graphs)
