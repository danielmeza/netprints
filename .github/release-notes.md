## Unreleased

**Changed:** the editor is one window. The launcher and the per-class windows are gone: a project opens in a single window with a project tree, tabbed graphs, an inspector, an Errors, Output and C# panel, a Variables panel, a menu bar and a command bar. Panes dock, tab and float, and a graph tab can float into its own window and dock back; **View › Reset layout** restores the default. Every action of the old windows is still reachable (class settings, members, event graphs, overriding a base method, variable getter, setter and type graphs, references, project settings, create and open project). One project per window; opening another unloads the current one.

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
