## Unreleased

**Breaking (source):** `IClassEmitter` and `IMemberEmitter` in `NetPrints.Core` are now `[Experimental("NPXE0003")]`; they
shipped unmarked in 0.1.1. Code that implements or references them, or builds a `TranslationEnvironment` with emitter
lists, gets error `NPXE0003` until the project opts in (`<NoWarn>$(NoWarn);NPXE0003</NoWarn>`). See
[API stability](https://danielmeza.github.io/netprints/guide/extensions#api-stability).

**Breaking:** The `netprints` command line (package `NetPrints.Cli`, shipped in 0.1.x) is rebuilt around commands. The 0.1 flags are removed, not deprecated; using one prints a message to stderr and exits 2. Replace `netprints -p X` (or `--project-path X`) with `netprints build X`, and `netprints -p X -r` (or `--run`) with `netprints run X`. Exit codes that 0.1 scripts may test also changed: `--help` and `--version` exit 0 (were 2), a project path that does not exist exits 2 (was 1), and Ctrl+C exits 130. See the [exit codes](https://danielmeza.github.io/netprints/guide/cli#exit-codes) and the [removed flags](https://danielmeza.github.io/netprints/guide/cli#removed-01-flags).

**New:** `netprints generate` (alias `regen`), with `--check` to fail CI on stale generated files, and `netprints migrate` to report graph schema versions. `run` forwards everything after `--` unchanged and streams the program's output. `generate` warns (and `--check` fails) when the tool's version differs from the project's `NetPrints.Sdk` package.

**New:** `netprints catalog` writes a type catalog (`*.npcat.json`, or a C# class with `--format csharp`) from assemblies, NuGet packages or a project's references, with `--check` for CI. It reads `netprints.catalog.json`; both files have JSON schemas (`npcat.v1.schema.json`, `netprints.catalog.v1.schema.json`). See the [catalogs guide](https://danielmeza.github.io/netprints/guide/catalogs).

**New (experimental):** the catalog profile API: `IExtensionBuilder.AddCatalogProfile` and `ExtensionRegistry.CatalogProfiles` let an extension contribute a profile that decides what a catalog lists; using it needs the `NPXE0004` opt-in. `NetPrints.Extensibility` now depends on `NetPrints.Catalog`. The `NetPrints.Annotations` package (experimental) is still in preview and not yet documented.

**Changed:** the editor's method, parameter and return documentation (`DocumentationUtil`) is now whitespace-collapsed plain text; `<see cref>`, `<paramref>` and `<see langword>` render as their target name (they rendered as nothing before), and an empty element gives no text instead of an empty string. It now also finds documentation for methods in multi-segment namespaces and for generic, nested and `ref` members, which had none.

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
