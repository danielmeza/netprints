using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NetPrints.Core;

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
    [NonBlankItems]
    [RequiresRestart]
    [SettingPath(PathKind.Folder)]
    [Description("Folders searched for extensions. Each subfolder that holds a manifest is loaded at the next start")]
    public IReadOnlyList<string> ExtensionPaths { get; init; } = [];

    /// <summary>
    /// Full <c>.csproj</c> paths whose <c>NetPrintsExtension</c> items the user allowed.
    /// </summary>
    [NonBlankItems]
    [SettingPath(PathKind.File)]
    [Description("Project files whose extensions you allowed to load")]
    public IReadOnlyList<string> TrustedProjects { get; init; } = [];

    /// <summary>
    /// Whether to enable UI animations (disable for accessibility/Reduced Motion).
    /// </summary>
    [Description("Turns the interface animations on. Turn them off to reduce motion")]
    public bool EnableAnimations { get; init; } = true;

    /// <summary>
    /// The UI language as a culture name such as <c>es-MX</c>; empty means the operating system's culture. A change applies after a restart.
    /// </summary>
    [CultureName]
    [RequiresRestart]
    [Description("The interface language as a culture name such as es-MX. Empty uses the system language")]
    public string Language { get; init; } = "";

    /// <summary>
    /// The default: no extension paths and no trusted projects, animations enabled.
    /// </summary>
    public static NetPrintsSettings Empty { get; } = new();

    /// <summary>
    /// The descriptor of the built-in section.
    /// </summary>
    [Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public static ExtensionSettingsDescriptor<NetPrintsSettings> Descriptor { get; } =
        new(SectionId, NetPrintsSettingsJsonContext.Default.NetPrintsSettings, Empty)
        {
            Validator = Validate,
        };

    private static string[] Validate(NetPrintsSettings value)
        => [.. new NetPrintsSettingsValidator().Validate(null, value).Failures ?? []];
}

/// <summary>
/// Source-generated JSON metadata for <see cref="NetPrintsSettings"/>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(NetPrintsSettings))]
internal sealed partial class NetPrintsSettingsJsonContext : JsonSerializerContext;
