# 0015: The `netprints` CLI: command surface and exit-code contract

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`, research R2–R5).

## Context

P1 shipped `netprints` as a 64-line CommandLineParser program with two flags (`-p/--project-path`, `-r/--run`)
and exit codes 0/1/2/3; `--version` and `--help` exit 2 (P1 deferral). The constitution names
Spectre.Console.Cli for the CLI. P2 adds catalogs, graph checks and git integration, and U1 needs
`netprints generate` for its Unreal loop. The SDK's build runs the internal `NetPrints.Generator` host
(ADR-0009), which must not depend on a globally installed tool.

## Decision

- **Commands.** `build`, `run`, `generate` (alias `regen`), `migrate`, `catalog`, `format`, `show`, `merge`,
  `git-install`, each with help and examples. Project commands take a project file, a directory with exactly one
  project file, or nothing (current directory).
- **Exit codes.** 0 success; 1 the operation failed or a check found differences or conflicts; 2 invalid usage;
  3 no compatible .NET SDK; 4 internal error. `run` returns the program's own code after a successful build.
  `--help`, `-h` and `--version` exit 0; `--version` prints `NetPrints.Cli <informational version>`.
- **No compatibility shim.** The P1 flags are removed; using them is a usage error (2) whose message names the
  replacement. The tool is 0.x with one release, and a shim would freeze a surface nobody depends on yet.
- **Generation.** `generate` runs the `NetPrints.Generation` library in process with the project's extensions
  and profile, like the build; `--check` writes nothing and fails on stale files. The SDK keeps execing the
  internal generator host.
- **Migrations.** `migrate` ships as the command plus a per-file schema-version report; it writes nothing while
  schema v1 is the only version. Real migrations arrive with schema v2.
- **Output.** Plain text when redirected or `NO_COLOR` is set; results and diagnostics on stdout, logs on stderr.
- **Composition.** Spectre `CommandApp` with Microsoft.Extensions.DependencyInjection behind a type registrar;
  CommandLineParser is removed.

## Consequences

- Scripts written against 0.1.x's flags must switch to `netprints build`/`netprints run`; the release notes and
  the install guide say how.
- CI can tell a crash (4) from a failed build (1) and uses `--check` modes as gates.
- Every command is testable in process with fake project systems and process runners.
