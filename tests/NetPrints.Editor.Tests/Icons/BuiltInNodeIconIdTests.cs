using System.Reflection;
using NetPrints.Editor.Icons;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Editor.Tests.Icons;

/// <summary>The built-in node library names icons by literal because NetPrints.Extensibility cannot reference the editor; this pins every literal to a real icon id.</summary>
public class BuiltInNodeIconIdTests
{
    private static readonly HashSet<string> AllIds =
    [
        .. typeof(IconIds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)(field.GetRawConstantValue() ?? string.Empty)),
    ];

    [Fact]
    public void EveryBuiltInSuggestionIconIsARegisteredIconId()
    {
        string[] unknown =
        [
            .. BuiltInNodeLibrary.Instance.NodeKinds
                .SelectMany(kind => kind.Suggestions)
                .Select(suggestion => suggestion.IconKey)
                .Where(key => key is null || !AllIds.Contains(key))
                .Select(key => key ?? "(null)"),
        ];

        Assert.Empty(unknown);
    }
}
