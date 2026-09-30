using System.Text.Json.Serialization;

namespace NetPrints.Catalog;

/// <summary>Source-generated JSON metadata for reading catalog files and generating their schema.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    Converters =
    [
        typeof(LowerCaseEnumConverter<CatalogTypeKind>),
        typeof(LowerCaseEnumConverter<CatalogVisibility>),
        typeof(LowerCaseEnumConverter<CatalogPassType>),
        typeof(LowerCaseEnumConverter<CatalogVariableKind>),
    ])]
[JsonSerializable(typeof(CatalogDocument))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext
{
}
