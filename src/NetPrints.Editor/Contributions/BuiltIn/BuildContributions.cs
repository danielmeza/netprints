using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Build menu (contracts/commands.md section 1).</summary>
public static class BuildContributions
{
    private const string MenuName = "Build";
    private const string RunGroup = "run";

    /// <summary>Registers the Build menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "compile",
            "Compile",
            new CompileCommandHandler(),
            IconKind: "Hammer",
            DefaultGestures: ["F7", "Ctrl+Shift+B"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, "build", 0),
            CommandBarOrder: 2));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "run",
            "Run",
            new RunCommandHandler(),
            IconKind: "Play",
            DefaultGestures: ["F5"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, RunGroup, 0),
            CommandBarOrder: 3));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "stop",
            "Stop",
            new StopCommandHandler(),
            IconKind: "Stop",
            DefaultGestures: ["Shift+F5"],
            Scope: CommandScope.Global,
            Menu: new MenuPlacement(MenuName, RunGroup, 1),
            CommandBarOrder: 3));
    }
}
