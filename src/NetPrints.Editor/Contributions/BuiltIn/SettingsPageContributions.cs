using NetPrints.Core;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in pages of the Settings document (FR-039), registered through the settings page kind in list order.</summary>
public static class SettingsPageContributions
{
    /// <summary>The id of the General page.</summary>
    public const string GeneralId = "netprints.settings.general";

    /// <summary>The id of the Appearance page.</summary>
    public const string AppearanceId = "netprints.settings.appearance";

    /// <summary>The id of the Keyboard shortcuts page.</summary>
    public const string KeyboardId = "netprints.settings.keyboard";

    /// <summary>The id of the Run page.</summary>
    public const string RunId = "netprints.settings.run";

    /// <summary>The id of the Extensions page.</summary>
    public const string ExtensionsId = "netprints.settings.extensions";

    /// <summary>Registers the pages.</summary>
    /// <param name="editor">The editor facet to add to.</param>
    public static void Register(IEditorContributions editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        editor
            .AddSettingsPage(new SettingsPageDescriptor(GeneralId, new LocalizedText("Settings.Page.General", Strings.Settings_Page_General), NetPrintsSettings.SectionId, 0))
            .AddSettingsPage(new SettingsPageDescriptor(AppearanceId, new LocalizedText("Settings.Page.Appearance", Strings.Settings_Page_Appearance), EditorSettings.SectionId, 1))
            .AddSettingsPage(new SettingsPageDescriptor(KeyboardId, new LocalizedText("Settings.Page.Keyboard", Strings.Settings_Page_Keyboard), KeyboardSettings.SectionId, 2))
            .AddSettingsPage(new SettingsPageDescriptor(RunId, new LocalizedText("Settings.Page.Run", Strings.Settings_Page_Run), RunSettings.SectionId, 3))
            .AddSettingsPage(new SettingsPageDescriptor(ExtensionsId, new LocalizedText("Settings.Page.Extensions", Strings.Settings_Page_Extensions), NetPrintsSettings.SectionId, 4));
    }
}
