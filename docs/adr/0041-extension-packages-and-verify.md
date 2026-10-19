# 0041: Extension packages, `netprints extension verify` and the author workflow

## Status

Accepted (2026-10-18, P3 spec, `specs/006-editor-extension-host/`, research R25, FR-079 to FR-081). Decided autonomously
on the owner's standing instruction for P3 (decide and record). P3c may change the package layout after its feed spike
(installing an extension from a feed or a file is P3c's, FR-095).

## Context

- ADR-0010 gave extension authors a test harness and a conformance suite, but nothing to hand to another person: an
  extension was a folder with a manifest and its assemblies, and nothing checked that a build made for one NetPrints API
  still matched the host that would load it.
- A check of the API a build references must not load the extension: loading runs its code, fails at the first missing
  member, and cannot inspect a package built for a newer host.
- The sample Godot extension (FR-037) must be built and checked from the packed packages only, and every extension
  repository needs the same build, test and verify steps.

## Decision

1. **Extension package.** An extension package is a `.nupkg` of package type `NetPrintsExtension` that holds the folder
   the loader reads (`netprints-extension.json`, the extension's assemblies and resources) under `extension/`. The
   `netprints-extension` template sets this up inline in the extension project, so authors see and can change it. The
   package has no dependencies and no build output of its own: the host supplies the NetPrints assemblies.
2. **`netprints extension verify <folder|package>`** runs the checks that need no test code: NPK001 (manifest), NPK002
   (packaging; for a package, also the package type and the `extension/` folder), NPK003 (type identity) and NPK017
   (API compatibility). NPK017 reads every type and member reference the extension assembly makes to a NetPrints contract
   assembly with `System.Reflection.Metadata` and looks each up in the NetPrints assemblies of the host folder
   (`--host`; by default the assemblies the CLI ships), by name and signature, through base types and interfaces.
   Nothing is loaded. Each missing member is reported with the extension type whose code references it.
3. **Report and exit codes.** Text, or `--format json` with a `schemaVersion`. Exit 0 when every check passed, 1 when a
   check failed, 2 when the path does not exist or is neither a folder nor a package, or an option is invalid
   (ADR-0015). A folder without a readable manifest is a failed NPK001, not a usage error.
4. **The checks live in the kit.** `ExtensionVerifier` in `NetPrints.Extensibility.Testing` runs the verification;
   the CLI only formats it. NPK017 is not part of the conformance suite (it needs the host folder, not a harness).
5. **Reusable author workflow.** `.github/workflows/netprints-extension.yml` is a `workflow_call` workflow that restores,
   builds, runs the kit tests, packs the extension and runs `verify` on the package. Its inputs are the extension folder
   and an optional artifact holding the packed NetPrints packages, used as a local feed. The sample Godot extension's CI
   calls it.

## Alternatives rejected

- **Load the extension to find missing members.** It stops at the first failure, runs the extension's code, and cannot
  inspect a package that targets a newer host.
- **Verify inside the editor only.** Authors and CI have no editor; the check must run from a command line.
- **A separate `netprints-verify` tool.** One CLI keeps one install, one exit-code contract and one set of options.
- **Compare against an API list file.** A second artifact to keep in step with the assemblies; reading the assemblies
  themselves cannot drift.

## Consequences

- NPK017 compares names and signatures, not visibility, so a reference to an internal member the host exposes by
  accident passes; the public API files remain the gate for what the host may break.
- A member the host removed that the extension reaches only through a base type outside the NetPrints assemblies is not
  seen; the base class library is not part of the check.
- `NetPrints.Cli` references `NetPrints.Extensibility.Testing`, so the tool ships the contract assemblies it checks
  against.
- The package layout is recorded here so P3c can change it with a new amendment instead of a silent edit.
