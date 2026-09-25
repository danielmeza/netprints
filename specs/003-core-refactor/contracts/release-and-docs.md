# Contract: Release, packages and docs (sub-phase L)

Source: `docs/research/2026-09-25-release-and-docs/README.md` (owner-approved 2026-09-25), research.md
R18–R19. Reference implementation for the release, local feed and README packing:
`danielmeza/kicad-sharp` (`.github/workflows/release.yml`, `NuGet.config`, `scripts/use-local-libs.sh`,
`Directory.Build.targets`, `.gitignore`). Test obligations (`RL-Txx`) at the end.

**Nothing publishes during P1.** No `v*` tag is pushed, no package reaches nuget.org, Pages and the wiki
are not deployed from a PR. Every job that publishes runs only for a `v*` tag or on `master` with an
owner-set repository variable (§11), so the P1 PR can only run dry runs.

| Artifact | Path |
|---|---|
| Versioning, package metadata | `Directory.Packages.props`, `Directory.Build.props`, `Directory.Build.targets` (new), `eng/PackageReadme.targets` (new) |
| Package icon | `assets/icons/netprints-icon.png` (new; 256×256 PNG extracted from `src/NetPrints.Desktop/NetPrintsLogo.ico`) |
| Packable projects | `src/NetPrints.Core`, `src/NetPrints.Reflection`, `src/NetPrints.Sdk`, `src/NetPrints.Cli` |
| Local feed | `NuGet.config` (new), `local-packages/.gitkeep` (new), `.gitignore`, `scripts/pack-local.sh` (new), `scripts/verify-packages.sh` (new) |
| Editor publish | `src/NetPrints.Desktop/NetPrints.Desktop.csproj`, `src/NetPrints.Desktop/ProjectCheck.cs` (new), `scripts/smoke-desktop.sh` (new), `scripts/archive-desktop.sh` (new) |
| Workflows | `.github/workflows/release.yml`, `docs.yml`, `wiki.yml` (new); `ci.yml` (two new jobs); `.github/release.yml`, `.github/release-notes.md` (new); `.github/dependabot.yml` |
| Docs site | `website/` (new), `docs/index.md`, `docs/guide/`, `docs/contributing/`, `docs/**/_category_.json` (new), `docs/api/` (new), `.config/dotnet-tools.json` (new), `scripts/build-docs.sh` (new) |
| Wiki | `.github/wiki/Home.md`, `.github/wiki/_Sidebar.md` (new) |
| Decision record | `docs/adr/0002-release-and-docs-stack.md` (new) |

Actions are pinned by commit SHA with the version in a comment, as in `ci.yml`. SHAs resolved on 2026-09-25
(`gh api repos/<owner>/<repo>/commits/<tag> --jq .sha`); Dependabot's `github-actions` entry keeps them current.

| Action | Version | SHA |
|---|---|---|
| actions/checkout | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` (as in `ci.yml`) |
| actions/setup-dotnet | v6.0.0 | `a98b56852c35b8e3190ac28c8c2271da59106c68` (as in `ci.yml`) |
| actions/upload-artifact | v7.0.1 | `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` (as in `ci.yml`) |
| actions/download-artifact | v8.0.1 | `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c` |
| actions/setup-node | v6.5.0 | `249970729cb0ef3589644e2896645e5dc5ba9c38` |
| NuGet/login | v1.2.0 | `8d196754b4036150537f80ac539e15c2f1028841` |
| actions/attest | v4.2.2 | `1e69f48acb82d1966a394da916b4c1698aa569d6` |
| softprops/action-gh-release | v3.0.3 | `efb35369e0ad2afab669f228072c1b0d510eae64` |
| actions/configure-pages | v6.0.0 | `45bfe0192ca1faeb007ade9deae92b16b8254a0d` |
| actions/upload-pages-artifact | v5.0.0 | `fc324d3547104276b827a68afc52ff2a11cc49c9` |
| actions/deploy-pages | v5.0.1 | `368f82528645a54fb793d4d04e342629a3f51346` |
| Andrew-Chen-Wang/github-wiki-action | v5.0.6 | `1bbb4280446f9630e8e21a18012cbacf3b0f992e` |

## 1. Versioning — MinVer

`Directory.Packages.props` (new item group):

```xml
<ItemGroup Label="Versioning">
  <!-- Every project's version comes from the nearest `v*` git tag (release contract §1). -->
  <GlobalPackageReference Include="MinVer" Version="8.0.0" />
</ItemGroup>
```

`Directory.Build.props` (new property group; `GlobalPackageReference` is `PrivateAssets=all` by default):

```xml
<PropertyGroup Label="Versioning">
  <MinVerTagPrefix>v</MinVerTagPrefix>
  <MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>
  <!-- A source archive has no .git: MinVer then warns and uses its default version. -->
  <MSBuildWarningsNotAsErrors>$(MSBuildWarningsNotAsErrors);MINVER1001</MSBuildWarningsNotAsErrors>
</PropertyGroup>
```

| Rule | Contract |
|---|---|
| Tagged commit `v1.2.3` | every package, assembly `InformationalVersion` and archive name uses `1.2.3`; `AssemblyVersion` = `1.0.0.0` (MinVer default `{Major}.0.0.0`) |
| Untagged commit | `0.1.0-alpha.0.<height>` until the first tag, then `<next patch>-alpha.0.<height>` |
| Local packs | `-p:MinVerVersionOverride=<version>` (not `-p:Version`: MinVer would still set `PackageVersion`) |
| Removed | `<Version>0.0.7</Version>` and `<Copyright>` in `src/NetPrints.Core/NetPrints.Core.csproj`; no other hard-coded version anywhere |
| Checkout | `fetch-depth: 0` in every job that packs or publishes (`ci.yml` `build-test`, `packages`, `desktop-publish`; `release.yml` `pack`, `desktop`). Shallow clones fall back to the default version without a warning |
| Reading the version in scripts | `dotnet msbuild src/NetPrints.Core/NetPrints.Core.csproj -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release` (after restore) |
| Repository scope | `samples/` (own `Directory.*` files, no CPM) and `legacy/NetPrintsVSIX` (CPM off) do not get MinVer |
| Editor version | the editor's `{NetPrintsSdkVersion}` for new/converted projects is its own `AssemblyInformationalVersion` without the `+<sha>` suffix (project-system.md §1) |

The generated-file header no longer carries a version (project-system.md §3): with MinVer every commit has
a different version, which would rewrite every committed `.netpc.g.cs` on each SDK update and make DF-T26
depend on git height.

## 2. Package metadata — `Directory.Build.props`, `Directory.Build.targets`

`Directory.Build.props` (added; the existing groups stay):

```xml
<PropertyGroup>
  <!-- The four shipped packages opt in (release contract §3); everything else stays unpacked. -->
  <IsPackable>false</IsPackable>
