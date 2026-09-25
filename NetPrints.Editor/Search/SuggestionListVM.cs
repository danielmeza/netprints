using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Search;

/// <summary>
/// The node search popup (PAR-47, 52..54). Suggestions are built off the UI thread; filtering is a
/// DynamicData pipeline driven by the debounced search text, bound to a virtualized list.
/// </summary>
public sealed partial class SuggestionListVM : ObservableObject, IDisposable
{
    private static readonly IReadOnlyDictionary<Type, TypeSpecifier[]> BuiltInNodes = new Dictionary<Type, TypeSpecifier[]>
    {
        [typeof(MethodGraph)] =
        [
            TypeSpecifier.FromType<ForLoopNode>(),
            TypeSpecifier.FromType<IfElseNode>(),
            TypeSpecifier.FromType<ConstructorNode>(),
            TypeSpecifier.FromType<TypeOfNode>(),
            TypeSpecifier.FromType<ExplicitCastNode>(),
            TypeSpecifier.FromType<ReturnNode>(),
            TypeSpecifier.FromType<MakeArrayNode>(),
            TypeSpecifier.FromType<LiteralNode>(),
            TypeSpecifier.FromType<TypeNode>(),
            TypeSpecifier.FromType<MakeArrayTypeNode>(),
            TypeSpecifier.FromType<ThrowNode>(),
            TypeSpecifier.FromType<AwaitNode>(),
            TypeSpecifier.FromType<TernaryNode>(),
            TypeSpecifier.FromType<DefaultNode>(),
        ],
        [typeof(ConstructorGraph)] =
        [
            TypeSpecifier.FromType<ForLoopNode>(),
            TypeSpecifier.FromType<IfElseNode>(),
            TypeSpecifier.FromType<ConstructorNode>(),
            TypeSpecifier.FromType<TypeOfNode>(),
            TypeSpecifier.FromType<ExplicitCastNode>(),
            TypeSpecifier.FromType<MakeArrayNode>(),
            TypeSpecifier.FromType<LiteralNode>(),
            TypeSpecifier.FromType<TypeNode>(),
            TypeSpecifier.FromType<MakeArrayTypeNode>(),
            TypeSpecifier.FromType<ThrowNode>(),
            TypeSpecifier.FromType<TernaryNode>(),
            TypeSpecifier.FromType<DefaultNode>(),
        ],
        [typeof(ClassGraph)] =
        [
            TypeSpecifier.FromType<TypeNode>(),
            TypeSpecifier.FromType<MakeArrayTypeNode>(),
        ],
    };

    private readonly NodeGraphVM graph;
    private readonly SourceList<SuggestionItem> source = new();
    private readonly BehaviorSubject<string> searchTextSubject = new("");
    private readonly IDisposable pipeline;
    private readonly ReadOnlyObservableCollection<SuggestionItem> items;
    private IReadOnlyList<SuggestionItem> allItems = [];
    private int openVersion;

    public SuggestionListVM(NodeGraphVM graph)
    {
        this.graph = graph;

        // Debounce: every new text cancels the pending one (Switch), so typing a word filters once.
        var predicates = searchTextSubject
            .Select(text => FilterThrottle <= TimeSpan.Zero
                ? Observable.Return(text)
                : Observable.Return(text).Delay(FilterThrottle))
            .Switch()
            .Select(BuildPredicate);

        pipeline = source.Connect()
            .Filter(predicates, ListFilterPolicy.ClearAndReplace)
            .ObserveOn(new UiDispatcherScheduler(graph.Context.Dispatcher))
            .Bind(out items, resetThreshold: 50)
            .Subscribe(_ => OnPropertyChanged(nameof(VisibleCount)));
    }

    /// <summary>Filtered rows (headers and suggestions) in display order.</summary>
    public ReadOnlyObservableCollection<SuggestionItem> Items => items;

    /// <summary>Number of visible rows.</summary>
    public int VisibleCount => items.Count;

    /// <summary>All suggestions of the last build (without headers).</summary>
    public IReadOnlyList<SuggestionItem> AllSuggestions => allItems.Where(i => !i.IsHeader).ToList();

    /// <summary>Debounce time for the search box (0 filters synchronously, used by tests).</summary>
    public TimeSpan FilterThrottle { get; set; } = TimeSpan.FromMilliseconds(100);

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private bool isLoading;

    /// <summary>Where the chosen node is created (graph coordinates).</summary>
    [ObservableProperty]
    private GraphPoint position;

    /// <summary>Pin that was dragged to open the search, or null.</summary>
    [ObservableProperty]
    private NodePin? suggestionPin;

    [ObservableProperty]
    private string searchText = "";

    partial void OnSearchTextChanged(string value) => searchTextSubject.OnNext(value ?? "");

    /// <summary>Opens the popup: clears the search text and builds the suggestions for a pin (or none).</summary>
    public async Task OpenAsync(GraphPoint position, NodePin? pin)
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
            built = await Task.Run(() => BuildItems(pin));
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
        searchTextSubject.OnNext(SearchText ?? "");
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

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

