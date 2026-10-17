namespace NetPrints.Editor.Contributions;

/// <summary>The texts of descriptors as the editor shows them, resolved through the editor's own resources.</summary>
public static class DescriptorDisplay
{
    extension(CommandDescriptor command)
    {
        /// <summary>Gets the label as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayLabel => command.Label.Resolve(Strings.ResourceManager, null);
    }

    extension(DialogDescriptor dialog)
    {
        /// <summary>Gets the title as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayTitle => dialog.Title.Resolve(Strings.ResourceManager, null);
    }

    extension(DashboardTileDescriptor tile)
    {
        /// <summary>Gets the title as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayTitle => tile.Title.Resolve(Strings.ResourceManager, null);
    }

    extension(ProjectTemplateDescriptor template)
    {
        /// <summary>Gets the name as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayNameText => template.DisplayName.Resolve(Strings.ResourceManager, null);
    }

    extension(PanelDescriptor panel)
    {
        /// <summary>Gets the title as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayTitle => panel.Title.Resolve(Strings.ResourceManager, null);
    }
}
