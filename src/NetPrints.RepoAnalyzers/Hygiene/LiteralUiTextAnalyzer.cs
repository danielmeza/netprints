using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Hygiene;

/// <summary>NPH006: a string literal passed to a user-visible text sink instead of a resource key.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LiteralUiTextAnalyzer : DiagnosticAnalyzer
{
    private const string Message = "message";

    private static readonly ImmutableDictionary<string, ImmutableHashSet<string>> Sinks = new Dictionary<string, string[]>
    {
        ["IDialogService"] = ["title", Message],
        ["IEditorDialogs"] = ["title", Message, "confirmLabel"],
        ["ShellViewModel"] = [Message],
        ["StatusBarViewModel"] = [Message],
    }.ToImmutableDictionary(entry => entry.Key, entry => entry.Value.ToImmutableHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static readonly DiagnosticDescriptor Rule = new(
        NphDiagnosticCodes.LiteralUiText,
        "User-visible text comes from a resource",
        "The literal '{0}' is passed to the user-visible text '{1}'; use a resource key",
        NphDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        helpLinkUri: DiagnosticHelpLinks.For(NphDiagnosticCodes.LiteralUiText));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            HygieneLedger ledger = HygieneLedger.Read(start.Options.AdditionalFiles, start.CancellationToken);
            start.RegisterOperationAction(site => Analyze(site, ledger), OperationKind.Argument);
        });
    }

    private static void Analyze(OperationAnalysisContext context, HygieneLedger ledger)
    {
        var argument = (IArgumentOperation)context.Operation;
        IParameterSymbol? parameter = argument.Parameter;
        if (parameter is null || !Sinks.TryGetValue(parameter.ContainingType.Name, out ImmutableHashSet<string>? names) || !names.Contains(parameter.Name))
        {
            return;
        }

        IOperation value = argument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        if (TextOf(value) is { } text
            && !ledger.Contains(NphDiagnosticCodes.LiteralUiText, argument.Syntax.SyntaxTree.FilePath, text))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), text, parameter.ContainingType.Name + "." + parameter.Name));
        }
    }

    private static string? TextOf(IOperation value)
    {
        if (value is ILiteralOperation { ConstantValue: { HasValue: true, Value: string literal } })
        {
            return literal;
        }

        if (value is not IInterpolatedStringOperation interpolated)
        {
            return null;
        }

        bool hasText = interpolated.Parts.OfType<IInterpolatedStringTextOperation>()
            .Any(part => part.Text.ConstantValue is { HasValue: true, Value: string text } && !string.IsNullOrWhiteSpace(text));
        return hasText
            ? string.Concat(interpolated.Parts.Select(part => part is IInterpolatedStringTextOperation { Text.ConstantValue.Value: string text } ? text : "{}"))
            : null;
    }
}
