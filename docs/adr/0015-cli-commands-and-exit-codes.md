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

## Amendment 1: tool and SDK version skew (Review C, 2026-09-30)

`generate` renders with the generator compiled into the installed tool, while `dotnet build` renders with the
generator of the project's `NetPrints.Sdk` package. When the two versions differ, a `regen --check` in CI would
fail on any release that changes the emitted C#, and `generate` would write output that the next build rewrites.

- **Mechanism.** The package's `build/NetPrints.Sdk.props` sets the MSBuild property `NetPrintsSdkVersion` to the
  name of the package's version folder (`<packages>/netprints.sdk/<version>/build/`). The CLI's project system
  reads it from the evaluated project (`ProjectSystemOptions.ExtraProperties`). No pack-time substitution is needed.
- **Skipped for the in-repo SDK.** With `NetPrintsUseLocalSdk=true` (the samples) the property stays empty and
  nothing is compared: those projects import the generator built from the same checkout.
- **Comparison.** Build metadata after `+` is ignored; anything else that differs (including a prerelease label)
  is a mismatch. An empty property (no SDK package) is not compared.
- **Plain `generate`.** Prints `warning: ...` to stderr naming both versions and continues; exit code unchanged.
- **`generate --check`.** Prints `error: ...` to stderr naming both versions and how to align them (`dotnet tool
  update NetPrints.Cli --version <sdk version>`, or set the `PackageReference` to the tool's version), and exits 1
  without generating.
- **Rejected.** Exec'ing the project's own generator host for `--check` would make the check exact, but it ties the
  tool to the host's private command line, which ADR-0009 keeps internal.
