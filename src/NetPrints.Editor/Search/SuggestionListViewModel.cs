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
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.Search;

/// <summary>
/// The node search popup (PAR-47, 52..54). Suggestions are built off the UI thread; filtering is a
/// DynamicData pipeline driven by the debounced search text, bound to a virtualized list.
/// </summary>
public sealed partial class SuggestionListViewModel : ObservableObject, IDisposable
{
    private const string NetPrintsCategory = "NetPrints";
    private const string ThisMethodsCategory = "This Methods";
    private const string ThisVariablesCategory = "This Variables";
    private const string StaticMethodsCategory = "Static Methods";
    private const string MethodVariablesCategory = "Method Variables";

    private readonly NodeGraphViewModel graph;
    private readonly SourceList<SuggestionItem> source = new();
    private readonly Subject<string> textChanges = new();
    private readonly Subject<string> refreshes = new();
    private readonly IDisposable pipeline;
    private readonly ReadOnlyObservableCollection<SuggestionItem> items;
    private IReadOnlyList<SuggestionItem> allItems = [];
    private int openVersion;

    /// <summary>
    /// Creates the search popup's view model and wires its filter pipeline: search text is throttled
    /// on <paramref name="graph"/>'s context scheduler, then re-applied as a DynamicData filter,
    /// observed back on the UI thread.
    /// </summary>
    /// <param name="graph">Graph view model the search is opened for.</param>
    public SuggestionListViewModel(NodeGraphViewModel graph)
    {
        this.graph = graph;

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
    public ReadOnlyObservableCollection<SuggestionItem> Items => items;

    /// <summary>Number of visible rows.</summary>
    public int VisibleCount => items.Count;

    /// <summary>All suggestions of the last build (without headers).</summary>
    public IReadOnlyList<SuggestionItem> AllSuggestions => allItems.Where(i => !i.IsHeader).ToList();

    /// <summary>Throttle window of the search box.</summary>
    public TimeSpan FilterThrottle { get; } = TimeSpan.FromMilliseconds(100);

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; }

    /// <summary>Gets a value indicating whether the list has loaded and no suggestion matches the search text.</summary>
    public bool IsEmpty => !IsLoading && !items.Any(item => !item.IsHeader);

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
    public partial SuggestionItem? SelectedItem { get; set; }

