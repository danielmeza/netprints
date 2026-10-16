using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch;

/// <summary>
/// The node search popup (PAR-47, 52..54). Suggestions are built off the UI thread; filtering is a
/// DynamicData pipeline driven by the debounced search text, bound to a virtualized list.
/// </summary>
public sealed partial class NodeSearchViewModel : ObservableObject, IDisposable
{
    private const string DefaultEmptyMessage = "No node matches your search.";

    private readonly NodeGraphViewModel graph;
    private readonly NodeSearchProviderRegistry providers;
    private readonly INodeCreationService creation;
    private readonly ISuggestionRanker ranker;
    private readonly SourceList<SuggestionRowViewModel> source = new();
    private readonly Subject<string> textChanges = new();
    private readonly Subject<string> refreshes = new();
    private readonly IDisposable pipeline;
    private readonly ReadOnlyObservableCollection<SuggestionRowViewModel> items;
    private IReadOnlyList<SuggestionRowViewModel> allItems = [];
    private int openVersion;

    /// <summary>
    /// Creates the search popup's view model and wires its filter pipeline: search text is throttled
    /// on <paramref name="graph"/>'s context scheduler, then re-applied as a DynamicData filter,
    /// observed back on the UI thread.
    /// </summary>
    /// <param name="graph">Graph view model the search is opened for.</param>
    public NodeSearchViewModel(NodeGraphViewModel graph)
        : this(graph, NodeSearchProviderRegistry.Default)
    {
    }

    internal NodeSearchViewModel(NodeGraphViewModel graph, NodeSearchProviderRegistry providers, ISuggestionRanker? ranker = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(providers);
        this.graph = graph;
        this.providers = providers;
        this.ranker = ranker ?? DefaultSuggestionRanker.Instance;
        creation = new NodeCreationService(graph, new DialogNodeSearchPrompts(graph.Context));

        // Typing is throttled on the context's scheduler (virtual time in tests); new items
        // re-apply the current text immediately.
        var predicates = textChanges
            .Throttle(FilterThrottle, graph.Context.Scheduler)
            .Merge(refreshes)
            .Select(BuildPredicate)
            .StartWith(_ => true);

        pipeline = source.Connect()
            .Filter(predicates, ListFilterPolicy.ClearAndReplace)
            .ObserveOn(new UiDispatcherScheduler(graph.Context.Dispatcher))
            .Bind(out items, resetThreshold: 50)
            .Subscribe(_ =>
            {
                OnPropertyChanged(nameof(VisibleCount));
                OnPropertyChanged(nameof(IsEmpty));

                // The debounced filter just landed in Items (FLAKE-01): safe to click again.
                IsFiltering = false;
            });
    }

    /// <summary>Filtered rows (headers and suggestions) in display order.</summary>
    public ReadOnlyObservableCollection<SuggestionRowViewModel> Items => items;

    /// <summary>Number of visible rows.</summary>
    public int VisibleCount => items.Count;

    /// <summary>All suggestions of the last build (without headers).</summary>
    public IReadOnlyList<SuggestionRowViewModel> AllSuggestions => allItems.Where(i => !i.IsHeader).ToList();

    /// <summary>Throttle window of the search box.</summary>
    public TimeSpan FilterThrottle { get; } = TimeSpan.FromMilliseconds(100);

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    /// <summary>Gets a value indicating whether the list has loaded and no suggestion matches the search text.</summary>
    public bool IsEmpty => !IsLoading && !items.Any(item => !item.IsHeader);

    /// <summary>Gets the id of the catalog that hides the type of the pin the search was opened from, or null when none does (FR-091).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    [NotifyPropertyChangedFor(nameof(HidingNotice))]
    [NotifyPropertyChangedFor(nameof(HasHidingNotice))]
    public partial string? HidingCatalogId { get; set; }

    /// <summary>Gets the sentence the empty state shows: the default, or the catalog that hides the pin's type.</summary>
    public string EmptyMessage => HidingCatalogId is { } id ? $"No members: this type is hidden by catalog {id}." : DefaultEmptyMessage;

    /// <summary>Gets the notice shown above the list, whatever rows it holds, while a catalog hides the pin's type (FR-091); empty otherwise.</summary>
    public string HidingNotice => HidingCatalogId is { } id ? $"Members of this type are hidden by catalog {id}." : "";

    /// <summary>Gets a value indicating whether <see cref="HidingNotice"/> is shown.</summary>
    public bool HasHidingNotice => HidingCatalogId is not null;

    /// <summary>Where the chosen node is created (graph coordinates).</summary>
    [ObservableProperty]
    public partial GraphPoint Position { get; set; }

    /// <summary>Pin that was dragged to open the search, or null.</summary>
    [ObservableProperty]
    public partial NodePin? SuggestionPin { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>
    /// True from the moment the search text changes until the debounced filter has actually been
    /// applied to <see cref="Items"/> (FLAKE-01). The result list disables itself meanwhile: a row
    /// that already matched the previous, unfiltered view (eg. "Major" on a freshly-dropped
    /// <see cref="Version"/> pin, before the user finishes typing "major") would otherwise be
    /// clickable, and get replaced out from under the pointer the moment the filter lands.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFiltering { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        IsFiltering = true;
        textChanges.OnNext(value ?? "");
    }