</PropertyGroup>

<PropertyGroup Label="Package metadata">
  <Authors>Daniel Meza, Robin Kahlow</Authors>
  <Product>NetPrints</Product>
  <Copyright>Copyright (c) 2018 Robin Kahlow and NetPrints contributors</Copyright>
  <PackageProjectUrl>https://danielmeza.github.io/netprints/</PackageProjectUrl>
  <RepositoryUrl>https://github.com/danielmeza/netprints</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>
  <PackageTags>netprints;visual-programming;node-graph;blueprints;code-generation</PackageTags>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
</PropertyGroup>
```

`Directory.Build.targets` (new; runs after each project's own properties, so `IsPackable` is final — the
kicad-sharp reason for putting items here):

```xml
<Project>
  <Import Project="eng/PackageReadme.targets" Condition="'$(IsPackable)' == 'true'" />
  <ItemGroup Condition="'$(IsPackable)' == 'true'">
    <None Include="$(MSBuildThisFileDirectory)assets/icons/netprints-icon.png"
          Pack="true" PackagePath="icon.png" Visible="false" />
  </ItemGroup>
</Project>
```

`eng/PackageReadme.targets`: copy of the owner's `readme-craft` skill example
(`~/.claude/skills/readme-craft/references/PackageReadme.targets.example`), adapted to
`danielmeza/netprints`. Hooked into `$(BeforePack)`, it reads the root `README.md`, turns the HTML the
README uses (`<p align>`, `<img>`, `<a>`, `<details>`) into Markdown, rewrites relative images to
`https://raw.githubusercontent.com/danielmeza/netprints/<commit>/…` and relative links to
`https://github.com/danielmeza/netprints/blob/<commit>/…` (commit = `$(SourceRevisionId)` from Source Link,
`master` when empty), writes `$(IntermediateOutputPath)README.md` and packs that file as `README.md`.
Reason: nuget.org drops raw HTML and relative links and loads images only from allow-listed hosts; one
README, generated per commit, instead of a second hand-kept copy. If the example is not available, the same
behaviour is implemented as a `RoslynCodeTaskFactory` inline task of about 60 lines.

The root README already credits the original project; the package descriptions (§3) repeat it in one
sentence so it shows on nuget.org without scrolling.

**XML documentation**: `GenerateDocumentationFile=true` is set in each packable project file (not in
`Directory.Build.targets`: the SDK reads it before `Directory.Build.targets` is imported). `NetPrints.Core`
and `NetPrints.Reflection` add `<NoWarn>$(NoWarn);CS1591</NoWarn>` with the comment `Legacy public API
without XML docs; remove when documented.`, so a missing doc comment is not an error under
warnings-as-errors. No other project suppresses CS1591.

## 3. Packable projects

| Project | Package id | Kind | Project-file additions |
|---|---|---|---|
| `src/NetPrints.Core` | `NetPrints.Core` | library | `IsPackable`, `GenerateDocumentationFile`, `NoWarn CS1591`, `EnablePackageValidation`, `Description` |
| `src/NetPrints.Reflection` | `NetPrints.Reflection` | library | same as Core |
| `src/NetPrints.Sdk` | `NetPrints.Sdk` | MSBuild targets + build tool (project-system.md §2) | `IsPackable`, `IncludeSymbols=false` (no build output: a symbol package would fail with NU5017), `Description` |
| `src/NetPrints.Cli` | `NetPrints.Cli` | dotnet tool, command `netprints` | `IsPackable`, `PackAsTool`, `ToolCommandName=netprints`, `GenerateDocumentationFile`, `Description` |

Descriptions (one paragraph each, ending with the attribution sentence
`NetPrints continues the original NetPrints by Robin Kahlow (https://github.com/RobinKa/netprints).`):

- Core: `The NetPrints graph model and C# translator: classes, methods and node graphs that NetPrints turns into readable C#.`
- Reflection: `Type and member discovery for NetPrints graphs, built on Roslyn.`
- Sdk: `Build-time code generation for NetPrints projects: add this package to an SDK-style .csproj and every *.netpc.json graph is translated into a committed *.netpc.g.cs file before compilation.`
- Cli: `The netprints command-line tool: builds and runs NetPrints projects and converts legacy .netpp projects. Installs as `netprints`.`

`src/NetPrints.Cli/NetPrints.Cli.csproj` gains (kicad-sharp pattern; no explicit `PackageType`, `PackAsTool` sets `DotnetTool`):

```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>netprints</ToolCommandName>
  <PackageId>NetPrints.Cli</PackageId>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

**Package validation**: `EnablePackageValidation=true` on the two libraries only (the tool and the SDK
package have no `lib/` API). `PackageValidationBaselineVersion` is added after the first stable release
(follow-up, not P1).

**Roslyn build host** (roslyn#80127, open): `MSBuildWorkspace` starts `BuildHost-netcore/` from the
application folder, and that folder is only copied for a direct `Microsoft.CodeAnalysis.Workspaces.MSBuild`
reference. `src/NetPrints.Desktop` and `src/NetPrints.Cli` therefore reference the package directly
(an ordinary `PackageReference`, version from `Directory.Packages.props`). RL-T03 and RL-T06 fail if the folder is missing from the tool package or
the editor publish.

Expected `dotnet pack NetPrints.slnx -c Release -o <dir>` output, nothing else:

```text
NetPrints.Cli.<v>.nupkg         NetPrints.Cli.<v>.snupkg
NetPrints.Core.<v>.nupkg        NetPrints.Core.<v>.snupkg
NetPrints.Reflection.<v>.nupkg  NetPrints.Reflection.<v>.snupkg
NetPrints.Sdk.<v>.nupkg
```

## 4. Local feed

`NuGet.config` (new, repository root):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!-- Git-ignored and normally empty; scripts/pack-local.sh fills it with 0.1.0-local.<timestamp>
         packages. An empty folder contributes nothing, so a default restore resolves from nuget.org. -->
    <add key="local-packages" value="local-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
```

