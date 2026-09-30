using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Catalog;

/// <summary>Source-generated JSON metadata for reading configuration files and generating their schema.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    RespectNullableAnnotations = true,
    Converters = [typeof(LowerCaseEnumConverter<CatalogOutputFormat>)])]
[JsonSerializable(typeof(CatalogConfig))]
internal sealed partial class CatalogConfigJsonContext : JsonSerializerContext
{
}
