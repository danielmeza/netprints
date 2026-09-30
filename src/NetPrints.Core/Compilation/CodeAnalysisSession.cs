#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Projects;
using NetPrints.Translator;

namespace NetPrints.Compilation;

/// <summary>
/// Signature and documentation summary of the symbol at a code-view position
/// (compilation-and-diagnostics.md §3).
/// </summary>
/// <param name="Signature">The symbol's minimally-qualified display string.</param>
/// <param name="Summary">Whitespace-normalized <c>&lt;summary&gt;</c> text from the symbol's XML
/// documentation comment, or <see langword="null"/> if it has none.</param>
public sealed record QuickInfo(string Signature, string? Summary);

/// <summary>
/// Analyzes the generated C# of a project's classes with Roslyn (compilation-and-diagnostics.md §3):
/// diagnostics and quick info only, no emit, so the editor can show them without a build. UI-free and
/// holds no unmanaged resources; the editor runs <see cref="AnalyzeAsync"/> off the UI thread itself
/// (via <c>Task.Run</c>) and recreates the session when the project's <see cref="ProjectSnapshot"/>
/// changes.
/// </summary>
public sealed class CodeAnalysisSession
{
    private readonly ImmutableArray<MetadataReference> references;
    private readonly ImmutableArray<SyntaxTree> otherSourceTrees;
    private readonly CSharpParseOptions parseOptions;
    private readonly CSharpCompilationOptions compilationOptions;

    private volatile Snapshot? snapshot;

    /// <summary>
    /// Creates a session that analyzes against <paramref name="references"/> and
    /// <paramref name="otherSources"/> (a <see cref="ProjectSnapshot"/>'s own, project-system.md §4),
    /// with the language version and nullable context <paramref name="compilationOptionsJson"/> carries.
    /// </summary>
    /// <param name="references">Resolved reference assemblies; a reference whose file does not exist is
    /// skipped instead of throwing. Metadata references carry XML documentation from each one's
    /// <see cref="ResolvedAssembly.DocumentationPath"/> when it exists (RC-T08).</param>
    /// <param name="otherSources">The project's <c>Compile</c> documents other than generated
    /// <c>*.netpc.g.cs</c> files.</param>
    /// <param name="compilationOptionsJson"><see cref="ProjectSnapshot.CompilationOptionsJson"/>.</param>
    public CodeAnalysisSession(IReadOnlyList<ResolvedAssembly> references, IReadOnlyList<SourceFile> otherSources, string compilationOptionsJson)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(otherSources);
        ArgumentException.ThrowIfNullOrEmpty(compilationOptionsJson);

        CompilationOptionsInfo options = JsonSerializer.Deserialize(compilationOptionsJson, CompilationOptionsJsonContext.Default.CompilationOptionsInfo)
            ?? new CompilationOptionsInfo("default", nameof(NullableContextOptions.Disable), false);

        parseOptions = new CSharpParseOptions(ParseLanguageVersion(options.LanguageVersion));

