using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>The resource keys that follow from a declaration's id (research R13, data-model section 11).</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public static class ConventionKeys
{
    /// <summary>Gets the prefix of a menu key.</summary>
    public const string MenuPrefix = "Menu.";

    private const string CommandPrefix = "Command.";
    private const string CommandLabelSuffix = ".Label";
    private const string PanelPrefix = "Panel.";
    private const string PanelTitleSuffix = ".Title";

    /// <summary>Gets the key of a command's label.</summary>
    /// <param name="commandId">The command id.</param>
    /// <returns><c>Command.&lt;id&gt;.Label</c>.</returns>
    public static string CommandLabelKey(string commandId) => CommandPrefix + commandId + CommandLabelSuffix;

    /// <summary>Gets the key of a panel's title.</summary>
    /// <param name="panelId">The panel id.</param>
    /// <returns><c>Panel.&lt;id&gt;.Title</c>.</returns>
    public static string PanelTitleKey(string panelId) => PanelPrefix + panelId + PanelTitleSuffix;

    /// <summary>Gets the key of a menu's header.</summary>
    /// <param name="menuId">The menu id, such as <c>File</c>.</param>
    /// <returns><c>Menu.&lt;id&gt;</c>.</returns>
    public static string MenuKey(string menuId) => MenuPrefix + menuId;

    internal static LocalizedText Command(string id, LocalizedText label) => Keyed(label, CommandLabelKey(id));

    internal static LocalizedText Panel(string id, LocalizedText title) => Keyed(title, PanelTitleKey(id));

    private static LocalizedText Keyed(LocalizedText text, string key)
        => string.Equals(text.Key, text.DefaultText, StringComparison.Ordinal) ? new LocalizedText(key, text.DefaultText) : text;
}
