using System.Globalization;
using System.Text;

namespace NetPrints.Catalog;

/// <summary>Writes .NET strings as C# string literals. Shared source: no file access, no System.Text.Json.</summary>
internal static class CSharpLiteral
{
    private const int FirstNonControlCharacter = 0x20;

    private const int DeleteCharacter = 0x7F;

    private const int LastC1ControlCharacter = 0x9F;

    private const char LineSeparator = '\u2028';

    private const char ParagraphSeparator = '\u2029';

    /// <summary>Quotes <paramref name="value"/> as a regular C# string literal.</summary>
    /// <param name="value">The text.</param>
    /// <returns>
    /// The literal with double quotes: <c>"</c> and <c>\</c> and the short escapes for line breaks and tabs, <c>\uXXXX</c> (upper-case hex) for other
    /// control characters, the C# line terminators U+0085, U+2028 and U+2029, and unpaired surrogates; every other character is written as is.
    /// </returns>
    public static string Quote(string value)
    {
        Guard.NotNull(value, nameof(value));

        StringBuilder builder = new(value.Length + 2);
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                    {
                        builder.Append(c).Append(value[index + 1]);
                        index++;
                    }
                    else if (NeedsEscape(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    private static bool NeedsEscape(char c) =>
        c < FirstNonControlCharacter
        || (c >= DeleteCharacter && c <= LastC1ControlCharacter)
        || c == LineSeparator
        || c == ParagraphSeparator
        || char.IsSurrogate(c);
}
