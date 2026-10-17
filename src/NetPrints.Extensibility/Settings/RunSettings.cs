using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// The built-in <c>run</c> settings section (contracts/settings-and-keybindings.md §5).
/// </summary>
public sealed record RunSettings
{
    /// <summary>
    /// The id of the section; it is a top-level key of the settings file.
    /// </summary>
    public const string SectionId = "run";

    private const int MinStopTimeoutSeconds = 1;
    private const int MaxStopTimeoutSeconds = 600;
    private const int MaxLinuxTerminalLength = 512;
    private const string NoControlCharacters = @"^[^\x00-\x1F\x7F]*$";

    /// <summary>
    /// Seconds a stopped program gets to exit before its process tree is killed.
    /// </summary>
    [Range(MinStopTimeoutSeconds, MaxStopTimeoutSeconds)]
    [Description("Seconds a stopped program has to exit before its process tree is killed")]
    public int StopTimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// The terminal command that hosts "Run in external terminal" on Linux; empty detects one.
    /// </summary>
    [Description("Linux only: the terminal command that hosts a run in an external terminal. Empty detects one")]
    [StringLength(MaxLinuxTerminalLength)]
    [RegularExpression(NoControlCharacters)]
    public string LinuxTerminal { get; init; } = "";

    /// <summary>
    /// The descriptor of the section.
    /// </summary>
    [Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public static ExtensionSettingsDescriptor<RunSettings> Descriptor { get; } =
        new(SectionId, RunSettingsJsonContext.Default.RunSettings, new RunSettings())
        {
            Validator = Validate,
        };

    private static string[] Validate(RunSettings value)
        => [.. new RunSettingsValidator().Validate(null, value).Failures ?? []];
}

/// <summary>
/// Validates <see cref="RunSettings"/> through the DataAnnotations on its members (generated).
/// </summary>
[OptionsValidator]
internal sealed partial class RunSettingsValidator : IValidateOptions<RunSettings>;

/// <summary>
/// Source-generated JSON metadata for <see cref="RunSettings"/>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(RunSettings))]
internal sealed partial class RunSettingsJsonContext : JsonSerializerContext;
