using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Pins;

/// <summary>NPA010 and NPA011: the pin keys of a node kind are recorded in <c>NodePins.Shipped.txt</c>/<c>NodePins.Unshipped.txt</c>, and a shipped key never disappears.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PinKeyTrackingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property holding the tracking line the diagnostic is about.</summary>
    internal const string LineProperty = "Line";

    /// <summary>The diagnostic property holding the shipped key a replaced property used to have.</summary>
    internal const string OldKeyProperty = "OldKey";

    private static readonly DiagnosticDescriptor Unrecorded = new(
        NpaDiagnosticCodes.UnrecordedKey,
        "A declared pin key is recorded in NodePins.Shipped.txt or NodePins.Unshipped.txt",
        "The pin key '{0}' of node kind '{1}' is in neither NodePins.Shipped.txt nor NodePins.Unshipped.txt",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.UnrecordedKey));

    private static readonly DiagnosticDescriptor ShippedGone = new(
        NpaDiagnosticCodes.ShippedKeyGone,
        "A shipped pin key is still declared",
        "The shipped pin key '{0}' of node kind '{1}' is no longer declared{2}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.ShippedKeyGone));

    private static readonly DiagnosticDescriptor Untracked = new(
        NpaDiagnosticCodes.TrackingMissing,
        "A node kind with declared pins has NodePins files",
        "The node kind '{0}' declares pins, but the project has neither NodePins.Shipped.txt nor NodePins.Unshipped.txt, so its pin keys are not tracked",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.TrackingMissing));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Unrecorded, ShippedGone, Untracked];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            PinKeyTracking? tracking = PinKeyTracking.Read(start.Options.AdditionalFiles, start.CancellationToken);
            if (tracking is not null)
            {
                start.RegisterSymbolAction(symbol => AnalyzeType(symbol, tracking), SymbolKind.NamedType);
            }
            else
            {
                start.RegisterSymbolAction(ReportUntracked, SymbolKind.NamedType);
            }
        });
    }

    private static void ReportUntracked(SymbolAnalysisContext context)
    {
        if (context.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } type
            && PinSymbols.UserLocation(type) is { } location
            && PinSymbols.ExplicitKey(PinSymbols.Find(type, PinReader.NodeKindAttribute)) is { } kind
            && Declared(type).Count > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Untracked, location, kind));
        }
    }

    private static void AnalyzeType(SymbolAnalysisContext context, PinKeyTracking tracking)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } type
            || PinSymbols.UserLocation(type) is not { } typeLocation
            || PinSymbols.ExplicitKey(PinSymbols.Find(type, PinReader.NodeKindAttribute)) is not { } kind)
        {
            return;
        }

        List<DeclaredPin> declared = Declared(type);
        HashSet<string> declaredLines = new(declared.Select(pin => Line(kind, pin)), StringComparer.Ordinal);
        List<DeclaredPin> unrecorded = [.. declared.Where(pin => !tracking.IsRecorded(Line(kind, pin)))];

        foreach (string gone in tracking.ShippedOf(kind).Where(line => !declaredLines.Contains(line)))
        {
            string oldReference = gone.Substring(kind.Length + 1);
            DeclaredPin? replacement = unrecorded.FirstOrDefault(pin => Group(Reference(pin)) == Group(oldReference));
            if (replacement is not null)
            {
                unrecorded.Remove(replacement);
            }

            Location location = replacement?.Property is { } property ? PinSymbols.UserLocation(property) ?? typeLocation : typeLocation;
            string suffix = replacement is null ? string.Empty : $"; the declared key '{Reference(replacement)}' replaced it";
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(LineProperty, gone)
                .Add(OldKeyProperty, oldReference.Substring(Group(oldReference).Length + 1));
            context.ReportDiagnostic(Diagnostic.Create(ShippedGone, location, properties, oldReference, kind, suffix));
        }

        foreach (DeclaredPin pin in unrecorded)
        {
            Location location = pin.Property is { } property ? PinSymbols.UserLocation(property) ?? typeLocation : typeLocation;
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty.Add(LineProperty, Line(kind, pin));
            context.ReportDiagnostic(Diagnostic.Create(Unrecorded, location, properties, Reference(pin), kind));
        }
    }

    private static List<DeclaredPin> Declared(INamedTypeSymbol type)
    {
        List<DeclaredPin> pins = [];
        foreach (INamedTypeSymbol member in PinSymbols.Ancestors(type).Reverse().Concat([type]))
        {
            List<PinProperty> properties = [.. PinSymbols.PinProperties(member).Where(pin => pin.Symbol.IsPartialDefinition)];
            if (properties.Count > 0)
            {
                pins.AddRange(properties.Select(pin => new DeclaredPin(pin.Kind, PinSymbols.KeyOf(pin.Symbol), pin.Symbol)));
            }
            else
            {
                pins.AddRange(PinSymbols.DeclaredKeys(member).Select(entry => new DeclaredPin(entry.Kind, entry.Key, null)));
            }
        }

        return pins;
    }

    private static string Line(string kind, DeclaredPin pin) => kind + " " + Reference(pin);

    private static string Reference(DeclaredPin pin) => Group(pin.Kind) + "." + pin.Key;

    private static string Group(PinKind kind)
    {
        string direction = kind is PinKind.InputExec or PinKind.InputData or PinKind.InputType ? "in" : "out";
        string collection = kind is PinKind.InputExec or PinKind.OutputExec ? "exec" : kind is PinKind.InputType or PinKind.OutputType ? "type" : "data";
        return direction + "." + collection;
    }

    private static string Group(string reference)
    {
        int first = reference.IndexOf('.');
        int second = first < 0 ? -1 : reference.IndexOf('.', first + 1);
        return second < 0 ? reference : reference.Substring(0, second);
    }

    private sealed record DeclaredPin(PinKind Kind, string Key, IPropertySymbol? Property);
}
