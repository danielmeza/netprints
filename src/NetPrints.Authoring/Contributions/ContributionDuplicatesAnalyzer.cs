using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using NetPrints.Authoring.Pins;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Contributions;

/// <summary>NPA023 and NPA024: two commands with the same constant id, or the same menu id, group and order, in one compilation (FR-031).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContributionDuplicatesAnalyzer : DiagnosticAnalyzer
{
    private const char Separator = '\u001F';

    private const string PathParameter = "Path";

    private const string GroupParameter = "Group";

    private const string OrderParameter = "Order";

    private static readonly DiagnosticDescriptor IdRule = new(
        NpaDiagnosticCodes.DuplicateCommandId,
        "A command id is unique",
        "The command id '{0}' is already used by another command",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.DuplicateCommandId),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor SlotRule = new(
        NpaDiagnosticCodes.DuplicateMenuSlot,
        "A menu slot holds one command",
        "The menu '{0}', group '{1}' and order {2} are already used by another command",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.DuplicateMenuSlot),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [IdRule, SlotRule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            ConcurrentBag<Site> ids = [];
            ConcurrentBag<Site> slots = [];
            start.RegisterOperationAction(site => Collect(site, ids, slots), OperationKind.ObjectCreation);
            start.RegisterCompilationEndAction(end =>
            {
                Report(end, ids, IdRule, site => new object[] { site.Key });
                Report(end, slots, SlotRule, site => site.Key.Split(Separator).Cast<object>().ToArray());
            });
        });
    }

    private static void Collect(OperationAnalysisContext context, ConcurrentBag<Site> ids, ConcurrentBag<Site> slots)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (ContributionTypes.IsNamed(creation.Type, ContributionTypes.CommandDescriptor))
        {
            if (ContributionTypes.Argument(creation, ContributionTypes.IdParameter) is { } id && ContributionTypes.ConstantText(id.Value) is { } text)
            {
                ids.Add(new Site(text, id.Value.Syntax.GetLocation()));
            }

            return;
        }

        if (ContributionTypes.IsNamed(creation.Type, ContributionTypes.MenuPlacement)
            && creation.Parent is IArgumentOperation { Parameter.ContainingType: { } owner }
            && ContributionTypes.IsNamed(owner, ContributionTypes.CommandDescriptor)
            && SlotKey(creation) is { } key)
        {
            slots.Add(new Site(key, creation.Syntax.GetLocation()));
        }
    }

    private static string? SlotKey(IObjectCreationOperation placement)
    {
        IArgumentOperation? path = ContributionTypes.Argument(placement, PathParameter);
        IArgumentOperation? group = ContributionTypes.Argument(placement, GroupParameter);
        IArgumentOperation? order = ContributionTypes.Argument(placement, OrderParameter);
        if (path is null || group is null || order is null
            || ContributionTypes.ConstantText(path.Value) is not { } menu
            || ContributionTypes.ConstantText(group.Value) is not { } groupName
            || order.Value.ConstantValue is not { HasValue: true, Value: int position })
        {
            return null;
        }

        return string.Join(Separator.ToString(), menu, groupName, position.ToString(CultureInfo.InvariantCulture));
    }

    private static void Report(CompilationAnalysisContext context, ConcurrentBag<Site> sites, DiagnosticDescriptor rule, Func<Site, object[]> arguments)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (Site site in sites.OrderBy(item => item.Location.GetLineSpan().Path, StringComparer.Ordinal).ThenBy(item => item.Location.SourceSpan.Start))
        {
            if (!seen.Add(site.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(rule, site.Location, arguments(site)));
            }
        }
    }

    private sealed record Site(string Key, Location Location);
}
