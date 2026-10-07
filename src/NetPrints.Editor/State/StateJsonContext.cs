using System.Text.Json.Serialization;

namespace NetPrints.Editor.State;

/// <summary>Source-generated JSON for the per-user state files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(WindowState))]
[JsonSerializable(typeof(LayoutState))]
[JsonSerializable(typeof(RecentState))]
[JsonSerializable(typeof(StartState))]
[JsonSerializable(typeof(SessionState))]
internal sealed partial class StateJsonContext : JsonSerializerContext;
