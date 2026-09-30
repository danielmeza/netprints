namespace NetPrints.Testing;

/// <summary>Where the fixture extensions under <c>tests/Fixtures/Extensions</c> are built; the test projects build them first and never reference them.</summary>
public static class FixtureExtensions
{
    /// <summary>The extension folder of <c>fx.catalog</c>: the fixture catalog, the <c>fixture-flags</c> catalog profile and the <c>fx.catalog.profile</c> project profile.</summary>
    /// <returns>The folder holding <c>netprints-extension.json</c>, in the configuration of the running tests.</returns>
    public static string CatalogFolder() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Extensions",
        "Fx.Catalog",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "extensions",
        "fx.catalog");
}
