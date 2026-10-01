using NetPrints.Catalog;
using NetPrints.Extensibility;

namespace Fx.Catalog;

/// <summary>Contributes the fixture catalog, the <c>fixture-flags</c> catalog profile and a project profile that selects it.</summary>
public sealed class FxCatalogExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.catalog";

    private const string CatalogFile = "public-api.npcat.json";
    private const string ProfileFile = "fixture-flags.npprofile.json";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string folder = Path.GetDirectoryName(typeof(FxCatalogExtension).Assembly.Location)
            ?? throw new InvalidOperationException("The extension assembly has no location.");
        builder
            .AddTypeCatalog(CatalogLoader.LoadFile(Path.Combine(folder, CatalogFile)))
            .AddCatalogProfile(ProfileJson.Parse(File.ReadAllText(Path.Combine(folder, ProfileFile))))
            .AddProjectProfile(new FxCatalogProjectProfile());
    }
}
