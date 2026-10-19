# Data Model: Editor Extension Host (P3)

Entities from the spec's Key Entities, with fields, validation and state changes. The public type shapes are in
[contracts/extension-api.md](contracts/extension-api.md); file formats are pinned by tests and listed here. Exact
member names may change in implementation if the tests and the contracts change together; the invariants may not.

## 1. Extensions

### Extension manifest (`netprints-extension.json`)

| Field | Type | Rule |
|---|---|---|
| `id` | string | Unique; matches `^[a-z0-9]+(\.[a-z0-9]+)*$`; the prefix of every id the extension contributes |
| `name` | string | Display name; shown as the owner in the palette and the shortcuts sheet |
| `version` | SemVer string | Shown in diagnostics (the project-versus-user precedence message names both versions) |
| `assembly` | string | File name next to the manifest |
| `netprintsApi` | `major.minor` | At most the host's `ExtensionApi.Version`; `themes` needs the 0.4 minor or later |
| `dependsOn` | string[] | Extension ids, ADR-0010 type sharing |
| `themes` | string[] (new) | Relative paths of `*.nptheme.json` files; data only, never code or XAML (ADR-0035) |

Unknown manifest fields are ignored with an info message, so a newer manifest loads in an older host when its
`netprintsApi` allows it.

### Extension sources and precedence

| Source | Loaded | Group | Trust |
|---|---|---|---|
| A trusted project's `NetPrintsExtension` items | with the project | that project's session group | the per-project trust prompt (P3a); the CLI and the build load them without a check (FR-038) |
| User extension directories (`netprints.extensionPaths`, `NETPRINTS_EXTENSION_PATH`) | once, at editor start | app group for app-level kinds, each session group for project-level kinds | none beyond the user's own setting |

When a project declares an id that is also loaded from a user directory, the project's copy serves that session, the
user copy adds nothing to it, and an info diagnostic (NPX012) names both versions. The session group hides the app group's items of that id while the project is open (ADR-0020 Amendment 2).
Changes to user directories apply after a restart.

### Extension package (`.nupkg`, package type `NetPrintsExtension`)

```text
<id>.<version>.nupkg
├── <id>.nuspec            # <packageTypes><packageType name="NetPrintsExtension" /></packageTypes>
└── extension/
    ├── netprints-extension.json
    ├── <assembly>.dll (+ private dependencies, satellite assemblies)
    └── themes/*.nptheme.json   (optional, listed in the manifest)
```

`netprints extension verify` reads it; installing it is P3c's.

## 2. Contributions

### Contribution (common shape)

| Field | Rule |
|---|---|
| `Id` | `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`; built-ins use `netprints.<kind>.`; extensions use `<manifest id>.` (NPX013 otherwise) |
| `Label` | `LocalizedText` (resource key plus default text); the default text shows when no translation exists |
| `Owner` | set by the host: `netprints.editor` for built-ins, the manifest id for extensions; never set by the descriptor |
| `AppliesTo` | optional `Func<TContext, bool>`; false hides the item (context menus, status-bar items) |

### Kinds

| Kind | Registered through | Level | Surface |
|---|---|---|---|
| Command | `Editor.AddCommand` | app or project (by whether its handler needs a project) | menus, command bar, key bindings, palette, shortcuts sheet |
| Panel | `Editor.AddPanel` | project | dock layout (default dock and order) |
| Dashboard tile | `Editor.AddDashboardTile` | app | start page |
| Project template | `AddProjectTemplate` (flat) | app | New project, `netprints new` |
| Context-menu item | `Editor.AddContextMenuItem` | project | node, pin, connection, tree and canvas menus |
| Tooltip provider | `Editor.AddTooltipProvider` | project | connection and pin tooltips |
| Go-to provider | `Editor.AddGoToProvider` | project | go to anything |
| Status-bar item | `Editor.AddStatusBarItem` | app or project | status bar (left or right, by order) |
| Settings page | `Editor.AddSettingsPage`; one generated page per `AddSettings` section | app | Settings document |
| Dialog | `Editor.AddDialog` | app or project | shown by id through `IDialogService` |
| View resources and icons | `Editor.AddViewResources` | with the group | DataTemplates, `Icon.<extension id>.<name>` |
| Node search provider | `AddSuggestionProvider` (flat) | project | node search |
| Node-data migration | `AddDocumentMigration` (flat) | project | document load |
| Inspector section, value editor (G) | `Editor.AddInspectorSection`, `Editor.AddValueEditor` | project | inspector, pin and default editors |
| Graph rule (G) | `AddGraphRule` (flat) | project | Errors, `netprints build` and `generate` |

### Contribution group

