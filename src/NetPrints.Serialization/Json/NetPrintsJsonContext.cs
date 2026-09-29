#nullable enable
using System.Text.Json.Serialization;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for <see cref="ClassDocument"/> and every type
/// it references (document-format.md §2.3): camelCase property names, enums written as strings, and
/// default/empty values omitted on write. <see cref="NetPrintsJsonOptions"/> combines this context's
/// metadata with each loaded extension's own resolver.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(ClassDocument))]
internal sealed partial class NetPrintsJsonContext : JsonSerializerContext;
