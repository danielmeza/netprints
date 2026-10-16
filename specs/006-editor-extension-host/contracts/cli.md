# Contract: command-line surfaces

FR-036, FR-079, FR-080, FR-107, FR-108; ADR-0015 (exit codes), ADR-0037 (`run`, in
[run-profiles.md](run-profiles.md)); research R17, R25. Output rules as in `docs/guide/cli.md`: results on stdout,
diagnostics on stderr, `--format json` where stated.

## 1. `netprints new`

```text
netprints new <template> [--name <name>] [--output <folder>] [--force]
netprints new --list [--format text|json]
```

| Option | Rule |
|---|---|
| `<template>` | a template's short name (`console`, `library`, or an extension template's short name) or its full id (`netprints.template.console`) |
| `--name` | the project name; default: the output folder's name; must be a valid C# identifier after the `dotnet new` name rules (dots allowed) |
| `--output` | default: `./<name>`; created if missing |
| `--force` | allow a non-empty output folder (files are overwritten); without it, a non-empty folder exits 1 |
| `--list` | one row per template: short name, id, display name, profile id, owner; built-ins first, then per-user extensions in load order |

- Templates come from the registry: the built-ins and those of the extensions in the user extension directories
  (`netprints.extensionPaths`, `NETPRINTS_EXTENSION_PATH`). Project-declared extensions do not apply (there is no
  project yet).
- Replacement: `NetPrintsProject` → the name in paths and text; `NetPrintsRootNamespace` → the name made a valid
  namespace; `NetPrintsProfileId` → the template's profile id.
- Output: `created <folder> from <template id>` and the next step (`netprints build <folder>`).
- Exit codes: 0 created; 1 the folder is not empty (without `--force`), a write failed; 2 an unknown template, an
  invalid name or option.
- `--format json` for `--list`: `{ "templates": [ { "shortName", "id", "displayName", "profileId", "owner" } ] }`.

## 2. `dotnet new` templates (`NetPrints.Templates` package)

| Short name | Creates | Same files as |
|---|---|---|
| `netprints-console` | a console NetPrints project | `netprints new console`, editor New project › Console |
| `netprints-library` | a library NetPrints project | `netprints new library`, editor New project › Library |
| `netprints-extension` | an extension project and its kit test project | — (builds, passes the kit and packs a `NetPrintsExtension` package with no edits) |

- `sourceName` is `NetPrintsProject`; symbols `RootNamespace` and `ProfileId` map to the literal tokens above.
- A test installs the packed template package into a temporary hive (`--debug:custom-hive`), runs each project
  template, and compares the tree byte for byte with `netprints new` (SC: US15 independent test).

## 3. `netprints extension verify`

```text
netprints extension verify <folder|package.nupkg> [--host <folder>] [--format text|json]
```

- Input: an extension folder (holding `netprints-extension.json`) or a `NetPrintsExtension` package.
- `--host`: the folder of the host assemblies to check against; default: the assemblies the CLI tool ships.
- Checks: NPK001 manifest, NPK002 packaging, NPK003 type identity, NPK017 API compatibility (every type and member the
  extension references from a NetPrints assembly exists in the host, read through metadata, nothing loaded).
- Text output: one line per check (`passed`, `failed`, `skipped`), then each failure's messages, then each missing
  member as `<assembly>: <type>.<member> (referenced from <extension type>)`.
- JSON output:

```json
{
  "schemaVersion": 1,
  "extension": { "id": "…", "version": "…", "netprintsApi": "0.3" },
  "host": { "apiVersion": "0.3", "assemblies": ["NetPrints.Core", "…"] },
  "checks": [ { "id": "NPK017", "name": "apiCompatibility", "status": "failed", "messages": ["…"] } ],
  "missingMembers": [ { "assembly": "…", "type": "…", "member": "…", "referencedFrom": "…" } ]
}
```

- Exit codes: 0 every check passed; 1 a check failed; 2 the path does not exist or is neither a folder nor a package,
  or an invalid option.

## 4. `NetPrints.Desktop --profile <id>`

- `--profile <id>` names a project profile (the built-in one or one an extension contributes) for this session's start
  page: New project preselects it, and the template list is filtered to it with a "Show all" switch. It is not
  remembered and does not change an existing project's profile.
- An unknown id opens the start page unfiltered with a message naming the id and the known ids.
- With a project path as well, the project opens and the profile is ignored with an info message.
- The samples gallery filter is P3c's.

## 5. Tests that pin this contract

- `netprints new`: list (text and JSON), create each built-in template and build it, unknown template exits 2, non-empty
  folder exits 1, an extension template from a user extension directory (`Fx` fixture).
- `dotnet new` byte comparison for `netprints-console` and `netprints-library`; `netprints-extension` builds, passes the
  kit and packs (the `packages` CI job).
- `verify`: a passing fixture package; one fixture per failing check (NPK001–NPK003, NPK017) with its message; JSON
  output schema; exit codes.
- Desktop: `--profile` known, unknown, and with a project path (headless start-page tests plus one E2E scenario,
  `StartWithProfile`).
