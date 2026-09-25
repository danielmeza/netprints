# Contract: Reference resolution, compilation and diagnostics (Core)

Project: `src/NetPrints.Core` (UI-free, no logging dependency: problems are returned as data).
Tests: `tests/NetPrints.Core.Tests/References/`, `…/Compilation/`, `…/Translator/`. Test obligations
(`RC-Txx`) at the end.

## 1. Environment — `src/NetPrints.Core/References/DotNetEnvironment.cs`

```csharp
namespace NetPrints.References;

/// Machine facts the resolver needs; tests construct it with fake values and temp directories.
public sealed record DotNetEnvironment(
    Func<string, string?> GetVariable,      // environment variable lookup
    string RuntimeDirectory,                // RuntimeEnvironment.GetRuntimeDirectory()
    Version RuntimeVersion,                 // Environment.Version
    string UserProfileDirectory,            // Environment.SpecialFolder.UserProfile
    OSPlatform Platform)
{
    public static DotNetEnvironment Current();
}
```

## 2. Resolver — `src/NetPrints.Core/References/*.cs`

```csharp
namespace NetPrints.References;

public sealed record ReferenceRequest(
    string TargetFramework,                        // "net10.0" (netX.Y only; else ArgumentException)
    IReadOnlyList<string> FrameworkReferences,     // "Microsoft.NETCore.App", "Microsoft.AspNetCore.App"
    IReadOnlyList<CompilationReference> References);

public enum ReferenceSource { ConfiguredPack, InstalledPack, NuGetCachePack, RuntimeMajorPack, RuntimeDirectory }

public sealed record ResolvedAssembly(string Path, string? DocumentationPath);
public sealed record ReferenceWarning(string Code, string Message);
public sealed record RuntimeFramework(string Name, string Version);  // for runtimeconfig.json: "Microsoft.NETCore.App", "10.0.0"

public sealed record ReferenceResolution(
    IReadOnlyList<ResolvedAssembly> Assemblies,    // distinct by full path (OrdinalIgnoreCase on Windows, Ordinal elsewhere), ordinal-sorted
    IReadOnlyList<ReferenceWarning> Warnings,      // in probe order
    RuntimeFramework RuntimeFramework,
    ReferenceSource Source);                       // where Microsoft.NETCore.App came from

public sealed record ReferenceResolutionOptions(IReadOnlyList<string> PackRoots); // configured roots, in order

public interface IReferenceResolver
{
    ReferenceResolution Resolve(ReferenceRequest request, CancellationToken cancellationToken);
}

public sealed class ReferencePackResolver : IReferenceResolver
{
    public ReferencePackResolver(ReferenceResolutionOptions options, DotNetEnvironment environment);
}
```

### 2.1 Probe order per framework reference `F` (pack `F.Ref`) and `netX.Y`

| # | Candidates (first existing wins; highest patch `X.Y.*` within a root, `Version` compare) | `Source` |
|---|---|---|
| 1 | each `options.PackRoots[i]`: `<root>/packs/F.Ref/X.Y.*/ref/netX.Y/` and `<root>/F.Ref/X.Y.*/ref/netX.Y/` | `ConfiguredPack` |
| 2 | dotnet roots, in order: `DOTNET_ROOT`, `RuntimeDirectory/../../..`, `/usr/share/dotnet`, `/usr/lib/dotnet`, `/usr/local/share/dotnet` (Linux/macOS), `%ProgramFiles%\dotnet` (Windows, from `GetVariable("ProgramFiles")`): `<root>/packs/F.Ref/X.Y.*/ref/netX.Y/` | `InstalledPack` |
| 3 | `NUGET_PACKAGES` or `<UserProfile>/.nuget/packages`: `<lowercase F>.ref/X.Y.*/ref/netX.Y/` | `NuGetCachePack` |
| 4 | steps 1–3 again for `net<RuntimeVersion.Major>.<Minor>` (only if different) + warning `NPR002` | `RuntimeMajorPack` |
| 5 | `Microsoft.NETCore.App` only: managed `*.dll` in `RuntimeDirectory` (P0 behavior) + warning `NPR003`; other `F` → `NPR006` and skipped | `RuntimeDirectory` |

A configured root that does not exist → `NPR005` (once per root). Files: every `*.dll` in the
selected `ref/netX.Y/` folder; `DocumentationPath` = same name `.xml` if it exists. In the
`RuntimeDirectory` fallback, `DocumentationPath` is looked up in the highest installed
`Microsoft.NETCore.App.Ref/<runtime major.minor>.*/ref/net*/` (the D5 probe) or `null`.

### 2.2 Other references

| Reference | Result |
|---|---|
| `AssemblyReference` whose file exists | added (doc = sibling `.xml` if present) |
| `AssemblyReference` missing | skipped, `NPR004` (P0 behavior) |
| `FrameworkAssemblyReference` (legacy `.NETFramework/v4.x/*`) | ignored for resolution; if any present → one `NPR001` ("legacy .NET Framework references were mapped to Microsoft.NETCore.App netX.Y") |
| `SourceDirectoryReference` | not an assembly; unchanged (sources passed separately) |

