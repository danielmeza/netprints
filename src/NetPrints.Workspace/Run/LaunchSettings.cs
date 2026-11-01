using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Workspace.Run;

/// <summary>An immutable snapshot of one read of a project's <c>Properties/launchSettings.json</c>.</summary>
public sealed record LaunchSettings
{
    /// <summary>The path of the file, relative to the project folder.</summary>
    public const string RelativePath = "Properties/launchSettings.json";

    /// <summary>The settings of a project with no file: no profiles and no problems.</summary>
    public static LaunchSettings None { get; } = new();

    /// <summary>The full path of the file; <see langword="null"/> for <see cref="None"/>.</summary>
    public string? FilePath { get; init; }

    /// <summary>The profiles in file order. Kept when NPR002 is reported, empty after NPR001.</summary>
    public IReadOnlyList<LaunchProfile> Profiles { get; init; } = [];

    /// <summary>The problems of the read: NPR001 and NPR002 only.</summary>
    public IReadOnlyList<RunProblem> Problems { get; init; } = [];

    /// <summary>Whether the file has comments, which makes the Run section read-only.</summary>
    public bool HasComments { get; init; }

    /// <summary>Whether the read found no problem.</summary>
    public bool IsUsable => Problems.Count == 0;

    /// <summary>Finds a profile by name, ignoring case.</summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The first profile with that name, or <see langword="null"/>.</returns>
    public LaunchProfile? Find(string name) =>
        Profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The profile a run uses when none is named: the first in file order that can run the project.</summary>
    /// <param name="outputType">The project's output type.</param>
    /// <returns>The profile, or <see langword="null"/> when the file is not usable or no profile can run.</returns>
    public LaunchProfile? DefaultProfile(BinaryType outputType) =>
        IsUsable ? Profiles.FirstOrDefault(profile => profile.AvailabilityFor(outputType).CanRun) : null;
}
