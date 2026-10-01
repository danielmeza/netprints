using System;
using System.IO;

namespace NetPrints.Catalog.Tests;

internal static class TestPaths
{
    public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    public static bool UpdateSnapshots => Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1";

    public static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NetPrints.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("NetPrints.slnx not found above " + AppContext.BaseDirectory);
    }

    public static string GoldenPath(string fileName) =>
        Path.Combine(RepositoryRoot(), "tests", "NetPrints.Catalog.Tests", "Format", "Golden", fileName);

    public static string SnapshotPath(string fileName) =>
        Path.Combine(RepositoryRoot(), "tests", "NetPrints.Catalog.Tests", "Snapshots", fileName);
}
