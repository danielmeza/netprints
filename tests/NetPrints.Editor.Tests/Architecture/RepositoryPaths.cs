namespace NetPrints.Editor.Tests.Architecture;

/// <summary>Shared repository-root lookup for the architecture gate tests (ED-T10).</summary>
internal static class RepositoryPaths
{
    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> to find the checked-out repository root.</summary>
    /// <returns>The repository root directory (the one containing <c>NetPrints.slnx</c>).</returns>
    /// <exception cref="InvalidOperationException">No <c>NetPrints.slnx</c> was found above the running tests' output directory.</exception>
    public static string Root()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NetPrints.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"Could not find the repository root (NetPrints.slnx) above '{AppContext.BaseDirectory}'.");
    }
}
