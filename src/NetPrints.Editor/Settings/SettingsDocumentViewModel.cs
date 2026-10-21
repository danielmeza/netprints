using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Editor.Theming;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>
/// The Settings document (FR-039): the pages of the settings page kind (the built-in General, Appearance, Keyboard shortcuts,
/// Run and Extensions, then the extensions' pages under Extensions or under the page they name), and a generated page for
/// every loaded extension section no page claims. It rebuilds the extension pages when the registry changes or the extension host loads another set of extensions, which also refreshes the list of loaded extensions. A search filters
/// the fields of every page; a field that needs a restart raises the "Restart to apply" bar.
/// </summary>
internal sealed partial class SettingsDocumentViewModel : DocumentViewModel
{
    /// <summary>The id of the Keyboard shortcuts page.</summary>
    public const string KeyboardPageId = SettingsPageContributions.KeyboardId;

    private const string ExtensionPagePrefix = "netprints.settings.extension.";
    private const string EnableAnimationsKey = "enableAnimations";
    private const string LanguageKey = "language";
    private const string ExtensionPathsKey = "extensionPaths";
    private const string TrustedProjectsKey = "trustedProjects";

    private readonly ISettingsStore store;
    private readonly IContributionRegistry registry;
    private readonly IExtensionHost extensions;
    private readonly IUiDispatcher dispatcher;
    private readonly Func<SettingsPageDescriptor, ISettingsPageContext> pageContexts;
    private readonly SettingsFieldServices services;
    private readonly Func<CancellationToken, Task> restart;
    private readonly List<SettingsPageViewModel> builtInPages = [];
    private readonly List<SettingsPageViewModel> extensionPages = [];
    private readonly Dictionary<string, string?> extensionParents = new(StringComparer.Ordinal);
    private string shownExtensionKey = "";
    private bool closed;

