using NetPrints.Editor.Commands;
using NetPrints.Editor.Icons;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the View menu (contracts/commands.md section 1).</summary>
public static class ViewContributions
{
    internal const string MenuName = "View";
    internal const string PanelsGroup = "panels";
    private const string ViewportGroup = "viewport";
    private const string LayoutGroup = "layout";
    private const string ThemeGroup = "theme";
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
            IconId: IconIds.ZoomToFit,
            DefaultGestures: ["F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 0),
            CommandBarOrder: null));

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "fitAll",
            "Fit all",
            new FitAllCommandHandler(),
            IconId: IconIds.FitToScreen,
            DefaultGestures: ["Home", "Shift+F"],
            Scope: CommandScope.Graph,
            Menu: new MenuPlacement(MenuName, ViewportGroup, 1),
            CommandBarOrder: null));

        (string Name, string Label, string PanelId, string Icon)[] panels =
        [
            ("projectTree", "Project", PanelContributions.ProjectTreeId, IconIds.PanelProjectTree),
            ("inspector", "Inspector", PanelContributions.InspectorId, IconIds.PanelInspector),
            ("variables", "Variables", PanelContributions.VariablesId, IconIds.PanelVariables),
            ("errors", "Errors", PanelContributions.ErrorsId, IconIds.PanelErrors),
            ("output", "Output", PanelContributions.OutputId, IconIds.PanelOutput),
            ("csharp", "C#", PanelContributions.CSharpId, IconIds.PanelCSharp),
        ];
        foreach ((int order, (string name, string label, string panelId, string icon)) in panels.Index())
        {
            registry.AddCommand(new CommandDescriptor(
                ContributionIds.CommandPrefix + "showPanel." + name,
                label,
                new ShowPanelCommandHandler(panelId),
                IconId: icon,
                Menu: new MenuPlacement(MenuName, PanelsGroup, order)));
        }

        (string Id, string Label, ICommandHandler Handler, string Icon)[] layout =
        [
            ("floatDocument", "Float tab", new FloatDocumentCommandHandler(), IconIds.FloatDocument),
            ("dockDocument", "Dock tab", new DockDocumentCommandHandler(), IconIds.DockDocument),
            ("resetLayout", "Reset layout", new ResetLayoutCommandHandler(), IconIds.ResetLayout),
        ];
        foreach ((int order, (string id, string label, ICommandHandler handler, string icon)) in layout.Index())
        {
            registry.AddCommand(new CommandDescriptor(
                ContributionIds.CommandPrefix + id,
                label,
                handler,
                IconId: icon,
                Menu: new MenuPlacement(MenuName, LayoutGroup, order)));
        }

        (string Id, string Label, EditorTheme Theme)[] themes =
        [
            ("theme.dark", "Dark", EditorTheme.Dark),
            ("theme.light", "Light", EditorTheme.Light),
            ("theme.system", "System", EditorTheme.System),
        ];
        foreach ((int order, (string id, string label, EditorTheme theme)) in themes.Index())
        {
            registry.AddCommand(new CommandDescriptor(
                ContributionIds.CommandPrefix + id,
                label,
                new ThemeCommandHandler(theme),
                Menu: new MenuPlacement(MenuName, ThemeGroup, order)));
        }

        registry.AddCommand(new CommandDescriptor(
            ContributionIds.CommandPrefix + "commandPalette",
            "Command palette…",
            new CommandPaletteCommandHandler(),
            DefaultGestures: ["Ctrl+Shift+P"],
            Menu: new MenuPlacement(MenuName, FindGroup, 0)));
    }
}