`.gitignore` (appended; `*.nupkg` is already ignored):

```gitignore
# Local package feed (scripts/pack-local.sh). The folder is tracked through .gitkeep because NuGet
# fails restore (NU1301) when a configured local source does not exist.
local-packages/*
!local-packages/.gitkeep

# Docs site and API reference build output (scripts/build-docs.sh)
website/node_modules/
website/build/
website/.docusaurus/
website/static/img/*.png
docs/api/_site/
docs/api/reference/
```

`scripts/pack-local.sh` (new, executable, `set -euo pipefail`):

```bash
#!/usr/bin/env bash
#
# Packs every NetPrints package into ./local-packages with a distinct local version, so a restore can
# never silently fall back to (or prefer) a published package.
#
#   scripts/pack-local.sh                   pack and print how to use the packages
#   scripts/pack-local.sh --print-version   pack and print only the version (for scripts and CI)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_VERSION="0.1.0-local.$(date -u +%Y%m%d%H%M%S)"
FEED="$REPO_ROOT/local-packages"
mkdir -p "$FEED"

dotnet pack "$REPO_ROOT/NetPrints.slnx" -c Release -p:MinVerVersionOverride="$LOCAL_VERSION" -o "$FEED" >&2

if [[ "${1:-}" == "--print-version" ]]; then echo "$LOCAL_VERSION"; exit 0; fi

cat <<MSG
Packed NetPrints $LOCAL_VERSION into $FEED

  Tool:     dotnet tool install -g NetPrints.Cli --version $LOCAL_VERSION --add-source "$FEED"
  Project:  <PackageReference Include="NetPrints.Sdk" Version="$LOCAL_VERSION" PrivateAssets="all" />
            (with a nuget.config that lists $FEED)
  Verify:   scripts/verify-packages.sh "$FEED" $LOCAL_VERSION

Nothing is committed: local-packages/ is git-ignored.
MSG
```

`scripts/verify-packages.sh <feed> <version>` (new, executable): runs RL-T01…RL-T04 against the packages in
`<feed>` and exits non-zero on the first failure with a message naming the check. It works in a
`mktemp -d` directory with `NUGET_PACKAGES=<tmp>/nuget` (isolated cache) and never touches the repository:

1. The exact file list of §3 for `<version>`.
2. Each `.nupkg` (read with `unzip`): the `.nuspec` has `<license type="expression">MIT</license>`,
   `<icon>icon.png</icon>`, `<readme>README.md</readme>`, a `<repository type="git" url="https://github.com/danielmeza/netprints" commit="…">`;
   the package contains `icon.png` and a `README.md` with no HTML tag (`grep -E '<[A-Za-z/]'` finds nothing)
   and no relative link or image (`grep -P '\]\((?!https?://|#)'` finds nothing). Core and Reflection contain
   `lib/net10.0/<id>.dll` and `lib/net10.0/<id>.xml`. `NetPrints.Cli` has `<packageType name="DotnetTool" />`
   and `tools/net10.0/any/BuildHost-netcore/`. `NetPrints.Sdk` has `<developmentDependency>true</developmentDependency>`,
   `build/NetPrints.Sdk.props`, `build/NetPrints.Sdk.targets`, `tools/net10.0/NetPrints.Generator.dll`, and no `lib/`.
3. Tool: `dotnet tool install NetPrints.Cli --tool-path <tmp>/tools --version <version> --add-source <feed>`;
   `<tmp>/tools/netprints --version` output contains `<version>`.
4. SDK: copy `samples/HelloWorld/HelloWorld.csproj` and `HelloWorld.Program.netpc.json` (not the `.netpc.g.cs`)
   to `<tmp>/app`; in the copy, drop the `NetPrintsUseLocalSdk` condition and set the `NetPrints.Sdk` version
   to `<version>` (`sed`); write `<tmp>/app/nuget.config` (`<clear/>`, `<feed>` as an absolute path, nuget.org);
   `dotnet build <tmp>/app -c Release` succeeds; `<tmp>/app/HelloWorld.Program.netpc.g.cs` is byte-identical to
   the committed `samples/HelloWorld/HelloWorld.Program.netpc.g.cs`; `dotnet run --project <tmp>/app -c Release --no-build`
   prints `Hello, World!`; `<tmp>/tools/netprints -p <tmp>/app/HelloWorld.csproj -r` prints `Hello, World!`.

## 5. Editor publish and reference assemblies (research R19)

`src/NetPrints.Desktop/NetPrints.Desktop.csproj` gains:

```xml
<PropertyGroup>
  <!-- Release archives are self-contained folders (release contract §5): the editor loads MSBuild and
       Roslyn's build host from disk and compiles against arbitrary assemblies, so no single-file,
       trimming or Native AOT. -->
  <PublishSingleFile>false</PublishSingleFile>
  <PublishTrimmed>false</PublishTrimmed>
  <PublishAot>false</PublishAot>
</PropertyGroup>
```

**Reference assemblies (decision, R19)**: the editor compiles only against what MSBuild resolves for the open
project (R14, FR-011): `ProjectSnapshot.References`, i.e. the reference packs of the installed .NET SDK
(`<sdk root>/packs/Microsoft.NETCore.App.Ref/<ver>/ref/net10.0/*.dll`), packages and project references.
Nothing enumerates the editor's own folder or `RuntimeEnvironment.GetRuntimeDirectory()`; T063 already
deletes `ReferenceAssemblyResolver` (the P0 `FindRuntimeAssemblyPaths` fallback) and `CodeCompiler`.
`Basic.Reference.Assemblies.Net100` is not added. The editor archive is self-contained, but opening, analysing,
building and running a project needs the **.NET 10 SDK**, which includes the runtime a compiled program
needs. Microsoft.Build.Locator only accepts SDKs whose major.minor is not newer than the editor's runtime, so
the self-contained editor needs a 10.0 SDK specifically; a machine with only a newer SDK gets the NPW001
message.

