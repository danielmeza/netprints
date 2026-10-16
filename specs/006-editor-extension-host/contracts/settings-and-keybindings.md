# Contract: settings pages and user key bindings

FR-039–FR-050, FR-066–FR-069 (the Appearance page), ADR-0040, ADR-0020, ADR-0034, ADR-0035; research R6, R20, R21,
R24. File layout in [data-model.md](../data-model.md) §3.

## 1. Commands

| Id | Label | Default gesture | Menu |
|---|---|---|---|
| `netprints.command.openSettings` | Settings | `Ctrl+,` (Global) | File › Settings; palette |
| `netprints.command.keyboardShortcuts` | Keyboard shortcuts | P3a's gesture, unchanged | Help; opens the Settings document on the Keyboard shortcuts page |

`keyboardShortcuts` keeps P3a's read-only shortcuts sheet as the printable view and adds "Edit shortcuts", which opens
the page.

## 2. The Settings document

- One document per window, id `netprints.document.settings`; reopening activates it.
- Page list on the left, in this order: General, Appearance, Keyboard shortcuts, Run, Extensions; under Extensions, one
  page per loaded extension section (or the extension's own page), ordered by extension name.
- A search box filters fields across every page by label and description (case-insensitive, culture-aware); pages
  with no match are hidden; an empty search shows everything.
- The "Restart to apply" bar appears when a field marked `x-netprints-restart` changed in this session, with a
  "Restart now" button that runs the normal exit path (the unload prompt applies).

### Built-in pages (FR-044: every user setting is on a page)

| Page | Fields |
|---|---|
| General | `netprints.language` (drop-down of the shipped cultures plus "System"; restart), `netprints.enableAnimations` |
| Appearance | `netprints.editor.theme` (Dark, Light, System), `darkPreset`, `lightPreset` (drop-downs of the presets for that variant; applied at once), the preset preview strip |
| Keyboard shortcuts | §4 |
| Run | `run.stopTimeoutSeconds` (1–600), `run.linuxTerminal` (Linux only; empty = detect) |
| Extensions | `netprints.extensionPaths` (folder list; restart), `netprints.trustedProjects` (file list with remove), the list of loaded extensions with their version and source |

## 3. Generated pages

- A section registered with `AddSettings` and no custom page gets a page generated from its exported schema
  ([data-model.md](../data-model.md) §3 "Settings field").
- Label: the `Settings.<section>.<key>.Label` resource of the owner, else the JSON property name split into words.
  Help text: `Settings.<section>.<key>.Description`, else the schema `description`.
- A value is validated when the field loses focus or the user presses Enter, by the section's generated validator. An
  invalid value shows the validator's message under the field and is not saved. A valid value is saved at once
  through the store and applied at once, unless the field needs a restart.
- The store notifies the section's `IOptionsMonitor<T>` after a write; the editor and extensions read settings only
  through it.
- A section of an extension that is not loaded is not shown and is written back unchanged.

## 4. The Keyboard shortcuts page

- One row per registered command, built-in and contributed: label, owner, scope, default gestures, current gestures,
  and a marker when the user changed it or when a user binding overrides an extension default.
- Filter by text (label, id, owner) or by recording a gesture (rows whose current gestures contain it).
- Per row: "Add gesture" (records the next key chord), "Remove" (per gesture), "Reset". Page actions: "Reset all".
- Recording checks, in order: parse (ADR-0034 boundary parse); scope rules (ADR-0020: no single-key or Shift-only
  gesture in `Global`, F1–F24 exempt; refused with the reason); conflicts with any command whose effective gestures
  contain it in an overlapping scope. A conflict asks first and names the other command; accepting removes the gesture
  from it (a `remove` entry), cancelling changes nothing.
- A change applies at once: key bindings, menus, tooltips, the command bar, the palette and the shortcuts sheet read
  the new effective set from the next registry snapshot.
- "Not loaded": overrides whose command is not registered, kept in the file, with "Remove".
- Invalid gesture strings in the file are listed with the reason and ignored.
- macOS shows `Ctrl` as `Cmd`, as for defaults; the file keeps the canonical form.

## 5. `settings.json` sections this contract adds

```json
"netprints": { "language": "" },
"keyboard": { "bindings": [ { "command": "<id>", "scope": "Global", "add": ["<gesture>"], "remove": ["<gesture>"] } ] },
"run": { "stopTimeoutSeconds": 5, "linuxTerminal": "" },
"extensions": { "netprints.editor": { "darkPreset": "netprints.dark", "lightPreset": "netprints.light" } }
```

- Overrides are differences from the defaults: a command with no change has no entry; "Reset" deletes its entry;
  "Reset all" writes an empty list.
- Precedence: user override, then built-in default, then extension default in load order (FR-050).
- `darkPreset` and `lightPreset` are strings; an unknown id falls back to the variant's default with a log entry, and
  the other appearance keys are kept (FR-069).

## 6. Schema file

- `schemas/settings.v1.schema.json`: the built-in sections, exported by `JsonSchemaExporter` with the research R6
  keywords, written canonically; a golden test fails when a section changes without the file.
- The docs' settings table (`docs/guide/editor/settings.md`) is generated from it, with a staleness test.
- Extensions' section schemas are exported at runtime for their generated pages; they are not files.

## 7. Errors and fallbacks

| Case | Behaviour |
|---|---|
| `settings.json` is not valid JSON | start with defaults; one message; the file is copied to `settings.json.unreadable-<yyyyMMddHHmmss>` before the next write |
| A section fails validation at start | that section uses its defaults; the message names each key and reason (NPX014 for extensions) |
| A newer `schemaVersion` | read what is known, preserve the rest, warn once (P1 rule) |
| A write fails (read-only file, disk full) | the field shows the error; the in-memory value is reverted |

## 8. Tests that pin this contract

- Store: unknown sections and keys round-trip byte-identically; the unreadable copy is made once; section change
  notification reaches `IOptionsMonitor<T>`.
- Validators: each built-in section's generated validator rejects each out-of-range value with a message naming the key.
- Pages: a headless test opens every page; the schema-to-field mapping covers each row of the mapping table; search
  filters across pages; an invalid value is not saved; a restart field raises the bar.
- An extension section (`NetPrints.TestExtension`) gets a generated page under Extensions.
- Key bindings: precedence, conflict accept and cancel, scope-rule refusal, "Not loaded", reset one and all, and a
  restart round trip (SC-006); the keyboard-only E2E scenario `RebindShortcuts` (SC-007).
- Presets: the Appearance page applies a preset with no restart, and the overlay test shows 0 Light changes for a Dark
  preset (SC-009).
