using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Authoring.Pins;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Localization;

/// <summary>NPA040 to NPA042: the convention resource keys of a node kind are in the neutral resx, a satellite follows its neutral file, and no key is left over (FR-027).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResourceKeyAnalyzer : DiagnosticAnalyzer
{
    private const string NodePrefix = "Node.";

    private const string PinInfix = ".Pin.";

    private const string TitleIsFixedName = "TitleIsFixed";

    private static readonly string[] TextSuffixes = ["Title", "Description", "Keywords"];

    private static readonly DiagnosticDescriptor Missing = new(
        NpaDiagnosticCodes.MissingResourceKey,
        "A convention resource key is in the neutral resx",
        "The resource key '{0}' of node kind '{1}' is missing from the neutral resx",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.MissingResourceKey));

    private static readonly DiagnosticDescriptor Mismatch = new(
        NpaDiagnosticCodes.SatelliteMismatch,
        "A satellite resx follows its neutral resx",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.SatelliteMismatch),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor Unused = new(
        NpaDiagnosticCodes.UnusedResourceKey,
        "A resx key is used by a declaration",
        "The resource key '{0}' belongs to node kind '{1}', which does not declare it",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.UnusedResourceKey),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Missing, Mismatch, Unused];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext start)
    {
        List<ResxFile> files = ResxFile.ReadAll(start.Options.AdditionalFiles, start.CancellationToken);
        if (files.Count == 0)
        {
            return;
        }

        List<ResxFile> neutral = [.. files.Where(file => file.IsNeutral)];
        HashSet<string> neutralKeys = new(neutral.SelectMany(file => file.Entries.Keys), StringComparer.Ordinal);
        ConcurrentDictionary<string, HashSet<string>> impliedByKind = new(StringComparer.Ordinal);

        if (neutral.Count > 0)
        {
            start.RegisterSymbolAction(symbol => AnalyzeType(symbol, neutralKeys, impliedByKind), SymbolKind.NamedType);
        }

        start.RegisterCompilationEndAction(end =>
        {
            ReportSatellites(end, files);
            ReportUnused(end, neutral, impliedByKind);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, HashSet<string> neutralKeys, ConcurrentDictionary<string, HashSet<string>> impliedByKind)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } type
            || PinSymbols.UserLocation(type) is not { } typeLocation
            || PinSymbols.ExplicitKey(PinSymbols.Find(type, PinReader.NodeKindAttribute)) is not { } kind)
        {
            return;
        }

        string titleKey = NodePrefix + kind + ".Title";
        HashSet<string> implied = new(StringComparer.Ordinal) { titleKey };
        if (OverridesTitleIsFixed(type) && !neutralKeys.Contains(titleKey))
        {
            context.ReportDiagnostic(Diagnostic.Create(Missing, typeLocation, titleKey, kind));
        }

        foreach (DeclaredPin pin in DeclaredPin.Of(type))
        {
            string key = NodePrefix + kind + PinInfix + pin.Reference;
            implied.Add(key);
            if (!neutralKeys.Contains(key))
            {
                Location location = pin.Property is { } property ? PinSymbols.UserLocation(property) ?? typeLocation : typeLocation;
                context.ReportDiagnostic(Diagnostic.Create(Missing, location, key, kind));
            }
        }

        impliedByKind[kind] = implied;
    }

    private static bool OverridesTitleIsFixed(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(TitleIsFixedName).OfType<IPropertySymbol>().Any(property => property.IsOverride))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportSatellites(CompilationAnalysisContext context, List<ResxFile> files)
    {
        foreach (ResxFile satellite in files.Where(file => !file.IsNeutral))
        {
            if (satellite.NeutralIn(files) is not { } neutral)
            {
                continue;
            }

            foreach (KeyValuePair<string, ResxEntry> entry in satellite.Entries)
            {
                if (!neutral.Entries.TryGetValue(entry.Key, out ResxEntry? original))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Mismatch, entry.Value.Location, $"The key '{entry.Key}' is in the satellite resx but not in the neutral one"));
                }
                else if (ResxFile.PlaceholderCount(entry.Value.Value) != ResxFile.PlaceholderCount(original.Value))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Mismatch, entry.Value.Location, $"The key '{entry.Key}' has a different placeholder count than the neutral resx"));
                }
            }
        }
    }

    private static void ReportUnused(CompilationAnalysisContext context, List<ResxFile> neutral, ConcurrentDictionary<string, HashSet<string>> impliedByKind)
    {
        foreach (ResxFile file in neutral)
        {
            foreach (KeyValuePair<string, ResxEntry> entry in file.Entries)
            {
                string? kind = KindOf(entry.Key, impliedByKind.Keys);
                if (kind is not null
                    && !impliedByKind[kind].Contains(entry.Key)
                    && !IsTextKey(entry.Key, kind))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Unused, entry.Value.Location, entry.Key, kind));
                }
            }
        }
    }

    private static string? KindOf(string key, IEnumerable<string> kinds) =>
        key.StartsWith(NodePrefix, StringComparison.Ordinal)
            ? kinds.Where(kind => key.StartsWith(NodePrefix + kind + ".", StringComparison.Ordinal)).OrderByDescending(kind => kind.Length).FirstOrDefault()
            : null;

    private static bool IsTextKey(string key, string kind) =>
        TextSuffixes.Any(suffix => string.Equals(key, NodePrefix + kind + "." + suffix, StringComparison.Ordinal));
}
