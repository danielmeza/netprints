using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrints.Workspace;

/// <summary>A variable NetPrints' MSBuild registration added (<paramref name="PriorValue"/> is <see langword="null"/>) or overwrote.</summary>
/// <param name="Name">The variable's name, as the registration left it.</param>
/// <param name="PriorValue">The value the process had before the registration, or <see langword="null"/> when the variable did not exist.</param>
public sealed record MsBuildVariable(string Name, string? PriorValue)
{
    /// <summary>The variables whose value differs between the two snapshots, names compared <see cref="StringComparer.OrdinalIgnoreCase"/> on Windows and <see cref="StringComparer.Ordinal"/> elsewhere.</summary>
    /// <param name="before">The environment before the registration.</param>
    /// <param name="after">The environment after it.</param>
    /// <returns>Each added or changed variable, ordered by name, with its earlier value.</returns>
    public static IReadOnlyList<MsBuildVariable> Between(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after) =>
        Between(before, after, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>The variables whose value differs between the two snapshots, names compared with <paramref name="comparer"/>.</summary>
    /// <param name="before">The environment before the registration.</param>
    /// <param name="after">The environment after it.</param>
    /// <param name="comparer">How names are matched between the snapshots; values always compare ordinally.</param>
    /// <returns>Each added or changed variable, ordered by name, with its earlier value.</returns>
    public static IReadOnlyList<MsBuildVariable> Between(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after, StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(comparer);

        var prior = new Dictionary<string, string>(comparer);
        foreach ((string name, string value) in before)
        {
            prior[name] = value;
        }

        var changes = new List<MsBuildVariable>();
        foreach ((string name, string value) in after)
        {
            if (!prior.TryGetValue(name, out string? earlier))
            {
                changes.Add(new MsBuildVariable(name, null));
            }
            else if (!string.Equals(earlier, value, StringComparison.Ordinal))
            {
                changes.Add(new MsBuildVariable(name, earlier));
            }
        }

        return [.. changes.OrderBy(variable => variable.Name, StringComparer.Ordinal)];
    }
}
