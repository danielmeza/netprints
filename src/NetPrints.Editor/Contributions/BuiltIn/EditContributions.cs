using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Edit menu (contracts/commands.md section 1).</summary>
public static class EditContributions
{
    /// <summary>The name, without the command prefix, of the <c>addVariable</c> command that panels run through the invoker.</summary>
    public const string AddVariableName = "addVariable";

    private const string MenuName = "Edit";
    private const string HistoryGroup = "history";
    private const string SelectionGroup = "selection";
    private const string ClassGroup = "class";

    /// <summary>Registers the Edit menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "undo",
            "Undo",
            new UndoCommandHandler(),
            IconKind: "Undo",
            DefaultGestures: ["Ctrl+Z"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, HistoryGroup, 0),
            CommandBarOrder: 4));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "redo",
            "Redo",
            new RedoCommandHandler(),
            IconKind: "Redo",
            DefaultGestures: ["Ctrl+Y", "Ctrl+Shift+Z"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, HistoryGroup, 1),
            CommandBarOrder: 5));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "delete",
            "Delete",
            new DeleteCommandHandler(),
            IconKind: "Delete",
            DefaultGestures: ["Delete"],
            Scope: CommandScope.Graph | CommandScope.ProjectTree,
            Menu: new MenuPlacement(MenuName, SelectionGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "rename",
            "Rename",
            new RenameCommandHandler(),
            IconKind: "RenameBox",
            DefaultGestures: ["F2"],
            Scope: CommandScope.Graph | CommandScope.ProjectTree,
            Menu: new MenuPlacement(MenuName, SelectionGroup, 1),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "selectAll",
            "Select all",
            new SelectAllCommandHandler(),
            IconKind: "SelectAll",
            DefaultGestures: ["Ctrl+A"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, SelectionGroup, 2),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "cancel",
            "Cancel",
            new CancelCommandHandler(),
            IconKind: null,
            DefaultGestures: ["Esc"],
            Scope: CommandScope.Graph,
            Menu: null,
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "nodeSearch",
            "Add node…",
            new NodeSearchCommandHandler(),
            IconKind: "PlusBox",
            DefaultGestures: ["Ctrl+Space"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, "nodes", 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "classSettings",
            "Class settings",
            new ClassSettingsCommandHandler(),
            IconKind: "Cog",
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 0),
            CommandBarOrder: 6));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "addMethod",
            "Add method",
            new AddMethodCommandHandler(),
            IconKind: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 1),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "addConstructor",
            "Add constructor",
            new AddConstructorCommandHandler(),
            IconKind: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 2),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + AddVariableName,
            "Add variable",
            new AddVariableCommandHandler(),
            IconKind: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 3),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "addEventGraph",
            "Add event graph",
            new AddEventGraphCommandHandler(),
            IconKind: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 4),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "overrideMethod",
            "Override method…",
            new OverrideMethodCommandHandler(),
            IconKind: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 5),
            CommandBarOrder: null));
    }
}
