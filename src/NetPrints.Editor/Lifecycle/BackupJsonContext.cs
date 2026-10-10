using System.Text.Json.Serialization;

namespace NetPrints.Editor.Lifecycle;

/// <summary>Source-generated JSON for the backup manifest.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BackupManifest))]
internal sealed partial class BackupJsonContext : JsonSerializerContext;
