namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>
/// Registers every built-in contribution, in menu order (File, Edit, View, Go, Build, Help), through the editor facet as the in-process
/// extension <see cref="OwnerId"/>, before any other extension.
/// </summary>
public static class BuiltInContributions
{
    /// <summary>The id of the in-process extension that owns the built-in contributions.</summary>
    public const string OwnerId = "netprints.editor";

    /// <summary>Registers the built-in commands, panels, tiles, menus, tooltips and views.</summary>
    /// <param name="editor">The facet of the built-in extension.</param>
    public static void Register(IEditorContributions editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        FileContributions.Register(editor);
        EditContributions.Register(editor);
        ViewContributions.Register(editor);
        GoContributions.Register(editor);
        BuildContributions.Register(editor);
        HelpContributions.Register(editor);
        TreeContributions.Register(editor);
        PanelContributions.Register(editor);
        StartPageContributions.Register(editor);
        TooltipContributions.Register(editor);
        editor.AddViewResources(BuiltInViewsUri);
    }

    /// <summary>Registers everything the built-in extension contributes to <paramref name="registry"/>: the facet contributions, then the project templates.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(ContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry.For(OwnerId));
        ProjectTemplateContributions.Register(registry);
    }

    /// <summary>The <c>avares</c> URI of the dictionary that holds the built-in view templates.</summary>
    public const string BuiltInViewsUri = "avares://NetPrints.Editor/BuiltInViews.axaml";
}
