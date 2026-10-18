---
sidebar_position: 9
---

# Settings

The editor keeps your settings in one `settings.json` file. The file is described by the
[settings schema](https://danielmeza.github.io/netprints/schemas/settings.v1.schema.json), which editors such as VS Code use for
completion and checking.

## The Settings document

Open it with **File › Settings**, with `Ctrl+,` or from the command palette. It opens as a tab, and opening it again
switches to the open tab. It needs no project, so you can change a setting from the start page.

The page list on the left holds **General**, **Appearance**, **Run** and **Extensions**. The pages of the extensions that
have settings are listed under Extensions, ordered by the extension's name. The fields of the selected page are on the
right. Each field has a label, a help text and, when the setting needs a restart, the note **Requires restart**.

| Page | What is on it |
|---|---|
| General | `netprints.language` and `netprints.enableAnimations` |
| Appearance | `netprints.editor.theme` and the Dark and Light presets |
| Run | `run.stopTimeoutSeconds` and `run.linuxTerminal` |
| Extensions | `netprints.extensionPaths`, `netprints.trustedProjects` and the list of loaded extensions with their version and the folder they came from |

Every setting in the table at the end of this page is on one of these pages, and nothing is settable only by editing the
file.

## Fields

A page is built from the setting's schema, so a setting always has the editor that fits its type.

| Setting type | Editor |
|---|---|
| Yes or no | A check box |
| Whole or decimal number | A text box that checks the text is a number; the allowed range is checked when you save |
| Text | A text box |
| Text with a fixed set of values | A drop-down |
| Path to a file or a folder | A text box with **Browse…** |
| List of text | A list where you add, remove and move entries; a list of paths also has **Browse…** |

## Search

The box above the page list filters the fields of every page by label and description, ignoring case. A page with no
matching field disappears from the list, and Extensions stays while one of the pages under it matches. Clearing the box
shows everything again.

## Saving and validation

A value is checked when its field loses focus or you press Enter. A list, a check box and a drop-down are checked as soon
as you change them.

- A value that is not valid shows the reason under the field and is not saved. The file keeps the last good value.
- A valid value is saved to `settings.json` at once, and applied at once unless the field says **Requires restart**.
- A value that did not change writes nothing.

## Restart

When you save a field that needs a restart, the bar **Restart to apply** appears at the top of the document. **Restart now**
leaves the editor the way **File › Exit** does, so the prompt about unsaved work still appears. The setting is already saved,
so it applies the next time you start the editor.

## Extension pages

An extension that registers a settings section gets a page under Extensions, without writing any user interface. Its
fields come from the same schema rules as above; the label is the property name in words and the help text is the
property's description. A section of an extension that is not loaded is not shown, and it stays in the file unchanged.

## An unreadable settings file

If `settings.json` cannot be read, the editor starts with the default value of every setting. Before the first save it
copies the unreadable file to `settings.json.unreadable-<timestamp>` next to it, so nothing you had written is lost, and
then writes a new file. Unknown sections and keys in a readable file are always kept as they are.

## All built-in settings

The table is generated from the schema; do not edit it by hand.

<!-- settings:begin -->

| Key | Type | Default | Allowed | Restart | Description |
|---|---|---|---|---|---|
| `netprints.extensionPaths` | list of string | empty | a folder path | Yes | Folders searched for extensions. Each subfolder that holds a manifest is loaded at the next start |
| `netprints.trustedProjects` | list of string | empty | a file path |  | Project files whose extensions you allowed to load |
| `netprints.enableAnimations` | boolean | `true` |  |  | Turns the interface animations on. Turn them off to reduce motion |
| `netprints.language` | string | empty |  | Yes | The interface language as a culture name such as es-MX. Empty uses the system language |
| `run.stopTimeoutSeconds` | integer | `5` | 1 to 600 |  | Seconds a stopped program has to exit before its process tree is killed |
| `run.linuxTerminal` | string | empty | up to 512 characters; matches `^[^\x00-\x1F\x7F]*$` |  | Linux only: the terminal command that hosts a run in an external terminal. Empty detects one |
| `netprints.editor.theme` | string | `dark` | `dark`, `light`, `system` |  | The theme variant the editor starts in |
| `netprints.editor.darkPreset` | string | `netprints.dark` | up to 128 characters; matches `^[a-z0-9]+([._-][a-z0-9]+)*$`; choices from `netprints.presets.dark` |  | The id of the preset the Dark variant uses |
| `netprints.editor.lightPreset` | string | `netprints.light` | up to 128 characters; matches `^[a-z0-9]+([._-][a-z0-9]+)*$`; choices from `netprints.presets.light` |  | The id of the preset the Light variant uses |

<!-- settings:end -->