`src/NetPrints.Desktop/ProjectCheck.cs` (new): a headless check that uses the same services as the editor
(Workspace, Serialization, Core; no Avalonia type is touched, no display needed).

```csharp
namespace NetPrints.Desktop;

internal static class ProjectCheck
{
    // NetPrints.Desktop --check-project <path.csproj> [--run]
    public static Task<int> RunAsync(string projectPath, bool run, TextWriter output, CancellationToken cancellationToken);
}
```

`Program.Main`: after `MsBuildRegistration.EnsureRegistered()`, when `args[0] == "--check-project"` it
returns `ProjectCheck.RunAsync(...)` and never builds the Avalonia app.

| Step | Output line (stdout, in this order) | Failure → exit code |
|---|---|---|
| start | `NetPrints <informational version without +sha>` | — |
| registration | `msbuild: <MSBuild path> (<SDK version>)` | no SDK → `error NPW001: …`, **3** |
| `IProjectSystem.LoadAsync` | `project: <full path>`, `references: <n> (System.Console: <path>)` | `ProjectSystemException` → message, **1** |
| `ProjectPersistence.LoadAsync` | `graphs: <n>` | document errors → canonical error lines, **1** |
| translate + `CodeAnalysisSession.AnalyzeAsync` | `analysis: <e> errors, <w> warnings`, then each error in canonical format | `e > 0` → **1** |
| `IProjectSystem.BuildAsync` | `build: succeeded` / `build: failed` + messages | failed → **1** |
| `--run` (only for `OutputType` Exe): `GetRunCommand` + `IProcessRunner` | the program's stdout, then `run: exit <code>` | non-zero → **4** |
| bad arguments | usage line on stderr | **2** |

On Windows the app is `WinExe`, so stdout is not attached to a console; only the exit code is meaningful there.
The install page documents `--check-project` as "check that NetPrints can open and build your project".

`scripts/smoke-desktop.sh <publish-dir>` (new, executable, Linux and macOS): RL-T06.

1. Layout: the apphost `NetPrints.Desktop`, `NetPrints.Core.dll`, `NetPrints.Editor.dll`,
   `NetPrints.Workspace.dll`, `System.Private.CoreLib.dll` (self-contained) exist as separate files;
   `BuildHost-netcore/` exists; no `*.dll` is missing because of single-file bundling.
2. `dotnet build src/NetPrints.Generator` (Debug, the in-repo SDK mode of project-system.md §2.1 reads
   `bin/$(Configuration)` with `Debug` as default).
3. `env -u DISPLAY -u WAYLAND_DISPLAY "<publish-dir>/NetPrints.Desktop" --check-project samples/HelloWorld/HelloWorld.csproj --run`
   exits 0; output has `references: <n>` with `n > 0`, a `System.Console` path containing
   `packs/Microsoft.NETCore.App.Ref/`, `analysis: 0 errors`, `build: succeeded`, `Hello, World!`, `run: exit 0`.
4. `git diff --exit-code -- samples/` (the check regenerated nothing).

`scripts/archive-desktop.sh <publish-dir> <version> <rid>` (new): writes `artifacts/desktop/NetPrints-<version>-<rid>.tar.gz`
(`tar -czf`, preserves the executable bit) for `linux-x64` and `osx-arm64`, and `NetPrints-<version>-win-x64.zip`
(`zip -r -X`) for `win-x64`; the archive has one top-level folder `NetPrints-<version>-<rid>/`. It also copies
`LICENSE` and a `README.txt` (three lines: what it is, "requires the .NET 10 SDK to open and build projects",
the docs URL) into that folder.

## 6. CI additions — `.github/workflows/ci.yml`

`build-test` checkout gets `fetch-depth: 0` (PS-T05 packs). Two new jobs, Linux only (constitution
Development Workflow):

```yaml
  packages:
    name: Packages (local feed)
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - name: MinVer version of an untagged commit
        run: |
          dotnet restore src/NetPrints.Core
          ver=$(dotnet msbuild src/NetPrints.Core/NetPrints.Core.csproj -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release)
          echo "MinVer: $ver"
          [[ "$ver" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-alpha\.0\.[0-9]+)?$ ]]
      - name: Pack into local-packages
        id: pack
        run: echo "version=$(scripts/pack-local.sh --print-version)" >> "$GITHUB_OUTPUT"
      - name: Verify packages, tool and SDK from the local feed
        run: |
          [[ "${{ steps.pack.outputs.version }}" =~ ^0\.1\.0-local\.[0-9]{14}$ ]]
          scripts/verify-packages.sh local-packages "${{ steps.pack.outputs.version }}"

  desktop-publish:
    name: Self-contained editor smoke (linux-x64)
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - name: Publish
        run: >
          dotnet publish src/NetPrints.Desktop -c Release -r linux-x64 --self-contained
          -p:PublishSingleFile=false -p:PublishTrimmed=false -o out/linux-x64
      - name: Smoke (headless project check)
        run: scripts/smoke-desktop.sh out/linux-x64
```

Both jobs use the NuGet cache step of `build-test` (same key). They are not required to publish anything.

## 7. Release workflow — `.github/workflows/release.yml`

