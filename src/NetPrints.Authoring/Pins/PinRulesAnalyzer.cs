using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Pins;

/// <summary>NPA001 to NPA009: the rules a node class must follow for the pin convention generator to declare its pins.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PinRulesAnalyzer : DiagnosticAnalyzer
{
    private static readonly char[] ReservedKeyCharacters = ['.', '/', '~'];

    private static readonly DiagnosticDescriptor PinShape = new(
        NpaDiagnosticCodes.PinShape,
        "A pin property must be a partial get-only property in a partial class",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.PinShape));

    private static readonly DiagnosticDescriptor DuplicateKey = new(
        NpaDiagnosticCodes.DuplicateKey,
        "A pin key is declared twice in one collection",
        "The pin key '{0}' is already declared in the {1} pins of this class or its bases",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.DuplicateKey));

    private static readonly DiagnosticDescriptor DeclarePinsCall = new(
        NpaDiagnosticCodes.DeclarePinsCall,
        "A node constructor calls DeclarePins() exactly once, before it adds pins",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.DeclarePinsCall));

    private static readonly DiagnosticDescriptor InvalidKey = new(
        NpaDiagnosticCodes.InvalidKey,
        "A pin key is not empty and has no '.', '/' or '~'",
        "The pin key '{0}' is not valid: it is empty or contains '.', '/' or '~'",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.InvalidKey));

    private static readonly DiagnosticDescriptor BaseAddsPins = new(
        NpaDiagnosticCodes.BaseAddsPins,
        "A base class does not add pins imperatively to a collection that declares pins",
        "Declare this pin with a pin property: an imperative pin in a base class shifts the offsets of the declared {0} pins",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.BaseAddsPins),
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor DefaultValueType = new(
        NpaDiagnosticCodes.DefaultValueType,
        "A DefaultValue converts to the type of its pin",
        "The default value does not convert to '{0}'",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.DefaultValueType));

    private static readonly DiagnosticDescriptor PinTypeMember = new(
        NpaDiagnosticCodes.PinTypeMember,
        "An untyped data pin and an output type pin name an ObservableValue<BaseType> member in PinType",
        "{0}",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.PinTypeMember));

    private static readonly DiagnosticDescriptor NameWithoutSuffix = new(
        NpaDiagnosticCodes.NameWithoutSuffix,
        "A pin property name ends in Pin or has a Pin attribute",
        "'{0}' does not end in 'Pin' and has no [Pin] attribute, so the convention cannot derive its key",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.NameWithoutSuffix));

    private static readonly DiagnosticDescriptor MissingMarker = new(
        NpaDiagnosticCodes.MissingMarker,
        "Pin properties belong to a class marked NodeKind or NodeBase",
        "'{0}' is a pin property of a class that has neither [NodeKind] nor [NodeBase]",
        NpaDiagnosticCodes.Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(NpaDiagnosticCodes.MissingMarker));

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [PinShape, DuplicateKey, DeclarePinsCall, InvalidKey, BaseAddsPins, DefaultValueType, PinTypeMember, NameWithoutSuffix, MissingMarker];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            Collector collector = new();
            start.RegisterSymbolAction(symbol => AnalyzeType(symbol, collector), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end => ReportBaseAdds(end, collector));
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, Collector collector)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } type || PinSymbols.UserLocation(type) is not { } typeLocation)
        {
            return;
        }

        bool isKind = PinSymbols.Find(type, PinReader.NodeKindAttribute) is not null;
        bool isBase = PinSymbols.Find(type, PinReader.NodeBaseAttribute) is not null;
        List<PinProperty> pins = PinSymbols.PinProperties(type);
        if (!isKind && !isBase)
        {
            ReportUnmarked(context, pins);
            return;
        }

        List<PinProperty> declared = [.. pins.Where(pin => pin.Symbol.IsPartialDefinition)];
        ReportShape(context, type, typeLocation, pins);
        ReportKeys(context, type, declared);
        ReportDefaults(context, declared);
        ReportMembers(context, type, declared);
        if (isKind && declared.Count > 0)
        {
            ReportConstructors(context, type, typeLocation, declared);
        }

        if (declared.Count > 0)
        {
            collector.Classes.Add(new ClassKinds(type, [.. declared.Select(pin => pin.Kind)]));
        }

        if (isBase)
        {
            foreach (ImperativeAdd add in PinSymbols.Constructors(type).SelectMany(PinSymbols.Invocations).Select(PinSymbols.AsImperativeAdd).OfType<ImperativeAdd>())
            {
                collector.Adds.Add((type, add));
            }
        }
    }

    private static void ReportUnmarked(SymbolAnalysisContext context, List<PinProperty> pins)
    {
        foreach (PinProperty pin in pins.Where(candidate => candidate.Symbol.IsPartialDefinition))
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMarker, PinSymbols.UserLocation(pin.Symbol), pin.Symbol.Name));
        }
    }

    private static void ReportShape(SymbolAnalysisContext context, INamedTypeSymbol type, Location typeLocation, List<PinProperty> pins)
    {
        if (pins.Count > 0 && !PinSymbols.IsPartial(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(PinShape, typeLocation, $"The class '{type.Name}' declares pin properties, so it must be partial"));
        }

        foreach (PinProperty pin in pins)
        {
            Location? location = PinSymbols.UserLocation(pin.Symbol);
            if (pin.Symbol.IsPartialDefinition && pin.Symbol.SetMethod is null)
            {
                continue;
            }

            string message = pin.Symbol.IsPartialDefinition
                ? $"The pin property '{pin.Symbol.Name}' must be get-only"
                : $"The pin property '{pin.Symbol.Name}' must be a partial property";
            context.ReportDiagnostic(Diagnostic.Create(PinShape, location, message));
        }

        foreach (IPropertySymbol property in type.GetMembers().OfType<IPropertySymbol>().Where(candidate => PinSymbols.UserLocation(candidate) is not null))
        {
            PinProperty? pin = pins.FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.Symbol, property));
            ReportAttributeMismatch(context, property, pin);
        }
    }

    private static void ReportAttributeMismatch(SymbolAnalysisContext context, IPropertySymbol property, PinProperty? pin)
    {
        Location fallback = PinSymbols.UserLocation(property) ?? Location.None;
        if (PinSymbols.Find(property, PinReader.PinAttribute) is { } pinAttribute && pin is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(PinShape, PinSymbols.AttributeLocation(pinAttribute, fallback), $"[Pin] is on '{property.Name}', which is not a pin property"));
        }

        if (PinSymbols.Find(property, PinReader.PinTypeAttribute) is { } typeAttribute && (pin is null || !pin.NeedsMember))
        {
            context.ReportDiagnostic(Diagnostic.Create(PinShape, PinSymbols.AttributeLocation(typeAttribute, fallback), $"[PinType] is on '{property.Name}', which takes no type member"));
        }
    }

    private static void ReportKeys(SymbolAnalysisContext context, INamedTypeSymbol type, List<PinProperty> declared)
    {
        HashSet<(PinKind, string)> seen = [.. PinSymbols.Ancestors(type).SelectMany(PinSymbols.DeclaredKeys)];
        foreach (PinProperty pin in declared)
        {
            Location location = PinSymbols.UserLocation(pin.Symbol) ?? Location.None;
            AttributeData? attribute = PinSymbols.Find(pin.Symbol, PinReader.PinAttribute);
            if (PinSymbols.ExplicitKey(attribute) is { } explicitKey && (explicitKey.Length == 0 || explicitKey.IndexOfAny(ReservedKeyCharacters) >= 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidKey, KeyLocation(attribute, location), explicitKey));
            }

            string key = PinSymbols.KeyOf(pin.Symbol);
            if (!seen.Add((pin.Kind, key)))
            {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateKey, location, key, pin.Kind));
            }

            if (attribute is null && !PinSymbols.HasSuffix(pin.Symbol.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(NameWithoutSuffix, location, pin.Symbol.Name));
            }
        }
    }

    private static Location KeyLocation(AttributeData? attribute, Location fallback)
    {
        if (attribute?.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax
            && syntax.ArgumentList?.Arguments.FirstOrDefault(argument => argument.NameEquals is null) is { } argument
            && PinSymbols.IsUserSource(argument.GetLocation()))
        {
            return argument.GetLocation();
        }

        return fallback;
    }

    private static void ReportDefaults(SymbolAnalysisContext context, List<PinProperty> declared)
    {
        foreach (PinProperty pin in declared.Where(candidate => candidate.Kind == PinKind.InputData && candidate.Generic && candidate.Argument is not null))
        {
            if (PinSymbols.Find(pin.Symbol, PinReader.DefaultValueAttribute) is not { ConstructorArguments.Length: 1 } attribute
                || pin.Argument is not { } target
                || Converts(context.Compilation, attribute.ConstructorArguments[0], target))
            {
                continue;
            }

            Location location = PinSymbols.AttributeLocation(attribute, PinSymbols.UserLocation(pin.Symbol) ?? Location.None);
            context.ReportDiagnostic(Diagnostic.Create(DefaultValueType, location, target.ToDisplayString()));
        }
    }

    private static bool Converts(Compilation compilation, TypedConstant value, ITypeSymbol target)
    {
        if (target.TypeKind == TypeKind.Error || value.Kind is TypedConstantKind.Error or TypedConstantKind.Array || value.Type is not { } source)
        {
            return true;
        }

        if (value.IsNull)
        {
            return target.IsReferenceType || target.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }

        Conversion conversion = compilation.ClassifyConversion(source, target);
        return conversion.Exists && !conversion.IsUserDefined;
    }

    private static void ReportMembers(SymbolAnalysisContext context, INamedTypeSymbol type, List<PinProperty> declared)
    {
        foreach (PinProperty pin in declared.Where(candidate => candidate.NeedsMember))
        {
            Location location = PinSymbols.UserLocation(pin.Symbol) ?? Location.None;
            if (PinSymbols.Find(pin.Symbol, PinReader.PinTypeAttribute) is not { } attribute)
            {
                context.ReportDiagnostic(Diagnostic.Create(PinTypeMember, location, $"The pin '{pin.Symbol.Name}' needs a [PinType] attribute that names an ObservableValue<BaseType> member"));
                continue;
            }

            string? member = PinSymbols.PinTypeMember(pin.Symbol);
            ITypeSymbol? memberType = member is null ? null : PinSymbols.MemberType(type, member);
            if (memberType is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(PinTypeMember, PinSymbols.AttributeLocation(attribute, location), $"The [PinType] member '{member}' of '{pin.Symbol.Name}' does not exist on '{type.Name}'"));
            }
            else if (!PinSymbols.IsObservableBaseType(memberType))
            {
                context.ReportDiagnostic(Diagnostic.Create(PinTypeMember, PinSymbols.AttributeLocation(attribute, location), $"The [PinType] member '{member}' of '{pin.Symbol.Name}' must be an ObservableValue<BaseType>"));
            }
        }
    }

    private static void ReportConstructors(SymbolAnalysisContext context, INamedTypeSymbol type, Location typeLocation, List<PinProperty> declared)
    {
        List<ConstructorDeclarationSyntax> constructors = [.. PinSymbols.Constructors(type)];
        if (constructors.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(DeclarePinsCall, typeLocation, $"The class '{type.Name}' has no constructor that calls DeclarePins()"));
            return;
        }

        HashSet<PinKind> kinds = [.. declared.Select(pin => pin.Kind), .. PinSymbols.Ancestors(type).SelectMany(PinSymbols.DeclaredKeys).Select(key => key.Kind)];
        foreach (ConstructorDeclarationSyntax constructor in constructors.Where(candidate => !candidate.Initializer.IsKind(SyntaxKind.ThisConstructorInitializer)))
        {
            List<InvocationExpressionSyntax> invocations = [.. PinSymbols.Invocations(constructor)];
            List<InvocationExpressionSyntax> declares = [.. invocations.Where(PinSymbols.IsDeclarePinsCall)];
            if (declares.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclarePinsCall, constructor.Identifier.GetLocation(), $"The constructor of '{type.Name}' must call DeclarePins() exactly once, but calls it {declares.Count} times"));
                continue;
            }

            foreach (ImperativeAdd add in invocations.Where(call => call.SpanStart < declares[0].SpanStart).Select(PinSymbols.AsImperativeAdd).OfType<ImperativeAdd>().Where(add => kinds.Contains(add.Kind)))
            {
                context.ReportDiagnostic(Diagnostic.Create(DeclarePinsCall, add.Invocation.GetLocation(), "Add pins to a declared collection after DeclarePins(), not before it"));
            }
        }
    }

    private static void ReportBaseAdds(CompilationAnalysisContext context, Collector collector)
    {
        Dictionary<INamedTypeSymbol, HashSet<PinKind>> kinds = new(SymbolEqualityComparer.Default);
        foreach (ClassKinds entry in collector.Classes)
        {
            foreach (INamedTypeSymbol owner in new[] { entry.Type }.Concat(PinSymbols.Ancestors(entry.Type)))
            {
                if (!kinds.TryGetValue(owner, out HashSet<PinKind>? set))
                {
                    set = [];
                    kinds[owner] = set;
                }

                set.UnionWith(entry.Kinds);
            }
        }

        foreach ((INamedTypeSymbol owner, ImperativeAdd add) in collector.Adds)
        {
            if (kinds.TryGetValue(owner, out HashSet<PinKind>? set) && set.Contains(add.Kind))
            {
                context.ReportDiagnostic(Diagnostic.Create(BaseAddsPins, add.Invocation.GetLocation(), add.Kind));
            }
        }
    }

    private sealed record ClassKinds(INamedTypeSymbol Type, ImmutableArray<PinKind> Kinds);

    private sealed class Collector
    {
        public ConcurrentBag<ClassKinds> Classes { get; } = [];

        public ConcurrentBag<(INamedTypeSymbol Type, ImperativeAdd Add)> Adds { get; } = [];
    }
}
