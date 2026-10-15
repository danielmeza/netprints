namespace NetPrints.Editor.Tests.Search;

/// <summary>Text goldens of the node search under <c>Search/Goldens</c>; <c>NETPRINTS_UPDATE_SNAPSHOTS=1</c> rewrites them.</summary>
public static class SearchGolden
{
    public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    public static void Check(string name, string actual)
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "NetPrints.Editor.Tests", "Search", "Goldens", name + ".txt");
        if (Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException("The golden path has no folder."));
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"Missing golden {name}; regenerate with {UpdateSnapshotsVariable}=1");
        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NetPrints.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("NetPrints.slnx was not found above the test output.");
    }
}
