#nullable enable
using System;
using System.Globalization;
using NetPrints.Core;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Converts a genuine, runtime-typed .NET value (an unconnected value using its pin's own CLR type, an
/// explicit method-parameter default) to and from a <see cref="TypedValue"/> (document-format.md §1.6),
/// using <see cref="CultureInfo.InvariantCulture"/> throughout. A pin whose unconnected value is stored
/// pre-formatted as a string by the model itself (an enum-typed pin; see <see cref="NetPrints.Graph.NodeInputDataPin.UnconnectedValue"/>'s
/// validation) is handled directly by the mapper using the pin's declared type, not through this class
/// (implementation-notes.md, T028/T035).
/// </summary>
public static class TypedValueConverter
{
    /// <summary>
    /// Converts <paramref name="value"/> to a <see cref="TypedValue"/>: <see cref="TypedValue.Type"/> is
    /// <paramref name="value"/>'s runtime type's full name, and <see cref="TypedValue.Value"/> is its
    /// string form (member name, flags combined with <c>", "</c>, for an enum; invariant-culture
    /// <c>ToString</c> for every other supported type).
    /// </summary>
    /// <param name="value">Value to convert; must not be <see langword="null"/> (callers convert a
    /// <see langword="null"/> value to a <see langword="null"/> <see cref="TypedValue"/> instead of
    /// calling this method).</param>
    /// <param name="where">Location (e.g. <c>"&lt;nodeId&gt;/&lt;pin&gt;"</c>) used in the exception
    /// message for an unsupported type.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="DocumentFormatException"><paramref name="value"/>'s runtime type is not one of
    /// the supported primitive, decimal, char, string or enum types.</exception>
    public static TypedValue ToTypedValue(object value, string where)
    {
        Type type = value.GetType();
        string typeName = type.FullName ?? type.Name;

        string formatted = value switch
        {
            Enum v => v.ToString(),
            _ when PrimitiveKinds.TryGet(typeName, out PrimitiveKind? kind) => kind.Format(value),
            _ => throw new DocumentFormatException($"Unsupported value type '{typeName}' in {where}"),
        };

        return new TypedValue(typeName, formatted);
    }

    /// <summary>
    /// Converts <paramref name="value"/> back to a runtime value: parses <see cref="TypedValue.Value"/>
    /// according to <see cref="TypedValue.Type"/>, invariant culture.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>The converted value, or <see langword="null"/> if <see cref="TypedValue.Value"/> is
    /// <see langword="null"/>.</returns>
    /// <exception cref="DocumentFormatException"><see cref="TypedValue.Type"/> is not one of the
    /// supported types, an enum type that cannot be resolved, or <see cref="TypedValue.Value"/> is not a
    /// valid literal for it.</exception>
    public static object? FromTypedValue(TypedValue value)
    {
        if (value.Value is null)
        {
            return null;
        }

        string text = value.Value;

        try
        {
            return PrimitiveKinds.TryGet(value.Type, out PrimitiveKind? kind)
                ? kind.Parse(text)
                : ParseEnum(value.Type, text);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new DocumentFormatException($"Value '{text}' is not a valid '{value.Type}' literal.", inner: ex);
        }
    }

    private static object ParseEnum(string typeName, string text)
    {
        Type? enumType = Type.GetType(typeName, throwOnError: false);
        if (enumType is null || !enumType.IsEnum)
        {
            throw new DocumentFormatException($"Unsupported value type '{typeName}'.");
        }

        return Enum.Parse(enumType, text);
    }
}
