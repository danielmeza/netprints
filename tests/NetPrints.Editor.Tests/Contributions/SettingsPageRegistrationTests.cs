using NetPrints.Editor.Contributions;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>NPXE0009: a settings page added through an extension's facet reaches the registry.</summary>
public class SettingsPageRegistrationTests
{
    private readonly CollectingLogger<ContributionRegistry> logger = new();

    [Fact]
    public void AnFxPageRegisteredWithAddSettingsPageIsInTheRegistryAndOwnedByItsExtension()
    {
        var registry = new ContributionRegistry(logger);

        registry.For("fx").AddSettingsPage(new SettingsPageDescriptor("fx.settings.main", "Fx", "fx", 3));

        SettingsPageDescriptor page = Assert.Single(registry.SettingsPages);
        Assert.Equal("fx.settings.main", page.Id);
        Assert.Equal("fx", registry.OwnerOf(page.Id));
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public void ADuplicateIdOrABlankTitleIsReportedAndIgnored()
    {
        var registry = new ContributionRegistry(logger);
        IEditorContributions facet = registry.For("fx");

        facet.AddSettingsPage(new SettingsPageDescriptor("fx.settings.main", "Fx", null, 0));
        facet.AddSettingsPage(new SettingsPageDescriptor("fx.settings.main", "Again", null, 1));
        facet.AddSettingsPage(new SettingsPageDescriptor("fx.settings.blank", " ", null, 2));

        Assert.Single(registry.SettingsPages);
        Assert.Equal(2, registry.Issues.Count);
    }
}
