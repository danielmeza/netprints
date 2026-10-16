using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetPrints.Roslyn;

namespace NetPrints.Authoring.Pins;

/// <summary>Reads node classes and their pin properties into pipeline models.</summary>
internal static class PinReader
{
    private const string GraphNamespace = "NetPrints.Graph";

    private const string NodeKindAttribute = "NetPrints.Graph.NodeKindAttribute";

    private const string PinAttribute = "NetPrints.Graph.PinAttribute";

    private const string PinTypeAttribute = "NetPrints.Graph.PinTypeAttribute";

    private const string LayoutAttribute = "NetPrints.Graph.NodePinLayoutAttribute";

    private const string DefaultValueAttribute = "System.ComponentModel.DefaultValueAttribute";

    private const string PinSuffix = "Pin";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static PinSetModel? ReadKind(GeneratorAttributeSyntaxContext context, CancellationToken token) =>
        Read(context, requireNodeKind: true, token);

    public static PinSetModel? ReadBase(GeneratorAttributeSyntaxContext context, CancellationToken token) =>
        Read(context, requireNodeKind: false, token);

    private static PinSetModel? Read(GeneratorAttributeSyntaxContext context, bool requireNodeKind, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type || type.TypeKind != TypeKind.Class || type.IsGenericType || !IsPartial(type, token))
        {
            return null;
        }

        if (!requireNodeKind && type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == NodeKindAttribute))
        {
            return null;
        }

        List<PinModel> pins = ReadPins(type);
        if (pins.Count == 0)
        {
            return null;
        }

        List<string> containers = [];
        for (INamedTypeSymbol? container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.IsGenericType)
            {
                return null;
            }

            containers.Insert(0, container.Name);
        }

        PinCounts offsets = default;
        bool hasBase = false;
        for (INamedTypeSymbol? ancestor = type.BaseType; ancestor is not null && ancestor.SpecialType != SpecialType.System_Object; ancestor = ancestor.BaseType)
        {
            foreach (PinKind kind in DeclaredKinds(ancestor))
            {
                offsets = offsets.Add(kind);
                hasBase = true;
            }
        }

        string ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
        return new PinSetModel(ns, new EquatableArray<string>(containers.ToArray()), type.Name, type.IsSealed, hasBase, offsets, new EquatableArray<PinModel>(pins.ToArray()));
    }

    private static bool IsPartial(INamedTypeSymbol type, CancellationToken token) =>
        type.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax(token) is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(modifier => modifier.Text == "partial"));

    private static List<PinModel> ReadPins(INamedTypeSymbol type)
    {
        List<PinModel> pins = [];
        foreach (IPropertySymbol property in PartialProperties(type))
        {
            if (ReadPin(property) is { } pin)
            {
                pins.Add(pin);
            }
        }

        return pins;
    }

    private static IEnumerable<IPropertySymbol> PartialProperties(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IPropertySymbol>()
            .Where(property => property.IsPartialDefinition && property.Parameters.Length == 0 && property.Locations.Length > 0)
            .OrderBy(property => property.Locations[0].SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(property => property.Locations[0].SourceSpan.Start);

    private static IEnumerable<PinKind> DeclaredKinds(INamedTypeSymbol type)
    {
        AttributeData? layout = type.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == LayoutAttribute);
        if (layout is not null)
        {
            foreach (TypedConstant entry in layout.ConstructorArguments.SelectMany(argument => argument.Kind == TypedConstantKind.Array ? argument.Values : [argument]))
            {
                if (entry.Value is string text && TryParseLayout(text, out PinKind kind))
                {
                    yield return kind;
                }
            }

            yield break;
        }

        foreach (IPropertySymbol property in PartialProperties(type))
        {
            if (ReadPin(property) is { } pin)
            {
                yield return pin.Kind;
            }
        }
    }

    private static bool TryParseLayout(string text, out PinKind kind)
    {
        string[] parts = text.Split('.');
        kind = default;
        if (parts.Length < 3)
        {
            return false;
        }

        (bool known, PinKind found) = (parts[0], parts[1]) switch
        {
            ("in", "exec") => (true, PinKind.InputExec),
            ("out", "exec") => (true, PinKind.OutputExec),
            ("in", "data") => (true, PinKind.InputData),
            ("out", "data") => (true, PinKind.OutputData),
            ("in", "type") => (true, PinKind.InputType),
            ("out", "type") => (true, PinKind.OutputType),
            _ => (false, default(PinKind)),
        };
        kind = found;
        return known;
    }

    private static PinModel? ReadPin(IPropertySymbol property)
    {
        if (!TryClassify(property.Type, out PinKind kind, out bool generic, out ITypeSymbol? argument))
        {
            return null;
        }

        string member = string.Empty;
        string? key = null;
        string defaultExpression = string.Empty;
        foreach (AttributeData attribute in property.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case PinAttribute:
                    key = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
                    break;
                case PinTypeAttribute:
                    member = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string ?? string.Empty : string.Empty;
                    break;
                case DefaultValueAttribute:
                    defaultExpression = kind == PinKind.InputData && generic ? DefaultExpression(attribute, argument) : string.Empty;
                    break;
            }
        }

        bool needsMember = kind == PinKind.OutputType || (kind is PinKind.InputData or PinKind.OutputData && !generic);
        if (needsMember && member.Length == 0)
        {
            return null;
        }

        string name = property.Name;
        string resolved = key ?? (name.Length > PinSuffix.Length && name.EndsWith(PinSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - PinSuffix.Length) : name);
        return new PinModel(name, Keyword(property.DeclaredAccessibility), kind, resolved,property.Type.ToDisplayString(TypeFormat), member, defaultExpression);
    }

    private static string DefaultExpression(AttributeData attribute, ITypeSymbol? argument)
    {
        if (argument is null || argument.SpecialType is SpecialType.None or SpecialType.System_Object || attribute.ConstructorArguments.Length != 1)
        {
            return string.Empty;
        }

        TypedConstant constant = attribute.ConstructorArguments[0];
        if (constant.Kind != TypedConstantKind.Primitive || constant.Value is null)
        {
            return string.Empty;
        }

        string? literal = SymbolDisplay.FormatPrimitive(constant.Value, quoteStrings: true, useHexadecimalNumbers: false);
        if (literal is null)
        {
            return string.Empty;
        }

        return argument.SpecialType == SpecialType.System_String ? literal : "(" + argument.ToDisplayString(TypeFormat) + ")" + literal;
    }

    private static bool TryClassify(ITypeSymbol type, out PinKind kind, out bool generic, out ITypeSymbol? argument)
    {
        kind = default;
        generic = false;
        argument = null;
        if (type is not INamedTypeSymbol named || named.ContainingNamespace.ToDisplayString() != GraphNamespace)
        {
            return false;
        }

        generic = named.Arity == 1;
        argument = generic ? named.TypeArguments[0] : null;
        switch (named.Name)
        {
            case "NodeInputExecPin":
                kind = PinKind.InputExec;
                return named.Arity == 0;
            case "NodeOutputExecPin":
                kind = PinKind.OutputExec;
                return named.Arity == 0;
            case "NodeInputTypePin":
                kind = PinKind.InputType;
                return named.Arity == 0;
            case "NodeOutputTypePin":
                kind = PinKind.OutputType;
                return named.Arity == 0;
            case "NodeInputDataPin":
                kind = PinKind.InputData;
                return named.Arity <= 1;
            case "NodeOutputDataPin":
                kind = PinKind.OutputData;
                return named.Arity <= 1;
            default:
                return false;
        }
    }

    private static string Keyword(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };
}
