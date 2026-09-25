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
| P0 | Modernize build + Avalonia editor at parity | ~6 w (manual est.) | — | in progress (`specs/001-modernize-build`) |
| P1 | Core refactor + extension points | ~3.5 w | P0 | not started |
| P2 | Catalog tooling + Spectre CLI | ~2.5 w | P1 | not started |
| P3 | Editor extension host | ~2.5 w | P0, P1 | not started |
| P4 | VSIX (WpfAvaloniaHost) | ~2 w | P3 | **deferred** by owner (2026-09-24) |
| P5 | VS Code extension + browser build + sidecar | ~2.5–3 w | P3 | not started |
| U1 | UnrealSharp codegen + catalog (NetPrintsUnreal) | ~2 w | P2 | not started |
| U2 | Unreal nodes | ~2–3 w | U1 | not started |
| U3 | UE plugin, launcher, upstream PR | ~1.5 w | U1, P3 | not started |

### P0 — Modernize build + Avalonia editor at parity
- `global.json` (.NET 10 SDK), Central Package Management, `Directory.Build.props`.
- Core and a new UI-free `NetPrints.Reflection` (moved out of the editor) multi-target
  `netstandard2.0` + `net10.0`; Roslyn 4.14; drop dead deps; minimal reference-assembly fallback.
- Replace the WPF editor with `NetPrints.Editor` (Avalonia 11 + Nodify.Avalonia,
  CommunityToolkit.Mvvm, DynamicData search) + `NetPrints.Desktop`, at feature parity (60-item
  parity inventory in `specs/001-modernize-build/spec.md`).
- Tests on MSTest 4 (Microsoft.Testing.Platform) + Avalonia.Headless UI tests.
- Linux-only CI workflow `CI` (`.github/workflows/ci.yml`). Legacy VSIX removed from the
  solution build (source kept, pending P4).
- **Done when:** the whole solution builds and all tests pass on Linux CI and the parity
  checklist is verified.

### P1 — Core refactor and extension points
Reference-assembly resolution (ref packs / configured paths); serialization layer (versioned
DTOs, `IDocumentFormat`/`IDocumentStore`, JSON, legacy XML importer, migrations); composite provider
over `NetPrints.Reflection` (moved in P0); extension points (node
libraries, class/member emitters, type catalogs, project/target profiles, `IHostChannel`,
per-extension settings, plugin manifest/loading via AssemblyLoadContext); event graphs
(multiple entry points); model INPC from Fody to CommunityToolkit.Mvvm.
Done when: old sample loads, saves as JSON, generates identical C#.

### P2 — Catalog tooling + Spectre CLI
`NetPrints.Catalog` engine (versioned schema, `ICatalogFilter` profiles), tool flavor
(`netprints.catalog.json` + CLI overrides, dotnet tool), annotations flavor
(`NetPrints.Annotations` + source generator), cross-flavor snapshot tests; `NetPrints.Cli` on
Spectre.Console.Cli (`build`, `generate`, `run`, `catalog`, `migrate`).

### P3 — Editor extension host
`NetPrints.Desktop --profile`, plugin-loaded editor extensions, UI contributions (commands,
inspector sections, panels, settings pages), DynamicData/ReactiveUI performance work beyond
parity, sample non-Unreal extension, anything left beyond P0 parity.

### P4 — VSIX (deferred)
Deferred by the project owner on 2026-09-24; revisit after P3/P5. When resumed:
Spike WpfAvaloniaHost in VS 2022/2026 (assembly conflicts); editor factory for `.netpc.json`
with VS project `IDocumentStore`; Community.VisualStudio.Toolkit, SDK-style. Separate VSIX CI
workflow chained after `CI` (`workflow_run`, windows-latest, path-filtered). Resolve the
Nodify.Avalonia 1.0.2 net7.0-only limitation (editor cannot target netstandard2.0) — e.g.
out-of-process editor window or a maintained/forked canvas.

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
