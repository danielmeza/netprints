# 0017: Experimental API opt-in is per project and per id

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`). Amends the opt-in clause of ADR-0010 decision 3, which
proposed one repo-wide `NetPrintsExperimentalOptIn` property holding every `NPXE` id. The owner delegated the
call: do what is best for the project.

## Context

`[Experimental]` diagnostics are errors by design, and consumers must opt in. The repository forbids
suppressions (ADR-0003) because they hide defects. An experimental opt-in hides no defect: it is the
acknowledgement .NET expects from a consumer of unstable API. A blanket repo-wide opt-in, though, hides which
in-repo components depend on unstable API, and it makes the fixture extensions unrepresentative of third-party
authors, who must opt in themselves.

## Decision

1. Keep `[Experimental]` with `NPXE0001`–`NPXE0004`. It is the only compile-time stability signal, and
   `NetPrints.Core` is already on NuGet.
2. Each project that uses an experimental API declares only the ids it uses, as MSBuild items in its own csproj:
   `<NetPrintsExperimentalOptIn Include="NPXE0003" />`. `Directory.Build.targets` turns the items into `NoWarn`
   on one line, and that stays the only `<NoWarn>` in the repository. There is no repo-wide opt-in and nothing is
   inherited.
3. A defining assembly declares an opt-in only if the build shows the diagnostic for its own usage. This is
   verified in sub-phase B, and the observed compiler behaviour is recorded in the implementation notes.
4. Fixture and test extensions opt in per project like any consumer. The extensions guide still tells external
   authors to use `<NoWarn>$(NoWarn);NPXE000n</NoWarn>` (or a narrow `#pragma`), the standard .NET way.
5. `SourceHygieneTests` enforces:
   - the only `<NoWarn>` is the `Directory.Build.targets` line;
   - every opt-in item is an id declared in `ExperimentalApiIds`, so unknown or stale ids fail and graduating an
     API forces its opt-ins out;
   - no `#pragma warning disable NPXE*`;
   - no `.editorconfig` or globalconfig severity entries for `NPXE` ids.
6. The probe test (AP-T02) is unchanged: an external compilation without the opt-in gets the error.
7. ADR-0003's exception-ledger row points to this ADR.

## Consequences

- Each dependency on unstable API is visible in its csproj and in review diffs.
- Consuming projects carry a few extra lines.
- Graduation cleanup is enforced by a test, so a stable API cannot keep a stale opt-in.
