using System.Text.Json.Serialization;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>Source-generated JSON for the persisted dock layout.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(DockLayoutDto))]
internal sealed partial class DockJsonContext : JsonSerializerContext;