Warning codes: `NPR001` legacy mapped, `NPR002` target pack missing → runtime major pack, `NPR003`
runtime directory fallback, `NPR004` assembly missing, `NPR005` configured root missing, `NPR006`
non-core framework without pack. Thread-safety: stateless; safe to share. `RuntimeFramework` =
`(Microsoft.NETCore.App, "<X>.<Y>.0")` of the target actually used.

### 2.3 Mapping old → new

| Old | New |
|---|---|
| `ReferenceAssemblyResolver.ResolveAssemblyPaths(refs, warnings)` + `UsesRuntimeAssemblies` | `IReferenceResolver.Resolve(request, ct)`; `Source == RuntimeDirectory` replaces `UsesRuntimeAssemblies` |
| `ReferenceAssemblyResolver.GetDotNetHostPath()` | `DotNetHost.GetHostPath(DotNetEnvironment)` (`References/DotNetHost.cs`, same logic) |
| `FrameworkAssemblyReference.UpdateFrameworkPath()` (`ProgramFilesX86`) | deleted; `AssemblyPath` of legacy framework references becomes `null` |
| `Project.DefaultReferences` (`.NETFramework/v4.5/…`) | deleted; `Project.CreateNew(name, ns, profile)` sets `TargetFramework`/`FrameworkReferences` from the profile |
| `DocumentationUtil.GetAssemblyDocumentationPath` (`ProgramFilesX86`, sibling, cwd) | `ResolvedAssembly.DocumentationPath`, passed through `ReflectionProvider` (extension-points.md §4) |
| `ReflectionHost.ReloadAsync` building paths with `ReferenceAssemblyResolver` | uses `IReferenceResolver` from `EditorContext` |

## 3. Compilation — `src/NetPrints.Core/Compilation/*.cs`

```csharp
namespace NetPrints.Compilation;

public sealed record SourceFile(string Path, string Text);   // Path = "<Class.FullName>.cs" or the source file path

public enum CodeDiagnosticSeverity { Info, Warning, Error }

public sealed record CodeDiagnostic(
    CodeDiagnosticSeverity Severity,
    string Id,                         // "CS0103", "NPT001", "NPR004", …
    string Message,
    string? ClassFullName,
    string? GraphKey,                  // GraphKeys.For(graph) (document-format.md §1.4)
    string? NodeId,
    string? SourcePath,
    LinePositionSpan? Span);           // Microsoft.CodeAnalysis.Text; 0-based

public sealed record CodeCompileResults(bool Success, IReadOnlyList<CodeDiagnostic> Diagnostics, string? PathToAssembly);

public interface ICodeCompiler
{
    CodeCompileResults CompileSources(string outputPath, IReadOnlyList<ResolvedAssembly> references,
        IReadOnlyList<SourceFile> sources, bool generateExecutable, CancellationToken cancellationToken);
}

public sealed class CodeCompiler : ICodeCompiler;  // Roslyn; deterministic emit (unchanged); diagnostics of severity ≥ Warning, sorted by path, line, id

public sealed record ProjectCompileResult(bool Success, IReadOnlyList<CodeDiagnostic> Diagnostics, string? PathToAssembly, ReferenceResolution References);

public sealed class ProjectCompiler
{
    public ProjectCompiler(TranslationEnvironment translation, IReferenceResolver references, ICodeCompiler compiler);
    public Task<ProjectCompileResult> CompileAsync(Project project, IProjectProfile profile, CancellationToken cancellationToken);
}
```

| Member | Contract |
|---|---|
| `CompileAsync` | Preconditions: `project.Path` set; not already compiling (`InvalidOperationException`). Sets `project.IsCompiling = true`, `CompilationMessage = "Compiling..."` on the calling thread (UI), runs translation + compile on the thread pool, then on the calling synchronization context sets `IsCompiling = false`, `LastCompilationSucceeded`, `LastDiagnostics` (new, replaces `LastCompileErrors`), `CompilationMessage` ("Build succeeded" / "Build failed with N error(s)"), and `LastCompiledAssemblyPath`. Output dirs from `profile` (`{ProjectName}` substituted). Writes `{Name}.runtimeconfig.json` with `References.RuntimeFramework` for executables. Class translation errors → `NPT00x` diagnostics with class/graph/node; reference warnings → `Warning` diagnostics with the `NPR` code. Cancellation → `OperationCanceledException`, state reset. |
| Diagnostic mapping | Roslyn diagnostic in `<Class>.cs` → `SourceMap.Find(position)` of that class → `GraphKey`, `NodeId` (null if outside any node). |
| Determinism | Sources ordered by `Path` (ordinal); unchanged P0 output. |

Mapping old → new: `Project.CompileProject()` (async void) → `ProjectCompiler.CompileAsync`;
`Project.LastCompileErrors` (strings) → `Project.LastDiagnostics` (`ObservableRangeCollection<CodeDiagnostic>`);
`CodeCompileResults.Errors` (strings) → `Diagnostics`; `Project.RunProject()`/`GetRunCommand()` stay,
using `DotNetHost`.

