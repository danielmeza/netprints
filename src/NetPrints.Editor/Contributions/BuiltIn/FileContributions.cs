using NetPrints.Editor.Commands;
using NetPrints.Editor.Icons;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the File menu (contracts/commands.md section 1).</summary>
public static class FileContributions
{
    private const string MenuName = "File";
    private const string ProjectGroup = "project";
    private const string ClassGroup = "class";
    private const string SaveGroup = "save";
    private const string ProjectSettingsGroup = "project-settings";

    /// <summary>Registers the File menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "newProject",
            "New project…",
            new NewProjectCommandHandler(),
            IconId: IconIds.NewProject,
            DefaultGestures: ["Ctrl+Shift+N"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ProjectGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "openProject",
            "Open folder or project…",
            new OpenProjectCommandHandler(),
            IconId: IconIds.OpenProject,
            DefaultGestures: ["Ctrl+O"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ProjectGroup, 1),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "closeProject",
            "Close project",
            new CloseProjectCommandHandler(),
            IconId: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ProjectGroup, 2),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "newClass",
            "New class",
            new NewClassCommandHandler(),
            IconId: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "addExistingClass",
            "Add existing class…",
            new AddExistingClassCommandHandler(),
            IconId: null,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ClassGroup, 1),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "save",
            "Save",
            new SaveCommandHandler(),
            IconId: IconIds.Save,
            DefaultGestures: ["Ctrl+S"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, SaveGroup, 0),
            CommandBarOrder: 1));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "saveAll",
            "Save all",
            new SaveAllCommandHandler(),
            IconId: IconIds.SaveAll,
            DefaultGestures: ["Ctrl+Shift+S"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, SaveGroup, 1),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "projectSettings",
            "Project settings",
            new ProjectSettingsCommandHandler(),
            IconId: IconIds.ProjectSettings,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ProjectSettingsGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "references",
            "References…",
            new ReferencesCommandHandler(),
            IconId: IconIds.References,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, ProjectSettingsGroup, 1),
            CommandBarOrder: 7));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "exit",
            "Exit",
            new ExitCommandHandler(),
            IconId: IconIds.Exit,
            DefaultGestures: null,
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "exit", 0),
            CommandBarOrder: null));
    }
}