```text
            Register ok                  project unloads / editor exits
 Loading ──────────────► Active ─────────────────────────────────────► Removed
    │ Register throws
    └──────────────► Failed (no item added; NPX005 in Errors; the editor keeps working)
```

- The app group is created at start: built-ins (`netprints.editor`, always first), then user extensions in load order.
- A session group is created when a project's session starts and removed as a whole when it ends.
- Every change produces a new immutable registry snapshot; surfaces rebuild from it. Within a kind, the first
  registration of an id wins; built-ins always register first.

## 3. Settings

### `settings.json` (per-user config folder, ADR-0033; schema version stays 1)

```json
{
  "schemaVersion": 1,
  "netprints": { "enableAnimations": true, "extensionPaths": [], "trustedProjects": [], "language": "" },
  "keyboard": { "bindings": [ { "command": "netprints.command.save", "scope": "Global", "add": ["Ctrl+Alt+S"], "remove": ["Ctrl+S"] } ] },
  "run": { "stopTimeoutSeconds": 5, "linuxTerminal": "" },
  "extensions": {
    "netprints.editor": { "theme": "dark", "darkPreset": "netprints.dark", "lightPreset": "netprints.light" },
    "<extension id>": { }
  }
}
```

- Canonical writing as today: ordinal key order, UTF-8 without BOM, two-space indent, `\n`, final newline.
- Unknown sections and unknown keys inside known sections are written back unchanged (FR-043).
- An unreadable file (invalid JSON) is copied to `settings.json.unreadable-<yyyyMMddHHmmss>` before the next write; the
  editor starts with defaults and says so once.
- New keys: `netprints.language`, the `keyboard` and `run` sections, `darkPreset` and `lightPreset`.

### Settings section

| Field | Rule |
|---|---|
| Key | `netprints`, `keyboard`, `run` (host), `extensions.<id>` (editor and extensions) |
| Options type | sealed class with `init` members, in a source-generated JSON context |
| Validator | `[OptionsValidator]`-generated, over DataAnnotations; run at composition and on each page edit |
| Schema | `JsonSchemaExporter` output plus `description`, ranges and the `x-netprints-*` keywords (research R6) |
| Owner | the host, the editor or an extension; a section of an extension that is not loaded is preserved, not shown |

An invalid section keeps its defaults, and the message names each invalid key and the reason (NPX014 for an extension
section; a log entry and a one-time message for a built-in one). The editor still starts.

### Settings field (generated page)

| Schema | Field |
|---|---|
| `boolean` | toggle |
| `integer`, `number` (with `minimum`/`maximum`) | numeric box with range |
| `string` | text box |
| `string` with `enum` or `x-netprints-choices` | drop-down |
| `string` with `x-netprints-path` | path picker (file or folder) |
| `array` of `string` | string list (add, remove, reorder) |

```text
 Clean ──edit──► Editing ──focus loss / Enter──► valid?  ──yes──► Saved ──(x-netprints-restart)──► RestartPending
                                                     └──no───► Invalid (message under the field; nothing saved)
```

### Key binding override (`keyboard.bindings[]`)

| Field | Rule |
|---|---|
| `command` | required, a command id; kept when the command is not loaded ("Not loaded" list); an entry without one is kept in the file, listed there, and never costs the other entries; a `null` array element is dropped; an entry that is not an object or has a wrong-typed member is kept as written (raw JSON, written back unchanged), listed as not loaded, reported with its index and command, and never costs the other entries |
| `scope` | `Global`, `Graph`, `ProjectTree` or a comma list; the command's declared scope when written; Global when omitted |
| `add` | gestures added; canonical form; each parsed at load, an unparseable one reported and ignored; none when omitted |
| `remove` | default gestures removed; none when omitted |

The effective binding of a command = (its defaults − `remove`) ∪ `add`. Precedence when two commands claim a gesture
in overlapping scopes: user override, then built-in default, then extension default in load order. A gesture that
breaks ADR-0020's scope rules (a single-key or Shift-only gesture in `Global`; function keys exempt) is refused.

## 4. Run

### Launch profile (the subset NetPrints reads from `Properties/launchSettings.json`)

| Field | Kinds | Rule |
|---|---|---|
| name (the key) | all | case-insensitive; two names differing only by case are an error (NPR002) |
| `commandName` | all | case-sensitive; `Project` and `Executable` run, others are listed disabled with the reason |
| `executablePath` | `Executable` | `%NAME%` and `$(Name)` expanded; with a separator: relative to the working directory; bare: `PATH` |
| `commandLineArgs` | both | expanded, then split with `CommandLineToArgvW` rules into an argument list |
| `workingDirectory` | both | relative to the project folder; absent = the project folder |
| `environmentVariables` | both | expanded; passed to the program only, with `DOTNET_LAUNCH_PROFILE` |

