namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>Registers every built-in contribution, in menu order (File, Edit, View, Go, Build, Help).</summary>
public static class BuiltInContributions
{
    /// <summary>Registers the built-in commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        FileContributions.Register(registry);
        EditContributions.Register(registry);
        ViewContributions.Register(registry);
        GoContributions.Register(registry);
        BuildContributions.Register(registry);
        HelpContributions.Register(registry);
        TreeContributions.Register(registry);
        PanelContributions.Register(registry);
        StartPageContributions.Register(registry);
        ProjectTemplateContributions.Register(registry);
    }
}
