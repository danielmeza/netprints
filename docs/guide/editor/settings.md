---
sidebar_position: 9
---

# Settings

The editor keeps your settings in one `settings.json` file. The file is described by the
[settings schema](https://danielmeza.github.io/netprints/schemas/settings.v1.schema.json), which editors such as VS Code use for
completion and checking.

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
