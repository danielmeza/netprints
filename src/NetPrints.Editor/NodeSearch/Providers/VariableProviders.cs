using NetPrints.Core;
using NetPrints.Reflection;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch.Providers;

/// <summary>Variables of the type of the dragged output pin.</summary>
internal sealed class PinVariablesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "pinVariables";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.VariableGetter;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.OutputDataType is { } type
            ? context.Reflection.GetVariables(context.VariableQuery().WithType(type).WithStatic(false)).Select(variable => new VariableEntry(SearchCategories.PinVariables, variable))
            : [];
}

/// <summary>Variables of the class and its base types that an input pin can take, or all of them.</summary>
internal sealed class ThisVariablesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "thisVariables";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.VariableGetter;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        if (context.InputDataType is { } inputType)
        {
            return context.BaseTypes.SelectMany(baseType => Of(context, context.VariableQuery().WithType(baseType).WithVariableType(inputType, true)));
        }

        return context.IsExecutionCanvas
            ? context.BaseTypes.SelectMany(baseType => Of(context, context.VariableQuery().WithType(baseType).WithStatic(false)))
            : [];
    }

    private static IEnumerable<NodeSearchEntry> Of(NodeSearchContext context, ReflectionProviderVariableQuery query) =>
        context.Reflection.GetVariables(query).Select(variable => new VariableEntry(SearchCategories.ThisVariables, variable));
}

/// <summary>Static variables, offered on the canvas of an execution graph.</summary>
internal sealed class StaticVariablesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "staticVariables";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.VariableGetter;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.IsExecutionCanvas
            ? context.Reflection.GetVariables(context.VariableQuery().WithStatic(true)).Select(variable => new VariableEntry(SearchCategories.StaticVariables, variable))
            : [];
}

/// <summary>The local variables of the open method or constructor.</summary>
internal sealed class LocalVariablesProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "localVariables";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.VariableGetter;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context is { IsExecutionCanvas: true, Graph: ExecutionGraph graph }
            ? graph.LocalVariables.Select(local => new VariableEntry(SearchCategories.MethodVariables, local.ToSpecifier()))
            : [];
}
