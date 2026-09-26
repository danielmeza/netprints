#nullable enable

namespace NetPrints.Projects;

/// <summary>
/// A reference assembly resolved by <see cref="IProjectSystem.LoadAsync"/> (project-system.md §4):
/// moved from the legacy <c>References</c> reflection provider input, now sourced from MSBuild/NuGet
/// instead of a hand-rolled probe table.
/// </summary>
/// <param name="Path">Full path of the assembly file.</param>
/// <param name="DocumentationPath">Full path of the assembly's XML documentation file (a sibling
/// <c>.xml</c> next to <paramref name="Path"/>), or <see langword="null"/> if it does not exist.</param>
public sealed record ResolvedAssembly(string Path, string? DocumentationPath);
