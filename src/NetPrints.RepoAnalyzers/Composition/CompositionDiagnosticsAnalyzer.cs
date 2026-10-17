using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>
/// Declares the NPI001 to NPI005 descriptors that <see cref="RegistrationGenerator"/> reports, so that the release tracking
/// analyzer and the diagnostics guide see them as supported diagnostics. It analyzes nothing itself.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompositionDiagnosticsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        NpiDiagnosticCodes.SingletonTakesSessionScopedRule,
        NpiDiagnosticCodes.InvalidRegistrationRule,
        NpiDiagnosticCodes.AmbiguousConstructorRule,
        NpiDiagnosticCodes.DuplicateServiceRule,
        NpiDiagnosticCodes.SharedInstanceRule,
    ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
