using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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

        Assert.Equal(templates + 3, App.DataTemplates.Count);
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

    private static void CopyExtension(string target, string name)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(FixtureExtensions.ViewsFolder()))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        File.WriteAllText(
            Path.Combine(target, "netprints-extension.json"),
            $$"""{ "id": "fx.views", "name": "{{name}}", "version": "1.0.0", "assembly": "Fx.Views.dll", "netprintsApi": "0.4", "dependsOn": [] }""");
    }

    private static object NewModelOf(string folder)
    {
        string path = Path.Combine(folder, "Fx.Views.dll");
        Assembly assembly = AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .First(candidate => !candidate.IsDynamic && string.Equals(candidate.Location, path, StringComparison.Ordinal));
        Type type = assembly.GetType("Fx.Views.Model") ?? throw new InvalidOperationException("Fx.Views.Model is missing.");
        return Activator.CreateInstance(type) ?? throw new InvalidOperationException("Model could not be created.");
    }

    private static string[] TemplateOwners(object data) =>
        [.. App.DataTemplates.OfType<ContainedDataTemplate>().Where(template => template.Match(data)).Select(template => template.ExtensionName)];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AProjectsCopyOfAnExtensionServesItsSessionsViewResources()
    {
        string temp = Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));
        string userRoot = Path.Combine(temp, "user");
        string projectCopy = Path.Combine(temp, "project", "fx.views");
        CopyExtension(Path.Combine(userRoot, "fx.views"), "User copy");
        CopyExtension(projectCopy, "Project copy");
        try
        {
            var recorder = new ExtensionContributionRecorder();
            await using var appHost = new ExtensionHost(new ExtensionLoaderOptions([userRoot], [], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            recorder.ApplyToAppGroup(registry);
            registry.Freeze();
            ViewResourceLoader.Sync(App, registry.ViewResources);

            Assert.Equal(["User copy"], TemplateOwners(new Version(1, 0)));

            await using var session = new ExtensionHost(new ExtensionLoaderOptions([userRoot], [projectCopy], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
            registry.AddSessionGroup(GroupKey, group => recorder.PopulateSessionGroup(group, session.Current, [projectCopy]));
            ViewResourceLoader.Sync(App, registry.ViewResources);

            Assert.Equal(["Project copy"], TemplateOwners(new Version(1, 0)));
            Assert.Equal(["Project copy"], TemplateOwners(NewModelOf(projectCopy)));

            registry.RemoveSessionGroup(GroupKey);
            ViewResourceLoader.Sync(App, registry.ViewResources);

            Assert.Equal(["User copy"], TemplateOwners(new Version(1, 0)));
            Assert.Empty(TemplateOwners(NewModelOf(projectCopy)));
        }
        finally
        {
            ViewResourceLoader.Sync(App, []);
            Directory.Delete(temp, recursive: true);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnExtensionsTreeTemplateStillShowsItsChildren()
    {
        (ContributionRegistry registry, ExtensionHost host) = LoadGroup();
        await using ExtensionHost _ = host;
        ViewResourceLoader.Sync(App, registry.ViewResources);
        object root = NewModelOf(FixtureExtensions.ViewsFolder());
        ((System.Collections.IList)(root.GetType().GetProperty("Children")?.GetValue(root) ?? throw new InvalidOperationException("Children is missing."))).Add(NewModelOf(FixtureExtensions.ViewsFolder()));
        var tree = new TreeView { ItemsSource = new[] { root } };
        var window = new Window { Width = 300, Height = 200, Content = tree };
        try
        {
            window.Show();
            window.UpdateLayout();
            HeadlessDriver.Pump();

            TreeViewItem item = Assert.IsType<TreeViewItem>(tree.ContainerFromIndex(0));
            Assert.Equal(1, item.ItemCount);
        }
        finally
        {
            window.Close();
            registry.RemoveSessionGroup(GroupKey);
            ViewResourceLoader.Sync(App, registry.ViewResources);
        }
    }

    private sealed class PlainTemplate : IDataTemplate
    {
        public bool Match(object? data) => true;

        public Control? Build(object? param) => null;
    }

    [Fact]
    public void AContainedTemplateHasTheCapabilitiesOfItsTemplateAndNoOthers()
    {
        ContainedDataTemplate plain = ContainedDataTemplate.Wrap(new PlainTemplate(), "x", null);
        ContainedDataTemplate data = ContainedDataTemplate.Wrap(new Avalonia.Markup.Xaml.Templates.DataTemplate(), "x", null);
        ContainedDataTemplate tree = ContainedDataTemplate.Wrap(new Avalonia.Markup.Xaml.Templates.TreeDataTemplate(), "x", null);

        Assert.IsNotAssignableFrom<ITreeDataTemplate>(plain);
        Assert.IsNotAssignableFrom<IRecyclingDataTemplate>(plain);
        Assert.IsNotAssignableFrom<ITypedDataTemplate>(plain);
        Assert.IsAssignableFrom<IRecyclingDataTemplate>(data);
        Assert.IsAssignableFrom<ITypedDataTemplate>(data);
        Assert.IsNotAssignableFrom<ITreeDataTemplate>(data);
        Assert.IsAssignableFrom<ITreeDataTemplate>(tree);
        Assert.IsAssignableFrom<ITypedDataTemplate>(tree);
        Assert.IsNotAssignableFrom<IRecyclingDataTemplate>(tree);
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

        Assert.Equal(templates + 3, App.DataTemplates.Count);

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
