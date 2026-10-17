using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string id, HygieneInputs inputs)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: inputs.Path, cancellationToken: TestContext.Current.CancellationToken);
        CSharpCompilation compilation = CSharpCompilation.Create("Sample", [tree], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)])
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, specificDiagnosticOptions: [new KeyValuePair<string, ReportDiagnostic>(id, ReportDiagnostic.Error)]));

        ImmutableArray<AdditionalText> files = inputs.Ledger is null ? [] : [new MemoryText("HygieneLedger.txt", inputs.Ledger)];
        AnalyzerOptions options = new(files, new OptionsProvider(inputs.Options ?? new Dictionary<string, string>()));
        ImmutableArray<Diagnostic> all = await compilation.WithAnalyzers([AnalyzerHarness.Analyzer(id)], options).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return [.. all.Where(diagnostic => diagnostic.Id == id).OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    private sealed class MemoryText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class OptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new Options(values);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(values);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(values);
    }

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}
