using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NetPrints.RepoAnalyzers.SmartEnums;

/// <summary>
/// Declares the NPI010 and NPI011 descriptors that <see cref="SmartEnumGenerator"/> reports, so that the release tracking
/// analyzer and the diagnostics guide see them as supported diagnostics. It analyzes nothing itself.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SmartEnumDiagnosticsAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        SmartEnumDiagnosticCodes.ShapeRule,
        SmartEnumDiagnosticCodes.MemberRule,
    ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