    /// <summary>Builds the pages over a store and the loaded extensions.</summary>
    /// <param name="store">The settings store every field reads and writes.</param>
    /// <param name="registry">The registry whose settings pages are listed, whose commands the Keyboard shortcuts page lists and whose key bindings it changes.</param>
    /// <param name="extensions">The extension host whose loaded extensions and settings sections are listed.</param>
    /// <param name="dispatcher">Runs the refresh that follows the extension host on the UI thread.</param>
    /// <param name="pageContexts">Gives the context an extension page's view model factory is given.</param>
    /// <param name="picker">The file and folder picker of the path fields, or <see langword="null"/> when there is none.</param>
    /// <param name="restart">Runs the normal exit path for "Restart now".</param>
    public SettingsDocumentViewModel(
        ISettingsStore store,
        IContributionRegistry registry,
        IExtensionHost extensions,
        IUiDispatcher dispatcher,
        Func<SettingsPageDescriptor, ISettingsPageContext> pageContexts,
        IFilePickerService? picker,
        Func<CancellationToken, Task> restart)
        : base(DocumentId.Settings, Strings.Settings_Title)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(pageContexts);
        ArgumentNullException.ThrowIfNull(restart);
        this.store = store;
        this.registry = registry;
        this.extensions = extensions;
        this.dispatcher = dispatcher;
        this.pageContexts = pageContexts;
        this.restart = restart;
        services = new SettingsFieldServices(picker, ChoicesOf, ChoiceLabelsOf);
        builtInPages.AddRange(BuildBuiltInPages());
        registry.Changed += OnRegistryChanged;
        extensions.RegistryChanged += OnExtensionsChanged;
        RebuildExtensionPages(force: true);
        SelectedPage = Pages.FirstOrDefault();
    }

    /// <summary>Gets every page in list order, the pages of extensions directly after Extensions.</summary>
    [ObservableProperty]
    public partial ImmutableArray<SettingsPageViewModel> Pages { get; private set; }

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

    /// <summary>Selects the first page the search shows (Down in the search box).</summary>
    [RelayCommand]
    private void SelectFirstVisiblePage()
    {
        if (VisiblePages.Count > 0)
        {
            SelectedPage = VisiblePages[0];
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch(value);

    /// <summary>Shows a page.</summary>
    /// <param name="pageId">The id of a page, such as <see cref="KeyboardPageId"/>.</param>
    /// <returns><see langword="true"/> when the page exists and passes the search.</returns>
    public bool SelectPage(string pageId)
    {
        SettingsPageViewModel? page = VisiblePages.FirstOrDefault(candidate => candidate.Id == pageId);
        if (page is null)
        {
            return false;
        }

        SelectedPage = page;
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closed = true;
            registry.Changed -= OnRegistryChanged;
            extensions.RegistryChanged -= OnExtensionsChanged;
            DisposeItems(Pages);
        }

        base.Dispose(disposing);
    }

    private static IReadOnlyList<string>? ChoicesOf(string providerId) => providerId switch
    {
        EditorSettings.DarkPresetChoices => ThemePresets.IdsFor(ThemeBase.Dark),
        EditorSettings.LightPresetChoices => ThemePresets.IdsFor(ThemeBase.Light),
        NetPrintsSettings.LanguageChoices => LanguageChoices.Values,
        _ => null,
    };

    private static Func<string, string>? ChoiceLabelsOf(string providerId) =>
        providerId == NetPrintsSettings.LanguageChoices ? LanguageChoices.LabelOf : null;

    private static void DisposeItems(IReadOnlyList<SettingsPageViewModel> pages)
    {
        foreach (IDisposable item in pages.SelectMany(page => page.Items).OfType<IDisposable>())
        {
            item.Dispose();
        }
    }

    private static int CompareExtensionPages(ExtensionPage left, ExtensionPage right)
    {
        int byOwner = StringComparer.CurrentCultureIgnoreCase.Compare(left.OwnerName, right.OwnerName);
        if (byOwner != 0)
        {
            return byOwner;
        }

        int byOrder = left.Order.CompareTo(right.Order);
        return byOrder != 0 ? byOrder : StringComparer.CurrentCultureIgnoreCase.Compare(left.Page.Title, right.Page.Title);
    }

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

    private List<SettingsPageViewModel> BuildBuiltInPages()
    {
        List<SettingsPageViewModel> pages = [];
        foreach (SettingsPageDescriptor descriptor in registry.Snapshot.SettingsPages
            .Where(candidate => registry.Snapshot.OwnerNameOf(candidate.Id) is null)
            .OrderBy(candidate => candidate.Order))
        {
            if (BuiltInItems(descriptor.Id) is not { } items)
            {
                continue;
            }

            pages.Add(new SettingsPageViewModel(descriptor.Id, descriptor.Title.Resolve(Strings.ResourceManager, null), items, isChild: false));
        }

        foreach (SettingsFieldViewModel field in pages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>())
        {
            field.Saved += OnFieldSaved;
        }

        return pages;
    }

    private IReadOnlyList<SettingsItemViewModel>? BuiltInItems(string pageId) =>
        pageId switch
        {
            SettingsPageContributions.GeneralId => [.. new SettingsSection(NetPrintsSettings.Descriptor, store).CreateFields(services, [EnableAnimationsKey, LanguageKey])],
            SettingsPageContributions.AppearanceId => [.. new SettingsSection(EditorSettings.Descriptor, store).CreateFields(services), new ThemePreviewItemViewModel(store)],
            SettingsPageContributions.KeyboardId => [new KeyboardShortcutsItemViewModel(registry, store)],
            SettingsPageContributions.RunId => [.. new SettingsSection(RunSettings.Descriptor, store).CreateFields(services)],
            SettingsPageContributions.ExtensionsId => [.. new SettingsSection(NetPrintsSettings.Descriptor, store).CreateFields(services, [ExtensionPathsKey, TrustedProjectsKey]), new LoadedExtensionsItemViewModel(LoadedRows(extensions.Current))],
            _ => null,
        };

    /// <summary>The lookup order of an extension page's title and its section's labels: the loaded extensions' string resources, then the editor's.</summary>
    private IReadOnlyList<ResourceManager> ExtensionResources() => [.. extensions.Current.StringResources, Strings.ResourceManager];

    private void RebuildExtensionPages(bool force)
    {
        IContributionSnapshot snapshot = registry.Snapshot;
        ExtensionRegistry loaded = extensions.Current;
        SettingsPageDescriptor[] descriptors = [.. snapshot.SettingsPages.Where(page => snapshot.OwnerNameOf(page.Id) is not null)];
        HashSet<string> builtInSections = [NetPrintsSettings.SectionId, RunSettings.SectionId, EditorSettings.SectionId, KeyboardSettings.SectionId];
        HashSet<string> claimed = [.. descriptors.Select(page => page.SectionKey).OfType<string>()];
        ExtensionSettingsDescriptor[] sections = [.. loaded.Settings.Where(section => !builtInSections.Contains(section.ExtensionId) && !claimed.Contains(section.ExtensionId))];
        string key = string.Join('|', descriptors.Select(page => page.Id).Concat(sections.Select(section => section.ExtensionId)))
            + "@" + RuntimeHelpers.GetHashCode(loaded).ToString(CultureInfo.InvariantCulture);
        if (!force && key == shownExtensionKey)
        {
            return;
        }

        shownExtensionKey = key;
        DisposeItems(extensionPages);
        extensionPages.Clear();
        extensionParents.Clear();

        IReadOnlyList<ResourceManager> resources = ExtensionResources();
        List<ExtensionPage> created = [];
        foreach (SettingsPageDescriptor page in descriptors)
        {
            string title = page.Title.Resolve(resources, null);
            IReadOnlyList<SettingsItemViewModel>? items = null;
            if (page.CreateViewModel is { } create)
            {
                items = [new CustomSettingsPageItemViewModel(title, create(pageContexts(page)))];
            }
            else if (page.SectionKey is { } sectionKey && loaded.Settings.FirstOrDefault(candidate => candidate.ExtensionId == sectionKey) is { } section)
            {
                items = [.. new SettingsSection(section, store, resources).CreateFields(services)];
            }

            if (items is null)
            {
                continue;
            }

            created.Add(new ExtensionPage(new SettingsPageViewModel(page.Id, title, items, isChild: true), snapshot.OwnerNameOf(page.Id) ?? "", page.Order, page.ParentPageId));
        }

        foreach (ExtensionSettingsDescriptor section in sections)
        {
            string title = loaded.Loaded.FirstOrDefault(manifest => manifest.Id == section.ExtensionId)?.Name ?? section.ExtensionId;
            created.Add(new ExtensionPage(
                new SettingsPageViewModel(ExtensionPagePrefix + section.ExtensionId, title, [.. new SettingsSection(section, store, resources).CreateFields(services)], isChild: true),
                title,
                0,
                null));
        }

        created.Sort(CompareExtensionPages);
        foreach (ExtensionPage entry in created)
        {
            extensionPages.Add(entry.Page);
            extensionParents[entry.Page.Id] = entry.ParentId;
        }

        foreach (SettingsFieldViewModel field in extensionPages.SelectMany(page => page.Items).OfType<SettingsFieldViewModel>())
        {
            field.Saved += OnFieldSaved;
        }

        string? selectedId = SelectedPage?.Id;
        Pages = [.. Compose()];
        ApplySearch(SearchText);
        SelectedPage = VisiblePages.FirstOrDefault(page => page.Id == selectedId) ?? VisiblePages.FirstOrDefault();
    }

    private List<SettingsPageViewModel> Compose()
    {
        HashSet<string> known = [.. builtInPages.Select(page => page.Id), .. extensionPages.Select(page => page.Id)];
        List<SettingsPageViewModel> pages = [];
        HashSet<string> placed = new(StringComparer.Ordinal);
        foreach (SettingsPageViewModel page in builtInPages)
        {
            AddWithChildren(page, pages, placed);
            if (page.Id != SettingsPageContributions.ExtensionsId)
            {
                continue;
            }

            foreach (SettingsPageViewModel candidate in extensionPages)
            {
                if (extensionParents.GetValueOrDefault(candidate.Id) is { } parent && known.Contains(parent))
                {
                    continue;
                }

                AddWithChildren(candidate, pages, placed);
            }
        }

        foreach (SettingsPageViewModel candidate in extensionPages.Where(candidate => !placed.Contains(candidate.Id)))
        {
            AddWithChildren(candidate, pages, placed);
        }

        return pages;
    }

    private void AddWithChildren(SettingsPageViewModel page, List<SettingsPageViewModel> pages, HashSet<string> placed)
    {
        if (!placed.Add(page.Id))
        {
            return;
        }

        pages.Add(page);
        foreach (SettingsPageViewModel child in extensionPages.Where(candidate => extensionParents.GetValueOrDefault(candidate.Id) == page.Id))
        {
            AddWithChildren(child, pages, placed);
        }
    }

    private void ApplySearch(string value)
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

    private void OnRegistryChanged(object? sender, ContributionSnapshot snapshot) => RebuildExtensionPages(force: false);

    private void OnExtensionsChanged(object? sender, ExtensionRegistry loaded) => dispatcher.Post(FollowExtensions);

    private void FollowExtensions()
    {
        if (closed)
        {
            return;
        }

        List<LoadedExtensionRow> rows = LoadedRows(extensions.Current);
        foreach (LoadedExtensionsItemViewModel list in builtInPages.SelectMany(page => page.Items).OfType<LoadedExtensionsItemViewModel>())
        {
            list.Rows = rows;
        }

        RebuildExtensionPages(force: true);
    }

    private void OnFieldSaved(object? sender, EventArgs e)
    {
        if (sender is SettingsFieldViewModel { RequiresRestart: true })
        {
            RestartPending = true;
        }
    }

    private sealed record ExtensionPage(SettingsPageViewModel Page, string OwnerName, int Order, string? ParentId);
}
