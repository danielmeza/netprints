using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NetPrints.Catalog;

/// <summary>Reads the attributes catalogs care about from Roslyn symbols, by full name (the attribute types are embedded per assembly).</summary>
internal static class SymbolAttributes
{
    public const string TypeAttribute = "NetPrints.Annotations.NetPrintsTypeAttribute";

    public const string NodeAttribute = "NetPrints.Annotations.NetPrintsNodeAttribute";

    public const string IgnoreAttribute = "NetPrints.Annotations.NetPrintsIgnoreAttribute";

    public const string ObsoleteAttribute = "System.ObsoleteAttribute";

    private const string FlagsAttribute = "System.FlagsAttribute";

    private const string DisplayNameArgument = "DisplayName";

    private const string CategoryArgument = "Category";

    private const string KeywordsArgument = "Keywords";

    private const string TokenSeparator = ", ";

    private const string FloatFormat = "R";

    public static bool Has(ISymbol symbol, string attributeName) => Find(symbol, attributeName) is not null;

    public static AttributeData? Find(ISymbol symbol, string attributeName) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsNamed(attribute, attributeName));

    public static bool IsNamed(AttributeData attribute, string attributeName) =>
        attribute.AttributeClass is { } attributeClass && string.Equals(attributeClass.ToDisplayString(), attributeName, StringComparison.Ordinal);

    /// <summary>Reads <c>[Obsolete]</c>: <see langword="null"/> when the symbol is not obsolete.</summary>
    public static CatalogObsoleteInfo? ObsoleteOf(ISymbol symbol)
    {
        if (Find(symbol, ObsoleteAttribute) is not { } attribute)
        {
            return null;
        }

        string? message = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        bool error = attribute.ConstructorArguments.Length > 1 && attribute.ConstructorArguments[1].Value is true;
        return new CatalogObsoleteInfo { Message = message, Error = error };
    }

    /// <summary>Reads the node hint of <c>[NetPrintsType]</c> (types) or <c>[NetPrintsNode]</c> (methods).</summary>
    public static CatalogNodeHint? NodeHintOf(ISymbol symbol)
    {
        string attributeName = symbol is ITypeSymbol ? TypeAttribute : NodeAttribute;
        if (Find(symbol, attributeName) is not { } attribute)
        {
            return null;
        }

        string? displayName = null;
        string? category = null;
        List<string>? keywords = null;
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case DisplayNameArgument:
                    displayName = argument.Value.Value as string;
                    break;
                case CategoryArgument:
                    category = argument.Value.Value as string;
                    break;
                case KeywordsArgument when !argument.Value.IsNull:
                    keywords = [.. argument.Value.Values.Select(value => value.Value as string).OfType<string>()];
                    keywords.Sort(StringComparer.Ordinal);
                    break;
            }
        }

        if (displayName is null && category is null && (keywords is null || keywords.Count == 0))
        {
            return null;
        }

        return new CatalogNodeHint { DisplayName = displayName, Category = category, Keywords = keywords is { Count: > 0 } ? keywords : null };
    }

    /// <summary>Finds the constant a profile argument match refers to: a named argument, a constructor parameter, or a constructor position.</summary>
    public static TypedConstant? Argument(AttributeData attribute, CatalogArgumentMatch match)
    {
        if (match.Position is { } position)
        {
            return position < attribute.ConstructorArguments.Length ? attribute.ConstructorArguments[position] : null;
        }

        foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
        {
            if (string.Equals(named.Key, match.Name, StringComparison.Ordinal))
            {
                return named.Value;
            }
        }

        if (attribute.AttributeConstructor is { } constructor)
        {
            for (int index = 0; index < constructor.Parameters.Length && index < attribute.ConstructorArguments.Length; index++)
            {
                if (string.Equals(constructor.Parameters[index].Name, match.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.ConstructorArguments[index];
                }
            }
        }

        return null;
    }

    /// <summary>Renders an attribute argument as the text profiles compare with: enum flags as member names joined by <c>, </c>.</summary>
    public static RenderedArgument? Render(TypedConstant constant)
    {
        if (constant.IsNull || constant.Kind == TypedConstantKind.Error)
        {
            return null;
        }

        switch (constant.Kind)
        {
            case TypedConstantKind.Array:
                List<string> elements = [.. constant.Values.SelectMany(value => Render(value)?.Tokens ?? [])];
                return new RenderedArgument(string.Join(TokenSeparator, elements), elements, TokensOnly: true);
            case TypedConstantKind.Enum when constant.Type is INamedTypeSymbol enumType && constant.Value is { } enumValue:
                IReadOnlyList<string> names = EnumNames(enumType, enumValue);
                return new RenderedArgument(string.Join(TokenSeparator, names), names, TokensOnly: true);
            case TypedConstantKind.Type when constant.Value is ITypeSymbol type:
                string typeName = type.ToDisplayString();
                return new RenderedArgument(typeName, [typeName], TokensOnly: false);
            default:
                string? text = Scalar(constant.Value);
                return text is null ? null : new RenderedArgument(text, [text], TokensOnly: false);
        }
    }

    /// <summary>Renders a constant the way typed values in graphs and catalogs write it (invariant culture, lower-case booleans).</summary>
    public static string? Scalar(object? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        float single => single.ToString(FloatFormat, CultureInfo.InvariantCulture),
        double number => number.ToString(FloatFormat, CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static IReadOnlyList<string> EnumNames(INamedTypeSymbol enumType, object value)
    {
        ulong bits = Bits(value);
        List<(string Name, ulong Bits)> members =
        [
            .. enumType.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.IsConst && field.ConstantValue is not null)
                .Select(field => (field.Name, Bits(field.ConstantValue ?? 0))),
        ];

        foreach ((string name, ulong memberBits) in members)
        {
            if (memberBits == bits)
            {
                return [name];
            }
        }

        if (!Has(enumType, FlagsAttribute) || bits == 0)
        {
            return [Scalar(value) ?? string.Empty];
        }

        List<string> names = [];
        ulong covered = 0;
        foreach ((string name, ulong memberBits) in members)
        {
            if (memberBits != 0 && (bits & memberBits) == memberBits)
            {
                names.Add(name);
                covered |= memberBits;
            }
        }

        return covered == bits ? names : [Scalar(value) ?? string.Empty];
    }

    private static ulong Bits(object value) => value switch
    {
        byte or ushort or uint or ulong => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
        _ => unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
    };
}

/// <summary>An attribute argument rendered for profile matching.</summary>
/// <param name="Text">The whole value as text.</param>
/// <param name="Tokens">The members of a flags value or the elements of an array; the text itself otherwise.</param>
/// <param name="TokensOnly">Whether <c>contains</c> matches whole tokens (enums, arrays) instead of substrings.</param>
internal sealed record RenderedArgument(string Text, IReadOnlyList<string> Tokens, bool TokensOnly);
