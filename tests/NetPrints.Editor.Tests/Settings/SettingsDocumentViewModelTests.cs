using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Settings;

public sealed class SettingsDocumentViewModelTests : IAsyncDisposable
{
    private const string TestExtensionId = "netprints.test";
    private const string LanguageLabelKey = "Settings.netprints.language.Label";

    private sealed record CustomPageState;

    private readonly string folder = Path.Combine(Path.GetTempPath(), "netprints-settings-document", Guid.NewGuid().ToString("N"));
    private readonly List<ExtensionHost> hosts = [];
    private int restarts;

    private string FilePath => Path.Combine(folder, "settings.json");

    public async ValueTask DisposeAsync()
    {
        foreach (ExtensionHost host in hosts)
        {
            await host.DisposeAsync();
        }

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private JsonFileSettingsStore NewStore() => new(FilePath, NullLogger<JsonFileSettingsStore>.Instance);

    private SettingsDocumentViewModel Open(JsonFileSettingsStore store, bool withTestExtension = false)
    {
        string[] folders = withTestExtension ? [FixtureExtensions.TestExtensionFolder()] : [];
        var host = new ExtensionHost(new ExtensionLoaderOptions([], folders, []), NullLoggerFactory.Instance);
        hosts.Add(host);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        return Create(store, host, registry);
    }

    private (SettingsDocumentViewModel Document, ContributionRegistry Registry) OpenWith(JsonFileSettingsStore store, Action<ContributionRegistry> register)
    {
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [], []), NullLoggerFactory.Instance);
        hosts.Add(host);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        register(registry);
        registry.Freeze();
        return (Create(store, host, registry), registry);
    }

    private SettingsDocumentViewModel Create(JsonFileSettingsStore store, ExtensionHost host, ContributionRegistry registry) =>
        new(store, registry, host, new InlineDispatcher(), _ => new EditorContexts(new CommandContext(new FakeShell(), null, null, null, CommandSelection.None)), new FakeFilePicker(), _ =>
        {
            restarts++;
            return Task.CompletedTask;
        });

    private static LoadedExtensionsItemViewModel LoadedList(SettingsDocumentViewModel document) =>
        document.Pages.Single(page => page.Id == SettingsPageContributions.ExtensionsId).Items.OfType<LoadedExtensionsItemViewModel>().Single();

    [Fact]
    public void TheDocumentHasTheSettingsIdAndTheBuiltInPagesInOrder()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        Assert.Equal(DocumentId.Settings, document.Id);
        Assert.Equal("Settings", document.Title);
        Assert.Equal(["General", "Appearance", "Keyboard shortcuts", "Run", "Extensions"], document.Pages.Select(page => page.Title));
        Assert.Same(document.Pages[0], document.SelectedPage);
    }

    [Fact]
    public void TheAppearancePageOffersThePresetsOfEachVariantAndAPreview()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        SettingsPageViewModel appearance = document.Pages.Single(page => page.Title == "Appearance");

        ChoiceFieldViewModel dark = appearance.Items.OfType<ChoiceFieldViewModel>().Single(field => field.QualifiedKey.EndsWith("darkPreset", StringComparison.Ordinal));
        ChoiceFieldViewModel light = appearance.Items.OfType<ChoiceFieldViewModel>().Single(field => field.QualifiedKey.EndsWith("lightPreset", StringComparison.Ordinal));
        Assert.Equal(["netprints.dark", "high-contrast.dark"], dark.Choices);
        Assert.Equal(["netprints.light", "high-contrast.light"], light.Choices);
        Assert.Single(appearance.Items.OfType<ThemePreviewItemViewModel>());
    }

    [Fact]
    public void EverySettingOfTheBuiltInSectionsIsOnAPage()
    {
        using SettingsDocumentViewModel document = Open(NewStore());
        ExtensionSettingsDescriptor[] sections = [NetPrintsSettings.Descriptor, RunSettings.Descriptor, EditorSettings.Descriptor];
        string[] expected =
        [
            .. sections.SelectMany(section => SettingsSchema.Export(section.SectionTypeInfo)["properties"]?.AsObject().Select(property => $"{section.ExtensionId}.{property.Key}") ?? []),
        ];

        string[] reachable = [.. document.Pages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>().Select(field => field.QualifiedKey)];

        Assert.NotEmpty(expected);
        Assert.All(expected, key => Assert.Contains(key, reachable));
    }

    [Fact]
    public void TheGeneralAndExtensionsPagesSplitTheHostSection()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        Assert.Equal(["netprints.enableAnimations", "netprints.language"], Keys(document, "General"));
        Assert.Equal(["netprints.extensionPaths", "netprints.trustedProjects"], Keys(document, "Extensions"));
        Assert.Equal(["run.stopTimeoutSeconds", "run.linuxTerminal"], Keys(document, "Run"));
    }

    [Fact]
    public void EveryBuiltInFieldTakesItsLabelAndHelpTextFromTheResources()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        foreach (SettingsFieldViewModel field in document.Pages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>())
        {
            string? label = Strings.ResourceManager.GetString($"Settings.{field.QualifiedKey}.Label", CultureInfo.InvariantCulture);
            string? description = Strings.ResourceManager.GetString($"Settings.{field.QualifiedKey}.Description", CultureInfo.InvariantCulture);
            Assert.NotNull(label);
            Assert.NotNull(description);
            Assert.Equal(label, field.Label);
            Assert.Equal(description, field.Description);
        }
    }

    [Fact]
    public void TheLanguageIsADropDownOfTheShippedCulturesAndSystem()
    {
        using SettingsDocumentViewModel document = Open(NewStore());
        SettingsPageViewModel general = document.Pages.Single(page => page.Title == "General");

        ChoiceFieldViewModel language = Assert.IsType<ChoiceFieldViewModel>(
            general.Items.OfType<SettingsFieldViewModel>().Single(field => field.QualifiedKey == "netprints.language"));

        Assert.Equal(["", "en"], language.Choices);
        Assert.Equal(["System", "English"], language.Options.Select(option => option.Label));
    }

    [Fact]
    public void TheTestExtensionSectionGetsAGeneratedPageUnderExtensions()
    {
        using SettingsDocumentViewModel document = Open(NewStore(), withTestExtension: true);

        SettingsPageViewModel page = Assert.Single(document.Pages, candidate => candidate.IsChild);
        Assert.Equal("NetPrints test extension", page.Title);
        Assert.Equal(["netprints.test.greeting"], page.Items.OfType<SettingsFieldViewModel>().Select(field => field.QualifiedKey));
        Assert.Equal("Extensions", document.Pages[document.Pages.IndexOf(page) - 1].Title);
    }

    [Fact]
    public void TheExtensionsPageListsTheLoadedExtensionsWithVersionAndSource()
    {
        using SettingsDocumentViewModel document = Open(NewStore(), withTestExtension: true);

        LoadedExtensionsItemViewModel list = document.Pages.Single(page => page.Title == "Extensions").Items.OfType<LoadedExtensionsItemViewModel>().Single();

        LoadedExtensionRow row = Assert.Single(list.Rows, candidate => candidate.Id == TestExtensionId);
        Assert.Equal("NetPrints test extension", row.Name);
        Assert.Equal("1.0.0", row.Version);
        Assert.False(string.IsNullOrEmpty(row.Source));
    }

    [Fact]
    public void SearchHidesThePagesWithoutAMatchAndAnEmptySearchShowsEverything()
    {
        using SettingsDocumentViewModel document = Open(NewStore(), withTestExtension: true);

        document.SearchText = "stop timeout";
        Assert.Equal(["Run"], document.VisiblePages.Select(page => page.Title));

        document.SearchText = "GREETING";
        Assert.Equal(["Extensions", "NetPrints test extension"], document.VisiblePages.Select(page => page.Title));

        document.SearchText = "no such setting anywhere";
        Assert.Empty(document.VisiblePages);

        document.SearchText = "";
        Assert.Equal(document.Pages.Select(page => page.Title), document.VisiblePages.Select(page => page.Title));
    }

    [Fact]
    public void SearchMatchesTheDescriptionAcrossPages()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        document.SearchText = "reduce motion";

        Assert.Equal(["General"], document.VisiblePages.Select(page => page.Title));
        Assert.Equal(["netprints.enableAnimations"], document.Pages[0].Items.OfType<SettingsFieldViewModel>().Where(field => field.IsVisible).Select(field => field.QualifiedKey));
    }

    [Fact]
    public async Task AnInvalidValueIsNotSavedAndAValidOneIsSavedAtOnce()
    {
        JsonFileSettingsStore store = NewStore();
        using SettingsDocumentViewModel document = Open(store);
        var timeout = Assert.IsType<NumberFieldViewModel>(document.FindField("run.stopTimeoutSeconds"));

        timeout.Text = "9999";
        await timeout.CommitCommand.ExecuteAsync(null);
        Assert.False(string.IsNullOrEmpty(timeout.Error));
        Assert.Equal(5, store.Get(RunSettings.Descriptor).StopTimeoutSeconds);

        timeout.Text = "20";
        await timeout.CommitCommand.ExecuteAsync(null);
        Assert.Null(timeout.Error);
        Assert.Equal(20, store.Get(RunSettings.Descriptor).StopTimeoutSeconds);
    }

    [Fact]
    public async Task ARestartFieldShowsTheBarAndRestartNowTakesTheExitPath()
    {
        using SettingsDocumentViewModel document = Open(NewStore());
        var language = Assert.IsType<ChoiceFieldViewModel>(document.FindField("netprints.language"));
        Assert.False(document.RestartPending);

        await language.ChooseCommand.ExecuteAsync("en");

        Assert.True(language.RequiresRestart);
        Assert.True(document.RestartPending);

        await document.RestartNowCommand.ExecuteAsync(null);
        Assert.Equal(1, restarts);
    }

    [Fact]
    public async Task AFieldThatNeedsNoRestartLeavesTheBarHidden()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        await ((ToggleFieldViewModel)document.FindField("netprints.enableAnimations")).ToggleCommand.ExecuteAsync(null);

        Assert.False(document.RestartPending);
    }

    [Fact]
    public async Task ChangedValuesSurviveARestart()
    {
        using (SettingsDocumentViewModel first = Open(NewStore()))
        {
            var timeout = Assert.IsType<NumberFieldViewModel>(first.FindField("run.stopTimeoutSeconds"));
            timeout.Text = "42";
            await timeout.CommitCommand.ExecuteAsync(null);
            await ((ToggleFieldViewModel)first.FindField("netprints.enableAnimations")).ToggleCommand.ExecuteAsync(null);
            var paths = Assert.IsType<StringListFieldViewModel>(first.FindField("netprints.extensionPaths"));
            paths.NewItem = "/opt/netprints-extensions";
            await paths.AddCommand.ExecuteAsync(null);
        }

        using SettingsDocumentViewModel second = Open(NewStore());

        Assert.Equal("42", Assert.IsType<NumberFieldViewModel>(second.FindField("run.stopTimeoutSeconds")).Text);
        Assert.False(Assert.IsType<ToggleFieldViewModel>(second.FindField("netprints.enableAnimations")).IsOn);
        Assert.Equal(["/opt/netprints-extensions"], Assert.IsType<StringListFieldViewModel>(second.FindField("netprints.extensionPaths")).Items.Select(item => item.Text));
    }

    [Fact]
    public async Task AnUnreadableFileOpensWithDefaultsAndIsCopiedAsideBeforeTheFirstSave()
    {
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(FilePath, "{ not json", TestContext.Current.CancellationToken);
        using SettingsDocumentViewModel document = Open(NewStore());
        var timeout = Assert.IsType<NumberFieldViewModel>(document.FindField("run.stopTimeoutSeconds"));
        Assert.Equal("5", timeout.Text);

        timeout.Text = "7";
        await timeout.CommitCommand.ExecuteAsync(null);

        string copy = Assert.Single(Directory.GetFiles(folder, "settings.json.unreadable-*"));
        Assert.Equal("{ not json", await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
        Assert.Equal(7, NewStore().Get(RunSettings.Descriptor).StopTimeoutSeconds);
    }

    [Fact]
    public void TheBuiltInPagesComeFromTheSettingsPageKind()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);

        BuiltInContributions.Register(registry);

        string[] ids =
        [
            SettingsPageContributions.GeneralId,
            SettingsPageContributions.AppearanceId,
            SettingsPageContributions.KeyboardId,
            SettingsPageContributions.RunId,
            SettingsPageContributions.ExtensionsId,
        ];
        Assert.Equal(ids, registry.SettingsPages.Select(page => page.Id));
        Assert.Equal([0, 1, 2, 3, 4], registry.SettingsPages.Select(page => page.Order));

        using SettingsDocumentViewModel document = Open(NewStore());
        Assert.Equal(ids, document.Pages.Select(page => page.Id));
    }

    [Fact]
    public void AnExtensionsOwnPageShowsItsViewModelUnderExtensions()
    {
        using SettingsDocumentViewModel document = OpenWith(NewStore(), registry => registry
            .For("fx.pages", "Pages extension")
            .AddSettingsPage(new SettingsPageDescriptor("fx.pages.custom", "Custom page", null, 0) { CreateViewModel = _ => new CustomPageState() })).Document;

        SettingsPageViewModel page = document.Pages[^1];
        Assert.Equal("fx.pages.custom", page.Id);
        Assert.Equal("Custom page", page.Title);
        Assert.True(page.IsChild);
        CustomSettingsPageItemViewModel item = Assert.IsType<CustomSettingsPageItemViewModel>(Assert.Single(page.Items));
        Assert.IsType<CustomPageState>(item.Content);
    }

    [Fact]
    public void APageWhoseFactoryThrowsShowsThePlaceholderNamingTheExtension()
    {
        using SettingsDocumentViewModel document = OpenWith(NewStore(), registry => registry
            .For("fx.pages", "Pages extension")
            .AddSettingsPage(new SettingsPageDescriptor("fx.pages.custom", "Custom page", null, 0) { CreateViewModel = _ => throw new InvalidOperationException("page factory") })).Document;

        SettingsPageViewModel page = document.Pages[^1];
        var item = Assert.IsType<CustomSettingsPageItemViewModel>(Assert.Single(page.Items));
        var error = Assert.IsType<ContributionErrorViewModel>(item.Content);
        Assert.Equal("Pages extension", error.ExtensionName);
        Assert.Equal("page factory", error.Message);
    }

    [Fact]
    public void APageNamingAParentIsListedUnderIt()
    {
        using SettingsDocumentViewModel document = OpenWith(NewStore(), registry => registry
            .For("fx.pages", "Pages extension")
            .AddSettingsPage(new SettingsPageDescriptor("fx.pages.child", "Child page", null, 0)
            {
                ParentPageId = SettingsPageContributions.AppearanceId,
                CreateViewModel = _ => new CustomPageState(),
            })).Document;

        Assert.Equal(
            [
                SettingsPageContributions.GeneralId,
                SettingsPageContributions.AppearanceId,
                "fx.pages.child",
                SettingsPageContributions.KeyboardId,
                SettingsPageContributions.RunId,
                SettingsPageContributions.ExtensionsId,
            ],
            document.Pages.Select(page => page.Id));
        Assert.True(document.Pages[2].IsChild);
    }

    [Fact]
    public void APageOfASessionGroupComesAndGoesWhileTheDocumentIsOpen()
    {
        var (document, registry) = OpenWith(NewStore(), _ => { });
        using (document)
        {
            Assert.DoesNotContain(document.Pages, page => page.Id == "fx.late.page");

            registry.AddSessionGroup("project", group => group
                .For("fx.late", "Late")
                .AddSettingsPage(new SettingsPageDescriptor("fx.late.page", "Late page", null, 0) { CreateViewModel = _ => new CustomPageState() }));

            Assert.Contains(document.Pages, page => page.Id == "fx.late.page");

            registry.RemoveSessionGroup("project");

            Assert.DoesNotContain(document.Pages, page => page.Id == "fx.late.page");
        }
    }

    [Fact]
    public async Task AProjectsExtensionsComeAndGoWhileTheDocumentIsOpen()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [], []), NullLoggerFactory.Instance);
        hosts.Add(host);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        using SettingsDocumentViewModel document = Create(NewStore(), host, registry);

        await host.LoadForProjectAsync([FixtureExtensions.TestExtensionFolder()], cancellationToken);

        Assert.Contains(document.Pages, page => page.Title == "NetPrints test extension");
        Assert.Contains(LoadedList(document).Rows, row => row.Id == TestExtensionId);

        await host.LoadForProjectAsync([], cancellationToken);

        Assert.DoesNotContain(document.Pages, page => page.Title == "NetPrints test extension");
        Assert.DoesNotContain(LoadedList(document).Rows, row => row.Id == TestExtensionId);
    }

    [Fact]
    public void AnExtensionResourceDoesNotRelabelABuiltInSetting()
    {
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        var manifest = new ExtensionManifest("fx.relabel", "Relabel", "1.0.0", string.Empty, api, []);
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [], [(manifest, new RelabelExtension())]), NullLoggerFactory.Instance);
        hosts.Add(host);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        using SettingsDocumentViewModel document = Create(NewStore(), host, registry);

        Assert.Contains(host.Current.StringResources, resources => resources is RelabelResources);
        Assert.Equal("Language", document.FindField("netprints.language").Label);
    }

    private static string[] Keys(SettingsDocumentViewModel document, string page) =>
        [.. document.Pages.Single(candidate => candidate.Title == page).Items.OfType<SettingsFieldViewModel>().Select(field => field.QualifiedKey)];

    private sealed class RelabelExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddStringResources(new RelabelResources());
    }

    private sealed class RelabelResources : ResourceManager
    {
        protected override ResourceSet? InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) => new RelabelSet();
    }

    private sealed class RelabelSet : ResourceSet
    {
        public override string? GetString(string name) => name == LanguageLabelKey ? "Relabelled" : null;

        public override string? GetString(string name, bool ignoreCase) => GetString(name);
    }
}
