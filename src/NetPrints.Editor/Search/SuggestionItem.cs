using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.Search;

/// <summary>
/// A row of the node search list: either a category header or a suggestion (PAR-52).
/// Ports the WPF <c>SuggestionListConverter</c> text and icon logic.
/// </summary>
public sealed class SuggestionItem
{
    private SuggestionItem(string category, object? value, string text, string iconKey, bool isHeader)
    {
        Category = category;
        Value = value;
        Text = text;
        IconKey = iconKey;
        IsHeader = isHeader;
        SearchText = isHeader ? category : $"{category} {text}";
    }

    /// <summary>The category this row belongs to (also this row's own text, for a header row).</summary>
    public string Category { get; }

    /// <summary>The suggested method, variable, type or <see cref="MakeDelegateTypeInfo"/>; null for headers.</summary>
    public object? Value { get; }

    /// <summary>Display text for the row.</summary>
    public string Text { get; }

    /// <summary>File name of the 16-px icon in the editor assets.</summary>
    public string IconKey { get; }

    /// <summary>Whether this row is a category header rather than a suggestion.</summary>
    public bool IsHeader { get; }

    /// <summary>Text matched by the multi-term, case-insensitive search.</summary>
    public string SearchText { get; }

    /// <summary>Creates a category header row.</summary>
    /// <param name="category">Category name, shown as the row's text.</param>
    /// <returns>A header row for the category.</returns>
    public static SuggestionItem Header(string category) => new(category, null, category, "", isHeader: true);

    /// <summary>Creates a suggestion row, computing its text and icon from <paramref name="value"/>.</summary>
    /// <param name="category">Category the suggestion belongs to.</param>
    /// <param name="value">The suggested method, variable, type or <see cref="MakeDelegateTypeInfo"/>.</param>
    /// <returns>A suggestion row for the value.</returns>
    /// <exception cref="NotSupportedException"><paramref name="value"/> is not one of the supported kinds.</exception>
    public static SuggestionItem Create(string category, object value)
    {
        var (text, icon) = Describe(value);
        return new SuggestionItem(category, value, text, icon, isHeader: false);
    }

    /// <summary>Whether every search term occurs in <see cref="SearchText"/> (case-insensitive).</summary>
    public bool Matches(IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (SearchText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns <see cref="Text"/>.</summary>
    /// <returns><see cref="Text"/>.</returns>
    public override string ToString() => Text;

    /// <summary>Text shown for a method: declaring type, name (operators by display name), parameters and return types.</summary>
    public static string FormatMethod(MethodSpecifier methodSpecifier)
    {
        string name = OperatorUtil.TryGetOperatorInfo(methodSpecifier, out var operatorInfo)
            ? $"Operator {operatorInfo.DisplayName}"
            : methodSpecifier.Name;

        string parameters = string.Join(", ", methodSpecifier.Parameters);
        string text = $"{methodSpecifier.DeclaringType} {name} ({parameters})";

        if (methodSpecifier.ReturnTypes.Count > 0)
        {
            text += $" : {string.Join(", ", methodSpecifier.ReturnTypes)}";
        }

        return text;
    }

    private static readonly Dictionary<TypeSpecifier, (string Text, string Icon)> BuiltInNodes = new()
    {
        [TypeSpecifier.FromType<ForLoopNode>()] = ("For Loop", "Loop_16x.png"),
        [TypeSpecifier.FromType<IfElseNode>()] = ("If Else", "If_16x.png"),
        [TypeSpecifier.FromType<ConstructorNode>()] = ("Construct New Object", "Create_16x.png"),
        [TypeSpecifier.FromType<TypeOfNode>()] = ("Type Of", "Type_16x.png"),
        [TypeSpecifier.FromType<ExplicitCastNode>()] = ("Explicit Cast", "Convert_16x.png"),
        [TypeSpecifier.FromType<ReturnNode>()] = ("Return", "Return_16x.png"),
        [TypeSpecifier.FromType<MakeArrayNode>()] = ("Make Array", "ListView_16x.png"),
        [TypeSpecifier.FromType<LiteralNode>()] = ("Literal", "Literal_16x.png"),
        [TypeSpecifier.FromType<TypeNode>()] = ("Type", "Type_16x.png"),
        [TypeSpecifier.FromType<MakeArrayTypeNode>()] = ("Make Array Type", "Type_16x.png"),
        [TypeSpecifier.FromType<ThrowNode>()] = ("Throw", "Throw_16x.png"),
        [TypeSpecifier.FromType<AwaitNode>()] = ("Await", "Task_16x.png"),
        [TypeSpecifier.FromType<TernaryNode>()] = ("Ternary", "ConditionalRule_16x.png"),
        [TypeSpecifier.FromType<DefaultNode>()] = ("Default", "None_16x.png"),
    };

    private static (string Text, string Icon) Describe(object value) => value switch
    {
        MethodSpecifier method => (FormatMethod(method), OperatorUtil.IsOperator(method) ? "Operator_16x.png" : "Method_16x.png"),
        VariableSpecifier variable => ($"{variable.Type} {variable.Name} : {variable.Type}", "Property_16x.png"),
        MakeDelegateTypeInfo makeDelegate => ($"Make Delegate For A Method Of {makeDelegate.Type.ShortName}", "Delegate_16x.png"),
        TypeSpecifier type when BuiltInNodes.TryGetValue(type, out var builtIn) => builtIn,
        TypeSpecifier type => (type.FullCodeName, "Type_16x.png"),
        _ => throw new NotSupportedException($"Unsupported suggestion {value.GetType()}"),
    };
}
