# 0005: Release, packaging and docs stack

## Status

Accepted (2026-09-25).

## Context

Before this phase, NetPrints had no published packages: `src/NetPrints.Core/NetPrints.Core.csproj`
carried a hard-coded `<Version>`/`<Copyright>`, nothing was `IsPackable`, and there was no local or
public feed to develop against. There was no documentation site either: user guides, the API
surface and architecture decision records lived only as files in the repository, unlinked and
unsearchable, and the wiki (if ever enabled) had no defined relationship to them. Separately, moving
the desktop editor to a self-contained, per-RID publish (needed so users don't have to install a
matching .NET runtime themselves) broke the single-file layout the old reference-assembly fallback
assumed. Phase L (`contracts/release-and-docs.md`) had to pick a versioning scheme, a package shape
and feed, a release pipeline, a publish strategy for the self-contained editor that keeps live
analysis correct, and a docs/wiki stack — all exercisable by the P1 pull request's CI without
publishing anything.

## Decision

- **Versioning**: [MinVer](https://github.com/adamralph/minver) 8.0.0 as a `GlobalPackageReference`,
  `v` tag prefix, minimum major.minor `0.1`. Local packs pass `MinVerVersionOverride` explicitly,
  because a global `<Version>` does not stop MinVer computing `PackageVersion` on its own.
- **Pipeline**: plain GitHub Actions workflows plus repository scripts (`pack-local.sh`,
  `verify-packages.sh`, `smoke-desktop.sh`, `archive-desktop.sh`, `build-docs.sh`), run identically
  in CI and locally, rather than a build-orchestration tool.
- **Packages**: four packages — `NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Sdk`,
  `NetPrints.Cli` (the `netprints` dotnet tool) — all under one `NetPrints.` id prefix. The package
  README is generated at pack time from the root `README.md` (HTML converted to Markdown, relative
  links pinned to the packed commit), because nuget.org drops HTML and relative links from a package
  README.
- **Publishing**: nuget.org [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC) from a `release` environment — no stored API key.
- **Desktop distribution**: self-contained per-RID folder archives (`.tar.gz` for Linux/macOS,
  `.zip` for Windows), never single-file or trimmed, with a `SHA256SUMS.txt` and GitHub build
  provenance attestations. Signed installers with auto-update (Velopack) are a follow-up.
- **References inside a self-contained editor**: MSBuild-resolved references only — the same
  `packs/Microsoft.NETCore.App.Ref/` the installed SDK gives `dotnet build` — not
  `Basic.Reference.Assemblies`. That package is one fixed BCL set with no XML documentation and
  would let live analysis in the editor disagree with what an actual build compiles against.
- **Docs site**: [Docusaurus 3](https://docusaurus.io/) under `website/`, reading `../docs` directly
  (no copy step), with a DocFX-generated `/api/` reference for the packable libraries. Research
  notes publish as a dated, "not updated" category; `specs/` is never published — links to specs
  point at GitHub instead.
- **Wiki**: a two-page pointer synced from `.github/wiki/` by `github-wiki-action`, gated by
  `vars.PUBLISH_WIKI` and never triggered by a pull request, so there is exactly one copy of the
  documentation and the wiki cannot drift from it.
- **Publishing gates**: every publishing side effect — GitHub Pages, the wiki sync, nuget.org, the
  GitHub Release — is gated by a repository variable or a `v*` tag, so CI (including a release dry
  run) exercises the whole pipeline on every pull request without publishing anything.

### Editor → Generator project reference (superseded by ADR-0009)

> **Superseded 2026-09-28**: the `ReferenceOutputAssembly="false"` + `HintPath` arrangement this
> subsection describes broke a clean-clone, RID-published build (`CS0234`) and could silently
> compile against a stale Generator DLL. [ADR-0009](0009-generation-library-split.md) replaces it by
> extracting the shared code into `NetPrints.Generation`, a normal library both `NetPrints.Generator`
> and `NetPrints.Editor` reference with a plain `ProjectReference`. The rest of this ADR (versioning,
> packaging, publishing, docs site, wiki) is unaffected and still applies; this subsection and its
> "do not simplify" warning in Consequences are kept only as the historical record of why the old
> shape existed.

`src/NetPrints.Editor` calls `NetPrints.Generator.GraphCodeGenerator` directly (`ClassEditorViewModel`/
`MainEditorViewModel`), so it needs a real reference to `NetPrints.Generator` — an `OutputType=Exe` project
that also runs standalone via `dotnet exec` from `NetPrints.Sdk.targets`. A self-contained,
RID-published `dotnet publish src/NetPrints.Desktop` failed through this reference in three stages,
because the .NET SDK treats any referenced `Exe` project as a sibling to also publish for the same
RID/self-contained-ness — including transitively, on its own build node, regardless of
`ReferenceOutputAssembly` metadata on an intermediate reference: NETSDK1152 (duplicate apphost and
`runtimeconfig.json` at the same publish path), then NETSDK1150 (a documented SDK check against
exactly this shape — a library-like `Exe` project referenced by a self-contained, RID-published
app), then NETSDK1067. `ValidateExecutableReferencesMatchSelfContained=false`, the documented
NETSDK1150/1151 workaround, did not stop it.

**Decision**: `NetPrints.Generator.csproj` sets `UseAppHost=false` (correct regardless of the publish
issue: Generator is never run as its own apphost, only `dotnet exec`'d or referenced as a library).
`NetPrints.Editor.csproj`'s `ProjectReference` to it is `ReferenceOutputAssembly="false"`
(build-order only — mirrors `NetPrints.Sdk.csproj`'s own reference to the same project — and removes
Generator from the SDK's "referenced executable" publish graph entirely), paired with a plain
`<Reference Include="NetPrints.Generator">` with a `HintPath` to its normal, non-RID build output
(`..\NetPrints.Generator\bin\$(Configuration)\net10.0\NetPrints.Generator.dll`) for the actual
compile-time and copy-to-output dependency.

## Consequences

- The .NET 10 SDK is a hard requirement to open or build a NetPrints project — not just to run a
  compiled one — documented in the README, `docs/guide/install.md` and the release notes.
- Editor builds are unsigned; users see SmartScreen and macOS Gatekeeper warnings and must verify
  checksums or attestations themselves until code signing is set up (a follow-up).
- The docs toolchain now depends on Node.js (Docusaurus) in addition to .NET.
- The owner has six one-time manual steps (release contract §11) before packages, Pages or the wiki
  publish anything; every workflow tolerates them being undone (skip, or fail naming the step).
- **Superseded by [ADR-0009](0009-generation-library-split.md)**: the Editor → Generator
  `ReferenceOutputAssembly="false"` + `HintPath` reference broke a clean-clone RID-published build
  (`CS0234`) and could silently compile against a stale Generator DLL. ADR-0009 removes it by moving
  the shared code into `NetPrints.Generation`, a library both projects reference normally.
- Follow-ups: Velopack installers and auto-update, code signing, a package-validation baseline
  beyond the two libraries, and registering the graph schema with SchemaStore.
