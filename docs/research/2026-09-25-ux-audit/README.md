# NetPrints Avalonia editor: UX and visual audit

Branch `001-modernize-build` @ `69083da`. Audit only, no code changes.
Date 2026-09-25.

## Sources

| Tag | Location |
|---|---|
| `BL/…` | `NetPrints.Editor.UITests/Snapshots/Baselines/*.png` (all 17 viewed) |
| `E2E/…` | `CI artifact e2e-results: ui/e2e/<Test>/*.png` (all 20 viewed) |
| `UX/…` | My own captures, `shots/*.png` (Xvfb `:123` + openbox, Debug build of the same commit, `samples/HelloWorld` copy). Processes cleaned up afterwards. |
| `WPF/…` | The original WPF editor's screenshots from the `master` README (imgur), saved as `wpf/{ld32kuo,qHF1cmq,NahX6AM,wekGSFs,qdYBLni,bq0vECa}.png`. These are older WPF builds ("Attributes" instead of "Variables"), but the theme, node styling and layout match `master:NetPrintsEditor/*.xaml`. |
| code | `NetPrints.Editor/**` and `git show master:NetPrintsEditor/**` |

**Phase legend.** **P0-fix** = a clear defect or regression vs the WPF editor; fix before the P0 merge.
**P1 / P3 / P6 / P8** = existing roadmap phases. **P3a (new)** = a proposed "editor shell" item: a single window,
commands, panels, document lifecycle. P3 needs this anyway for its "UI contributions (commands, inspector sections,
panels, settings pages)", so I suggest scheduling it as the first half of P3 or just before it.
**Covered** = the roadmap or parity inventory already plans it.

---

## Top 10

1. **Run shows no program output on Linux** (P0-fix). The console program's stdout goes to the editor's own stdout, so "Hello, World!" never appears in the UI.
2. **Pure-black theme instead of the WPF dark grey** (P0-fix). Only `Accent` is set, so Fluent's dark `AltHigh` region (#000000) is used. The WPF editor used MahApps Dark (#252525).
3. **Node selection chrome is not themed** (P0-fix). Nodify's default DodgerBlue (unselected) and orange (selected) container borders are drawn around NetPrints' own green border, which gives a double border and three colours.
4. **No unsaved-changes tracking** (P3a new). There is no "*" in the title and no prompt on close, so edits are lost silently. The WPF editor had the same gap, but it is the biggest data-loss risk.
5. **A single-window shell with a real command bar** (P3a new). It replaces the 800×600 launcher plus maximized per-class windows, and the 100-px round text buttons.
6. **Keyboard command set** (P3a/P6). Today there are only Delete, Ctrl+Z and Ctrl+Y. Missing: Ctrl+S, F5/F7, Ctrl+Shift+Z, Ctrl+A, copy/paste/duplicate, F2, F (frame) and Home (fit). Shortcuts should also appear in tooltips.
7. **Node creation and actions are hidden** (P6). Right-click is the only way to create a node, and there are no context menus anywhere.
8. **Compile feedback** (P1 covered + P6). The error list is plain strings with no severity, code or link to the node, and the status line is a 24-px black strip with no progress, colour or icon.
9. **Node search quality** (P6 covered, plus P0-fix for docs). Results are sorted alphabetically instead of by relevance (`Debug.WriteLine` comes before `Console.WriteLine`), there is no match highlighting, and there is no documentation preview. Documentation tooltips are missing on Linux (item D5).
10. **First run, empty states and accessibility basics** (P6). There is no start page, sample or recent-projects list, and the empty canvas and lists give no hints. 10 of 11 icon-only buttons have no accessible name, and one text colour fails WCAG AA (3.1:1).

---

## Prioritized findings

Effort: S ≤ 1 day, M 2–5 days, L > 1 week.

### A. Defects and regressions to fix before the P0 merge

