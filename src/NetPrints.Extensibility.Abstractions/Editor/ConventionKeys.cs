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
    private const string DialogPrefix = "Dialog.";
    private const string TilePrefix = "Tile.";
    private const string TemplatePrefix = "Template.";
    private const string TitleSuffix = ".Title";
    private const string TemplateNameSuffix = ".Name";
    private const string TemplateDescriptionSuffix = ".Description";

    /// <summary>Gets the key of a command's label.</summary>
    /// <param name="commandId">The command id.</param>
    /// <returns><c>Command.&lt;id&gt;.Label</c>.</returns>
    public static string CommandLabelKey(string commandId) => CommandPrefix + commandId + CommandLabelSuffix;

    /// <summary>Gets the key of a panel's title.</summary>
    /// <param name="panelId">The panel id.</param>
    /// <returns><c>Panel.&lt;id&gt;.Title</c>.</returns>
    public static string PanelTitleKey(string panelId) => PanelPrefix + panelId + TitleSuffix;

    /// <summary>Gets the key of a dialog's title.</summary>
    /// <param name="dialogId">The dialog id.</param>
    /// <returns><c>Dialog.&lt;id&gt;.Title</c>.</returns>
    public static string DialogTitleKey(string dialogId) => DialogPrefix + dialogId + TitleSuffix;

    /// <summary>Gets the key of a dashboard tile's title.</summary>
    /// <param name="tileId">The tile id.</param>
    /// <returns><c>Tile.&lt;id&gt;.Title</c>.</returns>
    public static string TileTitleKey(string tileId) => TilePrefix + tileId + TitleSuffix;

    /// <summary>Gets the key of a project template's name.</summary>
    /// <param name="templateId">The template id.</param>
    /// <returns><c>Template.&lt;id&gt;.Name</c>.</returns>
    public static string TemplateNameKey(string templateId) => TemplatePrefix + templateId + TemplateNameSuffix;

    /// <summary>Gets the key of a project template's description.</summary>
    /// <param name="templateId">The template id.</param>
    /// <returns><c>Template.&lt;id&gt;.Description</c>.</returns>
    public static string TemplateDescriptionKey(string templateId) => TemplatePrefix + templateId + TemplateDescriptionSuffix;

    /// <summary>Gets the key of a menu's header.</summary>
    /// <param name="menuId">The menu id, such as <c>File</c>.</param>
    /// <returns><c>Menu.&lt;id&gt;</c>.</returns>
    public static string MenuKey(string menuId) => MenuPrefix + menuId;

    internal static LocalizedText Command(string id, LocalizedText label) => Keyed(label, CommandLabelKey(id));

    internal static LocalizedText Panel(string id, LocalizedText title) => Keyed(title, PanelTitleKey(id));

    internal static LocalizedText Dialog(string id, LocalizedText title) => Keyed(title, DialogTitleKey(id));

    internal static LocalizedText Tile(string id, LocalizedText title) => Keyed(title, TileTitleKey(id));

    internal static LocalizedText TemplateName(string id, LocalizedText name) => Keyed(name, TemplateNameKey(id));

    internal static LocalizedText TemplateDescription(string id, LocalizedText description) => Keyed(description, TemplateDescriptionKey(id));

    private static LocalizedText Keyed(LocalizedText text, string key)
        => string.Equals(text.Key, text.DefaultText, StringComparison.Ordinal) ? new LocalizedText(key, text.DefaultText) : text;
}