The file is read with comments and trailing commas allowed. `<name>.run.json` files and `My Project/` are ignored.

### Run options (per user and per project, `sessions/<project-key>.json`)

```json
"run": {
  "profile": "Godot editor",
  "configuration": "Debug",
  "profiles": { "Godot editor": { "externalTerminal": false, "captureOutput": false } }
}
```

Never written to `launchSettings.json` (FR-053). A missing block means: the default profile, `Debug`, capture on,
external terminal off.

### Process request (resolved)

| Field | Rule |
|---|---|
| Program | absolute path (Windows: `PATH` with `.exe`, `.com`; macOS `.app` → `Contents/MacOS/<binary>`) |
| Arguments | argument list; `-- <args>` from the CLI replaces the profile's |
| Working directory | absolute |
| Environment | profile variables and `DOTNET_LAUNCH_PROFILE` added; the MSBuild variables NetPrints' locator set removed |
| Capture, input, external terminal | from the run options |

### Run lifecycle

```text
 Idle ──Run──► Resolving ──problem──► Idle (message; NPR code)
                  │ ok
                  ▼
               Building ──fails──► Idle
                  │ ok
                  ▼
               Running ──Stop──► Stopping ──exit or timeout (kill tree)──► Idle
                  │ exits
                  └──────────────────────────────────────────────────────► Idle
```

Unloading a project while an `Executable` run is alive asks whether to leave it running.

## 5. Themes

| Entity | Fields | Rule |
|---|---|---|
| Token (catalogue entry) | key, kind (Color, Brush, Number, Shadow, Shape, Thickness, CornerRadius, GridLength, FontFamily, Duration, Easing), one value per variant or one shared value | every visual element of the editor is a token (ADR-0035 decision 1) |
| Preset | id, label, `base` (`dark` or `light`), optional `semiVariant`, token values | built-in: `netprints.dark`, `netprints.light`, `high-contrast.dark` (Semi `Aquatic`) and `high-contrast.light` (Semi `Desert`), one file per variant; the ids are stored in `darkPreset` and `lightPreset`, so they are part of the settings format; gated by the contrast tests |
| Theme file (`*.nptheme.json`; user files in `<config>/NetPrints/themes/` and extension themes land in G) | `schemaVersion`, `id`, `label`, `base`, `tokens` (sparse), Semi keys | the same format as the embedded presets; unknown token: ignored with a warning; bad value: keeps the base value with a warning; newer schema: skipped; contrast failure: applied with a warning naming the pairs; size capped; no URLs, fonts or images; a Semi key is checked against the resources loaded at runtime |

A preset applies as an overlay into `ThemeDictionaries[Dark]` or `[Light]`, so a Dark preset changes no Light value.
An unknown preset id falls back to the variant's default and is logged.

## 6. C# target

| Entity | Fields | Rule |
|---|---|---|
| `CSharpLanguageVersion` | major, minor; the specified form (`latest`, `latestMajor`, `preview`, `default`, `major.minor`) | parsed by NetPrints, not Roslyn; `MaxKnown` is NetPrints' cap |
| `CSharpTarget` | resolved language version, the specified value, target frameworks, `Check(...)`; `Unconstrained` | one per project, on `ProjectSnapshot` (non-positional `init`), derived again on every snapshot |
| `CSharpFeature` (experimental) | id, minimum version, required types | `Availability`: Available, or Unavailable(reason, fixes) |

