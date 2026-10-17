using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetPrints.Authoring.Pins;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Contributions;

/// <summary>NPA020 to NPA022: the icon ids, gestures and ids that a contribution descriptor is built with (FR-031).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContributionLiteralsAnalyzer : DiagnosticAnalyzer
{
    private const string IconRegistryType = "NetPrints.Editor.Icons.IconIds";

    private const string BuiltInIconPrefix = "netprints.icon.";

    private const string OwnerOption = "build_property.NetPrintsExtensionId";

    private const string ScopeParameter = "Scope";

    private const string GesturesParameter = "DefaultGestures";

    private const string ModifiersParameter = "Modifiers";

    private const string KeyParameter = "Key";

    private const string ParseMethod = "Parse";

    private const string TryParseMethod = "TryParse";

    private static readonly DiagnosticDescriptor IconRule = new(
        NpaDiagnosticCodes.IconId,
        "An icon id follows the icon id pattern and is registered",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.IconId));

    private static readonly DiagnosticDescriptor GestureRule = new(
        NpaDiagnosticCodes.Gesture,
        "A gesture parses and fits the scope of its command",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.Gesture));

    private static readonly DiagnosticDescriptor IdRule = new(
        NpaDiagnosticCodes.ContributionId,
        "A contribution id follows the id pattern and the owner prefix",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.ContributionId));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [IconRule, GestureRule, IdRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            ImmutableHashSet<string>? icons = ReadRegisteredIcons(start.Compilation);
            start.RegisterOperationAction(site => AnalyzeCreation(site, icons), OperationKind.ObjectCreation);
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static ImmutableHashSet<string>? ReadRegisteredIcons(Compilation compilation)
    {
        INamedTypeSymbol? registry = compilation.GetTypeByMetadataName(IconRegistryType);
        if (registry is null)
        {
            return null;
        }

        ImmutableHashSet<string>.Builder ids = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (IFieldSymbol field in ContributionTypes.ConstantFields(registry))
        {
            if (field.ConstantValue is string id)
            {
                ids.Add(id);
            }
        }

        return ids.ToImmutable();
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, ImmutableHashSet<string>? icons)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (ContributionTypes.HasId(creation.Type) && ContributionTypes.Argument(creation, ContributionTypes.IdParameter) is { } id)
        {
            CheckId(context, id.Value);
        }

        if (ContributionTypes.HasIcon(creation.Type) && ContributionTypes.Argument(creation, ContributionTypes.IconParameter) is { } icon)
        {
            CheckIcon(context, icon.Value, icons);
        }

        if (ContributionTypes.IsNamed(creation.Type, ContributionTypes.CommandDescriptor))
        {
            CheckGestureScope(context, creation);
        }
    }

    private static void CheckId(OperationAnalysisContext context, IOperation value)
    {
        string? text = ContributionTypes.ConstantText(value);
        if (text is null)
        {
            return;
        }

        if (!ContributionTypes.IsIdPattern(text))
        {
            context.ReportDiagnostic(Diagnostic.Create(IdRule, value.Syntax.GetLocation(), $"The id '{text}' does not match the id pattern"));
        }
        else if (context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(OwnerOption, out string? owner)
            && owner.Length > 0
            && !text.StartsWith(owner + ".", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(IdRule, value.Syntax.GetLocation(), $"The id '{text}' does not start with the extension id '{owner}'"));
        }
    }

    private static void CheckIcon(OperationAnalysisContext context, IOperation value, ImmutableHashSet<string>? icons)
    {
        string? text = ContributionTypes.ConstantText(value);
        if (text is null)
        {
            return;
        }

        if (!ContributionTypes.IsIdPattern(text))
        {
            context.ReportDiagnostic(Diagnostic.Create(IconRule, value.Syntax.GetLocation(), $"The icon id '{text}' does not match the icon id pattern"));
        }
        else if (icons is not null && text.StartsWith(BuiltInIconPrefix, StringComparison.Ordinal) && !icons.Contains(text))
        {
            context.ReportDiagnostic(Diagnostic.Create(IconRule, value.Syntax.GetLocation(), $"The built-in icon id '{text}' is not registered"));
        }
    }

    private static void CheckGestureScope(OperationAnalysisContext context, IObjectCreationOperation creation)
    {
        if (ContributionTypes.Argument(creation, GesturesParameter) is not { } gestures || !IsGlobal(ContributionTypes.Argument(creation, ScopeParameter)))
        {
            return;
        }

        foreach (IOperation operation in gestures.Value.Descendants())
        {
            if (TryReadGesture(operation, out int modifiers, out string key) && GestureText.IsPlain(modifiers) && !GestureText.IsFunctionKey(key))
            {
                context.ReportDiagnostic(Diagnostic.Create(GestureRule, operation.Syntax.GetLocation(), $"The single-key gesture '{key}' is not allowed in the Global scope; give the command a scope"));
            }
        }
    }

    private static bool IsGlobal(IArgumentOperation? scope) =>
        scope is null || scope.Value.ConstantValue is { HasValue: true, Value: 0 };

    private static bool TryReadGesture(IOperation operation, out int modifiers, out string key)
    {
        modifiers = 0;
        key = string.Empty;
        if (operation is IObjectCreationOperation creation && ContributionTypes.IsNamed(creation.Type, ContributionTypes.CommandGesture))
        {
            IArgumentOperation? mods = ContributionTypes.Argument(creation, ModifiersParameter);
            IArgumentOperation? name = ContributionTypes.Argument(creation, KeyParameter);
            if (mods is null || name is null || mods.Value.ConstantValue is not { HasValue: true, Value: int flags } || ContributionTypes.Unwrap(name.Value) is not IFieldReferenceOperation field)
            {
                return false;
            }

            modifiers = flags;
            key = field.Field.Name;
            return true;
        }

        return operation is IInvocationOperation invocation
            && ContributionTypes.IsNamed(invocation.TargetMethod.ContainingType, ContributionTypes.CommandGesture)
            && invocation.TargetMethod.Name == ParseMethod
            && FirstText(invocation) is { } text
            && GestureText.TryParse(text, out modifiers, out key);
    }

    private static string? FirstText(IInvocationOperation invocation)
    {
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (ContributionTypes.ConstantText(argument.Value) is { } text)
            {
                return text;
            }
        }

        return null;
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;
        if (!ContributionTypes.IsNamed(method.ContainingType, ContributionTypes.CommandGesture)
            || (method.Name != ParseMethod && method.Name != TryParseMethod))
        {
            return;
        }

        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (ContributionTypes.ConstantText(argument.Value) is { } text && !GestureText.TryParse(text, out _, out _))
            {
                context.ReportDiagnostic(Diagnostic.Create(GestureRule, argument.Value.Syntax.GetLocation(), $"'{text}' is not a gesture"));
                return;
            }
        }
    }
}
