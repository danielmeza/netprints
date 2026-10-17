using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;

namespace NetPrints.Editor.UITests.Contributions;

/// <summary>FR-034, edge case 1: a view model factory of an extension that throws shows <see cref="ContributionErrorView"/> naming the extension while the rest of the surface renders.</summary>
public class ContributionErrorViewTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AThrowingFactoryShowsThePlaceholderNamingTheExtensionAndTheRestRenders()
    {
        string folder = FixtureExtensions.FaultyFolder();
        var recorder = new ExtensionContributionRecorder();
        await using var host = new ExtensionHost(new ExtensionLoaderOptions([], [folder], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);

        using SurfaceRig rig = SurfaceRig.Create(registry => registry.AddSessionGroup("project", group => recorder.PopulateSessionGroup(group, host.Current, [folder])));
        rig.Settle();

        PanelViewModel faulty = Assert.Single(rig.Shell.Panels, panel => panel.Id == "fx.faulty.panel");
        ContributionErrorViewModel error = Assert.IsType<ContributionErrorViewModel>(faulty.Content);
        Assert.Contains("Faulty fixture extension", error.Title, StringComparison.Ordinal);
        Assert.Contains("panel factory", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Shell.Panels.Where(panel => panel.Id != "fx.faulty.panel"), panel => panel.Content is ContributionErrorViewModel);
        IReadOnlyList<Control> placeholders = rig.FindAll(AutomationIds.ContributionError);
        Assert.NotEmpty(placeholders);
        Assert.All(placeholders, placeholder => Assert.Contains("Faulty fixture extension", Avalonia.Automation.AutomationProperties.GetName(placeholder), StringComparison.Ordinal));
        Assert.NotNull(rig.Find(AutomationIds.ShellMenuBar));
    }
}
