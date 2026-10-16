using NetPrints.Core;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch.Providers;

/// <summary>"Make delegate" for a method of the type of the dragged output pin.</summary>
internal sealed class MakeDelegateProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "makeDelegate";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.MakeDelegate;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context is { OutputDataType: { } type, ClassType: { } classType }
            ? [new MakeDelegateEntry(SearchCategories.NetPrints, new MakeDelegateTypeInfo(type, classType))]
            : [];
}

/// <summary>The suggestion of each built-in node kind the graph allows, for the canvas and execution pins.</summary>
internal sealed class NodeKindsProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "nodeKinds";

    /// <inheritdoc/>
    public string OwnerKind => NodeSearchProviders.AnyKind;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.Pin is null || context.IsExecPin
            ? context.SuggestedKinds
                .Where(kind => !NodeSearchProviders.IsExtensionKind(kind.Kind))
                .Select(kind => new NodeKindEntry(SearchCategories.NetPrints, kind.Suggestions[0]))
            : [];
}

/// <summary>The custom event and override entries an empty event graph starts from.</summary>
internal sealed class EventEntriesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "eventEntries";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.EventEntry;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        if (context is not { Pin: null, Graph: EventGraph, Class: { } cls })
        {
            yield break;
        }

        yield return new CustomEventEntry(SearchCategories.NetPrints);

        var alreadyNamed = new HashSet<string>(cls.Methods.Select(m => m.Name)
            .Concat(cls.EventGraphs.SelectMany(g => g.Entries.Select(e => e.EventName))));

        foreach (var method in context.BaseTypes.SelectMany(context.Reflection.GetOverridableMethodsForType).Where(m => !alreadyNamed.Contains(m.Name)))
        {
            yield return new OverrideEventEntry(SearchCategories.NetPrints, method);
        }
    }
}

/// <summary>The suggestions of the extensions' node kinds, after every built-in category.</summary>
internal sealed class ExtensionNodesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "extensionNodes";

    /// <inheritdoc/>
    public string OwnerKind => NodeSearchProviders.AnyKind;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.Pin is null || context.IsExecPin
            ? context.SuggestedKinds
                .Where(kind => NodeSearchProviders.IsExtensionKind(kind.Kind))
                .SelectMany(kind => kind.Suggestions.Select(suggestion => new NodeKindEntry(suggestion.Category, suggestion)))
            : [];
}
