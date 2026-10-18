using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>
/// The Settings document (FR-039): the built-in pages General, Appearance, Run and Extensions, and one generated page per
/// settings section of a loaded extension listed under Extensions. A search filters the fields of every page; a field that
/// needs a restart raises the "Restart to apply" bar.
/// </summary>
internal sealed partial class SettingsDocumentViewModel : DocumentViewModel
{
    private const string GeneralPageId = "netprints.settings.general";
    private const string AppearancePageId = "netprints.settings.appearance";
    private const string RunPageId = "netprints.settings.run";
    private const string ExtensionsPageId = "netprints.settings.extensions";
    private const string ExtensionPagePrefix = "netprints.settings.extension.";
    private const string EnableAnimationsKey = "enableAnimations";
    private const string LanguageKey = "language";
    private const string ExtensionPathsKey = "extensionPaths";
    private const string TrustedProjectsKey = "trustedProjects";

    private readonly Func<CancellationToken, Task> restart;

    /// <summary>Builds the pages over a store and the loaded extensions.</summary>
    /// <param name="store">The settings store every field reads and writes.</param>
    /// <param name="extensions">The extension host whose loaded extensions and settings sections are listed.</param>
    /// <param name="extensionPages">The settings pages the extensions contributed; they give a section's page its title.</param>
    /// <param name="picker">The file and folder picker of the path fields, or <see langword="null"/> when there is none.</param>
    /// <param name="restart">Runs the normal exit path for "Restart now".</param>
    public SettingsDocumentViewModel(
        ISettingsStore store,
        IExtensionHost extensions,
        IReadOnlyList<SettingsPageDescriptor> extensionPages,
        IFilePickerService? picker,
        Func<CancellationToken, Task> restart)
        : base(DocumentId.Settings, Strings.Settings_Title)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(extensionPages);
        ArgumentNullException.ThrowIfNull(restart);
        this.restart = restart;
        Pages = Build(store, extensions.Current, extensionPages, new SettingsFieldServices(picker, _ => null));
        foreach (SettingsFieldViewModel field in Pages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>())
        {
            field.Saved += OnFieldSaved;
        }

        foreach (SettingsPageViewModel page in Pages)
        {
            VisiblePages.Add(page);
        }