        IEnumerable<object> BuiltIns() => BuiltInNodes.TryGetValue(nodeGraph.GetType(), out var nodes) ? nodes : [];

        ReflectionProviderMethodQuery MethodQuery() => classType is null ? new() : new ReflectionProviderMethodQuery().WithVisibleFrom(classType);

        ReflectionProviderVariableQuery VariableQuery() => classType is null ? new() : new ReflectionProviderVariableQuery().WithVisibleFrom(classType);

        switch (pin)
        {
            case NodeOutputDataPin odp when odp.PinType.Value is TypeSpecifier pinType:
                if (classType is not null)
                {
                    Add("NetPrints", [new MakeDelegateTypeInfo(pinType, classType)]);
                }

                Add("Pin Variables", provider.GetVariables(VariableQuery().WithType(pinType).WithStatic(false)));
                Add("Pin Methods", provider.GetMethods(MethodQuery().WithStatic(false).WithType(pinType)));

                foreach (var baseType in baseTypes)
                {
                    Add("This Methods", provider.GetMethods(MethodQuery().WithStatic(false).WithArgumentType(pinType).WithType(baseType)));
                }

                Add("Static Methods", provider.GetMethods(MethodQuery().WithArgumentType(pinType).WithStatic(true)));
                break;

            case NodeInputDataPin idp when idp.PinType.Value is TypeSpecifier pinType:
                foreach (var baseType in baseTypes)
                {
                    Add("This Variables", provider.GetVariables(VariableQuery().WithType(baseType).WithVariableType(pinType, true)));
                }

                Add("Static Methods", provider.GetMethods(MethodQuery().WithStatic(true).WithReturnType(pinType)));
                break;

            case NodeOutputExecPin or NodeInputExecPin:
                Add("NetPrints", BuiltIns());

                foreach (var baseType in baseTypes)
                {
                    Add("This Methods", provider.GetMethods(MethodQuery().WithType(baseType).WithStatic(false)));
                }

                Add("Static Methods", provider.GetMethods(MethodQuery().WithStatic(true)));
                break;

            case NodeInputTypePin:
                Add("Types", provider.GetNonStaticTypes());
                break;

            case NodeOutputTypePin otp:
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
                Add("NetPrints", BuiltIns());

                if (nodeGraph is ExecutionGraph)
                {
                    foreach (var baseType in baseTypes)
                    {
                        Add("This Variables", provider.GetVariables(VariableQuery().WithType(baseType).WithStatic(false)));
                        Add("This Methods", provider.GetMethods(MethodQuery().WithType(baseType).WithStatic(false)));
                    }

                    Add("Static Methods", provider.GetMethods(MethodQuery().WithStatic(true)));
                    Add("Static Variables", provider.GetVariables(VariableQuery().WithStatic(true)));
                }
                else if (nodeGraph is ClassGraph)
                {
                    Add("Types", provider.GetNonStaticTypes());
                }
                break;
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
                case MethodSpecifier method:
                    AddNode<CallMethodNode>(method, method.GenericArguments.Select(a => (BaseType)new GenericType(a.Name)).ToList());
                    break;

                case VariableSpecifier variable:
                    graph.GetSetChooser.Open(variable, Position);
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

                case TypeSpecifier t when t == TypeSpecifier.FromType<ConstructorNode>():
                    if (await SelectTypeAsync() is { } constructedType
                        && provider.GetConstructors(constructedType).FirstOrDefault() is { } constructor)
                    {
                        AddNode<ConstructorNode>(constructor);
                    }
                    break;

                case TypeSpecifier t when t == TypeSpecifier.FromType<LiteralNode>():
                    if (await SelectTypeAsync() is { } literalType)
                    {
                        AddNode<LiteralNode>(literalType);
                    }
                    break;

                case TypeSpecifier t when t == TypeSpecifier.FromType<TypeNode>():
                    if (await SelectTypeAsync() is { } nodeType)
                    {
                        AddNode<TypeNode>(nodeType);
                    }
                    break;

                case TypeSpecifier t when t == TypeSpecifier.FromType<ForLoopNode>(): AddNode<ForLoopNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<IfElseNode>(): AddNode<IfElseNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<TypeOfNode>(): AddNode<TypeOfNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<ExplicitCastNode>(): AddNode<ExplicitCastNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<ReturnNode>(): AddNode<ReturnNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<MakeArrayNode>(): AddNode<MakeArrayNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<ThrowNode>(): AddNode<ThrowNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<TernaryNode>(): AddNode<TernaryNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<MakeArrayTypeNode>(): AddNode<MakeArrayTypeNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<AwaitNode>(): AddNode<AwaitNode>(); break;
                case TypeSpecifier t when t == TypeSpecifier.FromType<DefaultNode>(): AddNode<DefaultNode>(); break;

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

    public void Dispose()
    {
        pipeline.Dispose();
        source.Dispose();
        searchTextSubject.Dispose();
    }
}
