using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>T173: an extension's view resources load by the URI of its own assembly with its group and go with it.</summary>
public class ViewResourceLoaderTests
{
    private const string GroupKey = "project";
    private const string IconKey = "Icon.fx.views.mark";

    private static (ContributionRegistry Registry, ExtensionHost Host) LoadGroup()
    {
        var recorder = new ExtensionContributionRecorder();
        string folder = FixtureExtensions.ViewsFolder();
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [folder], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.Freeze();
        registry.AddSessionGroup(GroupKey, group => recorder.PopulateSessionGroup(group, host.Current, [folder]));
        return (registry, host);
    }

    private static Application App => Application.Current ?? throw new InvalidOperationException("The headless application is not running.");

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheResourcesOfAnExtensionLoadWithItsGroupAndAreRemovedWithIt()
    {
        (ContributionRegistry registry, ExtensionHost host) = LoadGroup();
        await using ExtensionHost _ = host;
        int templates = App.DataTemplates.Count;
        int styles = App.Styles.Count;

        IReadOnlyList<ViewResourceFailure> failures = ViewResourceLoader.Sync(App, registry.ViewResources);

        Assert.Equal(templates + 2, App.DataTemplates.Count);
        Assert.Equal(styles + 1, App.Styles.Count);
        Assert.True(App.TryFindResource(IconKey, out object? icon));
        Assert.NotNull(icon);
        Assert.Equal(2, failures.Count);

        registry.RemoveSessionGroup(GroupKey);
        ViewResourceLoader.Sync(App, registry.ViewResources);

        Assert.Equal(templates, App.DataTemplates.Count);
        Assert.Equal(styles, App.Styles.Count);
        Assert.False(App.TryFindResource(IconKey, out object? gone));
        Assert.Null(gone);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AUriThatResolvesToNothingOrToAnotherKindOfDocumentIsReportedAndIgnored()
    {
        (ContributionRegistry registry, ExtensionHost host) = LoadGroup();
        await using ExtensionHost _ = host;

        IReadOnlyList<ViewResourceFailure> failures = ViewResourceLoader.Sync(App, registry.ViewResources);

        Assert.Contains(failures, failure => failure.Declaration.Uri == "avares://Fx.Views/Missing.axaml");
        ViewResourceFailure other = Assert.Single(failures, failure => failure.Declaration.Uri == "avares://Fx.Views/Other.axaml");
        Assert.Equal("fx.views", other.Declaration.Owner);
        Assert.Contains("DataTemplates", other.Reason, StringComparison.Ordinal);

        registry.RemoveSessionGroup(GroupKey);
        ViewResourceLoader.Sync(App, registry.ViewResources);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SyncingTwiceDoesNotAddTheResourcesTwice()
    {
        (ContributionRegistry registry, ExtensionHost host) = LoadGroup();
        await using ExtensionHost _ = host;
        int templates = App.DataTemplates.Count;

        ViewResourceLoader.Sync(App, registry.ViewResources);
        ViewResourceLoader.Sync(App, registry.ViewResources);

        Assert.Equal(templates + 2, App.DataTemplates.Count);

        registry.RemoveSessionGroup(GroupKey);
        ViewResourceLoader.Sync(App, registry.ViewResources);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ATemplateThatThrowsShowsThePlaceholderNamingTheExtension()
    {
        (ContributionRegistry registry, ExtensionHost host) = LoadGroup();
        await using ExtensionHost _ = host;
        List<ViewResourceDeclaration> reported = [];
        ViewResourceLoader.Sync(App, registry.ViewResources, (declaration, _) => reported.Add(declaration));
        var content = new ContentControl { Content = new Uri("https://example.org/") };
        var window = new Window { Width = 200, Height = 100, Content = content };
        try
        {
            window.Show();
            window.UpdateLayout();
            HeadlessDriver.Pump();

            ContributionErrorView view = Assert.IsType<ContributionErrorView>(content.Presenter?.Child);
            ContributionErrorViewModel model = Assert.IsType<ContributionErrorViewModel>(view.DataContext);
            Assert.Equal("NetPrints view resources fixture extension", model.ExtensionName);
            Assert.Equal("view template", model.Message);
            Assert.Contains(reported, declaration => declaration.Uri == "avares://Fx.Views/Templates.axaml");
        }
        finally
        {
            window.Close();
            registry.RemoveSessionGroup(GroupKey);
            ViewResourceLoader.Sync(App, registry.ViewResources);
        }
    }
}
