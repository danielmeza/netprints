using System.Text.Json.Serialization;

namespace NetPrints.Editor.State;

/// <summary>The theme variant the editor uses (FR-082).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EditorTheme>))]
public enum EditorTheme
{
    /// <summary>The dark theme. The default.</summary>
    [JsonStringEnumMemberName("dark")]
    Dark,

    /// <summary>The light theme.</summary>
    [JsonStringEnumMemberName("light")]
    Light,

    /// <summary>Follows the operating system's light or dark setting.</summary>
    [JsonStringEnumMemberName("system")]
    System,
}
