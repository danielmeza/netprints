using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Catalog;

/// <summary>Reads and writes an enum as its lower-case name, the catalog wire form.</summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
internal sealed class LowerCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse(reader.GetString(), ignoreCase: true, out TEnum value)
            && Enum.IsDefined(value))
        {
            return value;
        }

        throw new JsonException($"'{reader.GetString()}' is not a valid {typeof(TEnum).Name}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
