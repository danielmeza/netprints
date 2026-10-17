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

    /// <summary>The extension folder of <c>fx.editor</c>: it registers editor contributions through the facet.</summary>
    /// <returns>The folder holding <c>netprints-extension.json</c>, in the configuration of the running tests.</returns>
    public static string EditorFolder() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Extensions",
        "Fx.Editor",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "extensions",
        "fx.editor");

    /// <summary>The extension folder of <c>fx.faulty</c>: contributions that throw at runtime and ones the registry must report.</summary>
    /// <returns>The folder holding <c>netprints-extension.json</c>, in the configuration of the running tests.</returns>
    public static string FaultyFolder() => OutputOf("Fx.Faulty", "fx.faulty");

    /// <summary>The extension folder of <c>fx.registerfault</c>: its <c>Register</c> throws after adding a command.</summary>
    /// <returns>The folder holding <c>netprints-extension.json</c>, in the configuration of the running tests.</returns>
    public static string RegisterFaultFolder() => OutputOf("Fx.RegisterFault", "fx.registerfault");

    /// <summary>The extension folder of <c>fx.views</c>: it declares compiled view resources of its own assembly by URI.</summary>
    /// <returns>The folder holding <c>netprints-extension.json</c>, in the configuration of the running tests.</returns>
    public static string ViewsFolder() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Extensions",
        "Fx.Views",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "extensions",
        "fx.views");

    private static string OutputOf(string project, string extension) => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Extensions",
        project,
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "extensions",
        extension);

    /// <summary>The built <c>CatalogFixtureLib.dll</c> the fixture catalog describes; the test projects build it first and never reference it.</summary>
    /// <returns>The assembly path, in the configuration of the running tests.</returns>
    public static string CatalogLibraryAssembly() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Catalog",
        "CatalogFixtureLib",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "net10.0",
        "CatalogFixtureLib.dll");

    /// <summary>The built <c>CatalogAnnotatedLib.dll</c>, which embeds its own <c>annotated</c> catalog; the test projects build it first and never reference it.</summary>
    /// <returns>The assembly path, in the configuration of the running tests.</returns>
    public static string AnnotatedLibraryAssembly() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "Catalog",
        "CatalogAnnotatedLib",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "net10.0",
        "CatalogAnnotatedLib.dll");
}
