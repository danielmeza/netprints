using System;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Catalog;

/// <summary>Reads strings like the default converter, but also keeps an unpaired surrogate written as a <c>\uXXXX</c> escape, which the writer emits and the default converter rejects.</summary>
internal sealed class LenientStringConverter : JsonConverter<string>
{
    private const int EscapeLength = 4;

    /// <inheritdoc />
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        try
        {
            return reader.GetString();
        }
        catch (InvalidOperationException)
        {
            byte[] raw = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray();
            return Unescape(Encoding.UTF8.GetString(raw));
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }

    private static string Unescape(string text)
    {
        StringBuilder builder = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (c != '\\' || index + 1 >= text.Length)
            {
                builder.Append(c);
                continue;
            }

            char escape = text[++index];
            switch (escape)
            {
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'u' when index + EscapeLength < text.Length
                    && int.TryParse(text.AsSpan(index + 1, EscapeLength), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code):
                    builder.Append((char)code);
                    index += EscapeLength;
                    break;
                default:
                    builder.Append(escape);
                    break;
            }
        }

        return builder.ToString();
    }
}
