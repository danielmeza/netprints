using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetPrints.Editor.Hosting.Automation;

/// <summary>
/// Source-generated serializer metadata for the automation protocol's line-delimited JSON: web
/// defaults (camelCase), omitting <see langword="null"/> properties.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AutomationRequest))]
[JsonSerializable(typeof(AutomationResponse))]
public sealed partial class AutomationJsonContext : JsonSerializerContext;
