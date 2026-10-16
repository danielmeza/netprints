using NetPrints.Core;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;

namespace NetPrints.Editor.NodeSearch;

/// <summary>
/// A suggestion a provider offers: the category it is grouped under, the text and icon of its row, and the value that creates the node.
/// </summary>
/// <param name="Category">Category the entry is grouped under.</param>
/// <param name="Text">Text of the row.</param>
/// <param name="IconKey">Icon id of the row.</param>
internal abstract record NodeSearchEntry(string Category, string Text, string IconKey)
{
    /// <summary>Gets extra words the search matches besides the category and the text.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
}

/// <summary>A method to call.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The method.</param>
internal sealed record MethodEntry(string Category, MethodSpecifier Method)
    : NodeSearchEntry(Category, SuggestionRowViewModel.FormatMethod(Method), OperatorUtil.IsOperator(Method) ? IconIds.CategoryOperator : IconIds.CategoryMethod);

/// <summary>A variable to read or write.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Variable">The variable.</param>
internal sealed record VariableEntry(string Category, VariableSpecifier Variable)
    : NodeSearchEntry(Category, $"{Variable.Type} {Variable.Name} : {Variable.Type}", IconIds.CategoryProperty);

/// <summary>A type node.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Type">The type.</param>
internal sealed record TypeEntry(string Category, TypeSpecifier Type)
    : NodeSearchEntry(Category, Type.FullCodeName, IconIds.CategoryType);

/// <summary>A node kind's own suggestion, built in or from an extension.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Suggestion">The suggestion of the node kind.</param>
internal sealed record NodeKindEntry(string Category, NodeSuggestion Suggestion)
    : NodeSearchEntry(Category, Suggestion.DisplayName, Suggestion.IconKey ?? IconIds.CategoryNone);

/// <summary>Make a delegate for a method of the type of the dragged pin.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Info">The type and the type the delegate is made from.</param>
internal sealed record MakeDelegateEntry(string Category, MakeDelegateTypeInfo Info)
    : NodeSearchEntry(Category, $"Make Delegate For A Method Of {Info.Type.ShortName}", IconIds.CategoryDelegate);

/// <summary>A custom event entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
internal sealed record CustomEventEntry(string Category)
    : NodeSearchEntry(Category, CustomEventSuggestion.DisplayText, IconIds.CategoryNone);

/// <summary>An override entry of an event graph.</summary>
/// <param name="Category">Category of the entry.</param>
/// <param name="Method">The base method to override.</param>
internal sealed record OverrideEventEntry(string Category, MethodSpecifier Method)
    : NodeSearchEntry(Category, $"Override {Method.Name}", IconIds.CategoryMethod);
