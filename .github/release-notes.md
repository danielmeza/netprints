## Unreleased

**Breaking (source):** `IClassEmitter` and `IMemberEmitter` in `NetPrints.Core` are now `[Experimental("NPXE0003")]`; they
shipped unmarked in 0.1.1. Code that implements or references them, or builds a `TranslationEnvironment` with emitter
lists, gets error `NPXE0003` until the project opts in (`<NoWarn>$(NoWarn);NPXE0003</NoWarn>`). See
[API stability](https://danielmeza.github.io/netprints/guide/extensions#api-stability).

**New:** Command-line tool `netprints` (package `NetPrints.Cli`) with `build`, `run`, `generate` (`regen`), and `migrate` commands. The `--version` flag and `--help` documentation are included. The tool replaces the `0.1` flags `-p` / `--project-path` and `-r` / `--run` with dedicated commands.

**New:** `NetPrints.Catalog` (experimental) and `NetPrints.Annotations` (experimental) packages for graph catalogs and source annotations (preview, not yet documented).

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

Verify a download: `sha256sum -c --ignore-missing SHA256SUMS.txt` and `gh attestation verify <file> -R danielmeza/netprints`.
