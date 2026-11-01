using System.Collections.Generic;
using System.Collections.Immutable;
using NetPrints.Core;

namespace NetPrints.Workspace.Run;

/// <summary>Everything <see cref="RunProfileResolver.Resolve"/> needs, already read from the project and the process.</summary>
public sealed record RunInputs
{
    /// <summary>Gets the full path of the project file.</summary>
    public required string ProjectFilePath { get; init; }

    /// <summary>Gets the project's output type.</summary>
    public required BinaryType OutputType { get; init; }

    /// <summary>Gets the build configuration.</summary>
    public required string Configuration { get; init; }

    /// <summary>Gets the launch settings of the project.</summary>
    public required LaunchSettings LaunchSettings { get; init; }

    /// <summary>Gets the profile to run; <see langword="null"/> for the default profile.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gets a value indicating whether the project runs with no profile.</summary>
    public bool NoLaunchProfile { get; init; }

    /// <summary>Gets the arguments that replace the profile's own; <see langword="null"/> keeps the profile's.</summary>
    public IReadOnlyList<string>? ArgumentOverride { get; init; }

    /// <summary>Gets the evaluated project properties the profile's <c>$(Name)</c> tokens read; keys compare case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> ProjectProperties { get; init; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>Gets a snapshot of the process environment.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the variables NetPrints' MSBuild registration added or overwrote; may be empty.</summary>
    public IReadOnlyList<MsBuildVariable> MsBuildVariables { get; init; } = [];
}
