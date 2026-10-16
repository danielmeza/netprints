namespace NetPrints.Editor.Contributions;

/// <summary>The texts of descriptors as the editor shows them, resolved through the editor's own resources.</summary>
public static class DescriptorDisplay
{
    extension(CommandDescriptor command)
    {
        /// <summary>Gets the label as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayLabel => command.Label.Resolve(Strings.ResourceManager, null);
    }

    extension(PanelDescriptor panel)
    {
        /// <summary>Gets the title as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
        public string DisplayTitle => panel.Title.Resolve(Strings.ResourceManager, null);
    }
}