| ID | Issue | Evidence | Impact | Suggested fix | Effort | Phase |
|---|---|---|---|---|---|---|
| D1 | The window background is pure black (#000000). WPF used MahApps `Dark.Emerald` (#252525). The panels (#2B2B2B), the canvas (#000) and the windows no longer read as one surface, and the black canvas with the heavy grid is harsher than WPF. | `EditorApp.axaml:11-12` (the palette sets only `Accent`). Compare `BL/main-window-empty.png` and `E2E/CreateProject/created-project.png` with `WPF/qdYBLni.png`; compare `UX/06-deselected.png` with `WPF/NahX6AM.png`. | High: this is the first impression, and it is a visual regression. | Set `RegionColor`, `AltHigh`, `AltMedium` and `ChromeLow/Medium` in the Dark `ColorPaletteResources` (for example region #202020 and panels #2B2B2B), or set a `Window` background resource. Update the snapshot baselines. | S | P0-fix |
| D2 | Node selection uses three colours. Nodify's `ItemContainer` draws DodgerBlue `rgb(30,144,255)` around the loaded nodes and orange around the selected node, on top of NetPrints' green `#009900` border. Newly created nodes have no blue border, which is inconsistent. Pin hover also uses Nodify's blue. | `UX/07z.png` (orange + green double border), `UX/06-deselected.png` (blue), `E2E/DragFromLists/dropped-nodes.png` (new nodes without the border), `UX/15c.png` (blue pin hover). Code: `GraphEditorView.axaml:37-43`, `NodeView.axaml:164-166`. WPF: single green border (`WPF/ld32kuo.png`, "For Loop"). | High: selection is the most frequent visual state and it looks broken. | In `ItemContainerTheme`, set `BorderThickness=0` (or `BorderBrush=Transparent`) and `SelectedBrush`/`HighlightBrush` to the accent, and keep one border. Override the `Connector` hover brushes the same way. | S | P0-fix |
| D3 | **Run shows no output.** `ProcessLauncher` starts the program with `UseShellExecute=false` and does not redirect output. On Windows a console child gets its own console window, which is what WPF users saw. On Linux and macOS the output goes to the editor's stdout, which is invisible when the editor is started from a desktop. | `NetPrints.Editor/Hosting/Avalonia/ProcessLauncher.cs:10`, `Main/MainEditorVM.cs:376`. `E2E/EditCompileAndRun/03-ran.png` shows only "Build succeeded", while `editor-stdout.txt` contains `Hello, World!`. | High: SC-008 ("prints Hello, World! in the editor (Run)") is only met in the log. A user on Linux thinks Run did nothing. | Minimal: redirect stdout/stderr and show it in an **Output** list (reuse the error-list slot with a tab), with a "Process exited (code N)" line. Alternative: start the platform terminal. The full Output panel follows in P3a. | S–M | P0-fix |
| D4 | The generated C# preview wraps lines (`TextWrapping="Wrap"`, horizontal scroll disabled). WPF used `NoWrap` with a horizontal scrollbar. At the 250-px inspector width, indentation breaks and statements split mid-token (`System.Console.WriteLine("` / `Hello, World!");`). The headless baseline also renders it in a proportional font, because `monospace` does not resolve there. | `ClassInspectorView.axaml:25-27`; `UX/04-classwin.png`, `BL/inspector-class.png`; WPF: `master:Controls/ClassPropertyEditorControl.xaml` (`TextWrapping="NoWrap" HorizontalScrollBarVisibility="Auto"`). | Medium: the code is unreadable, and this is the "learn C#" hook. | Use `NoWrap` with a horizontal scrollbar, and bundle a monospace font (for example Cascadia Code or JetBrains Mono as an embedded font) so the snapshots are deterministic. P1 replaces the preview with AvaloniaEdit. | S | P0-fix (then P1) |
| D5 | **Documentation tooltips are missing on Linux.** `DocumentationUtil` looks for `*.xml` next to the runtime assembly (`shared/Microsoft.NETCore.App/*`), which ships no XML docs. The XML files are in `packs/Microsoft.NETCore.App.Ref/<ver>/ref/net10.0/`. Hovering `Console.WriteLine` shows no tooltip. | `NetPrints.Reflection/DocumentationUtil.cs:55-81`, `Graph/Nodes/NodeVM.cs:119-128`; `UX/14c.png` (no tooltip after 2 s over the header); `~/.dotnet/packs/Microsoft.NETCore.App.Ref/10.0.11/ref/net10.0/System.Console.xml` exists. | Medium: PAR-39 "documentation tooltip" is effectively lost for the BCL on Linux. On Windows with .NET Framework reference assemblies it still works. | Add one probe: `<dotnet root>/packs/Microsoft.NETCore.App.Ref/<runtime major.minor>*/ref/net*/<name>.xml`. If that is not acceptable for P0, record it as a known gap and do it with P1 reference-pack resolution. | S | P0-fix (or P1 if deferred) |

### B. High priority after P0

| ID | Issue | Evidence | Impact | Suggested fix | Effort | Phase |
|---|---|---|---|---|---|---|
| H1 | No dirty-state tracking: no "*" in the title, no "Save changes?" on closing a window or the app, and no autosave or backup. | No `IsDirty`/`Closing` handler in `NetPrints.Editor` or `NetPrints/Core` (grep). WPF: the same gap. | High: silent data loss. | Add a document-dirty flag driven by the undo stack and model changes, a title suffix, a close prompt (Save / Don't save / Cancel), and optional autosave to `.bak`. | M | P3a (new) |
| H2 | Two-level window model: an 800×600 launcher (mostly empty) plus one maximized window per class. Users juggle OS windows. The existence of the E2E test `MinimizeAndRestoreClassWindow` shows the friction. No other reference editor (Unreal, Unity Shader Graph, Blender, Godot) works this way; they use one window with tabs and dock panels. | `BL/main-window-project.png`, `E2E/MinimizeAndRestoreClassWindow/restored.png`, `ClassEditorWindow.axaml:176`. | High for day-to-day work. | Single shell: a left **Project** tree (classes → methods, constructors, variables), centre **tabbed graphs** (one tab per method or class graph, with a breadcrumb `Class › Method`), a right **Inspector**, and a bottom panel (**Errors / Output / C#**). Use Dock.Avalonia or a simple `TabControl` with GridSplitters. Keep "open in new window" as an option. | L | P3a (new), prerequisite for the P3 panel contributions |
| H3 | Commands are 100-px round text buttons (MahApps circle-button parity): no icons, no shortcuts, no grouping, "References" touches the circle edge, and the Run and Compile state is not visible. They take a 100-px band on every window. | `EditorStyles.axaml:24-39`; `BL/main-window-empty.png`, `UX/02c.png` (square focus rectangle around a circle); WPF: `WPF/qdYBLni.png`. | High: this is the dominant visual element and it looks dated. | A Fluent command bar (32–40 px) with Material icons (already a dependency) and labels: Save, Compile (with an error-count badge), Run (▶), Class settings, References. Show the shortcut in the tooltip ("Compile (F7)"). A menu bar or app menu gives discoverability (File, Edit, View, Build, Help). | M | P3a (new) |
| H4 | Keyboard support is only Delete, Ctrl+Z and Ctrl+Y. Missing: Ctrl+S, F7 or Ctrl+Shift+B (compile), F5 (run), Ctrl+Shift+Z (redo), Ctrl+A, Ctrl+C/X/V and Ctrl+D (duplicate), F2 (rename), Esc (cancel), F (frame selection), Home or Shift+F (fit all), arrow-key nudge, and Tab or Space to open node search at the cursor (as in Unreal Blueprint, Unity Shader Graph and Blender Shift+A). None was in WPF, so none is a regression. | `ClassEditorWindow.axaml:178-182`. | High for power users and accessibility. | Central command registry (P3 needs one for contributions anyway) and `KeyBinding`s on the shell. Add a "Keyboard shortcuts" sheet (Nodify `HotKeyControl`, or a simple dialog). | M | P3a / P6 |
| H5 | Nodes can only be created by right-click on the canvas (or by dragging from a pin or list). Nothing tells the user this, there is no "Add node" button, and the canvas is an unlabelled black void until a graph is opened. | `UX/04-classwin.png` (black centre with no hint), `UX/06-deselected.png`. | High for newcomers, the P6 audience. | Empty-graph hint ("Right-click or press Tab to add a node · Drag from a pin to connect"), an "Add node" button in the canvas corner, and a Tab/Space shortcut. With no graph open: "Double-click a method, or create one". | S | P6 |
| H6 | No context menus anywhere: node (delete, duplicate, break links, collapse, add comment), pin (break links, promote to variable, reset default), cable (insert reroute, delete), list items (rename, delete, open), canvas (add node, paste, fit). Middle-click clears or disconnects and XButton1 toggles "faint", and none of this is discoverable. | No `ContextMenu`/`MenuItem` in `NetPrints.Editor` (grep). PAR-44/48 gestures. | High. | Context menus that expose every hidden gesture. Add alt-click to break links (Unreal) as an alias of middle-click. | M | P6 |
| H7 | The error list is a `ListBox` of plain strings: no severity icon, error code, class or method location, or count, and it is not linked to the node. It is a fixed 150-px grey block even when empty. The status line is a 24-px strip under the canvas only ("Build failed with 1 error(s)") with no colour, icon, progress or timestamp. | `ClassEditorWindow.axaml:286-290`; `UX/11-compile-error.png`, `E2E/EditCompileAndRun/03-ran.png`; WPF: the same (`WPF/qHF1cmq.png`). | High: compile is the core loop. | Error rows (icon, code, message, `Class.Method`, double click to open the graph and select the node), an auto-shown collapsible bottom panel, and a full-width status bar with a spinner while compiling, green or red state, error count and the time of the last build. P1 already plans "error list linked to the originating node". | M | P1 (covered) + P6 |
| H8 | Node search ranks by namespace alphabetically: `System.Diagnostics.Debug.WriteLine` fills the list before `Console.WriteLine`. Overloads flatten into long full signatures that are clipped at 700 px. There is no match highlighting, no documentation or preview pane, and the category header is 3.1:1 contrast. | `UX/08-search.png`, `UX/09-search-kbd.png`, `BL/search-popup.png`; `NodeSearchView.axaml:263`. | High: this is how every node is created. | Relevance ranking (exact name > prefix > contains, and common types such as `Console` and `Math` first), group overloads under one entry with an overload count, highlight the match, show the doc summary in a side panel, and track recent items and favourites. | M | P6 (covered: "ranked, strong filtering, favorites, recent") |
| H9 | 10 of 11 icon-only buttons have no `AutomationProperties.Name`: the "−" remove buttons in the method, constructor and variable lists and in the references dialog, and the node +/− pin buttons. Screen readers announce "button". The one that has a name (remove class) is named after the class, not the action. | `grep 'Classes="icon"'` in `NetPrints.Editor/**` (10 without a name); `Main/MainWindow.axaml:106`. | High for accessibility; cheap. | `AutomationProperties.Name="Remove method {Name}"` and similar, taken from the tooltips. Add an automation test that every button has a name. | S | P6 (could slip into P0 as a cheap improvement, but it is not a regression) |

### C. Medium priority

| ID | Issue | Evidence | Impact | Suggested fix | Effort | Phase |
|---|---|---|---|---|---|---|
| M1 | Pin labels sit in dark "chips" (#2D2D30, the default background of Nodify's `NodeInput`/`NodeOutput`). WPF had none. The inline value box is borderless and merges with the label ("Hello, World!│value: String"), and the input and output columns touch ("value: String Catch") with no gutter. | `BL/node-call-method.png`, `UX/14c.png`; WPF: `WPF/NahX6AM.png` (clean labels, bordered inline boxes). | Medium: visual noise and weak readability. | Transparent pin headers, an inline editor with a visible 1-px border and a darker fill, a minimum 16-px column gap, and right-aligned output labels. | S | P6 |
| M2 | Node density is about 1.3× the WPF node: 46-px header, 18-px title, 28-px pin rows, 14-px semibold labels, 224-px minimum width. The HelloWorld graph fills the canvas at 100 % zoom, and the maximum zoom is 1.0, so users cannot zoom in instead. | `NodeView.axaml:150,168,180,195`; compare `UX/06-deselected.png` with `WPF/NahX6AM.png`. | Medium: less of the graph fits on screen. | Type ramp for nodes: 14-px semibold title, 12–13-px pin labels, 24-px rows, 32-px header. Raise `MaxViewportZoom` to 2.0 (P6; PAR-51 keeps 1.0 for parity). | S–M | P6 |
| M3 | There is no typography or spacing system. Section headers are 24-px centred text ("Methods", "Constructors", "Variables", and the inspector title), buttons are 16 px, rows 14 px, the watermark 32 px bold, and margins are ad hoc (2, 4, 5, 8, 10). The inspector uses an Auto label column, so each inspector has a different label width (`BL/inspector-*.png`). | `EditorStyles.axaml`, `ClassEditorWindow.axaml:213,247,271`. | Medium: consistency. | Define tokens in `EditorStyles.axaml` (Caption 12, Body 14, Subtitle 16, Title 20; spacing 4/8/12/16) and use left-aligned 12–13-px uppercase or semibold panel headers (Fluent, VS, Unreal "My Blueprint"). | M | P3a (new) |
| M4 | 5-px saturated emerald splitters frame every pane, so the accent is used as chrome. Fluent reserves the accent for focus, selection and primary actions. | `EditorStyles.axaml:64-66`; `UX/06-deselected.png` (green frame around the canvas and error list). WPF had the same (parity), but its grey background softened it. | Medium: visual weight. | Use a 1-px `#3A3A3A` divider with a 5–6-px transparent hit area and accent on hover or drag. | S | P6 |
| M5 | List rows: the selected row is a 48-px full-emerald block, there is no hover state, and a destructive "−" circle sits before every name. It is the first hit target and has no confirmation (method removal has a no-op undo, PAR-60). | `UX/06-deselected.png` (Methods), `E2E/DragFromLists/dropped-nodes.png`. | Medium: accidental deletion, heavy look. | Subtle selection (accent left bar or a 20 % accent fill), a hover fill, a trailing delete icon on hover, Delete-key and context-menu removal, and a confirmation or real undo for method removal. | S–M | P6 |
| M6 | Variable rows are 4 rows tall ("Add getter", "Add setter", "Open type graph" as full-width buttons), so a few variables fill the pane. The variable inspector has no **Type** field (the type is changed through "Open type graph"). | `E2E/DragFromLists/dropped-nodes.png`, `BL/inspector-variable.png`. | Medium. | A compact row (type colour dot, name, type) with getter and setter as inspector toggles, and a type picker in the inspector (Unreal's variable details). | M | P6 |
| M7 | The inspector accepts invalid combinations (Abstract + Static on a method) and only the compiler reports it. There are no sections and no help text. | `UX/11-compile-error.png` ("A static member cannot be marked as 'abstract'"). | Medium. | Disable conflicting modifiers, validate inline, group fields into sections (Identity, Modifiers, Code). | S–M | P6 |
| M8 | Empty states: the main window with no project shows only disabled controls; the Constructors and Variables lists are blank; the canvas before a graph opens is black; the error list is a grey slab. | `BL/main-window-empty.png`, `UX/04-classwin.png`. | Medium: onboarding. | Short hint text plus the primary action in each empty area. | S | P6 |
| M9 | First run: no start page, recent projects, sample or template. `samples/HelloWorld` exists but the UI never offers it. "Create Project" makes a project whose Run button is disabled, with the reason only in a hover tooltip. | `E2E/CreateProject/created-project.png` (Run greyed); `BL/main-window-empty.png`. | Medium–High for the P6 audience. | A start page with New (Console app / Library templates), Open, Open sample and Recent. Disabled commands should say why ("Run needs an executable project, change it in Settings"). | M | P6 (class templates covered; start page and project templates new) |
| M10 | No minimap, fit-to-view, zoom indicator or zoom reset, although Nodify.Avalonia 2.0 ships `Minimap`, `FitToScreen()`, `ZoomIn/Out`, `BringIntoView` and `AlignSelection`. | `Nodify.Avalonia.xml` (types listed); `GraphEditorView.axaml`. | Medium once graphs grow. | A minimap overlay (toggle), a "Fit (Home)" button, and a zoom % in the status bar that resets on click. | S–M | P6 |
| M11 | Multi-selection works (box select, move), but there are no operations on it: align or distribute, straighten, delete links, collapse to function, comment around selection. Copy, paste and duplicate are absent (also in WPF). | `UX/07-selected-entry.png`; no clipboard code for nodes. | Medium. | Nodify `AlignSelection`, and duplicate or copy/paste by serializing the selected subgraph (easier after the P1 serialization layer). The P6 roadmap already has comment boxes and collapse-to-function. | M–L | P6 (comments and collapse covered) |
| M12 | The preview cable is a dashed blue straight line (Nodify default), not a pin-coloured bezier, and it does not show "incompatible". Pins and cables all use one pastel per kind (exec, data, type), with no colour per data type. | `BL/canvas-preview-cable.png`; `GraphConverters.cs:52-57`. | Medium. | A preview in the source pin's colour, red with a ⃠ tooltip ("Int32 is not compatible with String") over incompatible targets, and type colours (P6). | S (preview) / M (type colours) | P6 (type colours covered) |
| M13 | Dialogs are inconsistent and sparse. References has verb-less "Assembly" / "Source code" buttons, a full-width Close, rows truncated at the end so the useful part of the path is lost ("…/System.dll found a…"), and a ToggleSwitch labelled with its state ("Exclude") that is shown disabled on every assembly row. The error dialog has no icon or summary and no Copy button, even though it silently copies to the clipboard (PAR-04). Select Type and Select Method are 160-px windows with no Cancel and no Esc handling. The Get/Set chooser disappears when the pointer leaves (PAR-55 parity) and cannot be used from the keyboard. | `BL/dialog-references.png`, `E2E/AddReferences/references.png`, `BL/dialog-error.png`, `BL/dialog-select-*.png`, `E2E/DragFromLists/get-set-chooser.png` (the chooser also covers the watermark). | Medium. | Standard dialog footer (primary right, Cancel, Esc, Enter). "Add assembly…" / "Add source folder…". Two-line reference rows (name bold, path muted, middle-ellipsis) and an "Include in build" checkbox only on source rows. Error dialog: icon, one-line summary, expandable details, Copy button. Get/Set: a small menu with keyboard focus that stays open until Esc or a click outside. | M | P6 |
| M14 | The graph-name watermark (32-px bold, 80 % white) sits in the lower-left of the canvas, competes with nodes, and is overlapped by popups. | `BL/class-editor-main.png`, `E2E/DragFromLists/get-set-chooser.png`. | Low–Medium. | Replace it with a breadcrumb or tab title, or keep a subtle 15–25 % opacity watermark (Unreal's "BLUEPRINT" corner label). | S | P6 |
| M15 | Drag from the lists onto the canvas has no drag ghost, no "copy" cursor and no drop highlight on the canvas, and the item gives no hint that it can be dragged. | `E2E/DragFromLists/*`; `ClassEditorWindow.axaml:225`. | Medium (discoverability). | A drag adorner with the node name, a canvas drop highlight, and a "Drag onto the graph to call" hint in the tooltip. | S–M | P6 |
| M16 | No undo feedback: undo and redo have no visible affordance, disabled state, history or "Undid: Add node" toast. | `ClassEditorWindow.axaml:178-182` (key bindings only). | Medium. | Edit menu or command bar entries with the action name, a transient status message, and an optional history list. | S–M | P3a / P6 |
| M17 | Compile and Run feedback in the class window: Compile finishes with only the status text changing, the buttons keep their hover or pressed look afterwards, there is no progress indicator, and Run succeeding gives no sign that the program started or exited. | `UX/11-compile-error.png` (Compile still dark after the click), `E2E/EditCompileAndRun/03-ran.png` (Run). | Medium. | A status-bar spinner, a Compile badge, "Running HelloWorld… exited 0" in Output (see D3). | S | P6 (D3 covers the output part) |
| M18 | The canvas cannot be operated from the keyboard (no node focus or traversal), and the graph structure is not exposed to screen readers beyond node and pin names. Nodify 2 provides `KeyboardNavigation` layers and `MoveFocus`. | `Nodify.Avalonia.xml` (`EditorState.KeyboardNavigation`, `DirectionalNavigationGestures`). | Medium (accessibility). | Enable Nodify keyboard navigation (Tab or arrows between nodes and pins, Enter to connect via search), a visible focus ring on nodes, and announcements when a connection is made. | M–L | P6 |
| M19 | Contrast (WCAG 2.x, measured from screenshots): search category header `#008A00` on `#2B2B2B` = **3.13:1** (fails AA text); MakeDelegate header text `#EEE` on `#7A7A20` = 3.91:1 (fails at 18-px semibold); disabled round-button label `#7F7F7F` on `#333` = 3.16:1 (exempt, but it hides what is available); white on the selected row `#116411` = 7.4:1 OK; white on the accent `#008A00` = 4.53:1 (barely AA); pin labels `#EEE` on the chip = 11.8:1 OK. Non-text: the Entry header `#202050` vs the node body `#1E1E1E` = 1.1:1, so node kind is conveyed only by a very dark hue. | Pixel samples: `BL/search-popup.png`, `UX/06-deselected.png`, `UX/09-search-kbd.png`. | Medium. | Use a lighter accent for text on dark (for example `#3FBF3F`, ≥ 6:1 on #2B2B2B), lift the header lightness, and add a kind icon in the header (Unreal's "f" icon, Shader Graph category colours plus icons). | S | P6 |
| M20 | Scaling: all sizes are fixed px, there is no in-app UI scale or font-size setting, and the viewport zoom is capped at 1.0. It relies on OS scaling (Avalonia honours per-monitor DPI). | `EditorStyles.axaml`, `GraphEditorView.axaml:33-35`. | Medium (low vision, 4K). | A "UI scale" setting (root `LayoutTransformControl` or font tokens) and a maximum zoom of 2.0. | M | P6 |
| M21 | Focus visuals: the square focus rectangle does not follow the round buttons' corner radius, and the list-row and canvas focus is barely visible. | `UX/02c.png`, `UX/03c.png`. | Low–Medium. | Resolves with H3; otherwise set `FocusAdorner` with a matching `CornerRadius` and use a 2-px accent ring. | S | P6 |

### D. Low priority / later

| ID | Issue | Evidence | Impact | Suggested fix | Effort | Phase |
|---|---|---|---|---|---|---|
| L1 | Window chrome is the native WM frame. WPF had the accent title bar (MetroWindow). There is no app identity beyond the icon. | `E2E/*/00-started.png` vs `WPF/qdYBLni.png`. | Low. | Optional `ExtendClientAreaToDecorationsHint` with the command bar in the title bar on Windows and macOS; keep native decorations on Linux. | M | P3a (new), optional |
| L2 | Pane sizes, open tabs, the zoom per graph and the window position are not persisted. | No settings store in the editor. | Low–Medium. | Per-user settings JSON (P1 per-extension settings can host it). | S–M | P3a / P3 |
| L3 | Search cold start shows only "Loading..." text (1.5 s on a developer machine, 3.4 s on CI). | `NodeSearchView.axaml:251`; spec SC-005. | Low. | Spinner and skeleton rows; warm the index in the background after a project loads. | S | P8 (covered: search cold start) |
| L4 | Large-graph rendering, virtualization and minimap cost are unmeasured. | — | Low now. | Add to the P8 benchmarks (graph with 500 nodes and 1,000 cables). | M | P8 |
| L5 | Grid (being researched separately): the lines are 2–3 px and dark (1.4:1 vs black), heavier and denser than WPF's 0.5-px grey lines, and they read as noise under the semi-transparent nodes. | `UX/06-deselected.png` vs `WPF/NahX6AM.png`. | — | See the grid research (shader plus fallback). Use major and minor lines. | — | Separate |
| L6 | The C# view is read-only. Side-by-side C# synced with the selection, highlighting and squiggles are planned. | PAR-34. | — | — | — | P1 / P6 (covered) |
| L7 | In-app help: no shortcut sheet, tips or link to docs. | — | Low. | Help menu, shortcut sheet, "What's this node?" linking to the docs. | S | P6 |

---

## Items per phase

| Phase | Items | IDs |
|---|---|---|
| **P0-fix** | 5 | D1, D2, D3, D4, D5 (D5 may move to P1) |
| **P1** | 3 (H7 partly, L6 and D4 follow-ups; covered) | H7 (error list ↔ node), L6, D4 → AvaloniaEdit |
| **P3a (new, editor shell) / P3** | 8 | H1, H2, H3, H4 (with P6), M3, M16 (with P6), L1, L2 |
| **P6** | 26 | H4 (shared), H5, H6, H7 (UI part), H8, H9, M1, M2, M4–M15, M17–M21, L7 |
| **P8** | 2 | L3, L4 |
| **Separate** (grid research) | 1 | L5 |

Already covered by the roadmap or spec: H7 (P1 error list linked to nodes, P6 per-node markers), H8 (P6 ranking, favourites, recent), M11 comments and collapse (P6), M12 type colours (P6), M9 class templates (P6), L3 (P8 search cold start), L6 (P1/P6 C# view).
New relative to the roadmap: D1–D5, H1–H6, H9, M1–M8, M10, M13–M21, L1, L2, L4, L7.

## Proposed roadmap addition (for the coordinator; I did not edit governance files)

> **P3a — Editor shell (before the P3 extension host).** Single window with a project tree, tabbed graphs, an
> inspector and a bottom panel (Errors / Output / C#); a command registry with a command bar, menu and
> shortcuts (also the P3 contribution point); document lifecycle (dirty flag, close prompt,
> autosave); design tokens (type ramp, spacing, colours) and theme overrides for Nodify; persisted layout.
> Estimate: about 2–3 weeks.

## Annotated screenshot references

- `BL/main-window-empty.png`: pure-black background (D1). Disabled round buttons at 3.2:1 (M19). The list area gives no empty-state guidance (M8/M9).
- `BL/main-window-project-pane.png` and `main-window-settings-pane.png`: the side pane covers the Run button, with a 1-px accent edge. This matches the WPF flyout behaviour, so it is not a regression, but it goes away with H2.
- `BL/class-editor-main.png`: 100-px round command band (H3); 24-px centred section headers (M3); 5-px green frames (M4); the watermark "Main" (M14); an empty grey error slab (H7); the blue Nodify borders on the nodes (D2).
- `BL/canvas-every-node-kind.png`: the header colours by kind are dark and similar (M19). Pin chips everywhere (M1).
- `BL/canvas-preview-cable.png`: the dashed blue straight preview (M12).
- `BL/node-call-method.png`: the inline value box merges with the label, and "value: String" touches "Catch" (M1).
- `BL/search-popup.png` and `UX/08-search.png`: the green category header at 3.1:1 (M19); alphabetical ranking (H8); long signatures clipped at the right edge.
- `BL/inspector-class.png` and `UX/04-classwin.png`: the wrapped C# preview (D4); the headless baseline uses a proportional font.
- `BL/inspector-variable.png`: no Type field (M6).
- `BL/dialog-references.png` and `E2E/AddReferences/references.png`: verb-less buttons, truncated paths and a disabled "Exclude" switch on every assembly row (M13).
- `BL/dialog-error.png`: no title text, icon or Copy button in the body (M13).
- `BL/dialog-select-type.png` and `dialog-select-method.png`: no Cancel, cramped (M13).
- `E2E/EditCompileAndRun/02-if-else-wired.png`: the tooltip on a pin works; the cables loop backwards when the flow goes right-to-left, which is expected with bezier curves (reroute or straighten tools would help, M11).
- `E2E/EditCompileAndRun/03-ran.png`: after Run, only "Build succeeded" is visible, and the program output is missing (D3).
- `E2E/DragFromLists/dropped-nodes.png`: new nodes lack the blue border that the loaded nodes have (D2); tall variable rows (M6); a full-emerald selected row (M5).
- `E2E/DragFromLists/get-set-chooser.png`: the chooser overlaps the watermark and the list (M13, M14).
- `E2E/CreateProject/created-project.png`: a new project cannot Run, and the reason is shown only on hover (M9).
- `E2E/MinimizeAndRestoreClassWindow/restored.png`: the multi-window model (H2).
- `UX/02c.png` and `UX/03c.png`: square focus rectangles around round buttons (M21).
- `UX/06-deselected.png` vs `UX/07-selected-entry.png` / `UX/07z.png`: blue for unselected, orange plus green for selected (D2).
- `UX/11-compile-error.png`: the plain-string error; Abstract + Static was allowed (M7); the Compile button stays in its pressed look (M17).
- `UX/12-zoomed-out.png` / `UX/13-zoom-back.png`: zoom goes from 0.3 to 1.0 with no indicator or fit button (M10, M2).
- `UX/14c.png`: no documentation tooltip on `Console.WriteLine` (D5). `UX/15c.png`: the pin tooltip works; the hover highlight is Nodify blue (D2).
- `WPF/qdYBLni.png`, `WPF/NahX6AM.png`, `WPF/ld32kuo.png`: the reference look: #252525 background, thin grey grid, a single green selection border, no pin chips, denser nodes.

## Comparison notes (established node editors)

- **Unreal Blueprint:** a single editor window with "My Blueprint" (collapsible Functions, Macros and Variables sections with a + in each header), Details, a Compiler Results tab linked to nodes, a compile button that shows status (green check, yellow, red), a context-sensitive palette on right-click, Tab or drag, comment boxes (C), Q to straighten, alt-click to break links, F and Home to frame, and a minimap-free but fast "fit" workflow.
- **Unity Shader Graph / Visual Scripting:** a Blackboard (variables), a Graph Inspector, Space to create a node, F and A to frame, groups and sticky notes, per-node preview, and errors badged on nodes.
- **Blender:** Shift+A to add, Ctrl+J to frame, M to mute, H to hide, Ctrl+Shift+D to duplicate with links, header colours by category.
- **Godot:** a visual shader with a member dialog (search with a description panel), graph frames, and a minimap toggle in the corner.
- **Nodify samples** (same library): minimap, grouping nodes, cutting line, keyboard navigation, alignment tools. These are available now in Nodify.Avalonia 2.0.

## Method and limits

- Contrast values were computed from rendered pixels with the WCAG 2.x relative-luminance formula.
- I did not run the WPF editor. Wine and Proton are not installed, and WPF on Wine is unreliable. The comparison uses the six README screenshots plus the `master` XAML.
- I did not test on HiDPI, with a screen reader (Orca/AT-SPI), on Windows or on macOS.
