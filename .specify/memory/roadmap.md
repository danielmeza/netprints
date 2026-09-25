# NetPrints Modernization Roadmap

Source plan: https://claude.ai/artifact/Dpr7MBWPLeTrFiXBEMbSfh (NetPrints Unreal Roadmap).
Each phase = one Spec Kit feature (`specs/NNN-*`), one branch, one PR. Only specify a phase
when it is started.

## Repositories
- **NetPrints** (this repo, `danielmeza/netprints`): general-purpose visual language for .NET,
  extension-first. Phases P0–P5.
- **NetPrintsUnreal** (new repo, later): UnrealSharp integration built purely on NetPrints
  extension points. Phases U1–U3.

## Baseline findings (code read, 2026-09-24)
- Core `NetPrints/`: netstandard2.0, ~7.8k LOC, no WPF. Custom name-based type specifiers,
  string-built codegen (`Translator/ExecutionGraphTranslator.cs`, goto state machine), Roslyn
  2.10 `CSharpCompilation.Emit`, DataContract XML (`.netpc`/`.netpp`), PropertyChanged.Fody.
- .NET Framework assumptions: `Core/Project.cs:46-48`, `Core/FrameworkAssemblyReference.cs:34`,
  `Reflection/ReflectionProvider.cs:173`, `DocumentationUtil.cs:63` (ProgramFilesX86 v4.x paths).
- Editor `NetPrintsEditor/`: net461 WPF, MahApps, MvvmLight, 16 XAML files; `ReflectionProvider`
  (Roslyn metadata) lives here but is UI-free.
- CLI: 64 LOC, CommandLineParser. VSIX: old-style csproj, VS 15.9 SDK. Tests: MSTest 1.4, 11
  core tests; editor tests empty.
- Unused deps: Gapotchenko.FX, System.Management.

## Phases

| ID | Name | Est. | Depends on | Status |
|----|------|------|------------|--------|
| P0 | Modernize the build | ~1 w | — | in progress (`specs/001-*`) |
| P1 | Core refactor + extension points | ~3.5 w | P0 | not started |
| P2 | Catalog tooling + Spectre CLI | ~2.5 w | P1 | not started |
| P3 | Avalonia editor (extension host) | ~5.5 w | P1 | not started |
| P4 | VSIX (WpfAvaloniaHost) | ~2 w | P3 | not started |
| P5 | VS Code extension + browser build + sidecar | ~2.5–3 w | P3 | not started |
| U1 | UnrealSharp codegen + catalog (NetPrintsUnreal) | ~2 w | P2 | not started |
| U2 | Unreal nodes | ~2–3 w | U1 | not started |
| U3 | UE plugin, launcher, upstream PR | ~1.5 w | U1, P3 | not started |

### P0 — Modernize the build
- Central Package Management + `Directory.Build.props`; `global.json` pinning .NET 10 SDK.
- Core multi-targets `netstandard2.0` + `net10.0`; CLI and tests move to `net10.0`.
- Roslyn 2.10 → 4.x; drop Gapotchenko.FX, System.Management, WinForms reference.
- Tests on a modern framework (MSTest 3 or xUnit v3); GitHub Actions CI on Linux (+ Windows).
- WPF editor and VSIX stay as-is (Windows-only, excluded from the Linux build) until P3/P4.
- **Done when:** the 11 existing core tests pass on Linux in CI.
- Linux-first go/no-go spike for UnrealSharp on Linux is scheduled during P1–P2 (not P0).

### P1 — Core refactor and extension points
Reference-assembly resolution (ref packs / configured paths); serialization layer (versioned
DTOs, `IDocumentFormat`/`IDocumentStore`, JSON, legacy XML importer, migrations); move
`ReflectionProvider` to `NetPrints.Reflection` + composite provider; extension points (node
libraries, class/member emitters, type catalogs, project/target profiles, `IHostChannel`,
per-extension settings, plugin manifest/loading via AssemblyLoadContext); event graphs
(multiple entry points); model INPC from Fody to CommunityToolkit.Mvvm.
Done when: old sample loads, saves as JSON, generates identical C#.

### P2 — Catalog tooling + Spectre CLI
`NetPrints.Catalog` engine (versioned schema, `ICatalogFilter` profiles), tool flavor
(`netprints.catalog.json` + CLI overrides, dotnet tool), annotations flavor
(`NetPrints.Annotations` + source generator), cross-flavor snapshot tests; `NetPrints.Cli` on
Spectre.Console.Cli (`build`, `generate`, `run`, `catalog`, `migrate`).

### P3 — Avalonia editor
CommunityToolkit.Mvvm VMs without WPF types; Nodify canvas; DynamicData collections and
search; Fluent shell, Material icons, StorageProvider dialogs; triggers → style classes /
Xaml.Behaviors; `NetPrints.Desktop --profile`; UI contributions from extensions; sample
non-Unreal extension. Done when: all samples edit + compile on Linux.

### P4 — VSIX
Spike WpfAvaloniaHost in VS 2022/2026 (assembly conflicts); editor factory for `.netpc.json`
with VS project `IDocumentStore`; Community.VisualStudio.Toolkit, SDK-style.

### P5 — VS Code extension + browser build
Spike Avalonia.Browser + Nodify in a webview (CSP `wasm-unsafe-eval`, size, startup);
`NetPrints.Sidecar` (Roslyn/codegen, JSON-RPC `IHostChannel`, shipped as dotnet tool with
bundled self-contained binaries as fallback); TS `CustomEditorProvider`; standalone browser build.

### U1–U3 (NetPrintsUnreal repo)
U1: `[UClass] partial` emitters, `unreal-blueprint` catalog profile, CLI loop into UnrealSharp
`Script/` with hot reload (first live loop ~week 8). U2: event entry points, latent nodes as
async/await, delegates, components, containers. U3: `UnrealSharpNetPrints` C++ plugin
launcher, Unreal `IHostChannel` pipe, `unreal` project profile, upstream PR implementing
`CSAssetTypeAction_CSBlueprint::OpenAssetEditor` as an external-editor hook.
