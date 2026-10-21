using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>What a hygiene analyzer sees besides the source: the file path, the ledger and the editorconfig options of the file.</summary>
/// <param name="Path">The path of the source file.</param>
/// <param name="Ledger">The text of <c>HygieneLedger.txt</c>, or <see langword="null"/> for none.</param>
/// <param name="Options">The editorconfig options of the file.</param>
internal sealed record HygieneInputs(string Path = "Sample.cs", string? Ledger = null, IReadOnlyDictionary<string, string>? Options = null);

/// <summary>Runs a hygiene (NPH) analyzer over a source text with the diagnostics marked in it.</summary>
internal static class HygieneHarness
{
    /// <summary>Asserts that the analyzer reports exactly the marked spans.</summary>
    /// <param name="markup">The source with <c>{|ID:text|}</c> markers.</param>
    /// <param name="id">The diagnostic id to check.</param>
    /// <param name="inputs">The file path, ledger and options; the defaults when omitted.</param>
    /// <returns>The diagnostics, for further assertions.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AssertAsync(string markup, string id, HygieneInputs? inputs = null)
    {
        (string source, IReadOnlyList<AnalyzerHarness.Expected> expected) = AnalyzerHarness.Parse(markup);
        ImmutableArray<Diagnostic> actual = await AnalyzeAsync(source, id, inputs ?? new HygieneInputs());

        AnalyzerHarness.Expected[] reported = [.. actual.Select(diagnostic => new AnalyzerHarness.Expected(diagnostic.Id, diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal)];

        Assert.Equal(expected, reported);
        return actual;
    }

    /// <summary>Runs the analyzer over several files of one compilation and returns the reported locations as path and span start, in the order the analyzer reports them.</summary>
    /// <param name="id">The diagnostic id to check.</param>
    /// <param name="files">The files in compilation order, as path and source text.</param>
    /// <returns>The path and start of each reported span, ordered by path and start.</returns>
    public static async Task<IReadOnlyList<(string Path, int Start)>> AnalyzeFilesAsync(string id, params (string Path, string Source)[] files)
    {
        ImmutableArray<Diagnostic> actual = await AnalyzeAsync(files, id, new HygieneInputs());
        return [.. actual.Select(diagnostic => (diagnostic.Location.SourceTree?.FilePath ?? string.Empty, diagnostic.Location.SourceSpan.Start))
            .OrderBy(item => item.Item1, StringComparer.Ordinal).ThenBy(item => item.Item2)];
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string id, HygieneInputs inputs) =>
        AnalyzeAsync([(inputs.Path, source)], id, inputs);

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(IReadOnlyList<(string Path, string Source)> files, string id, HygieneInputs inputs)
    {
        SyntaxTree[] trees = [.. files.Select(file => CSharpSyntaxTree.ParseText(file.Source, path: file.Path, cancellationToken: TestContext.Current.CancellationToken))];
        CSharpCompilation compilation = CSharpCompilation.Create("Sample", trees, [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)])
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, specificDiagnosticOptions: [new KeyValuePair<string, ReportDiagnostic>(id, ReportDiagnostic.Error)]));

        ImmutableArray<AdditionalText> additional = inputs.Ledger is null ? [] : [new MemoryText("HygieneLedger.txt", inputs.Ledger)];
        AnalyzerOptions options = new(additional, new MapOptionsProvider(inputs.Options ?? new Dictionary<string, string>()));
        ImmutableArray<Diagnostic> all = await compilation.WithAnalyzers([AnalyzerHarness.Analyzer(id)], options).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return [.. all.Where(diagnostic => diagnostic.Id == id).OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    private sealed class MemoryText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

}
