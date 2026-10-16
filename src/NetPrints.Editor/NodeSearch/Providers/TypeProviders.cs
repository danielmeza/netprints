using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch.Providers;

/// <summary>All non-static types, for a type input pin and for the canvas of a class graph.</summary>
internal sealed class TypesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "types";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.Type;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.Pin is NodeInputTypePin || (context.Pin is null && context.Graph is ClassGraph)
            ? context.Reflection.GetNonStaticTypes().Select(type => new TypeEntry(SearchCategories.Types, type))
            : [];
}

/// <summary>What a type output pin offers: the static methods of its type, generic types and generic static methods.</summary>
internal sealed class TypePinProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "typePin";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.Type;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        if (context.Pin is not NodeOutputTypePin pin)
        {
            yield break;
        }

        bool inExecutionGraph = context.Graph is ExecutionGraph;
        if (inExecutionGraph && pin.InferredType.Value is TypeSpecifier typeSpecifier)
        {
            foreach (var method in context.Reflection.GetMethods(context.MethodQuery().WithType(typeSpecifier).WithStatic(true)))
            {
                yield return new MethodEntry(SearchCategories.PinStaticMethods, method);
            }
        }

        foreach (var type in context.Reflection.GetNonStaticTypes().Where(t => t.GenericArguments.Any()))
        {
            yield return new TypeEntry(SearchCategories.GenericTypes, type);
        }

        if (inExecutionGraph)
        {
            foreach (var method in context.Reflection.GetMethods(context.MethodQuery().WithStatic(true).WithHasGenericArguments(true)))
            {
                yield return new MethodEntry(SearchCategories.GenericStaticMethods, method);
            }
        }
    }
}
