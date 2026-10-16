using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Reflection;

namespace NetPrints.Editor.NodeSearch;

/// <summary>
/// An entry of the editor's own providers: it creates its node through the editor's <see cref="NodeCreationContext"/> and may take its
/// title and keywords from a catalog's node hint.
/// </summary>
/// <param name="Category">Category the entry is grouped under.</param>
/// <param name="Text">Text of the row.</param>
/// <param name="IconKey">Icon id of the row.</param>
internal abstract record EditorSearchEntry(string Category, string Text, string IconKey) : NodeSearchEntry(Category, Text, IconKey)
{
    /// <inheritdoc/>
    public sealed override Task<Node?> CreateAsync(INodeCreationContext context, GraphPoint position, NodePin? pin) =>
        context is NodeCreationContext editor ? CreateInEditorAsync(editor, position, pin) : Task.FromResult<Node?>(null);

    /// <summary>Gets the node hint a catalog gives this entry.</summary>
    /// <param name="hints">The catalogs' hints.</param>
    /// <returns>The hint, or <see langword="null"/> when the entry has none to look up or no catalog describes it.</returns>
    public virtual NodeHint? HintFrom(INodeHintSource hints) => null;

    /// <summary>Creates the node of this entry, asking for a type or method first where the entry needs one.</summary>
    /// <param name="context">The graph, prompts and creation service the entry may use.</param>
    /// <param name="position">Where the node is created (graph coordinates).</param>
    /// <param name="pin">Pin the search was opened for, or <see langword="null"/>.</param>
    /// <returns>The new node, or <see langword="null"/> when the prompt was cancelled or the entry opens a chooser instead.</returns>
    protected abstract Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin);
}

/// <summary>A method to call.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The method.</param>
internal sealed record MethodEntry(string Category, MethodSpecifier Method)
    : EditorSearchEntry(Category, SuggestionRowViewModel.FormatMethod(Method), OperatorUtil.IsOperator(Method) ? IconIds.CategoryOperator : IconIds.CategoryMethod)
{
    /// <inheritdoc/>
    public override NodeHint? HintFrom(INodeHintSource hints) => hints.GetMethodHint(Method);

    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Service.Create(position, pin, g => new CallMethodNode(g, Method)));
}

/// <summary>A variable to read or write.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Variable">The variable.</param>
internal sealed record VariableEntry(string Category, VariableSpecifier Variable)
    : EditorSearchEntry(Category, $"{Variable.Type} {Variable.Name} : {Variable.Type}", IconIds.CategoryProperty)
{
    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        context.Graph.GetSetChooser.Open(Variable, position, pin);
        return Task.FromResult<Node?>(null);
    }
}

/// <summary>A type node.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Type">The type.</param>
internal sealed record TypeEntry(string Category, TypeSpecifier Type)
    : EditorSearchEntry(Category, Type.FullCodeName, IconIds.CategoryType)
{
    /// <inheritdoc/>
    public override NodeHint? HintFrom(INodeHintSource hints) => hints.GetTypeHint(Type);

    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Service.Create(position, pin, g => new TypeNode(g, Type)));
}

/// <summary>A node kind's own suggestion, built in or from an extension.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Suggestion">The suggestion of the node kind.</param>
internal sealed record NodeKindEntry(string Category, NodeSuggestion Suggestion)
    : EditorSearchEntry(Category, Suggestion.DisplayName, Suggestion.IconKey ?? IconIds.CategoryNone)
{
    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        string? kind = context.Graph.Context.Extensions.Current.NodeKinds.FirstOrDefault(k => k.Suggestions.Contains(Suggestion))?.Kind;
        return NodeKindCreation.For(kind).CreateAsync(context, Suggestion, position, pin);
    }
}

/// <summary>Make a delegate for a method of the type of the dragged pin.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Info">The type and the type the delegate is made from.</param>
internal sealed record MakeDelegateEntry(string Category, MakeDelegateTypeInfo Info)
    : EditorSearchEntry(Category, $"Make Delegate For A Method Of {Info.Type.ShortName}", IconIds.CategoryDelegate)
{
    /// <inheritdoc/>
    protected override async Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        var methods = context.Graph.Context.Reflection.Provider.GetMethods(new ReflectionProviderMethodQuery()
            .WithType(Info.Type)
            .WithVisibleFrom(Info.FromType));

        return await context.SelectMethodAsync(methods).ConfigureAwait(true) is { } chosen
            ? context.Service.Create(position, pin, g => new MakeDelegateNode(g, chosen))
            : null;
    }
}

/// <summary>A custom event entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
internal sealed record CustomEventEntry(string Category)
    : EditorSearchEntry(Category, CustomEventSuggestion.DisplayText, IconIds.CategoryNone)
{
    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin)
    {
        if (context.Graph.Graph is EventGraph { Class: { } eventClass })
        {
            string name = MemberNames.Unique(eventClass, CustomEventSuggestion.NamePrefix);
            return Task.FromResult<Node?>(context.Graph.AddEventEntry(position, g => new EventEntryNode(g, name)));
        }

        return Task.FromResult<Node?>(null);
    }
}

/// <summary>An override entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The base method to override.</param>
internal sealed record OverrideEventEntry(string Category, MethodSpecifier Method)
    : EditorSearchEntry(Category, $"Override {Method.Name}", IconIds.CategoryMethod)
{
    /// <inheritdoc/>
    protected override Task<Node?> CreateInEditorAsync(NodeCreationContext context, GraphPoint position, NodePin? pin) =>
        Task.FromResult<Node?>(context.Graph.AddEventEntry(position, g => new EventEntryNode(g, Method)));
}
