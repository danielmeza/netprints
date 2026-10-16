using NetPrints.Reflection;
using NetPrints.Serialization.Documents;

namespace NetPrints.Editor.NodeSearch.Providers;

/// <summary>Methods callable on the value of the dragged output pin.</summary>
internal sealed class PinMethodsProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "pinMethods";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.CallMethod;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context) =>
        context.OutputDataType is { } type
            ? context.Reflection.GetMethods(context.MethodQuery().WithStatic(false).WithType(type)).Select(method => new MethodEntry(SearchCategories.PinMethods, method))
            : [];
}

/// <summary>Methods of the class and its base types.</summary>
internal sealed class ThisMethodsProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "thisMethods";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.CallMethod;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        if (context.OutputDataType is { } outputType)
        {
            return context.BaseTypes.SelectMany(baseType => Of(context, context.MethodQuery().WithStatic(false).WithArgumentType(outputType).WithType(baseType)));
        }

        return context.IsExecPin || context.IsExecutionCanvas
            ? context.BaseTypes.SelectMany(baseType => Of(context, context.MethodQuery().WithType(baseType).WithStatic(false)))
            : [];
    }

    private static IEnumerable<NodeSearchEntry> Of(NodeSearchContext context, ReflectionProviderMethodQuery query) =>
        context.Reflection.GetMethods(query).Select(method => new MethodEntry(SearchCategories.ThisMethods, method));
}

/// <summary>Static methods that take or return the type of the dragged pin, or all of them.</summary>
internal sealed class StaticMethodsProvider : INodeSearchProvider
{
    /// <inheritdoc/>
    public string Id => "staticMethods";

    /// <inheritdoc/>
    public string OwnerKind => BuiltInNodeKinds.CallMethod;

    /// <inheritdoc/>
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        if (context.OutputDataType is { } outputType)
        {
            return Of(context, context.MethodQuery().WithArgumentType(outputType).WithStatic(true));
        }

        if (context.InputDataType is { } inputType)
        {
            return Of(context, context.MethodQuery().WithStatic(true).WithReturnType(inputType));
        }

        return context.IsExecPin || context.IsExecutionCanvas ? Of(context, context.MethodQuery().WithStatic(true)) : [];
    }

    private static IEnumerable<NodeSearchEntry> Of(NodeSearchContext context, ReflectionProviderMethodQuery query) =>
        context.Reflection.GetMethods(query).Select(method => new MethodEntry(SearchCategories.StaticMethods, method));
}