```yaml
name: Release

# Packs the NuGet packages and the self-contained editor archives, publishes the packages to nuget.org
# with Trusted Publishing (OIDC, no stored API key) and creates a GitHub Release with checksums and
# provenance attestations. Push a tag like `v1.2.3` to release. workflow_dispatch and pull requests that
# touch the release inputs are dry runs: they build everything and publish nothing.

on:
  push:
    tags: ['v*']
  workflow_dispatch:
  pull_request:
    branches: [master]
    paths:
      - '.github/workflows/release.yml'
      - '.github/release.yml'
      - '.github/release-notes.md'
      - 'scripts/**'
      - 'eng/**'
      - 'Directory.Build.props'
      - 'Directory.Build.targets'
      - 'Directory.Packages.props'
      - 'src/**/*.csproj'

permissions:
  contents: read

concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false

env:
  DOTNET_NOLOGO: 1
  DOTNET_CLI_TELEMETRY_OPTOUT: 1

jobs:
  pack:
    name: Pack
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.ver.outputs.version }}
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0   # MinVer needs the tags and the history
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - name: Version (MinVer)
        id: ver
        run: |
          dotnet restore NetPrints.slnx
          ver=$(dotnet msbuild src/NetPrints.Core/NetPrints.Core.csproj -t:MinVer -getProperty:MinVerVersion -p:Configuration=Release)
          if [ "$GITHUB_REF_TYPE" = "tag" ] && [ "$ver" != "${GITHUB_REF_NAME#v}" ]; then
            echo "::error::MinVer computed $ver for tag $GITHUB_REF_NAME"; exit 1
          fi
          echo "version=$ver" >> "$GITHUB_OUTPUT"
      - name: Pack
        run: dotnet pack NetPrints.slnx -c Release --no-restore -o artifacts/packages
      - name: Verify packages, tool and SDK
        run: scripts/verify-packages.sh artifacts/packages "${{ steps.ver.outputs.version }}"
      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: nupkg
          path: |
            artifacts/packages/*.nupkg
            artifacts/packages/*.snupkg
          if-no-files-found: error

  desktop:
    name: Editor (${{ matrix.rid }})
    needs: pack
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: true
      matrix:
        include:
          - { os: ubuntu-latest, rid: linux-x64, smoke: true }
          - { os: ubuntu-latest, rid: win-x64, smoke: false }
          - { os: macos-latest, rid: osx-arm64, smoke: true }   # macOS signs the apphost ad hoc
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - name: Publish (self-contained folder, not single-file, not trimmed)
        run: >
          dotnet publish src/NetPrints.Desktop -c Release -r ${{ matrix.rid }} --self-contained
          -p:PublishSingleFile=false -p:PublishTrimmed=false -o out/${{ matrix.rid }}
      - name: Smoke (headless project check)
        if: matrix.smoke
        run: scripts/smoke-desktop.sh out/${{ matrix.rid }}
      - name: Archive
        run: scripts/archive-desktop.sh out/${{ matrix.rid }} "${{ needs.pack.outputs.version }}" ${{ matrix.rid }}
      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: desktop-${{ matrix.rid }}
          path: artifacts/desktop/*
          if-no-files-found: error

  assets:
    name: Checksums
    needs: [pack, desktop]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          path: dist
          merge-multiple: true
      - name: SHA256SUMS.txt
        working-directory: dist
        run: |
          sha256sum *.nupkg *.snupkg *.tar.gz *.zip > SHA256SUMS.txt
          sha256sum -c SHA256SUMS.txt
          cat SHA256SUMS.txt
      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: release-assets
          path: dist/*
          if-no-files-found: error

  publish-nuget:
    name: Publish to NuGet.org
    needs: [pack, assets]
    runs-on: ubuntu-latest
    # Only for real version tags (v*); never for workflow_dispatch or pull requests.
    if: startsWith(github.ref, 'refs/tags/v')
    environment: release
    permissions:
      id-token: write   # GitHub OIDC token for NuGet Trusted Publishing
      contents: read
    steps:
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          dotnet-version: 10.0.x
      - uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          name: nupkg
          path: packages
      - name: Check the one-time setup
        env:
          NUGET_USER: ${{ secrets.NUGET_USER }}
        run: |
          if [ -z "$NUGET_USER" ]; then
            echo "::error::Secret NUGET_USER is not set (release contract §11, step 1)"; exit 1
          fi
      - name: NuGet login (OIDC -> short-lived API key)
        id: login
        uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
        with:
          user: ${{ secrets.NUGET_USER }}   # nuget.org profile name, not the email
      # Pushing *.nupkg also pushes each matching *.snupkg.
      - name: Push packages
        run: >
          dotnet nuget push "packages/*.nupkg"
          --api-key ${{ steps.login.outputs.NUGET_API_KEY }}
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate

  github-release:
    name: GitHub Release
    needs: [pack, assets]
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/v')
    permissions:
      contents: write
      id-token: write
      attestations: write
      artifact-metadata: write
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          sparse-checkout: .github
      - uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          name: release-assets
          path: dist
      - uses: actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6 # v4.2.2
        with:
          subject-path: |
            dist/*.nupkg
            dist/*.tar.gz
            dist/*.zip
      - uses: softprops/action-gh-release@efb35369e0ad2afab669f228072c1b0d510eae64 # v3.0.3
        with:
          files: dist/*
          body_path: .github/release-notes.md
          generate_release_notes: true
          prerelease: ${{ contains(github.ref_name, '-') }}
          fail_on_unmatched_files: true
```

| Rule | Contract |
|---|---|
| Dry run | `workflow_dispatch` (once the file is on `master`) and the `pull_request` trigger run `pack`, `desktop`, `assets`; `publish-nuget` and `github-release` are skipped by their `if`; no job reads a secret; no environment is entered |
| Failure isolation | nothing is published unless `pack`, all three `desktop` legs and `assets` succeed |
| Re-run of a tag | `--skip-duplicate` makes a partial publish re-runnable; the GitHub Release step updates the existing release |
| Missing setup | `publish-nuget` fails with the message of its first step when `NUGET_USER` is missing; `github-release` is independent of it (the kicad-sharp shape) |
| Verification by users | `sha256sum -c SHA256SUMS.txt`; `gh attestation verify <file> -R danielmeza/netprints` |

`.github/release-notes.md` (new; `body_path`, the generated notes follow it):

```markdown
## Downloads

| Platform | File |
|---|---|
| Linux x64 | `NetPrints-<version>-linux-x64.tar.gz` |
| Windows x64 | `NetPrints-<version>-win-x64.zip` |
| macOS Apple silicon | `NetPrints-<version>-osx-arm64.tar.gz` |

The editor archives are self-contained: they run without a .NET runtime. Opening, building and running
NetPrints projects needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), which also
provides the runtime your compiled programs need.

Packages: `dotnet tool install -g NetPrints.Cli` (command `netprints`) and
`<PackageReference Include="NetPrints.Sdk" Version="…" PrivateAssets="all" />`.

**These builds are not code-signed.** Windows SmartScreen shows "Windows protected your PC": choose
*More info* → *Run anyway*. On macOS, open it once, then *System Settings* → *Privacy & Security* →
*Open Anyway* (or run `xattr -dr com.apple.quarantine NetPrints-<version>-osx-arm64`).

Verify a download: `sha256sum -c SHA256SUMS.txt` and `gh attestation verify <file> -R danielmeza/netprints`.
```

