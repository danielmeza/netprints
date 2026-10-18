using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Settings;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Settings;

public sealed class SettingsDocumentViewModelTests : IAsyncDisposable
{
    private const string TestExtensionId = "netprints.test";

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
        return new SettingsDocumentViewModel(store, host, [], new FakeFilePicker(), _ =>
        {
            restarts++;
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TheDocumentHasTheSettingsIdAndTheBuiltInPagesInOrder()
    {
        using SettingsDocumentViewModel document = Open(NewStore());

        Assert.Equal(DocumentId.Settings, document.Id);
        Assert.Equal("Settings", document.Title);
        Assert.Equal(["General", "Appearance", "Run", "Extensions"], document.Pages.Select(page => page.Title));
        Assert.Same(document.Pages[0], document.SelectedPage);
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
        var language = Assert.IsType<TextFieldViewModel>(document.FindField("netprints.language"));
        Assert.False(document.RestartPending);

        language.Text = "es";
        await language.CommitCommand.ExecuteAsync(null);

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

    private static string[] Keys(SettingsDocumentViewModel document, string page) =>
        [.. document.Pages.Single(candidate => candidate.Title == page).Items.OfType<SettingsFieldViewModel>().Select(field => field.QualifiedKey)];
}
