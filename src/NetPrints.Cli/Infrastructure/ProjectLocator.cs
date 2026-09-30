using System;
using System.IO;
using System.Linq;

namespace NetPrints.Cli.Infrastructure;

/// <summary>The outcome of <see cref="ProjectLocator.Locate"/>: a project path, or the reason there is none.</summary>
/// <param name="Path">The full path of the project file; <see langword="null"/> when <paramref name="Error"/> is set.</param>
/// <param name="Error">Why no project was resolved; <see langword="null"/> on success.</param>
internal sealed record ProjectLocation(string? Path, string? Error);

/// <summary>Resolves the optional <c>&lt;project&gt;</c> argument of a command (contracts/cli.md §3).</summary>
internal static class ProjectLocator
{
    private const string ProjectExtension = ".csproj";

    /// <summary>
    /// Resolves an existing <c>.csproj</c> file to itself, an existing directory to its single project, and an omitted
    /// argument to the single project of the environment's current directory.
    /// </summary>
    /// <param name="argument">The project argument, or <see langword="null"/> when omitted.</param>
    /// <param name="environment">Supplies the current directory relative paths resolve against.</param>
    /// <returns>The project path, or the reason (a missing path, a foreign file, no or several projects).</returns>
    public static ProjectLocation Locate(string? argument, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string path = Path.GetFullPath(argument ?? ".", environment.CurrentDirectory);

        if (File.Exists(path))
        {
            return path.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase)
                ? new ProjectLocation(path, null)
                : new ProjectLocation(null, $"'{path}' is not a {ProjectExtension} file.");
        }

        if (!Directory.Exists(path))
        {
            return new ProjectLocation(null, $"'{path}' does not exist.");
        }

        string[] candidates = Directory.EnumerateFiles(path, "*" + ProjectExtension, SearchOption.TopDirectoryOnly)
            .Where(file => file.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return candidates.Length switch
        {
            0 => new ProjectLocation(null, $"'{path}' contains no {ProjectExtension} file."),
            1 => new ProjectLocation(candidates[0], null),
            _ => new ProjectLocation(null,
                $"'{path}' contains several projects ({string.Join(", ", candidates.Select(Path.GetFileName))}); pass the one to use."),
        };
    }
}
