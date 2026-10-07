# Contract: per-user state, backups and preferences

FR-024 to FR-026 and FR-050 to FR-052. These files are written by the editor and are not user-facing artifacts.
They are still versioned, and the editor never fails to start because of them.

## 1. Locations

`<AppData>` is `Environment.SpecialFolder.ApplicationData`. That is `$XDG_CONFIG_HOME` or `~/.config` on Linux,
`~/Library/Application Support` on macOS (as .NET resolves it), and `%APPDATA%` on Windows. `<project-key>` is the
first 16 lowercase hex characters of SHA-256 over the project file's full path in UTF-8. On Windows the path is
case-folded first.

| File | Content |
|---|---|
| `<AppData>/NetPrints/settings.json` | existing P1 settings store; the editor adds `netprints.editor` → `{ "theme": "dark" \| "light" \| "system" }` |
| `<AppData>/NetPrints/state/window.json` | window bounds and maximized state |
| `<AppData>/NetPrints/state/layout.json` | panel and document layout (envelope below) |
| `<AppData>/NetPrints/state/recent.json` | recent projects |
| `<AppData>/NetPrints/state/start.json` | start page memory: the version whose release notes were shown, the last new project location and the startup behaviour |
| `<AppData>/NetPrints/state/sessions/<project-key>.json` | open tabs, active tab, viewports |
| `<AppData>/NetPrints/backups/<project-key>/manifest.json` | backup manifest |
| `<AppData>/NetPrints/backups/<project-key>/<relative-path>.bak.json` | backed-up content of one file |

- **Permissions:** on Linux and macOS, folders are created `0700` and files `0600`.
- **Writes:** every write goes to a uniquely named temporary file (`<file>.<random>.tmp`) in the same folder and is then
  renamed over the target, so two editors never share a temporary file. Temporary files older than a day are deleted.
- **Override:** `NETPRINTS_STATE_DIR` replaces `<AppData>/NetPrints` for tests and E2E workers, so parallel workers
  never share state.

## 2. Formats

All files are UTF-8 JSON without a byte order mark, with LF line endings, written through System.Text.Json source
generation. Every file carries `"schemaVersion": 1`. A reader that finds a missing, unreadable or newer
`schemaVersion` logs a warning and uses the defaults. A file with a `null` where the schema says non-null, or without a
required field, is unreadable and gets the same treatment. It never rewrites a newer file until the user changes that
state: the store skips the save (one warning per file) unless the caller says the user changed it (a layout change, a start
setting). `recent.json` is never rewritten while it is newer. `RecentProjects` re-reads the file before each change and
applies the change to what it read, so two editors keep each other's entries and pins.

```jsonc
// window.json
{ "schemaVersion": 1, "x": 120, "y": 80, "width": 1600, "height": 960, "isMaximized": false,
  "screen": { "x": 0, "y": 0, "width": 2560, "height": 1440 } }

// layout.json (ADR-0018 envelope; "engine" is "dock", or "grid" for the fallback)
// "dockLayout" is the NetPrints-owned DTO tree of the Dock adapter (Shell/Docking, T064), never Dock's own JSON.
{ "schemaVersion": 1, "engine": "dock", "dockLayout": {
  "activeDocument": "graph:Program.netpc.json#method:m000000001gs20",
  "root": { "kind": "proportional", "id": "netprints.root.column", "proportion": 1, "orientation": "vertical", "children": [
    { "kind": "tools", "id": "netprints.dock.bottom", "proportion": 0.25, "alignment": "Bottom", "activeId": "netprints.panel.errors", "children": [
      { "kind": "tool", "id": "netprints.panel.errors" }, { "kind": "tool", "id": "netprints.panel.output" } ] } ] },
  "hidden": ["netprints.panel.inspector"],
  "windows": [ { "x": 200, "y": 120, "width": 900, "height": 700,
    "root": { "kind": "documents", "id": "netprints.documents", "children": [ { "kind": "document", "id": "graph:Program.netpc.json#method:m000000001gs20" } ] } } ] } }

// recent.json
{ "schemaVersion": 1, "entries": [
  { "path": "/home/u/src/Hello/Hello.csproj", "displayName": "Hello", "lastOpenedUtc": "2026-10-01T10:00:00Z", "pinned": true } ] }

// start.json
{ "schemaVersion": 1, "whatsNewSeenVersion": "0.2.0", "newProjectLocation": "/work/projects" }

// sessions/<project-key>.json
{ "schemaVersion": 1, "projectPath": "/home/u/src/Hello/Hello.csproj",
  "openDocuments": ["graph:Program.netpc.json#method:m000000001gs20", "graph:Program.netpc.json#event:e000000000k3n0"],
  "activeDocument": "graph:Program.netpc.json#method:m000000001gs20",
  "viewports": { "graph:Program.netpc.json#method:m000000001gs20": { "x": -40.0, "y": 12.5, "zoom": 1.25 } } }

// backups/<project-key>/manifest.json
{ "schemaVersion": 1, "projectPath": "/home/u/src/Hello/Hello.csproj", "files": [
  { "originalPath": "Program.netpc.json", "backupPath": "Program.netpc.json.bak.json",
    "writtenUtc": "2026-10-01T10:02:31Z", "sha256": "…" } ] }
```

## 3. Rules

- **Recent list.** At most 20 unpinned entries, oldest dropped first. Pinned entries are never dropped. Paths are
  stored as given (full paths) and compared case-insensitively on Windows only.
- **Session restore.** Documents that cannot be resolved are skipped, and so are viewports with non-finite values.
  The active document falls back to the first restored one.
- **Window restore.** If the saved bounds do not intersect any current screen, the window is centred on the primary
  screen at its saved size, clamped to that screen.
- **Backups.**
  - Written 30 s after a file's last change (the debounce restarts on every change).
  - A backup holds the same canonical JSON a save would write.
  - Deleted on save, on Don't save, and on Discard in recovery.
  - At startup, each backup whose `writtenUtc` is older than 30 days is deleted (with its manifest entry), and a
    backup folder is deleted when it has no backups left or when its `projectPath` no longer exists.
  - `NETPRINTS_BACKUP_DELAY` (milliseconds) shortens the 30 s wait for E2E tests only.
  - Each editor instance writes backups only for the files it has open, keyed by path, so two instances on one
    project overwrite each other's backup of the same file. Last writer wins, matching the save behaviour.
- **Sessions with several instances.** The last instance to unload the project writes the session.
