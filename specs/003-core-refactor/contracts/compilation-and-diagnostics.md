# Contract: Compilation, diagnostics, source map and quick info (Core)

> Revised 2026-09-25 (csproj model): the custom reference resolver (`ReferencePackResolver`,
> `DotNetEnvironment`, probe table), `ProjectCompiler` and `runtimeconfig.json` writing are **removed**;
> references come from MSBuild and builds run through `IProjectSystem` (project-system.md §4). Former
> file name: `references-and-compilation.md`.

Project: `src/NetPrints.Core` (UI-free, no logging dependency: problems are returned as data).
Tests: `tests/NetPrints.Core.Tests/Compilation/`, `…/Translator/`. Test obligations (`RC-Txx`) at the end.

## 1. Diagnostics — `src/NetPrints.Core/Compilation/CodeDiagnostic.cs`, `DiagnosticMapper.cs`

```csharp
namespace NetPrints.Compilation;

public sealed record SourceFile(string Path, string Text);

public enum CodeDiagnosticSeverity { Info, Warning, Error }

public sealed record CodeDiagnostic(
    CodeDiagnosticSeverity Severity,
    string Id,                         // "CS0103", "NPT001", "NPD002", "NU1101", "MSB3073", …
    string Message,
    string? ClassFullName,
    string? GraphKey,                  // GraphKeys.For(graph) (document-format.md §1.4.1): "class", "<memberId>", "<variableId>/get", …
    string? NodeId,
    string? SourcePath,
    LinePositionSpan? Span);           // Microsoft.CodeAnalysis.Text; 0-based

public static class DiagnosticMapper
{
    /// Build messages (project-system.md §4.1) → diagnostics. A message in "<X>.netpc.g.cs" is mapped through
    /// the SourceMap of the class whose graph is "<X>.netpc.json" (translated in memory by the caller);
    /// a generator message with "(graph <key>, node <id>)" gets GraphKey/NodeId from the suffix, which is removed from Message.
    public static IReadOnlyList<CodeDiagnostic> FromBuild(IReadOnlyList<ProjectMessage> messages,
        IReadOnlyDictionary<string, (ClassGraph Class, TranslatedClass Translated)> classesByGeneratedPath);
    public static CodeDiagnostic FromRoslyn(Diagnostic diagnostic, ClassGraph? cls, SourceMap? map);
    public static CodeDiagnostic FromTranslation(TranslationException exception, ClassGraph cls);
}

// src/NetPrints.Serialization/DiagnosticExtensions.cs (DocumentIssue lives in Serialization):
namespace NetPrints.Serialization;
public static class DiagnosticExtensions
{
    public static CodeDiagnostic ToDiagnostic(this DocumentIssue issue);
}
```

Mapping old → new: `Project.LastCompileErrors` (strings) → `Project.LastDiagnostics`
(`ObservableRangeCollection<CodeDiagnostic>`); `CodeCompileResults`/`ICodeCompiler`/`CodeCompiler`
(Roslyn emit) → **deleted** (builds use the SDK); `Project.CompileProject`/`RunProject`/`GetRunCommand`
→ `IProjectSystem.BuildAsync` / `GetRunCommand`; `ReferenceAssemblyResolver` → deleted
(`ResolvedAssembly` moves to `NetPrints.Projects`).

## 2. Source map and translation errors — `src/NetPrints.Core/Translator/*.cs`

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
without events/locals/emitters (DF-T01) and identical whether or not the map is built (RC-T06). The file
written to disk is `GraphCodeGenerator.RenderFile(translated)` (header + `Code` with `\n` endings, project-system.md §3).

`GraphKeys` (`src/NetPrints.Core/Core/GraphKeys.cs`): `public static string For(NodeGraph graph)`
(document-format.md §1.4.1, data-model.md §2; `InvalidOperationException` if the graph is not attached to a class) and
`public static NodeGraph? Resolve(ClassGraph cls, string key)`.

## 3. Live analysis and quick info — `src/NetPrints.Core/Compilation/CodeAnalysisSession.cs`

```csharp
namespace NetPrints.Compilation;

public sealed record QuickInfo(string Signature, string? Summary);

public sealed class CodeAnalysisSession
{
    public CodeAnalysisSession(IReadOnlyList<ResolvedAssembly> references, IReadOnlyList<SourceFile> otherSources, string compilationOptionsJson); // MetadataReferences with XmlDocumentationProvider from DocumentationPath
    public Task<IReadOnlyList<CodeDiagnostic>> AnalyzeAsync(IReadOnlyList<TranslatedClass> classes, CancellationToken cancellationToken); // classes replace the on-disk .netpc.g.cs
    public Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken); // on the last analyzed snapshot; null if none/no symbol
}
```

| Rule | Contract |
|---|---|
| Work | Compilation only (`GetDiagnostics`, no emit); runs on the caller's thread — the editor calls it via `Task.Run`. |
| Snapshot | Each `AnalyzeAsync` replaces the snapshot atomically; `GetQuickInfoAsync` uses the latest completed one. |
| Thread-safety | Concurrent calls allowed; a newer `AnalyzeAsync` does not cancel an older one (the caller cancels). |
| Quick info | `SemanticModel.GetSymbolInfo` at the token; `Signature = symbol.ToDisplayString(MinimallyQualifiedFormat)`; `Summary` = `<summary>` text from `GetDocumentationCommentXml()`, whitespace-normalized. |
| Lifetime | Recreated when the project snapshot changes (reload after load/`ApplyAsync`); no unmanaged resources. |

## 4. Test obligations

| ID | Case |
|---|---|
| RC-T01…T05 | *Retired* (custom reference resolver removed). Replaced by PS-T07 (references and docs from MSBuild), PS-T08 (packages, project references), PS-T12 (`NPM001` for legacy framework references) |
| RC-T06 | Source map: building it does not change `Code` (byte compare on all fixtures); a `CS` error in a call-node argument maps to that node's id |
| RC-T07 | *Retired* → PS-T11 (build and run through the SDK) |
| RC-T08 | Quick info for `Console.WriteLine` returns the signature and the pack summary text on Linux (references from `IProjectSystem.LoadAsync`) |
| RC-T09 | Tooltip (D5): `IReflectionProvider.GetMethodDocumentation(Console.WriteLine(string))` non-empty on Linux, with references from `ProjectSnapshot.References` |
| RC-T10 | `DiagnosticMapper`: emitter/translator errors surface as `NPT` diagnostics with class, graph key and node id; a build message in `X.netpc.g.cs` maps to the node; a generator line's suffix is parsed and stripped |