## 4. Source map and translation errors — `src/NetPrints.Core/Translator/*.cs`

```csharp
namespace NetPrints.Translator;

public readonly record struct SourceMapEntry(TextSpan Span, string GraphKey, string NodeId);

public sealed class SourceMap
{
    public static SourceMap Empty { get; }
    public IReadOnlyList<SourceMapEntry> Entries { get; }   // sorted by Span.Start, non-overlapping
    public SourceMapEntry? Find(int position);              // entry containing position, else the nearest preceding entry in the same member, else null
}

public sealed record TranslatedClass(string FullName, string Code, SourceMap Map);

public sealed class TranslationException : Exception
{
    public TranslationException(string code, string message, string? graphKey = null, string? nodeId = null, Exception? inner = null);
    public string Code { get; }        // NPT001…NPT007
    public string? GraphKey { get; }
    public string? NodeId { get; }
}

public sealed class ClassTranslator
{
    public ClassTranslator(TranslationEnvironment environment);
    public string TranslateClass(ClassGraph cls);                 // == Translate(cls).Code
    public TranslatedClass Translate(ClassGraph cls);
}
```

Translation codes: `NPT001` cross-entry data flow, `NPT002` duplicate method/entry name, `NPT003`
graph contains nodes of a missing extension, `NPT004` local variable name conflict, `NPT005` emitter
failure, `NPT006` no translator for node type, `NPT007` invalid emitter output.

Invariant: `Translate(cls).Code` is byte-identical to the P0 `TranslateClass(cls)` for every class
without events/locals/emitters (DF-T01) and identical whether or not the map is built (RC-T06).

`GraphKeys` (`src/NetPrints.Core/Core/GraphKeys.cs`): `public static string For(NodeGraph graph)`
(document-format.md §1.4; `InvalidOperationException` if the graph is not attached to a class) and
`public static NodeGraph? Resolve(ClassGraph cls, string key)`.

## 5. Live analysis and quick info — `src/NetPrints.Core/Compilation/CodeAnalysisSession.cs`

```csharp
namespace NetPrints.Compilation;

public sealed record QuickInfo(string Signature, string? Summary);

public sealed class CodeAnalysisSession
{
    public CodeAnalysisSession(ReferenceResolution references);           // builds MetadataReferences with XmlDocumentationProvider
    public Task<IReadOnlyList<CodeDiagnostic>> AnalyzeAsync(IReadOnlyList<TranslatedClass> classes, IReadOnlyList<SourceFile> extraSources, CancellationToken cancellationToken);
    public Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken); // on the last analyzed snapshot; null if none/no symbol
}
```

| Rule | Contract |
|---|---|
| Work | Compilation only (`GetDiagnostics`, no emit); runs on the caller's thread — the editor calls it via `Task.Run`. |
| Snapshot | Each `AnalyzeAsync` replaces the snapshot atomically; `GetQuickInfoAsync` uses the latest completed one. |
| Thread-safety | Concurrent calls allowed; a newer `AnalyzeAsync` does not cancel an older one (the caller cancels). |
| Quick info | `SemanticModel.GetSymbolInfo` at the token; `Signature = symbol.ToDisplayString(MinimallyQualifiedFormat)`; `Summary` = `<summary>` text from `GetDocumentationCommentXml()`, whitespace-normalized. |
| Lifetime | Recreated when references change (reflection reload); no unmanaged resources. |

## 6. Test obligations

| ID | Case |
|---|---|
| RC-T01 | Fake dotnet root (temp dir) with `packs/Microsoft.NETCore.App.Ref/10.0.3` and `10.0.11` → picks `10.0.11`, `Source = InstalledPack`, docs paths set |
| RC-T02 | Configured root wins over installed; missing configured root → `NPR005` and continues |
| RC-T03 | Only a NuGet-cache pack → `NuGetCachePack` |
| RC-T04 | Target `net9.0` not found → runtime-major pack + `NPR002`; nothing found → runtime directory + `NPR003`, `RuntimeFramework` from the runtime version |
| RC-T05 | Legacy `.NETFramework` references → one `NPR001`; missing assembly → `NPR004`; no `ProgramFilesX86` string left in `src/` (grep test) |
| RC-T06 | Source map: building it does not change `Code` (byte compare on all fixtures); a `CS` error in a call-node argument maps to that node's id |
| RC-T07 | `ProjectCompiler`: HelloWorld compiles against the real installed pack on CI and prints `Hello, World!`; state transitions of `IsCompiling`/`CompilationMessage`; `runtimeconfig.json` content |
| RC-T08 | Quick info for `Console.WriteLine` returns the signature and the pack summary text on Linux |
| RC-T09 | Tooltip (D5): `IReflectionProvider.GetMethodDocumentation(Console.WriteLine(string))` non-empty on Linux with a pack; empty (no crash) with only the runtime directory |
| RC-T10 | Emitter/translator errors surface as `NPT` diagnostics with class, graph key and node id |