`.github/release.yml` (new; categories for the generated notes):

```yaml
changelog:
  exclude:
    labels: [ignore-for-release]
  categories:
    - title: Breaking changes
      labels: [breaking-change]
    - title: Features
      labels: [enhancement]
    - title: Fixes
      labels: [bug]
    - title: Documentation
      labels: [documentation]
    - title: Dependencies
      labels: [dependencies]
    - title: Other changes
      labels: ['*']
```

Dependabot already applies `dependencies`; `bug`, `documentation` and `enhancement` are GitHub defaults;
`breaking-change` and `ignore-for-release` are created once by the owner (§11).

## 8. Docs site — `website/` (Docusaurus 3)

Node 24 (`website/.nvmrc` = `24`). `website/package.json` (exact versions; `package-lock.json` committed;
`npm ci` everywhere):

```json
{
  "name": "netprints-website",
  "private": true,
  "scripts": {
    "start": "docusaurus start",
    "build": "docusaurus build",
    "serve": "docusaurus serve",
    "typecheck": "tsc"
  },
  "dependencies": {
    "@docusaurus/core": "3.10.2",
    "@docusaurus/preset-classic": "3.10.2",
    "@mdx-js/react": "3.1.1",
    "clsx": "2.1.1",
    "prism-react-renderer": "2.4.1",
    "react": "19.3.0",
    "react-dom": "19.3.0"
  },
  "devDependencies": {
    "@docusaurus/module-type-aliases": "3.10.2",
    "@docusaurus/tsconfig": "3.10.2",
    "@docusaurus/types": "3.10.2",
    "typescript": "5.9.3"
  },
  "engines": { "node": ">=20.0" }
}
```

Versions checked on npm on 2026-09-25. TypeScript stays on 5.9 (7.0 is not verified with `@docusaurus/tsconfig`);
the implementer takes newer patch versions if `npm view <pkg> version` reports them, keeping the majors.

`website/docusaurus.config.ts` (normative parts):

```ts
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import repoLinks from './src/remark/repo-links.mjs';

const config: Config = {
  title: 'NetPrints',
  tagline: 'Visual programming for .NET that compiles to readable C#',
  favicon: 'img/favicon.png',
  url: 'https://danielmeza.github.io',
  baseUrl: '/netprints/',
  organizationName: 'danielmeza',
  projectName: 'netprints',
  trailingSlash: true,
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'warn',
  markdown: {
    format: 'detect',                       // .md = CommonMark, only .mdx = MDX
    hooks: {onBrokenMarkdownLinks: 'throw', onBrokenMarkdownImages: 'throw'},
  },
  presets: [
    ['classic', {
      docs: {
        path: '../docs',
        routeBasePath: '/',
        include: ['**/*.md'],
        exclude: ['api/**', '**/prototypes/**'],
        numberPrefixParser: false,             // routes keep the file names (adr/0002-…, research/2026-09-25-…)
        editUrl: 'https://github.com/danielmeza/netprints/edit/master/docs/',
        beforeDefaultRemarkPlugins: [[repoLinks, {repo: 'https://github.com/danielmeza/netprints', branch: 'master'}]],
        // no sidebarPath: the sidebar is generated from folders, _category_.json and front matter
      },
      blog: false,
      theme: {customCss: './src/css/custom.css'},
    } satisfies Preset.Options],
  ],
  themeConfig: {
    navbar: {
      title: 'NetPrints',
      logo: {alt: 'NetPrints', src: 'img/logo.png'},
      items: [
        {type: 'docSidebar', sidebarId: 'defaultSidebar', label: 'Docs', position: 'left'},
        {href: 'https://danielmeza.github.io/netprints/api/', label: 'API', position: 'left', target: '_self'},
        {href: 'https://github.com/danielmeza/netprints/releases', label: 'Download', position: 'right'},
        {href: 'https://github.com/danielmeza/netprints', label: 'GitHub', position: 'right'},
      ],
    },
    footer: {
      style: 'dark',
      copyright: 'MIT licence. NetPrints continues the original NetPrints by Robin Kahlow (github.com/RobinKa/netprints).',
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
```

The API link is absolute because `/api/` is not a Docusaurus route (a relative `to` would fail
`onBrokenLinks`). `static/img/logo.png` and `static/img/favicon.png` come from `assets/icons/netprints-icon.png`
(copied by `scripts/build-docs.sh`, not committed twice).

`website/src/remark/repo-links.mjs` (new, ~40 lines): a remark plugin that rewrites a Markdown link or image
whose relative target resolves **outside `docs/`** (for example `../../specs/003-core-refactor/plan.md` from
an ADR, or `../../src/NetPrints.Core/…`) to `<repo>/blob/<branch>/<repo-relative path>` (links) or
`<repo>/raw/<branch>/<path>` (images), so the source files stay GitHub-friendly and the site has no broken
links. Links inside `docs/` are left to Docusaurus.

**Content layout** (`docs/` is the single source; `specs/` is not part of the site):

| Path | Site | Notes |
|---|---|---|
| `docs/index.md` | `/netprints/` | new; front matter `slug: /`, `sidebar_position: 1`; what NetPrints is, links to install, guide, API |
| `docs/guide/` | Guide | new, `_category_.json` `{ "label": "Guide", "position": 2 }`; `install.md` (T120), `projects.md`, `graph-format.md`, `extensions.md` (T104) |
| `docs/contributing/` | Contributing | new, position 3; `releasing.md` (local feed, release process, the owner steps of §11) |
| `docs/adr/` | Decisions | position 8; `README.md` is the category index; ADRs 0001, 0002 |
| `docs/research/` | Research notes | position 9; `_category_.json` with `link: { "type": "generated-index", "description": "Dated research notes behind design decisions. Each records what was true on its date and is not updated." }`; each `<date>-<topic>/README.md` is a page; `prototypes/` excluded |
| `docs/api/` | not a Docusaurus page | DocFX source (§9), excluded |
| `specs/**` | not published | links to specs are rewritten to GitHub by `repo-links` |

`scripts/build-docs.sh` (new; used by CI and locally, so both build the same thing):

