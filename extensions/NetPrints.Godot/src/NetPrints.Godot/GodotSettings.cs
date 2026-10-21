using System.Text.Json.Serialization;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Godot;

/// <summary>The settings section of the Godot extension.</summary>
/// <param name="ExecutablePath">The Godot executable the status bar reports; empty to use the <c>GODOT4</c> variable.</param>
public sealed record GodotSettings([property: SettingPath(PathKind.File)] string ExecutablePath = "")
{
    /// <summary>Gets the section: id <see cref="GodotExtension.Id"/>, no executable, the executable path validated as absolute.</summary>
    public static ExtensionSettingsDescriptor<GodotSettings> Descriptor { get; } =
        new(GodotExtension.Id, GodotJsonContext.Default.GodotSettings, new GodotSettings())
        {
            Validator = Validate,
        };

    private static List<string> Validate(GodotSettings settings) =>
        settings.ExecutablePath.Length > 0 && !Path.IsPathRooted(settings.ExecutablePath)
            ? [$"The Godot executable '{settings.ExecutablePath}' is not a full path."]
            : [];
}

/// <summary>The JSON metadata of <see cref="GodotSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GodotSettings))]
public sealed partial class GodotJsonContext : JsonSerializerContext;
