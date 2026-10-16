using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in go-to providers: graphs, nodes, variables, methods of the open project, and the commands.</summary>
public static class GoToProviderContributions
{
    /// <summary>Registers the providers as the built-in extension.</summary>
    /// <param name="registry">The registry to add to; not yet frozen.</param>
    /// <param name="session">Gets the open project session, or null while none is open; called each time a search runs.</param>
    public static void Register(ContributionRegistry registry, Func<ProjectSessionViewModel?> session)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Register(registry.For(BuiltInContributions.OwnerId), registry, session);
    }

    /// <summary>Registers the providers through the facet.</summary>
    /// <param name="editor">The facet of the built-in extension.</param>
    /// <param name="registry">The registry the commands provider searches.</param>
    /// <param name="session">Gets the open project session, or null while none is open; called each time a search runs.</param>
    public static void Register(IEditorContributions editor, IContributionRegistry registry, Func<ProjectSessionViewModel?> session)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        foreach (string kind in (string[])[GoToKinds.Graphs, GoToKinds.Nodes, GoToKinds.Variables, GoToKinds.Methods])
        {
            editor.AddGoToProvider(new ProjectGoToProvider(kind, session));
        }

        editor.AddGoToProvider(new CommandsGoToProvider(registry));
    }
}
