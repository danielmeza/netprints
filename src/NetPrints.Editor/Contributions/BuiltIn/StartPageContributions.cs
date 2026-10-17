using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in dashboard tiles of the start page, in order: recent, open, new, samples, learn, what's new.</summary>
public static class StartPageContributions
{
    /// <summary>Registers the tiles.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IEditorContributions registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "recent", "Recent projects", 0,
            context => RecentOf(StartPageContext.From(context))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "open", "Open folder or project", 1,
            context => new OpenProjectTileViewModel(StartPageContext.From(context).ProjectActions)));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "new", "New project", 2,
            context => new NewProjectTileViewModel(StartPageContext.From(context).ProjectActions)));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "samples", "Samples", 3,
            context => SamplesOf(StartPageContext.From(context))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "learn", "Learn", 4,
            context => LearnOf(StartPageContext.From(context))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "whatsNew", "What's new", 5,
            context => new WhatsNewTileViewModel(WhatsNewResource.Read(), StartPageContext.From(context).UrlLauncher)));
    }

    private static RecentProjectsTileViewModel RecentOf(StartPageContext context) =>
        new(context.Recent, context.ProjectActions, context.Time, context.Clipboard, context.FolderLauncher);

    private static SamplesTileViewModel SamplesOf(StartPageContext context) =>
        new(context.Samples, context.ProjectActions);

    private static LearnTileViewModel LearnOf(StartPageContext context) =>
        new(context.ProjectActions, context.UrlLauncher);
}
