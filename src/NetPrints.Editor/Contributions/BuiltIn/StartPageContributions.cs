using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in dashboard tiles of the start page, in order: recent, open, new, samples, what's new.</summary>
public static class StartPageContributions
{
    /// <summary>Registers the tiles.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "recent", "Recent projects", 0,
            services => new RecentProjectsTileViewModel(
                services.GetService(typeof(RecentProjects)) as RecentProjects, StartPageServices.Require<IProjectActions>(services),
                services.GetService(typeof(TimeProvider)) as TimeProvider, services.GetService(typeof(IClipboardService)) as IClipboardService, services.GetService(typeof(IFolderLauncher)) as IFolderLauncher)));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "open", "Open folder or project", 1,
            services => new OpenProjectTileViewModel(StartPageServices.Require<IProjectActions>(services))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "new", "New project", 2,
            services => new NewProjectTileViewModel(StartPageServices.Require<IProjectActions>(services))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "samples", "Samples", 3,
            services => new SamplesTileViewModel(services.GetService(typeof(SampleCatalog)) as SampleCatalog ?? SampleCatalog.Bundled, StartPageServices.Require<IProjectActions>(services))));
        registry.AddDashboardTile(new DashboardTileDescriptor(
            ContributionIds.TilePrefix + "whatsNew", "What's new", 4,
            services => new WhatsNewTileViewModel(WhatsNewResource.Read(), services.GetService(typeof(IUrlLauncher)) as IUrlLauncher ?? new ShellUrlLauncher())));
    }
}
