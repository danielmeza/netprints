using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>Editorconfig options from a dictionary, the same for every file.</summary>
/// <param name="values">The option values.</param>
internal sealed class MapOptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
{
    /// <inheritdoc/>
    public override AnalyzerConfigOptions GlobalOptions => new Options(values);

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(values);

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(values);

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}
