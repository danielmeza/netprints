using Avalonia;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;

namespace NetPrints.Editor.UITests.Contributions;

/// <summary>SC-004, the icon kind: the test extension's icon draws while its project is open and falls back to the unknown glyph once the project unloads.</summary>
public class ExtensionIconSurfaceTests
{
    private const string IconId = "netprints.test.mark";

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheIconOfTheTestExtensionResolvesWhileItsGroupIsLoadedAndNotAfter()
    {
        string folder = FixtureExtensions.TestExtensionFolder();
        var recorder = new ExtensionContributionRecorder();
        await using var host = new ExtensionHost(new ExtensionLoaderOptions([], [folder], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.Freeze();
        Application application = Application.Current ?? throw new InvalidOperationException("The headless application is not running.");
        var icons = IconRegistry.ForApplication(application, NullLogger<IconRegistry>.Instance);

        registry.AddSessionGroup("project", group => recorder.PopulateSessionGroup(group, host.Current, [folder]));
        ViewResourceLoader.Sync(application, registry.ViewResources);

        Assert.True(icons.IsKnown(IconId));
        Assert.NotNull(icons.Resolve(IconId).Geometry);

        registry.RemoveSessionGroup("project");
        ViewResourceLoader.Sync(application, registry.ViewResources);

        Assert.False(icons.IsKnown(IconId));
    }
}