    /// <summary>Opens the popup: clears the search text and builds the suggestions for a pin (or none).</summary>
    public async Task OpenAsync(GraphPoint position, NodePin? pin, CancellationToken cancellationToken = default)
    {
        int version = Interlocked.Increment(ref openVersion);

        Position = position;
        SuggestionPin = pin;
        SearchText = "";
        IsLoading = true;
        IsOpen = true;

        // As in the WPF editor, dragging from a connected exec output replaces its connection.
        if (pin is NodeOutputExecPin oxp)
        {
            GraphUtil.DisconnectOutputExecPin(oxp);
        }

        List<SuggestionItem> built;
        try
        {
            // Suggestions come from reflection: wait for the first load instead of showing nothing.
            await graph.Context.Reflection.Loaded.WaitAsync(cancellationToken);
            built = await Task.Run(() => BuildItems(pin), cancellationToken);
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

        SetItems(built);
        IsLoading = false;
    }

    /// <summary>Replaces the suggestion rows.</summary>
    internal void SetItems(IReadOnlyList<SuggestionItem> rows)
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

    private Func<SuggestionItem, bool> BuildPredicate(string text)
    {
        var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return _ => true;
        }

        var snapshot = allItems;
        var matched = new HashSet<SuggestionItem>(ReferenceEqualityComparer.Instance);
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
    /// Builds the suggestion rows for a pin (or none), grouped by category in order of first
    /// appearance with a header row per category (ported from the WPF <c>UpdateSuggestions</c>).
    /// </summary>
    internal List<SuggestionItem> BuildItems(NodePin? pin)
    {
        var suggestions = BuildSuggestions(pin);

        var groups = new List<(string Category, List<SuggestionItem> Items)>();
        var groupIndex = new Dictionary<string, int>();
        var seen = new HashSet<(string, object)>();

        foreach (var (category, value) in suggestions)
        {
            if (!seen.Add((category, value)))
            {
                continue;
            }

            if (!groupIndex.TryGetValue(category, out int index))
            {
                index = groups.Count;
                groupIndex[category] = index;
                groups.Add((category, []));
            }

            groups[index].Items.Add(SuggestionItem.Create(category, value));
        }

        var rows = new List<SuggestionItem>(seen.Count + groups.Count);
        foreach (var (category, categoryItems) in groups)
        {
            rows.Add(SuggestionItem.Header(category));
            rows.AddRange(categoryItems);
        }

        return rows;
    }

    private IEnumerable<(string Category, object Value)> BuildSuggestions(NodePin? pin)
    {
        var provider = graph.Context.Reflection.Provider;
        var nodeGraph = graph.Graph;
        var cls = nodeGraph.Class;
        var classType = cls?.Type;
        var baseTypes = cls?.AllBaseTypes.ToList() ?? [];

        var result = new List<(string, object)>();

        void Add(string category, IEnumerable<object> values) => result.AddRange(values.Select(v => (category, v)));

        GraphKinds graphKind = NodeGraphKinds.Of(nodeGraph);
        var suggestedKinds = graphKind == GraphKinds.None
            ? []
            : graph.Context.Extensions.Current.NodeKinds.Where(kind => kind.Suggestions.Count > 0 && kind.AllowedIn.HasFlag(graphKind)).ToList();

        // Built-in kinds are offered as their own suggestion (SelectAsync asks a dialog where a kind needs one); an extension's
        // suggestions come last, after every built-in category (extension-points.md §2).
        IEnumerable<object> BuiltIns() => suggestedKinds.Where(kind => !kind.Kind.Contains('/', StringComparison.Ordinal)).Select(kind => (object)kind.Suggestions[0]);

        IEnumerable<(string Category, object Value)> ExtensionNodes() => suggestedKinds
            .Where(kind => kind.Kind.Contains('/', StringComparison.Ordinal))
            .SelectMany(kind => kind.Suggestions.Select(suggestion => (suggestion.Category, (object)suggestion)));

        ReflectionProviderMethodQuery MethodQuery() => classType is null ? new() : new ReflectionProviderMethodQuery().WithVisibleFrom(classType);

        ReflectionProviderVariableQuery VariableQuery() => classType is null ? new() : new ReflectionProviderVariableQuery().WithVisibleFrom(classType);

        switch (pin)
        {
            case NodeOutputDataPin odp when odp.PinType.Value is TypeSpecifier pinType:
                if (classType is not null)
                {
                    Add(NetPrintsCategory, [new MakeDelegateTypeInfo(pinType, classType)]);
                }

                Add("Pin Variables", provider.GetVariables(VariableQuery().WithType(pinType).WithStatic(false)));
                Add("Pin Methods", provider.GetMethods(MethodQuery().WithStatic(false).WithType(pinType)));

                foreach (var baseType in baseTypes)
                {
                    Add(ThisMethodsCategory, provider.GetMethods(MethodQuery().WithStatic(false).WithArgumentType(pinType).WithType(baseType)));
                }

                Add(StaticMethodsCategory, provider.GetMethods(MethodQuery().WithArgumentType(pinType).WithStatic(true)));
                break;

            case NodeInputDataPin idp when idp.PinType.Value is TypeSpecifier pinType:
                foreach (var baseType in baseTypes)
                {
                    Add(ThisVariablesCategory, provider.GetVariables(VariableQuery().WithType(baseType).WithVariableType(pinType, true)));
                }

                Add(StaticMethodsCategory, provider.GetMethods(MethodQuery().WithStatic(true).WithReturnType(pinType)));
                break;

            case NodeOutputExecPin or NodeInputExecPin:
                Add(NetPrintsCategory, BuiltIns());

                foreach (var baseType in baseTypes)
                {
                    Add(ThisMethodsCategory, provider.GetMethods(MethodQuery().WithType(baseType).WithStatic(false)));
                }

                Add(StaticMethodsCategory, provider.GetMethods(MethodQuery().WithStatic(true)));
                break;

            case NodeInputTypePin:
                Add("Types", provider.GetNonStaticTypes());
                break;

            case NodeOutputTypePin otp:
                // NodeOutputTypePin.InferredType is non-nullable (its inferred type is never absent).
                if (nodeGraph is ExecutionGraph && otp.InferredType.Value is TypeSpecifier typeSpecifier)
                {
                    Add("Pin Static Methods", provider.GetMethods(MethodQuery().WithType(typeSpecifier).WithStatic(true)));
                }

                Add("Generic Types", provider.GetNonStaticTypes().Where(t => t.GenericArguments.Any()));

                if (nodeGraph is ExecutionGraph)
                {
                    Add("Generic Static Methods", provider.GetMethods(MethodQuery().WithStatic(true).WithHasGenericArguments(true)));
                }
                break;

            case null:
                Add(NetPrintsCategory, BuiltIns());

                if (nodeGraph is ExecutionGraph executionGraph)
                {
                    foreach (var baseType in baseTypes)
                    {
                        Add(ThisVariablesCategory, provider.GetVariables(VariableQuery().WithType(baseType).WithStatic(false)));
                        Add(ThisMethodsCategory, provider.GetMethods(MethodQuery().WithType(baseType).WithStatic(false)));
                    }

                    Add(StaticMethodsCategory, provider.GetMethods(MethodQuery().WithStatic(true)));
                    Add("Static Variables", provider.GetVariables(VariableQuery().WithStatic(true)));

                    // US5, sub-phase H: the opened method's or constructor's own local variables.
                    Add(MethodVariablesCategory, executionGraph.LocalVariables.Select(l => (object)l.ToSpecifier()));
                }
                else if (nodeGraph is ClassGraph)
                {
                    Add("Types", provider.GetNonStaticTypes());
                }
                else if (nodeGraph is EventGraph && cls is not null)
                {
                    // US4: an event graph starts empty; "Custom Event" and "Override <method>"
                    // create its entries (EventEntryNode) the same way the class editor's own
                    // lists create methods and constructors.
                    Add(NetPrintsCategory, [new CustomEventSuggestion()]);

                    var alreadyNamed = new HashSet<string>(cls.Methods.Select(m => m.Name)
                        .Concat(cls.EventGraphs.SelectMany(g => g.Entries.Select(e => e.EventName))));

                    Add(NetPrintsCategory, baseTypes.SelectMany(provider.GetOverridableMethodsForType)
                        .Where(m => !alreadyNamed.Contains(m.Name))
                        .Select(m => (object)new OverrideEventSuggestion(m)));
                }
                break;
        }

        if (pin is null or NodeOutputExecPin or NodeInputExecPin)
        {
            result.AddRange(ExtensionNodes());
        }

        return result;
    }

    /// <summary>
    /// Creates the node for a chosen suggestion, asking for a type or method first where needed
    /// (PAR-54), then closes the popup.
    /// </summary>
    [RelayCommand]
    public async Task SelectAsync(SuggestionItem? item)
    {
        if (item is null || item.IsHeader || item.Value is null)
        {
            return;
        }

        IsOpen = false;

        var context = graph.Context;
        var provider = context.Reflection.Provider;
        var pin = SuggestionPin;

        void AddNode<T>(params object[] arguments) where T : Node => graph.AddNode<T>(Position, pin, arguments);

        async Task<TypeSpecifier?> SelectTypeAsync() =>
            await context.Dialogs.SelectTypeAsync(context.Reflection.NonStaticTypes, TypeSpecifier.FromType<object>());

        try
        {
            switch (item.Value)
            {
                case NodeSuggestion suggestion:
                    switch (context.Extensions.Current.NodeKinds.FirstOrDefault(kind => kind.Suggestions.Contains(suggestion))?.Kind)
                    {
                        case BuiltInNodeKinds.Constructor:
                            if (await SelectTypeAsync() is { } constructedType
                                && provider.GetConstructors(constructedType).FirstOrDefault() is { } constructor)
                            {
                                AddNode<ConstructorNode>(constructor);
                            }
                            break;

                        case BuiltInNodeKinds.Literal:
                            if (await SelectTypeAsync() is { } literalType)
                            {
                                AddNode<LiteralNode>(literalType);
                            }
                            break;

                        case BuiltInNodeKinds.Type:
                            if (await SelectTypeAsync() is { } nodeType)
                            {
                                AddNode<TypeNode>(nodeType);
                            }
                            break;

                        default:
                            graph.AddNode(Position, pin, suggestion);
                            break;
                    }
                    break;

                case MethodSpecifier method:
                    // CallMethodNode builds its generic-argument type pins from method.GenericArguments
                    // itself (T103a): no separate generic-argument-types constructor argument needed.
                    AddNode<CallMethodNode>(method);
                    break;

                case VariableSpecifier variable:
                    graph.GetSetChooser.Open(variable, Position);
                    break;

                case CustomEventSuggestion:
                    if (graph.Graph is EventGraph { Class: { } eventClass })
                    {
                        string name = MemberNames.Unique(eventClass, CustomEventSuggestion.NamePrefix);
                        graph.AddEventEntry(Position, g => new EventEntryNode(g, name));
                    }
                    break;

                case OverrideEventSuggestion overrideEvent:
                    graph.AddEventEntry(Position, g => new EventEntryNode(g, overrideEvent.Method));
                    break;

                case MakeDelegateTypeInfo makeDelegate:
                    {
                        var methods = provider.GetMethods(new ReflectionProviderMethodQuery()
                            .WithType(makeDelegate.Type)
                            .WithVisibleFrom(makeDelegate.FromType));

                        if (await context.Dialogs.SelectMethodAsync(methods) is { } chosen)
                        {
                            AddNode<MakeDelegateNode>(chosen);
                        }
                        break;
                    }

                case TypeSpecifier type:
                    AddNode<TypeNode>(type);
                    break;
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create node", ex.ToString());
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
