using System.Text.Json.Serialization;

namespace NetPrints.Editor.State;

/// <summary>What the editor does when it starts with no project argument.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StartupBehavior>))]
public enum StartupBehavior
{
    /// <summary>Show the start page. The default.</summary>
    ShowStartPage,

    /// <summary>Open the project that was opened last; the start page shows with the reason when that fails.</summary>
    ReopenLastProject,
}
