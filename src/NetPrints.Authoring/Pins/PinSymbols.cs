using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.Authoring.Pins;

/// <summary>A property whose type is one of the pin types.</summary>
/// <param name="Symbol">The property.</param>
/// <param name="Kind">The collection the pin belongs to.</param>
/// <param name="Generic">Whether the pin type has a type argument.</param>
/// <param name="Argument">The type argument, or null.</param>
internal sealed record PinProperty(IPropertySymbol Symbol, PinKind Kind, bool Generic, ITypeSymbol? Argument)
{
    /// <summary>Gets a value indicating whether the pin needs a <c>[PinType]</c> member.</summary>
    public bool NeedsMember => Kind == PinKind.OutputType || (Kind is PinKind.InputData or PinKind.OutputData && !Generic);
}

/// <summary>An imperative pin addition found in a constructor.</summary>
/// <param name="Kind">The collection the call adds to.</param>
/// <param name="Invocation">The call.</param>
internal sealed record ImperativeAdd(PinKind Kind, InvocationExpressionSyntax Invocation);

/// <summary>Symbol and syntax helpers shared by the pin analyzers.</summary>
internal static class PinSymbols
{
    private const string DeclarePinsName = "DeclarePins";

    private const string ObservableValueName = "ObservableValue";

    private const string BaseTypeName = "BaseType";

    private const string ObservablePropertyName = "ObservablePropertyAttribute";

    private const int LayoutParts = 3;

    private const int KeyPart = 2;

    private static readonly Dictionary<string, PinKind> AddMethods = new(StringComparer.Ordinal)
    {
        ["AddInputExecPin"] = PinKind.InputExec,
        ["AddOutputExecPin"] = PinKind.OutputExec,
        ["AddInputTypePin"] = PinKind.InputType,
        ["AddOutputTypePin"] = PinKind.OutputType,
    };

    private static readonly Dictionary<string, PinKind> Collections = new(StringComparer.Ordinal)
    {
        ["InputExecPins"] = PinKind.InputExec,
        ["OutputExecPins"] = PinKind.OutputExec,
        ["InputDataPins"] = PinKind.InputData,
        ["OutputDataPins"] = PinKind.OutputData,
        ["InputTypePins"] = PinKind.InputType,
        ["OutputTypePins"] = PinKind.OutputType,
    };

    private static readonly string[] CollectionAdds = ["Add", "AddRange", "Insert"];