        NullableContextOptions nullableContextOptions = Enum.TryParse(options.Nullable, ignoreCase: true, out NullableContextOptions parsedNullable)
            ? parsedNullable
            : NullableContextOptions.Disable;
        compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: nullableContextOptions);

        this.references = [.. references.Where(assembly => File.Exists(assembly.Path)).Select(ToMetadataReference)];
        otherSourceTrees = [.. otherSources.Select(source => CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path))];
    }

    /// <summary>
    /// Compiles <paramref name="classes"/> together with the project's other sources and references —
    /// <paramref name="classes"/> replace the on-disk <c>.netpc.g.cs</c> files — and replaces the
    /// session's snapshot with the result. A newer call does not cancel an older one still running; the
    /// caller cancels through <paramref name="cancellationToken"/> instead.
    /// </summary>
    /// <param name="classes">Every class's latest translated code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Every diagnostic the compilation found, mapped to the node that produced it when its
    /// class has one at that position.</returns>
    public Task<IReadOnlyList<CodeDiagnostic>> AnalyzeAsync(IReadOnlyList<TranslatedClass> classes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(classes);
        cancellationToken.ThrowIfCancellationRequested();

        var treesByClass = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
        var classesByTree = new Dictionary<SyntaxTree, TranslatedClass>();
        var trees = new List<SyntaxTree>(otherSourceTrees.Length + classes.Count);
        trees.AddRange(otherSourceTrees);

        foreach (TranslatedClass cls in classes)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(cls.Code, parseOptions, $"{cls.FullName}.netpc.g.cs", cancellationToken: cancellationToken);
            treesByClass[cls.FullName] = tree;
            classesByTree[tree] = cls;
            trees.Add(tree);
        }

        CSharpCompilation compilation = CSharpCompilation.Create("NetPrintsAnalysis", trees, references, compilationOptions);
        snapshot = new Snapshot(compilation, treesByClass);

        IReadOnlyList<CodeDiagnostic> diagnostics = [.. compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
            .Select(diagnostic => ToDiagnostic(diagnostic, classesByTree))];

        return Task.FromResult(diagnostics);
    }

    /// <summary>
    /// Gets the signature and documentation summary of the symbol at <paramref name="position"/> in
    /// <paramref name="classFullName"/>'s code, on the last snapshot <see cref="AnalyzeAsync"/> completed.
    /// </summary>
    /// <param name="classFullName">Full name of the class, as its <see cref="TranslatedClass.FullName"/>
    /// was in the last <see cref="AnalyzeAsync"/> call.</param>
    /// <param name="position">Character offset into the class's code.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The symbol's quick info, or <see langword="null"/> if no <see cref="AnalyzeAsync"/> has
    /// completed yet, <paramref name="classFullName"/> is not one of its classes, or no symbol resolves
    /// at <paramref name="position"/>.</returns>
    public async Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(classFullName);
        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is not { } current || !current.TreesByClass.TryGetValue(classFullName, out SyntaxTree? tree))
        {
            return null;
        }

        SyntaxNode root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        if (position < 0 || position > root.FullSpan.End)
        {
            return null;
        }

        SyntaxNode? node = root.FindToken(position).Parent;
        if (node is null)
        {
            return null;
        }

        SemanticModel model = current.Compilation.GetSemanticModel(tree);
        SymbolInfo info = model.GetSymbolInfo(node, cancellationToken);
        ISymbol? symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

        return symbol is null
            ? null
            : new QuickInfo(
                symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                ExtractSummary(symbol.GetDocumentationCommentXml(cancellationToken: cancellationToken)));
    }

    private static CodeDiagnostic ToDiagnostic(Diagnostic diagnostic, IReadOnlyDictionary<SyntaxTree, TranslatedClass> classesByTree)
    {
        TranslatedClass? cls = diagnostic.Location.SourceTree is { } tree && classesByTree.TryGetValue(tree, out TranslatedClass? found)
            ? found
            : null;

        return DiagnosticMapper.FromRoslyn(diagnostic, null, cls?.Map) with { ClassFullName = cls?.FullName };
    }

    private static MetadataReference ToMetadataReference(ResolvedAssembly assembly)
    {
        DocumentationProvider? documentation = assembly.DocumentationPath is { } path && File.Exists(path)
            ? XmlDocumentationProvider.CreateFromFile(path)
            : null;

        return MetadataReference.CreateFromFile(assembly.Path, documentation: documentation);
    }

    private static LanguageVersion ParseLanguageVersion(string value) =>
        LanguageVersionFacts.TryParse(value, out LanguageVersion version) ? version : LanguageVersion.Default;

    private static string? ExtractSummary(string? xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return null;
        }

        try
        {
            XElement? summary = XElement.Parse(xml).Descendants("summary").FirstOrDefault();
            if (summary is null)
            {
                return null;
            }

            string[] words = summary.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 0 ? null : string.Join(' ', words);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private sealed record Snapshot(CSharpCompilation Compilation, IReadOnlyDictionary<string, SyntaxTree> TreesByClass);
}
