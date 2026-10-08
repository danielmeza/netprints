using NetPrints.Editor.Commands;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the View menu (contracts/commands.md section 1).</summary>
public static class ViewContributions
{
    private const string MenuName = "View";
    private const string ViewportGroup = "viewport";
    private const string PanelsGroup = "panels";
    private const string LayoutGroup = "layout";
    private const string FindGroup = "find";

    /// <summary>Registers the View menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "frameSelection",
            "Frame selection",
            new FrameSelectionCommandHandler(),
            IconKind: "CropFree",
            DefaultGestures: ["F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "fitAll",
            "Fit all",
            new FitAllCommandHandler(),
            IconKind: "FitToScreen",
            DefaultGestures: ["Home", "Shift+F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 1),
            CommandBarOrder: null));

        (string Name, string Label, string PanelId, string Icon)[] panels =
        [
            ("projectTree", "Project", PanelContributions.ProjectTreeId, "FileTree"),
            ("inspector", "Inspector", PanelContributions.InspectorId, "Tune"),
            ("variables", "Variables", PanelContributions.VariablesId, "Variable"),
            ("errors", "Errors", PanelContributions.ErrorsId, "AlertCircleOutline"),
            ("output", "Output", PanelContributions.OutputId, "Console"),
            ("csharp", "C#", PanelContributions.CSharpId, "LanguageCsharp"),
        ];
        foreach ((int order, (string name, string label, string panelId, string icon)) in panels.Index())
        {
            registry.AddCommand(new CommandDescriptor(
                ContributionIds.CommandPrefix + "showPanel." + name,
                label,
                new ShowPanelCommandHandler(panelId),
                IconKind: icon,
                Menu: new MenuPlacement(MenuName, PanelsGroup, order)));
        }

        (string Id, string Label, ICommandHandler Handler, string Icon)[] layout =
        [
            ("floatDocument", "Float tab", new FloatDocumentCommandHandler(), "OpenInNew"),
            ("dockDocument", "Dock tab", new DockDocumentCommandHandler(), "DockWindow"),
            ("resetLayout", "Reset layout", new ResetLayoutCommandHandler(), "Restore"),
        ];
        foreach ((int order, (string id, string label, ICommandHandler handler, string icon)) in layout.Index())
        {
            registry.AddCommand(new CommandDescriptor(
                ContributionIds.CommandPrefix + id,
                label,
                handler,
                IconKind: icon,
                Menu: new MenuPlacement(MenuName, LayoutGroup, order)));
        }

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "commandPalette",
            "Command palette…",
            new CommandPaletteCommandHandler(),
            DefaultGestures: ["Ctrl+Shift+P"],
            Menu: new MenuPlacement(MenuName, FindGroup, 0)));
    }
}