```bash
#!/usr/bin/env bash
# Builds the docs site into website/build: Docusaurus pages, the DocFX API reference at /api/ and
# the graph JSON Schema at /schemas/.
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"
mkdir -p website/static/img
cp assets/icons/netprints-icon.png website/static/img/logo.png
cp assets/icons/netprints-icon.png website/static/img/favicon.png
dotnet tool restore
dotnet restore NetPrints.slnx
dotnet docfx docs/api/docfx.json
(cd website && npm ci && npm run build)
mkdir -p website/build/api website/build/schemas
cp -r docs/api/_site/. website/build/api/
cp schemas/*.schema.json website/build/schemas/
test -f website/build/api/index.html
cmp schemas/netpc.v1.schema.json website/build/schemas/netpc.v1.schema.json
```

`website/static/img/logo.png` and `favicon.png` are git-ignored (§4).

## 9. API reference — DocFX

`.config/dotnet-tools.json` (new):

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "docfx": { "version": "2.81.0", "commands": ["docfx"], "rollForward": false }
  }
}
```

`docs/api/docfx.json` (new):

```json
{
  "metadata": [
    {
      "src": [
        { "src": "../../src", "files": ["NetPrints.Core/NetPrints.Core.csproj", "NetPrints.Reflection/NetPrints.Reflection.csproj"] }
      ],
      "dest": "reference",
      "properties": { "Configuration": "Release" },
      "namespaceLayout": "nested",
      "enumSortOrder": "declaringOrder"
    }
  ],
  "build": {
    "content": [
      { "files": ["reference/**.yml"] },
      { "files": ["index.md", "toc.yml"] }
    ],
    "resource": [{ "files": ["images/**"] }],
    "output": "_site",
    "template": ["default", "modern"],
    "xref": ["https://learn.microsoft.com/en-us/dotnet/.xrefmap.json"],
    "globalMetadata": {
      "_appTitle": "NetPrints API",
      "_appName": "NetPrints API",
      "_appFooter": "MIT licence · <a href=\"https://danielmeza.github.io/netprints/\">NetPrints docs</a>",
      "_enableSearch": true,
      "_disableContribution": false,
      "_gitContribute": { "repo": "https://github.com/danielmeza/netprints", "branch": "master" }
    },
    "sitemap": { "baseUrl": "https://danielmeza.github.io/netprints/api/" }
  }
}
```

`docs/api/index.md` (landing: packages, link back to the site) and `docs/api/toc.yml` (`- name: API`,
`href: reference/`). The API reference covers the packable libraries only; adding a library to it is one
line in `metadata.src.files`.

## 10. Docs and wiki workflows

`.github/workflows/docs.yml` (new):

```yaml
name: Docs

on:
  pull_request:
    branches: [master]
  push:
    branches: [master]
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: docs-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: Build site
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
      - uses: actions/setup-node@249970729cb0ef3589644e2896645e5dc5ba9c38 # v6.5.0
        with:
          node-version-file: website/.nvmrc
          cache: npm
          cache-dependency-path: website/package-lock.json
      - name: Build (Docusaurus + DocFX + schemas)
        run: scripts/build-docs.sh
      - uses: actions/upload-pages-artifact@fc324d3547104276b827a68afc52ff2a11cc49c9 # v5.0.0
        with:
          path: website/build

  deploy:
    name: Deploy to GitHub Pages
    needs: build
    # Only from master, and only once the owner enabled Pages (release contract §11, step 3).
    if: github.event_name != 'pull_request' && github.ref == 'refs/heads/master' && vars.PUBLISH_DOCS == 'true'
    runs-on: ubuntu-latest
    permissions:
      pages: write
      id-token: write
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/configure-pages@45bfe0192ca1faeb007ade9deae92b16b8254a0d # v6.0.0
      - id: deployment
        uses: actions/deploy-pages@368f82528645a54fb793d4d04e342629a3f51346 # v5.0.1
```

The checkout needs no history (no packing). The `$schema` URL (`NetPrintsSchema.V1Url`,
document-format.md §2.3) is served by this deployment at
`https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json`; the reader keeps ignoring `$schema`.

`.github/dependabot.yml` gains:

```yaml
  - package-ecosystem: "npm"
    directory: "/website"
    schedule:
      interval: "weekly"
    groups:
      docusaurus:
        patterns: ["@docusaurus/*"]
      npm:
        patterns: ["*"]
        exclude-patterns: ["@docusaurus/*"]
```

The existing `nuget` entry at `/` is expected to cover `.config/dotnet-tools.json` (docfx); check it in the
Dependabot log after merge and add a note to research R18 if it does not.

`.github/wiki/Home.md` (new):

```markdown
# NetPrints

The documentation lives on the website: **https://danielmeza.github.io/netprints/**. This wiki only
points there, so there is one copy of every page. Edits made here are overwritten by the next sync.

