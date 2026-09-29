#nullable enable
using System.Collections.Generic;

namespace NetPrints.Projects;

/// <summary>
/// Result of <see cref="IProjectSystem.BuildAsync"/> (project-system.md §4).
/// </summary>
/// <param name="Success">Whether the build succeeded (exit code 0).</param>
/// <param name="Messages">Messages parsed from the build's output, de-duplicated and ordered by
/// file, line and code.</param>
/// <param name="OutputAssemblyPath">The built assembly's path (<c>-getProperty:TargetPath</c>), or
/// <see langword="null"/> if the build failed before that property could be evaluated.</param>
/// <param name="Log">The build's full, unparsed stdout and stderr.</param>
public sealed record BuildResult(
    bool Success,
    IReadOnlyList<ProjectMessage> Messages,
    string? OutputAssemblyPath,
    string Log);
