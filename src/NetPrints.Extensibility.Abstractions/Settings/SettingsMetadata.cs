using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>What a path setting points at.</summary>
[Experimental(ExperimentalApiIds.SettingsPages, UrlFormat = ExperimentalApiIds.UrlFormat)]
public enum PathKind
{
    /// <summary>A file.</summary>
    File,

    /// <summary>A folder.</summary>
    Folder,
}

/// <summary>Marks a settings property whose change applies only after a restart; the exported schema carries <c>x-netprints-restart</c>.</summary>
[Experimental(ExperimentalApiIds.SettingsPages, UrlFormat = ExperimentalApiIds.UrlFormat)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiresRestartAttribute : Attribute;

/// <summary>Marks a string settings property as a path; the exported schema carries <c>x-netprints-path</c> and the page shows a path picker.</summary>
/// <param name="kind">Whether the path is a file or a folder.</param>
[Experimental(ExperimentalApiIds.SettingsPages, UrlFormat = ExperimentalApiIds.UrlFormat)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingPathAttribute(PathKind kind) : Attribute
{
    /// <summary>Gets what the path points at.</summary>
    public PathKind Kind { get; } = kind;
}

/// <summary>Names the provider of the values an open string set offers (preset ids, language ids); the exported schema carries <c>x-netprints-choices</c>.</summary>
/// <param name="providerId">The id of the choices provider.</param>
[Experimental(ExperimentalApiIds.SettingsPages, UrlFormat = ExperimentalApiIds.UrlFormat)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingChoicesAttribute(string providerId) : Attribute
{
    /// <summary>Gets the id of the choices provider.</summary>
    public string ProviderId { get; } = providerId;
}
