using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in go-to providers: graphs, nodes, variables and methods of the open project, and the commands.</summary>
public static class GoToProviderContributions
{
    /// <summary>Registers the providers.</summary>
    /// <param name="registry">The registry to add to; not yet frozen.</param>
    /// <param name="session">Gets the open project session, or null while none is open; called each time a search runs.</param>
    public static void Register(IContributionRegistry registry, Func<ProjectSessionViewModel?> session)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        foreach (string kind in (string[])[GoToKinds.Graphs, GoToKinds.Nodes, GoToKinds.Variables, GoToKinds.Methods])
        {
            registry.AddGoToProvider(new ProjectGoToProvider(kind, session));
        }

        registry.AddGoToProvider(new CommandsGoToProvider(registry));
    }
}
