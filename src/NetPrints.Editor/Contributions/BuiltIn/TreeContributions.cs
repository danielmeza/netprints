using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The project tree's own command and its context menus (contracts/commands.md section 2).</summary>
public static class TreeContributions
{
    private const string OpenGroup = "open";
    private const string EditGroup = "edit";
    private const string AddGroup = "add";
    private const string ClassGroup = "class";
    private const string OpenGraphName = "openGraph";
    private const string RenameName = "rename";
    private const string DeleteName = "delete";
    private const string ItemPrefix = ContributionIds.MenuPrefix + "tree.";

    /// <summary>Registers the <c>openGraph</c> command and the tree context menu items.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + OpenGraphName,
            "Open",
            new OpenGraphCommandHandler(),
            IconKind: null,
            DefaultGestures: ["Enter"],
            Scope: CommandScope.ProjectTree,
            Menu: null,
            CommandBarOrder: null));

        AddItems(registry, ContextMenuTarget.TreeClass, "class", [OpenGraphName, RenameName, "addMethod", "addConstructor", EditContributions.AddVariableName, "addEventGraph", "overrideMethod", "classSettings", DeleteName]);
        AddItems(registry, ContextMenuTarget.TreeMember, "member", [OpenGraphName, RenameName, DeleteName]);
        AddItems(registry, ContextMenuTarget.TreeEventGraph, "eventGraph", [OpenGraphName, RenameName, DeleteName]);
    }

    private static void AddItems(IContributionRegistry registry, ContextMenuTarget target, string name, string[] commands)
    {
        for (int order = 0; order < commands.Length; order++)
        {
            registry.AddContextMenuItem(new ContextMenuItemDescriptor(
                $"{ItemPrefix}{name}.{commands[order]}",
                target,
                ContributionIds.CommandPrefix + commands[order],
                GroupOf(commands[order]),
                order));
        }
    }

    private static string GroupOf(string command) => command switch
    {
        OpenGraphName => OpenGroup,
        RenameName or DeleteName => EditGroup,
        "classSettings" => ClassGroup,
        _ => AddGroup,
    };
}
