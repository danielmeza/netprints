using System.Text.Json.Serialization;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// The built-in <c>netprints</c> settings section (extension-points.md §7).
/// </summary>
public sealed record NetPrintsSettings
{
    /// <summary>
    /// The id of the built-in section; also the extension id of the built-in extension.
    /// </summary>
    public const string SectionId = "netprints";

    /// <summary>
    /// Directories searched for extensions; each immediate subfolder holding a manifest is loaded.
    /// </summary>
    public IReadOnlyList<string> ExtensionPaths { get; init; } = [];

    /// <summary>
    /// Full <c>.csproj</c> paths whose <c>NetPrintsExtension</c> items the user allowed.
    /// </summary>
    public IReadOnlyList<string> TrustedProjects { get; init; } = [];

    /// <summary>
    /// The default: no extension paths and no trusted projects.
    /// </summary>
    public static NetPrintsSettings Empty { get; } = new();

    /// <summary>
    /// The descriptor of the built-in section.
    /// </summary>
    public static ExtensionSettingsDescriptor<NetPrintsSettings> Descriptor { get; } =
        new(SectionId, NetPrintsSettingsJsonContext.Default.NetPrintsSettings, Empty);
}

/// <summary>
/// Source-generated JSON metadata for <see cref="NetPrintsSettings"/>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(NetPrintsSettings))]
internal sealed partial class NetPrintsSettingsJsonContext : JsonSerializerContext;
