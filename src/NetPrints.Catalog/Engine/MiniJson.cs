using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NetPrints.Catalog;

/// <summary>A JSON number kept as its source text; the consumer decides how to read it.</summary>
/// <param name="Text">The number as written.</param>
internal sealed record MiniJsonNumber(string Text);

/// <summary>
/// A strict JSON reader that builds plain .NET values (<c>Dictionary&lt;string, object?&gt;</c>, <c>List&lt;object?&gt;</c>,
/// <c>string</c>, <see cref="MiniJsonNumber"/>, <c>bool</c>, null) without System.Text.Json, so it also compiles into
/// the source generator. Defects throw <see cref="FormatException"/>.
/// </summary>
internal sealed class MiniJson
{
    private const int MaxDepth = 64;

    private readonly string text;

    private int position;

    private MiniJson(string text)
    {
        this.text = text;
    }

    public static object? Parse(string json)
    {
        MiniJson reader = new(json);
        reader.SkipWhitespace();
        object? value = reader.ReadValue(0);
        reader.SkipWhitespace();
        if (reader.position != json.Length)
        {
            throw reader.Error("unexpected content after the value");
        }

        return value;
    }

    private FormatException Error(string message) =>
        new(string.Format(CultureInfo.InvariantCulture, "{0} at offset {1}", message, position));

    private void SkipWhitespace()
    {
        while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\n' || text[position] == '\r'))
        {
            position++;
        }
    }

    private object? ReadValue(int depth)
    {
        if (depth > MaxDepth)
        {
            throw Error("nesting is too deep");
        }

        if (position >= text.Length)
        {
            throw Error("unexpected end of input");
        }

        char current = text[position];
        switch (current)
        {
            case '{':
                return ReadObject(depth);
            case '[':
                return ReadArray(depth);
            case '"':
                return ReadString();
            case 't':
                ReadLiteral("true");
                return true;
            case 'f':
                ReadLiteral("false");
                return false;
            case 'n':
                ReadLiteral("null");
                return null;
            default:
                return ReadNumber();
        }
    }

    private void ReadLiteral(string literal)
    {
        if (string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
        {
            throw Error("invalid literal");
        }

        position += literal.Length;
    }

    private Dictionary<string, object?> ReadObject(int depth)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        position++;
        SkipWhitespace();
        if (TryConsume('}'))
        {
            return result;
        }

        while (true)
        {
            SkipWhitespace();
            if (position >= text.Length || text[position] != '"')
            {
                throw Error("expected a property name");
            }

            string name = ReadString();
            SkipWhitespace();
            Expect(':');
            SkipWhitespace();
            result[name] = ReadValue(depth + 1);
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            Expect(',');
        }
    }

    private List<object?> ReadArray(int depth)
    {
        List<object?> result = [];
        position++;
        SkipWhitespace();
        if (TryConsume(']'))
        {
            return result;
        }

        while (true)
        {
            SkipWhitespace();
            result.Add(ReadValue(depth + 1));
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            Expect(',');
        }
    }

    private bool TryConsume(char expected)
    {
        if (position < text.Length && text[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    private void Expect(char expected)
    {
        if (!TryConsume(expected))
        {
            throw Error("expected '" + expected + "'");
        }
    }

    private string ReadString()
    {
        position++;
        StringBuilder builder = new();
        while (position < text.Length)
        {
            char current = text[position++];
            if (current == '"')
            {
                return builder.ToString();
            }

            if (current < ' ')
            {
                throw Error("control character in a string");
            }

            builder.Append(current == '\\' ? ReadEscape() : current);
        }

        throw Error("unterminated string");
    }

    private char ReadEscape()
    {
        if (position >= text.Length)
        {
            throw Error("unterminated escape");
        }

        char escape = text[position++];
        switch (escape)
        {
            case '"':
            case '\\':
            case '/':
                return escape;
            case 'b':
                return '\b';
            case 'f':
                return '\f';
            case 'n':
                return '\n';
            case 'r':
                return '\r';
            case 't':
                return '\t';
            case 'u':
                return ReadUnicodeEscape();
            default:
                throw Error("invalid escape");
        }
    }

    private char ReadUnicodeEscape()
    {
        const int digits = 4;
        if (position + digits > text.Length
            || !int.TryParse(text.Substring(position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
        {
            throw Error("invalid unicode escape");
        }

        position += digits;
        return (char)code;
    }

    private MiniJsonNumber ReadNumber()
    {
        int start = position;
        if (position < text.Length && text[position] == '-')
        {
            position++;
        }

        int digitsStart = position;
        while (position < text.Length && (char.IsDigit(text[position]) || text[position] == '.' || text[position] == 'e' || text[position] == 'E' || text[position] == '+' || text[position] == '-'))
        {
            position++;
        }

        string token = text.Substring(start, position - start);
        if (position == digitsStart || !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || !char.IsDigit(text[digitsStart]))
        {
            position = start;
            throw Error("invalid value");
        }

        return new MiniJsonNumber(token);
    }
}
