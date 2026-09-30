# Install

There are three ways to get NetPrints, depending on what you want to do.

## Editor

Download the archive for your platform from the [latest release](https://github.com/danielmeza/netprints/releases/latest)
and unpack it — there is no installer.

| Platform | Archive |
|---|---|
| Linux (x64) | `NetPrints-<version>-linux-x64.tar.gz` |
| Windows (x64) | `NetPrints-<version>-win-x64.zip` |
| macOS (Apple Silicon) | `NetPrints-<version>-osx-arm64.tar.gz` |

Each archive is self-contained (it bundles its own .NET runtime to run), but you still need the
[.NET 10 SDK](https://dotnet.microsoft.com/download) installed to **open and build** a project: the
editor evaluates and builds your `.csproj` with MSBuild, the same way `dotnet build` would. Just
**running** an already-compiled graph only needs the .NET runtime the SDK includes.

## Command-line tool

```bash
dotnet tool install -g NetPrints.Cli
netprints build MyApp.csproj
netprints run MyApp.csproj
```

`netprints build` compiles the graph to C# and builds the project. `netprints run` does the same and then runs the program.
See the [command-line guide](cli.md) for all commands and options. Requires the .NET 10 SDK for the same reason as the editor.

## In a project

```bash
dotnet add package NetPrints.Sdk
```

Adding the `NetPrints.Sdk` package to a `.csproj` makes every `*.netpc.json` graph file in the
project compile to a committed `*.netpc.g.cs` file as part of the build — see the
[Projects guide](projects.md) for the project model and what gets committed. Requires the .NET 10
SDK.

## Requirements

Building or opening a NetPrints project — with the editor, the CLI, or the SDK package — needs the
**.NET 10 SDK specifically** (10.0.100 or later; a newer major SDK is not enough). This is a
constraint of [Microsoft.Build.Locator](https://github.com/microsoft/MSBuildLocator), which the
editor and CLI use to find and run MSBuild: it skips SDKs whose major/minor version is newer than
the runtime the tool itself runs on, and the self-contained editor bundles the .NET 10 runtime. If
only a newer SDK is installed, the editor and CLI report `NPW001` and exit rather than silently
using the wrong one.

Running a project that has already been compiled only needs the .NET runtime that the SDK includes
— for example, a machine that only runs a published NetPrints app doesn't need the SDK.

## Unsigned builds

Editor builds are not code-signed. Expect a platform warning the first time you run one, and expect
to click through it — this is normal for an unsigned open-source build, not a sign that the archive
is broken:

- **Windows**: SmartScreen will say the app is unrecognized. Click **More info**, then **Run
  anyway**.
- **macOS**: Gatekeeper will refuse to open the app from Finder ("cannot be opened because the
  developer cannot be verified"). Clear the quarantine attribute from a terminal, then run the
  editor from there:

  ```bash
  xattr -dr com.apple.quarantine NetPrints-<version>-osx-arm64
  cd NetPrints-<version>-osx-arm64
  ./NetPrints.Desktop
  ```

Code signing is tracked as a follow-up (see [ADR 0005](../adr/0005-release-and-docs-stack.md)).

### Verifying a download

Every release includes `SHA256SUMS.txt` alongside the archives and packages. Verify a download
against it:

```bash
sha256sum -c --ignore-missing SHA256SUMS.txt
```

Release artifacts also carry
[GitHub build provenance attestations](https://docs.github.com/en/actions/security-guides/using-artifact-attestations-to-establish-provenance-for-builds),
which prove an artifact was built by this repository's release workflow from a specific commit. The
[`gh`](https://cli.github.com/) CLI verifies them directly:

```bash
gh attestation verify NetPrints-<version>-linux-x64.tar.gz --repo danielmeza/netprints
```

## `--check-project`

The desktop editor and the archives it ships in include a headless diagnostic mode, useful in
scripts or to confirm a project opens cleanly without launching the UI:

```bash
NetPrints.Desktop --check-project MyApp.csproj [--run]
```

It evaluates the project, translates every graph, runs Roslyn analysis, and (with `--run`) builds
and runs it, printing a summary line (`analysis: 0 errors, 0 warnings`, or the errors found) and
exiting with a code that scripts can check:

| Exit code | Meaning |
|---|---|
| `0` | The project opened, translated and analyzed cleanly (and ran, with `--run`) |
| `1` | Analysis or the build found errors — the summary line and the first error are printed |
| `2` | No project path was given |
| `3` | No usable MSBuild/.NET SDK was found (`NPW001`) — see [Requirements](#requirements) |
| `4` | `--run` built successfully but the program exited with a non-zero code |