Resolution: `latest`, `preview`, `default` → min(the framework's `MaxSupportedLangVersion`, `MaxKnown`) with NPW006; a
version newer than the framework's default is allowed with NPW007; an unknown or newer-than-`MaxKnown` version →
`MaxKnown` with NPW008; multi-target → the minimum version and the common capabilities.

## 7. Node model

| Entity | Fields | Rule |
|---|---|---|
| Typed data pin | `NodeInputDataPin<T>`, `NodeOutputDataPin<T>` | default `default(T)`; no generic-math constraint |
| Pin declaration | property name, key (name without `Pin`, or `[Pin("…")]`), direction, exec or data, type, role, default | generated `DeclarePins()`; analyzers NPA001–NPA009 |
| `PinRole` | `Target`, `Argument`, `Result`, `OutParam`, `Exception` | purity: at least one `Result` or `OutParam` output data pin |
| `out`/`ref` output pin | key `out.data.<parameterName>` | carries the value after the call; the input must connect to a variable |
| Pin-key tracking | `NodePins.Shipped.txt`, `NodePins.Unshipped.txt` per project | one line per `<kind> <direction>.<exec|data|type>.<key>` (type pins are tracked too); NPA010, NPA011 |
| `kindVersion` (new, optional) | int on a node document | written only when the kind's `DataVersion` > 0; migrations run from it (absent = 0) |
| Unknown members | `[JsonExtensionData]` on every document record | written after the known members, ordinal key order; opaque to `format`, `show` and `merge`; the editor's open and save keep them on every record with a model object, not inside the reference records (type, method, variable, value), which it writes from the model's types |

The 8 renamed properties (the first count was 13 renames and 2 overrides) change no stored key; the S0 pin-layout golden changes only in the property-name column (12 lines), every slot is the same. A typed primitive input pin starts at `default(T)`, except `IfElseNode.ConditionPin` and `ForLoopNode.MaxIndexPin`, which start unset so stored graphs stay byte-identical.

## 8. Node search

| Entity | Fields | Rule |
|---|---|---|
| `NodeSearchContext` | graph kind, pin (direction, type), project target, cancellation | the same for every provider |
| `NodeSearchEntry` | id, title (`LocalizedText`), category id, icon id, keywords, `Requires` (experimental), factory `Func<NodeGraph, Node>` or an async prepare step | no node is created until the user picks |
| Provider | id (owner-prefixed), owning node kind, sync `IEnumerable` | built once per registry snapshot; a throwing provider loses only its own rows |
| Ranker | host-only, fixed category order | the default equals the P3a goldens, except the recorded This Variables and This Methods order |

## 9. Templates

| Entity | Fields | Rule |
|---|---|---|
| Project template | id, display name, description, profile id, output type, icon id, files | built-ins `netprints.template.console`, `netprints.template.library` |
| Template file | relative path, content (text or embedded resource) | `NetPrintsProject` replaced in paths and text; `NetPrintsRootNamespace`, `NetPrintsProfileId` symbols |

## 10. Author kit

| Entity | Fields | Rule |
|---|---|---|
| Conformance check | id (`NPK001`–`NPK017`), name, message | each has a fixture extension that fails it with a message naming the rule and the item |
| Verify report | schema version, extension (id, version, `netprintsApi`), host (API version, assemblies), checks (id, status, messages), missing members (assembly, type, member, referenced from) | text or `--format json`; exit 0, 1 or 2 |

## 11. Localization keys

| Key | Owner |
|---|---|
| `Node.<kind>.Title`, `.Description`, `.Keywords`, `Node.<kind>.Pin.<in|out>.<exec|data>.<key>` | node kinds |
| `Category.<id>` | node search categories |
| `Command.<id>.Label`, `.Tooltip`; `Menu.<menuId>`; `Panel.<panelId>.Title` | contributions |
| `Settings.<section>.<key>.Label`, `.Description` | settings sections |

Fallback: the UI culture, its parent, the neutral resx, then the default text (pin labels: the raw key).

## 12. NuGet packages (US12)

Research R32–R35, [contracts/nuget-references.md](contracts/nuget-references.md), ADR-0042.

| Entity | Fields | Rule |
|---|---|---|
| Package source | name, URL, enabled, the `nuget.config` that declares it | read from the `nuget.config` hierarchy of the project folder; enabled sources in file order; never stored by NetPrints; credentials stay with NuGet (`packageSourceCredentials`, credential plugins); logs show the name and the URL without user info, query or fragment |
| Package reference (declared) | id, version or range as written, `VersionOverride`, condition, the file that holds the version (project or `Directory.Packages.props`), editable, read-only reason | read from the project and props files; a version from an MSBuild property, or one of several conditioned items for the id, is read-only (NPW013) |
| Installed package | id, resolved version, direct or transitive, target framework, compile assemblies, dependencies | from `obj/project.assets.json`; transitive packages are read-only |
| Search result | id, versions, description, authors, license, project URL, deprecation, vulnerabilities, compatible frameworks, offline flag | cached in memory 5 minutes per source, operation and arguments; offline results come from the global packages folder |
| Package change | kind (add, update, remove), id, version, `useVersionOverride`, the files it writes with their original bytes | one change at a time per project; applied as one unit; rolled back byte-identical on a failed or cancelled restore |
| Removal impact | package id, the assemblies only it brings, the nodes that use them (graph, node, member) | computed before a removal; proceeding keeps those nodes as unresolved nodes |

```text
Package change:
Idle ──apply──► Writing ──► Restoring ──success──► Refreshing (reflection provider, graph validation) ──► Idle
                                │ restore failed, load threw, or cancelled
                                └──► Rolling back (original bytes) ──► Idle, NPW010
```