        SelectedPage = Pages.FirstOrDefault();
    }

    /// <summary>Gets every page in list order, the pages of extensions directly after Extensions.</summary>
    public ImmutableArray<SettingsPageViewModel> Pages { get; }

    /// <summary>Gets the pages that pass the search, in list order.</summary>
    public ObservableCollection<SettingsPageViewModel> VisiblePages { get; } = [];

    /// <summary>Gets or sets the page whose fields are shown.</summary>
    [ObservableProperty]
    public partial SettingsPageViewModel? SelectedPage { get; set; }

    /// <summary>Gets or sets the search text; fields are filtered by label and description across every page.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>Gets a value indicating whether a field that needs a restart was saved in this session.</summary>
    [ObservableProperty]
    public partial bool RestartPending { get; private set; }

    /// <summary>Finds a field by its section id and key.</summary>
    /// <param name="qualifiedKey">The section id, a dot and the key, as the settings table lists it.</param>
    /// <returns>The field.</returns>
    /// <exception cref="InvalidOperationException">No page has the field.</exception>
    public SettingsFieldViewModel FindField(string qualifiedKey) =>
        Pages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>().SingleOrDefault(field => field.QualifiedKey == qualifiedKey)
        ?? throw new InvalidOperationException($"No settings field '{qualifiedKey}'.");

    [RelayCommand]
    private Task RestartNowAsync(CancellationToken cancellationToken) => restart(cancellationToken);

    partial void OnSearchTextChanged(string value)
    {
        foreach (SettingsPageViewModel page in Pages)
        {
            page.ApplyFilter(value);
        }

        for (int index = 0; index < Pages.Length; index++)
        {
            if (!Pages[index].IsChild)
            {
                for (int child = index + 1; child < Pages.Length && Pages[child].IsChild; child++)
                {
                    if (Pages[child].IsVisible)
                    {
                        Pages[index].ShowForChild();
                    }
                }
            }
        }

        SettingsPageViewModel? previous = SelectedPage;
        VisiblePages.Clear();
        foreach (SettingsPageViewModel page in Pages.Where(page => page.IsVisible))
        {
            VisiblePages.Add(page);
        }

        SelectedPage = previous is { IsVisible: true } ? previous : VisiblePages.FirstOrDefault();
    }

    private static ImmutableArray<SettingsPageViewModel> Build(
        ISettingsStore store,
        ExtensionRegistry registry,
        IReadOnlyList<SettingsPageDescriptor> extensionPages,
        SettingsFieldServices services)
    {
        var host = new SettingsSection(NetPrintsSettings.Descriptor, store);
        var appearance = new SettingsSection(EditorSettings.Descriptor, store);
        var run = new SettingsSection(RunSettings.Descriptor, store);
        var builtInIds = new HashSet<string>([host.Id, appearance.Id, run.Id], StringComparer.Ordinal);

        List<SettingsItemViewModel> extensionItems = [.. host.CreateFields(services, [ExtensionPathsKey, TrustedProjectsKey])];
        extensionItems.Add(new LoadedExtensionsItemViewModel(LoadedRows(registry)));

        var pages = new List<SettingsPageViewModel>
        {
            new(GeneralPageId, Strings.Settings_Page_General, [.. host.CreateFields(services, [EnableAnimationsKey, LanguageKey])], isChild: false),
            new(AppearancePageId, Strings.Settings_Page_Appearance, [.. appearance.CreateFields(services)], isChild: false),
            new(RunPageId, Strings.Settings_Page_Run, [.. run.CreateFields(services)], isChild: false),
            new(ExtensionsPageId, Strings.Settings_Page_Extensions, extensionItems, isChild: false),
        };

        foreach ((string title, ExtensionSettingsDescriptor descriptor) in ExtensionSections(registry, extensionPages, builtInIds))
        {
            var section = new SettingsSection(descriptor, store);
            pages.Add(new SettingsPageViewModel(ExtensionPagePrefix + section.Id, title, [.. section.CreateFields(services)], isChild: true));
        }

        return [.. pages];
    }

    private static IEnumerable<(string Title, ExtensionSettingsDescriptor Descriptor)> ExtensionSections(
        ExtensionRegistry registry,
        IReadOnlyList<SettingsPageDescriptor> extensionPages,
        HashSet<string> builtInIds) =>
        registry.Settings
            .Where(descriptor => !builtInIds.Contains(descriptor.ExtensionId))
            .Select(descriptor => (Title: TitleOf(descriptor.ExtensionId, registry, extensionPages), Descriptor: descriptor))
            .OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase);

    private static string TitleOf(string sectionId, ExtensionRegistry registry, IReadOnlyList<SettingsPageDescriptor> extensionPages) =>
        extensionPages.FirstOrDefault(page => page.SectionKey == sectionId)?.Title.Text
        ?? registry.Loaded.FirstOrDefault(manifest => manifest.Id == sectionId)?.Name
        ?? sectionId;

    private static List<LoadedExtensionRow> LoadedRows(ExtensionRegistry registry) =>
    [
        .. registry.Results
            .OfType<ExtensionLoadResult.Loaded>()
            .Select(result => new LoadedExtensionRow(
                result.Manifest.Id,
                result.Manifest.Name,
                result.Manifest.Version,
                result.ManifestPath is { } path && Path.GetDirectoryName(path) is { Length: > 0 } folder ? folder : Strings.Settings_BuiltInSource))
            .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase),
    ];

    private void OnFieldSaved(object? sender, EventArgs e)
    {
        if (sender is SettingsFieldViewModel { RequiresRestart: true })
        {
            RestartPending = true;
        }
    }
}