    public static bool IsUserSource(Location location) =>
        location.IsInSource && !(location.SourceTree?.FilePath ?? string.Empty).EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);

    public static Location? UserLocation(ISymbol symbol) => symbol.Locations.FirstOrDefault(IsUserSource);

    public static AttributeData? Find(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

    public static Location AttributeLocation(AttributeData attribute, Location fallback) =>
        attribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax && IsUserSource(syntax.GetLocation()) ? syntax.GetLocation() : fallback;

    public static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword));

    public static List<PinProperty> PinProperties(INamedTypeSymbol type)
    {
        List<PinProperty> pins = [];
        foreach (IPropertySymbol property in type.GetMembers().OfType<IPropertySymbol>().Where(candidate => !candidate.IsStatic && !candidate.IsIndexer && UserLocation(candidate) is not null && !IsObservableProperty(candidate)))
        {
            if (PinReader.TryClassify(property.Type, out PinKind kind, out bool generic, out ITypeSymbol? argument))
            {
                pins.Add(new PinProperty(property, kind, generic, argument));
            }
        }

        return [.. pins.OrderBy(pin => UserLocation(pin.Symbol)?.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal).ThenBy(pin => UserLocation(pin.Symbol)?.SourceSpan.Start ?? 0)];
    }

    public static bool IsObservableProperty(IPropertySymbol property) =>
        property.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == ObservablePropertyName);

    public static bool HasSuffix(string name) =>
        name.Length > PinReader.PinSuffix.Length && name.EndsWith(PinReader.PinSuffix, StringComparison.Ordinal);

    public static string KeyOf(IPropertySymbol property)
    {
        if (ExplicitKey(Find(property, PinReader.PinAttribute)) is { } key)
        {
            return key;
        }

        return HasSuffix(property.Name) ? property.Name.Substring(0, property.Name.Length - PinReader.PinSuffix.Length) : property.Name;
    }

    public static string? ExplicitKey(AttributeData? attribute) =>
        attribute is { ConstructorArguments.Length: 1 } ? attribute.ConstructorArguments[0].Value as string : null;

    public static string? PinTypeMember(IPropertySymbol property) =>
        Find(property, PinReader.PinTypeAttribute) is { ConstructorArguments.Length: 1 } attribute ? attribute.ConstructorArguments[0].Value as string : null;

    public static IEnumerable<(PinKind Kind, string Key)> DeclaredKeys(INamedTypeSymbol type)
    {
        if (Find(type, PinReader.LayoutAttribute) is { } layout)
        {
            foreach (TypedConstant entry in layout.ConstructorArguments.SelectMany(argument => argument.Kind == TypedConstantKind.Array ? argument.Values : [argument]))
            {
                if (entry.Value is string text && TryParseLayout(text, out PinKind kind, out string key))
                {
                    yield return (kind, key);
                }
            }

            yield break;
        }

        foreach (PinProperty pin in PinProperties(type).Where(candidate => candidate.Symbol.IsPartialDefinition))
        {
            yield return (pin.Kind, KeyOf(pin.Symbol));
        }
    }

    public static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? ancestor = type.BaseType; ancestor is not null && ancestor.SpecialType != SpecialType.System_Object; ancestor = ancestor.BaseType)
        {
            yield return ancestor;
        }
    }

    public static bool IsObservableBaseType(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: ObservableValueName, Arity: 1 } named && named.TypeArguments[0].Name == BaseTypeName;

    public static ITypeSymbol? MemberType(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(name))
            {
                switch (member)
                {
                    case IFieldSymbol field:
                        return field.Type;
                    case IPropertySymbol property:
                        return property.Type;
                }
            }
        }

        return null;
    }

    public static bool IsDeclarePinsCall(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0 && CalleeName(invocation, out string? receiver) == DeclarePinsName && receiver is null;

    public static ImperativeAdd? AsImperativeAdd(InvocationExpressionSyntax invocation)
    {
        string? method = CalleeName(invocation, out string? receiver);
        if (method is null)
        {
            return null;
        }

        if (receiver is null && AddMethods.TryGetValue(method, out PinKind added))
        {
            return new ImperativeAdd(added, invocation);
        }

        if (receiver is not null && Collections.TryGetValue(receiver, out PinKind collection) && CollectionAdds.Contains(method, StringComparer.Ordinal))
        {
            return new ImperativeAdd(collection, invocation);
        }

        return null;
    }

    public static IEnumerable<ConstructorDeclarationSyntax> Constructors(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences
            .Where(reference => IsUserSource(reference.GetSyntax().GetLocation()))
            .SelectMany(reference => reference.GetSyntax().ChildNodes().OfType<ConstructorDeclarationSyntax>())
            .Where(constructor => !constructor.Modifiers.Any(SyntaxKind.StaticKeyword));

    public static IEnumerable<InvocationExpressionSyntax> Invocations(ConstructorDeclarationSyntax constructor) =>
        ((SyntaxNode?)constructor.Body ?? constructor.ExpressionBody)?.DescendantNodes().OfType<InvocationExpressionSyntax>() ?? [];

    private static string? CalleeName(InvocationExpressionSyntax invocation, out string? receiver)
    {
        receiver = null;
        switch (invocation.Expression)
        {
            case IdentifierNameSyntax identifier:
                return identifier.Identifier.Text;
            case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access:
                return access.Name.Identifier.Text;
            case MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax target } access:
                receiver = target.Identifier.Text;
                return access.Name.Identifier.Text;
            case MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } inner } access:
                receiver = inner.Name.Identifier.Text;
                return access.Name.Identifier.Text;
            default:
                return null;
        }
    }

    private static bool TryParseLayout(string text, out PinKind kind, out string key)
    {
        string[] parts = text.Split(['.'], LayoutParts);
        kind = default;
        key = string.Empty;
        if (parts.Length < LayoutParts)
        {
            return false;
        }

        key = parts[KeyPart];
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
}
