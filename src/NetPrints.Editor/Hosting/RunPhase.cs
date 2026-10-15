using System.Text.Json.Serialization;

namespace NetPrints.Editor.Hosting;

/// <summary>Where the last launched program is in its life, as <see cref="RunStateTracker"/> follows it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RunPhase>))]
public enum RunPhase
{
    /// <summary>No compile or run has happened since the editor started or the last build failed.</summary>
    [JsonStringEnumMemberName("notStarted")]
    NotStarted,

    /// <summary>The project is compiling.</summary>
    [JsonStringEnumMemberName("building")]
    Building,

    /// <summary>The program was started and has not exited.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The program exited; its exit code is known.</summary>
    [JsonStringEnumMemberName("exited")]
    Exited,
}