    /// <summary>Highlighted result, two-way bound to the result list's <c>SelectedItem</c> (PAR-52, batch X2b).</summary>
    [ObservableProperty]
    public partial SuggestionRowViewModel? SelectedItem { get; set; }

    /// <summary>Opens the popup: clears the search text and builds the suggestions for a pin (or none).</summary>
    public async Task OpenAsync(GraphPoint position, NodePin? pin, CancellationToken cancellationToken = default)
    {
        int version = Interlocked.Increment(ref openVersion);

        Position = position;
        SuggestionPin = pin;
        SearchText = "";
        HidingCatalogId = null;
        IsLoading = true;
        IsOpen = true;

        List<SuggestionRowViewModel> built;
        string? hidingCatalog;
        try
        {
            // Suggestions come from reflection: wait for the first load instead of showing nothing.
            await graph.Context.Reflection.Loaded.WaitAsync(cancellationToken);
            (built, hidingCatalog) = await Task.Run(() => (BuildItems(pin), FindHidingCatalog(pin)), cancellationToken);
        }
        catch (Exception ex)
        {
            IsLoading = false;
            IsOpen = false;
            await graph.Context.Dialogs.ShowErrorAsync("Failed to build suggestions", ex.ToString());
            return;
        }

        if (version != Volatile.Read(ref openVersion))
        {
            return;
        }

        HidingCatalogId = hidingCatalog;
        SetItems(built);
        IsLoading = false;
    }

    private string? FindHidingCatalog(NodePin? pin) =>
        pin is NodeOutputDataPin odp && odp.PinType.Value is TypeSpecifier pinType && graph.Context.Reflection.Provider is ICatalogScope scope
            ? scope.GetHidingCatalogId(pinType)
            : null;

    /// <summary>Replaces the suggestion rows.</summary>
    internal void SetItems(IReadOnlyList<SuggestionRowViewModel> rows)
    {
        allItems = rows;
        source.Edit(list =>
        {
            list.Clear();
            list.AddRange(rows);
        });

        // Re-evaluate the header visibility for the current text.
        refreshes.OnNext(SearchText ?? "");
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>
    /// Enter in the search box: picks and opens the first non-header suggestion (batch X2b). Flushes
    /// the pending <see cref="FilterThrottle"/> window synchronously first (R2-14): otherwise, pressing
    /// Enter inside that window reads <see cref="Items"/> before the debounced filter for the latest
    /// <see cref="SearchText"/> has landed, and picks the previous text's first result instead.
    /// </summary>
    [RelayCommand]
    private void SelectFirst()
    {
        var matches = BuildPredicate(SearchText ?? "");
        SelectCommand.Execute(allItems.FirstOrDefault(i => !i.IsHeader && matches(i)));
    }

    /// <summary>Down in the search box: highlights the first non-header suggestion before focus moves
    /// to the result list (batch X2b).</summary>
    [RelayCommand]
    private void HighlightFirst() => SelectedItem = Items.FirstOrDefault(i => !i.IsHeader);

    private Func<SuggestionRowViewModel, bool> BuildPredicate(string text)
    {
        var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return _ => true;
        }

        var snapshot = allItems;
        var matched = new HashSet<SuggestionRowViewModel>(ReferenceEqualityComparer.Instance);
        var categories = new HashSet<string>();

        foreach (var item in snapshot)
        {
            if (!item.IsHeader && item.Matches(terms))
            {
                matched.Add(item);
                categories.Add(item.Category);
            }
        }

        return item => item.IsHeader ? categories.Contains(item.Category) : matched.Contains(item);
    }

    /// <summary>
    /// Builds the suggestion rows for a pin (or none): the entries of the providers of the registry in use, grouped by category in
    /// order of first appearance with a header row per category.
    /// </summary>
    internal List<SuggestionRowViewModel> BuildItems(NodePin? pin)
    {
        var context = new NodeSearchContext(graph, pin);
        var registryProviders = providers.For(graph.Context.Extensions.Current);
        return SuggestionRows.Build(ranker.Rank(registryProviders.SelectMany(provider => provider.Suggest(context))));
    }

    /// <summary>
    /// Creates the node for a chosen suggestion, asking for a type or method first where needed
    /// (PAR-54), then closes the popup.
    /// </summary>
    [RelayCommand]
    public async Task SelectAsync(SuggestionRowViewModel? item)
    {
        if (item is null || item.IsHeader || item.Entry is not { } entry)
        {
            return;
        }

        IsOpen = false;

        try
        {
            await creation.CreateAsync(entry, Position, SuggestionPin);
        }
        catch (Exception ex)
        {
            await graph.Context.Dialogs.ShowErrorAsync("Failed to create node", ex.ToString());
        }
    }

    /// <summary>Disposes the filter pipeline and its subjects.</summary>
    public void Dispose()
    {
        pipeline.Dispose();
        source.Dispose();
        textChanges.Dispose();
        refreshes.Dispose();
    }
}
