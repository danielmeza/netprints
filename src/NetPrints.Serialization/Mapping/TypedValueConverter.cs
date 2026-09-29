#nullable enable
using System;
using System.Globalization;
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
            bool b => b ? "true" : "false",
            byte v => v.ToString(CultureInfo.InvariantCulture),
            sbyte v => v.ToString(CultureInfo.InvariantCulture),
            short v => v.ToString(CultureInfo.InvariantCulture),
            ushort v => v.ToString(CultureInfo.InvariantCulture),
            int v => v.ToString(CultureInfo.InvariantCulture),
            uint v => v.ToString(CultureInfo.InvariantCulture),
            long v => v.ToString(CultureInfo.InvariantCulture),
            ulong v => v.ToString(CultureInfo.InvariantCulture),
            float v => v.ToString("R", CultureInfo.InvariantCulture),
            double v => v.ToString("R", CultureInfo.InvariantCulture),
            decimal v => v.ToString(CultureInfo.InvariantCulture),
            char v => v.ToString(),
            string v => v,
            Enum v => v.ToString(),
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
            return value.Type switch
            {
                "System.Boolean" => bool.Parse(text),
                "System.Byte" => byte.Parse(text, CultureInfo.InvariantCulture),
                "System.SByte" => sbyte.Parse(text, CultureInfo.InvariantCulture),
                "System.Int16" => short.Parse(text, CultureInfo.InvariantCulture),
                "System.UInt16" => ushort.Parse(text, CultureInfo.InvariantCulture),
                "System.Int32" => int.Parse(text, CultureInfo.InvariantCulture),
                "System.UInt32" => uint.Parse(text, CultureInfo.InvariantCulture),
                "System.Int64" => long.Parse(text, CultureInfo.InvariantCulture),
                "System.UInt64" => ulong.Parse(text, CultureInfo.InvariantCulture),
                "System.Single" => float.Parse(text, CultureInfo.InvariantCulture),
                "System.Double" => double.Parse(text, CultureInfo.InvariantCulture),
                "System.Decimal" => decimal.Parse(text, CultureInfo.InvariantCulture),
                "System.Char" => ParseChar(text),
                "System.String" => text,
                _ => ParseEnum(value.Type, text),
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new DocumentFormatException($"Value '{text}' is not a valid '{value.Type}' literal.", inner: ex);
        }
    }

    private static char ParseChar(string text)
    {
        if (text.Length != 1)
        {
            throw new FormatException($"'{text}' is not a single character.");
        }

        return text[0];
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
