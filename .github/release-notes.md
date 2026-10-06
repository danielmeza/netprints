## Unreleased

**Changed:** the editor is one window. The launcher and the per-class windows are gone: a project opens in a single window with a project tree, tabbed graphs, an inspector, an Errors, Output and C# panel, a Variables panel, a menu bar and a command bar. Panes dock, tab and float, and a graph tab can float into its own window and dock back; **View › Reset layout** restores the default. Every action of the old windows is still reachable (class settings, members, event graphs, overriding a base method, variable getter, setter and type graphs, references, project settings, create and open project). One project per window; opening another unloads the current one.

**Added:** unsaved changes are marked. A class with changes not yet saved shows `*` on its tab, its row in the project tree and the window title, and the mark goes away when you save or undo back to the saved state. **Save** (Ctrl+S) writes the class you are editing and the status bar says how many files it saved; **Save all** (Ctrl+Shift+S) writes everything.

**Added:** the editor asks before it throws changes away. Closing the window, Exit, Close project, Open project and New project show an "unsaved changes" prompt with Save all, Don't save and Cancel; Exit also asks before stopping a program that is still running.

**Added:** backups and recovery. Thirty seconds after your last change the editor backs up each unsaved class in its own data folder (outside your project). If the editor is killed or crashes, opening the project offers to restore the backed-up files as unsaved changes, discard them, or decide later. Saving, undoing back to the saved state or choosing Don't save removes the backup.

**Added:** the menu bar has File, Edit, View, Go, Build and Help, and the command bar above the editor has Save, Compile, Run or Stop, Undo, Redo, Class settings and References. The status bar reports undo, redo, save, build results and whether the program is running. Adding and deleting nodes can now be undone. **Help › Keyboard shortcuts** lists every shortcut and **Help › About** shows the version.

**Added:** keyboard shortcuts: Save Ctrl+S, Save all Ctrl+Shift+S, Open Ctrl+O, New project Ctrl+Shift+N, Undo Ctrl+Z, Redo Ctrl+Y or Ctrl+Shift+Z, Compile F7 or Ctrl+Shift+B, Run F5, **Stop Shift+F5**, Next and previous tab Ctrl+Tab and Ctrl+Shift+Tab, Close tab Ctrl+W, Add node Ctrl+Space, Select all Ctrl+A, Delete, Rename F2, Frame selection F, Fit all Home or Shift+F. Single-key shortcuts never fire while you type in a text field.

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
