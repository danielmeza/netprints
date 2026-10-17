using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace NetPrints.Authoring.Contributions;

/// <summary>The extension API types the contribution analyzers read, matched by name so that no reference to the API is needed.</summary>
internal static class ContributionTypes
{
    public const string CommandDescriptor = "CommandDescriptor";

    public const string CommandGesture = "CommandGesture";

    public const string MenuPlacement = "MenuPlacement";

    public const string IdParameter = "Id";

    public const string IconParameter = "IconId";

    private const string Namespace = "NetPrints.Extensibility.Editor";

    private const char Dot = '.';

    private static readonly ImmutableHashSet<string> IdTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "CommandDescriptor",
        "PanelDescriptor",
        "DashboardTileDescriptor",
        "StatusBarItemDescriptor",
        "DialogDescriptor",
        "SettingsPageDescriptor",
        "ProjectTemplateDescriptor",
        "ContextMenuItemDescriptor");

    private static readonly ImmutableHashSet<string> IconTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "CommandDescriptor",
        "PanelDescriptor",
        "ProjectTemplateDescriptor");

    public static bool IsNamed(ITypeSymbol? type, string name) =>
        type is INamedTypeSymbol { ContainingNamespace: { } space } named
        && named.Name == name
        && space.ToDisplayString() == Namespace;

    public static bool HasId(ITypeSymbol? type) => type is not null && IdTypes.Contains(type.Name) && IsNamed(type, type.Name);

    public static bool HasIcon(ITypeSymbol? type) => type is not null && IconTypes.Contains(type.Name) && IsNamed(type, type.Name);

    public static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    public static string? ConstantText(IOperation operation) =>
        operation.ConstantValue is { HasValue: true, Value: string text } ? text : null;

    public static IArgumentOperation? Argument(IObjectCreationOperation creation, string parameter)
    {
        foreach (IArgumentOperation argument in creation.Arguments)
        {
            if (argument.Parameter?.Name == parameter)
            {
                return argument;
            }
        }

        return null;
    }

    public static bool IsIdPattern(string text)
    {
        string[] segments = text.Split(Dot);
        if (segments.Length < 2)
        {
            return false;
        }

        for (int index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length == 0)
            {
                return false;
            }

            foreach (char c in segments[index])
            {
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || (index > 0 && c >= 'A' && c <= 'Z');
                if (!allowed)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static IEnumerable<IFieldSymbol> ConstantFields(INamedTypeSymbol type)
    {
        foreach (ISymbol member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, ConstantValue: string } field)
            {
                yield return field;
            }
        }
    }
}
