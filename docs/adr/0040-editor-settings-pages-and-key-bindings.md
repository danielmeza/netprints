# 0040: Editor settings: one store, typed sections, generated pages and user key bindings

## Status

Accepted (2026-10-15, P3 spec, `specs/006-editor-extension-host/`, clarifications 3 and 4). Decided autonomously on the
owner's standing instruction for P3 (decide and record). It settles the question ADR-0031 decision 7 left to the P3
spec, how options relate to the settings store, and amends that decision: the configuration binding generator is not
used for settings.

## Context

- Settings live in `settings.json` in the per-user config folder (ADR-0033), read and written by `JsonFileSettingsStore`
  through `ISettingsStore`. Extensions declare their own sections with `ExtensionSettingsDescriptor<T>` and
  `IExtensionBuilder.AddSettings`, under the experimental id NPXE0002. No view shows extension settings today.
- ADR-0031 decision 7 adopted the configuration binding generator and `[OptionsValidator]` validators, and left to the
  P3 spec how `IConfiguration` relates to the store. ADR-0034 forbids binding smart enums through `IConfiguration`, and
  ADR-0035 decision 7 keeps preset choices as strings because an unknown enum value discards a whole section.
- P3 adds settings pages for the editor and for extensions, user-defined key bindings (deferred by P3a to these pages),
  theme presets (ADR-0035) and run options (ADR-0037). The AOP plan's S10 slice exports a JSON schema of the settings.

## Decision

1. **One persisted source.** `settings.json`, through the existing settings store, is the only place settings are
   persisted. No `IConfiguration` provider reads it in the editor, and no second settings file is added.
2. **Typed sections.** Each section, built-in or contributed by an extension, is a typed options class read with the
   source-generated JSON contexts (no reflection serialization) and checked by source-generated `[OptionsValidator]`
   validators, which the composition calls at start. An invalid section keeps its defaults, and the message names the
   key. The store notifies a section's typed options when a page or another writer changes it.
3. **Preservation.** Sections and keys the running editor does not know (an extension that is not loaded, a newer
   editor's key) are written back unchanged. An unreadable file is copied aside before the next write.
4. **Generated pages by default.** A section without a custom view gets a settings page generated from its exported JSON
   schema: toggles for booleans, numeric boxes with ranges, text boxes, drop-downs for enumerated strings, path pickers
   and string lists, with descriptions as help text and validator messages under the field. An extension may contribute
   its own page view through its view resources, under an experimental id; the conformance kit scans that view for
   literals a theme cannot reach.
5. **User key bindings** are the `keyboard` section of `settings.json`: per command id and scope, the gestures the user
   added or removed relative to the defaults. Gestures are parsed at this boundary (ADR-0034). A binding for a command
   that is not loaded is kept. Precedence: the user's override, then built-in defaults, then extension defaults in load
   order (ADR-0020: built-ins register first). User bindings follow ADR-0020's scope rules.
6. **Strings for open sets.** Settings whose valid values can grow (preset ids, profile ids, language) are strings parsed
   at the boundary, never enums, so an unknown value falls back for that key only.

## Alternatives rejected

- **`IConfiguration` over `settings.json` with the binding generator.** Two parse paths for one file (bind on read,
  serialize on write) that disagree on arrays (configuration merges arrays by index) and on value typing, and a second
  model of the same data next to the extension settings API that already ships.
- **A separate `keybindings.json`.** It would duplicate the store's preservation, corrupt-file handling and schema
  export for a list of overrides that fits in one section.
- **Custom views only.** Every extension would write UI for a handful of settings, and each view would be one more place
  for untokenized literals.
- **Generated pages only.** Engine paths and pickers (an Unreal project, a Godot executable) need richer editors than a
  schema can describe.

## Consequences

- The configuration binding generator is not added for settings; `[OptionsValidator]` validators are.
- The settings schema (S10) becomes a published file with a golden test, and the docs list the built-in settings from
  it.
- Extensions keep NPXE0002 for sections; the page contribution kind and the custom page view get their own experimental
  ids, assigned in the P3 plan.
- A future hot reload of settings from disk, or a settings sync, plugs into the store's change notification.
