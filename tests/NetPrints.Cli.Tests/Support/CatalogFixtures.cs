using System.IO;
using NetPrints.Testing;

namespace NetPrints.Cli.Tests.Support;

/// <summary>Paths of the catalog test data: the built fixture library, its project and the committed snapshots.</summary>
internal static class CatalogFixtures
{
    public const string LibraryName = "CatalogFixtureLib";

    public static string LibraryProject { get; } = Path.Combine(Root, "tests", "Fixtures", "Catalog", LibraryName, LibraryName + ".csproj");

    public static string LibraryOutput { get; } =
        Path.Combine(Root, "tests", "Fixtures", "Catalog", LibraryName, "bin", LocalSdkLayout.DetectConfiguration(), "net10.0");

    public static string LibraryAssembly { get; } = Path.Combine(LibraryOutput, LibraryName + ".dll");

    public const string DependentName = "CatalogDependentLib";

    public static string DependentOutput { get; } =
        Path.Combine(Root, "tests", "Fixtures", "Catalog", DependentName, "bin", LocalSdkLayout.DetectConfiguration(), "net10.0");

    public static string DependentAssembly { get; } = Path.Combine(DependentOutput, DependentName + ".dll");

    private static string Root => LocalSdkLayout.FindRepositoryRoot();

    public static string Snapshot(string fileName) => File.ReadAllText(Path.Combine(Root, "tests", "NetPrints.Catalog.Tests", "Snapshots", fileName));
}