- [Install](https://danielmeza.github.io/netprints/guide/install/)
- [Guide](https://danielmeza.github.io/netprints/guide/projects/)
- [API reference](https://danielmeza.github.io/netprints/api/)
- [Downloads (Releases)](https://github.com/danielmeza/netprints/releases)
- [Discussions](https://github.com/danielmeza/netprints/discussions)
```

`.github/wiki/_Sidebar.md` (new): the same five links as a list under `**NetPrints**`.

`.github/workflows/wiki.yml` (new):

```yaml
name: Wiki

on:
  push:
    branches: [master]
    paths: ['.github/wiki/**']
  workflow_dispatch:

permissions:
  contents: write

jobs:
  sync:
    name: Sync .github/wiki to the wiki
    # Only once the owner enabled the wiki and created its first page (release contract §11, step 4).
    if: github.repository == 'danielmeza/netprints' && vars.PUBLISH_WIKI == 'true'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - name: Check that the wiki repository exists
        id: wiki
        run: |
          if git ls-remote "https://github.com/${GITHUB_REPOSITORY}.wiki.git" >/dev/null 2>&1; then
            echo "ready=true" >> "$GITHUB_OUTPUT"
          else
            echo "::notice::The wiki is not initialized yet; create its first page (release contract §11, step 4)."
          fi
      - if: steps.wiki.outputs.ready == 'true'
        uses: Andrew-Chen-Wang/github-wiki-action@1bbb4280446f9630e8e21a18012cbacf3b0f992e # v5.0.6
        with:
          path: .github/wiki
          strategy: init
          preprocess: false
```

No pull-request trigger: nothing reaches the wiki from a PR.

## 11. Owner one-time steps (none is needed for the P1 PR)

Every workflow tolerates these steps being undone: PR and dry runs never need them; the gated jobs skip
(`vars.*`) or fail with a message naming the step (`NUGET_USER`).

1. **nuget.org Trusted Publishing**: nuget.org → account (or the organisation that will own the packages) →
   *Trusted Publishing* → add a policy: owner `danielmeza`, repository `netprints`, workflow file `release.yml`,
   environment `release`. Then add the repository (or `release` environment) secret `NUGET_USER` = the
   nuget.org profile name (not the email). Optional: ask nuget.org for the `NetPrints.` ID prefix
   reservation (account@nuget.org).
2. **Environment `release`**: Settings → Environments → New `release`; optionally required reviewers and a
   deployment tag rule `v*`.
3. **GitHub Pages**: Settings → Pages → Source = *GitHub Actions*; then Settings → Variables → Actions →
   `PUBLISH_DOCS` = `true`. The next push to `master` (or a manual Docs run) deploys; the `$schema` URL
   resolves from then on.
4. **Wiki**: Settings → Features → Wikis on (restrict editing to collaborators); create the first page in the
   web UI (this creates `netprints.wiki.git`); then `PUBLISH_WIKI` = `true`.
5. **Labels**: `gh label create breaking-change --color B60205 --repo danielmeza/netprints` and
   `gh label create ignore-for-release --color EDEDED --repo danielmeza/netprints`.
6. **First release** (after P1 is merged, not part of it): `git tag v0.1.0 && git push origin v0.1.0`.

## 12. README and ADR

README (T120): badges — CI, Release (`https://img.shields.io/github/v/release/danielmeza/netprints?include_prereleases`),
NuGet `NetPrints.Sdk` (flat-container badge of the readme-craft packaging guide), Docs
(`https://img.shields.io/badge/docs-site-blue` → the site), License. An **Install** section right after the
introduction with three rows: *Editor* (download table linking the release archives, "requires the .NET 10
SDK to open and build projects", unsigned note linking the install page), *Command-line tool*
(`dotnet tool install -g NetPrints.Cli`, then `netprints -p MyApp.csproj -r`), *In a project*
(`dotnet add package NetPrints.Sdk`). The build-from-source part stays, shortened, under "Contributing" with a
link to `docs/contributing/releasing.md`.

`docs/adr/0002-release-and-docs-stack.md` (T121), the 0001 format (Status, Context, Decision, Consequences):
Status "Accepted (2026-09-25)"; Context = no packages, hard-coded version, no docs site, single-file breaks the
old reference fallback; Decision = the picks and runner-ups of the research §10 summary (MinVer; plain Actions
+ scripts; self-contained folder archives with SHA256SUMS and attestations; Trusted Publishing; Docusaurus 3 +
DocFX `/api/`; wiki as a pointer; MSBuild-resolved references, no `Basic.Reference.Assemblies`);
Consequences = the SDK requirement, the owner steps of §11, unsigned builds, Node in the docs toolchain,
follow-ups (Velopack installers and auto-update, code signing, package validation baseline, SchemaStore).

## 13. Test obligations

| ID | Case |
|---|---|
| RL-T01 | `dotnet pack NetPrints.slnx -c Release` produces exactly the seven files of §3 (no test, generator, workspace or editor package) |
| RL-T02 | Package contents and metadata per §4 `verify-packages.sh` step 2 (licence expression, icon, README without HTML or relative links, repository commit, XML docs, tool type and build host, SDK layout) |
| RL-T03 | The tool installs from the local feed into an isolated tool path and cache, reports the packed version, and builds and runs a HelloWorld copy (`Hello, World!`) |
| RL-T04 | A fresh SDK-style HelloWorld restoring `NetPrints.Sdk` from the local feed builds, generates a `.netpc.g.cs` byte-identical to the committed sample's, and prints `Hello, World!` |
| RL-T05 | Versions: MinVer on the checked-out commit matches `^\d+\.\d+\.\d+(-alpha\.0\.\d+)?$`; `pack-local.sh` versions match `^0\.1\.0-local\.\d{14}$`; on a tag the pack job fails unless MinVer equals the tag without `v` |
| RL-T06 | Self-contained linux-x64 publish: separate-file layout with `BuildHost-netcore/`, `--check-project … --run` with no display prints references from `packs/Microsoft.NETCore.App.Ref/`, 0 analysis errors, a successful build and `Hello, World!`; `samples/` unchanged (§5) |
| RL-T07 | Grep test in `tests/NetPrints.Core.Tests/Architecture/NoRuntimeDirectoryReferencesTests.cs`: no file under `src/` contains `GetRuntimeDirectory`, `FindRuntimeAssemblyPaths` or `Basic.Reference.Assemblies` |
| RL-T08 | `scripts/build-docs.sh` succeeds; `website/build/index.html`, a page per research note, `website/build/adr/0002-release-and-docs-stack/index.html`, `website/build/api/index.html` and the `NetPrints.Graph.Node` API page exist; `website/build/schemas/netpc.v1.schema.json` equals the committed schema; no `specs/` route exists; adding a link to a missing page makes the build fail (shown once in the PR) |
| RL-T09 | `NetPrintsSchema.V1Url` equals `https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json` and its path below `/netprints/` equals the schema's path in `website/build` (asserted by `build-docs.sh` and DF-T24's `$id`) |
| RL-T10 | The release workflow's pull-request run on the P1 PR is green for `pack`, `desktop` (3 legs), `assets`, with `publish-nuget` and `github-release` skipped, no secrets read; the `release-assets` artifact holds 7 package files, 3 archives and a `SHA256SUMS.txt` that verifies |
| RL-T11 | `actionlint` (v1.7.x, run locally) reports nothing for all workflows; `docs.yml` `deploy` and `wiki.yml` `sync` are skipped on the PR and on `master` while their variables are unset |
| RL-T12 | `.github/release-notes.md` states that builds are unsigned, the SmartScreen and macOS *Open Anyway* steps, the .NET 10 SDK requirement and the checksum/attestation commands; `.github/release.yml` has the six categories of §7 |
