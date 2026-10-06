using Material.Icons;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>contracts/commands.md section 1 as data: every registered built-in matches its row, and the rows still to come are listed with their task.</summary>
public class BuiltInCommandTableTests
{
    private sealed record Row(
        string Id,
        string Label,
        string? Menu,
        string? Group,
        string[] Gestures,
        CommandScope Scope,
        int? Bar,
        Type? Handler);

    private const CommandScope Tree = CommandScope.ProjectTree;
    private const CommandScope Canvas = CommandScope.Graph;
    private const CommandScope Everywhere = CommandScope.Global;

    private static readonly Row[] Table =
    [
        new("newProject", "New project…", "File", "project", ["Ctrl+Shift+N"], Everywhere, null, typeof(NewProjectCommandHandler)),
        new("openProject", "Open folder or project…", "File", "project", ["Ctrl+O"], Everywhere, null, typeof(OpenProjectCommandHandler)),
        new("closeProject", "Close project", "File", "project", [], Everywhere, null, typeof(CloseProjectCommandHandler)),
        new("newClass", "New class", "File", "class", [], Everywhere, null, typeof(NewClassCommandHandler)),
        new("addExistingClass", "Add existing class…", "File", "class", [], Everywhere, null, typeof(AddExistingClassCommandHandler)),
        new("save", "Save", "File", "save", ["Ctrl+S"], Everywhere, 1, typeof(SaveCommandHandler)),
        new("saveAll", "Save all", "File", "save", ["Ctrl+Shift+S"], Everywhere, null, typeof(SaveAllCommandHandler)),
        new("projectSettings", "Project settings", "File", "project-settings", [], Everywhere, null, typeof(ProjectSettingsCommandHandler)),
        new("references", "References…", "File", "project-settings", [], Everywhere, 7, typeof(ReferencesCommandHandler)),
        new("exit", "Exit", "File", "exit", [], Everywhere, null, typeof(ExitCommandHandler)),
        new("undo", "Undo", "Edit", "history", ["Ctrl+Z"], Everywhere, 4, typeof(UndoCommandHandler)),
        new("redo", "Redo", "Edit", "history", ["Ctrl+Y", "Ctrl+Shift+Z"], Everywhere, 5, typeof(RedoCommandHandler)),
        new("delete", "Delete", "Edit", "selection", ["Delete"], Canvas | Tree, null, typeof(DeleteCommandHandler)),
        new("rename", "Rename", "Edit", "selection", ["F2"], Canvas | Tree, null, typeof(RenameCommandHandler)),
        new("openGraph", "Open", null, null, ["Enter"], Tree, null, typeof(OpenGraphCommandHandler)),
        new("selectAll", "Select all", "Edit", "selection", ["Ctrl+A"], Canvas, null, typeof(SelectAllCommandHandler)),
        new("cancel", "Cancel", null, null, ["Esc"], Canvas, null, typeof(CancelCommandHandler)),
        new("nodeSearch", "Add node…", "Edit", "nodes", ["Ctrl+Space"], Canvas, null, typeof(NodeSearchCommandHandler)),
        new("classSettings", "Class settings", "Edit", "class", [], Everywhere, 6, typeof(ClassSettingsCommandHandler)),
        new("addMethod", "Add method", "Edit", "class", [], Everywhere, null, typeof(AddMethodCommandHandler)),
        new("addConstructor", "Add constructor", "Edit", "class", [], Everywhere, null, typeof(AddConstructorCommandHandler)),
        new("addVariable", "Add variable", "Edit", "class", [], Everywhere, null, typeof(AddVariableCommandHandler)),
        new("addEventGraph", "Add event graph", "Edit", "class", [], Everywhere, null, typeof(AddEventGraphCommandHandler)),
        new("overrideMethod", "Override method…", "Edit", "class", [], Everywhere, null, typeof(OverrideMethodCommandHandler)),
        new("frameSelection", "Frame selection", "View", "viewport", ["F"], Canvas, null, typeof(FrameSelectionCommandHandler)),
        new("fitAll", "Fit all", "View", "viewport", ["Home", "Shift+F"], Canvas, null, typeof(FitAllCommandHandler)),
        new("showPanel.projectTree", "Project", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("showPanel.inspector", "Inspector", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("showPanel.variables", "Variables", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("showPanel.errors", "Errors", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("showPanel.output", "Output", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("showPanel.csharp", "C#", "View", "panels", [], Everywhere, null, typeof(ShowPanelCommandHandler)),
        new("floatDocument", "Float tab", "View", "layout", [], Everywhere, null, typeof(FloatDocumentCommandHandler)),
        new("dockDocument", "Dock tab", "View", "layout", [], Everywhere, null, typeof(DockDocumentCommandHandler)),
        new("resetLayout", "Reset layout", "View", "layout", [], Everywhere, null, typeof(ResetLayoutCommandHandler)),
        new("theme.dark", "Dark", "View", "theme", [], Everywhere, null, null),
        new("theme.light", "Light", "View", "theme", [], Everywhere, null, null),
        new("theme.system", "System", "View", "theme", [], Everywhere, null, null),
        new("commandPalette", "Command palette…", "View", "find", ["Ctrl+Shift+P"], Everywhere, null, null),
        new("goToAnything", "Go to anything…", "Go", "find", ["Ctrl+P"], Everywhere, null, null),
        new("navigateBack", "Back", "Go", "history", ["Alt+Left"], Everywhere, null, null),
        new("navigateForward", "Forward", "Go", "history", ["Alt+Right"], Everywhere, null, null),
        new("goToSource", "Go to source", "Go", "connection", [], Canvas, null, null),
        new("goToTarget", "Go to target", "Go", "connection", [], Canvas, null, null),
        new("nextTab", "Next tab", "Go", "tabs", ["Ctrl+Tab"], Everywhere, null, typeof(CycleTabCommandHandler)),
        new("previousTab", "Previous tab", "Go", "tabs", ["Ctrl+Shift+Tab"], Everywhere, null, typeof(CycleTabCommandHandler)),
        new("closeTab", "Close tab", "Go", "tabs", ["Ctrl+W"], Everywhere, null, typeof(CloseTabCommandHandler)),
        new("compile", "Compile", "Build", "build", ["F7", "Ctrl+Shift+B"], Everywhere, 2, typeof(CompileCommandHandler)),
        new("run", "Run", "Build", "run", ["F5"], Everywhere, 3, typeof(RunCommandHandler)),
        new("stop", "Stop", "Build", "run", ["Shift+F5"], Everywhere, 3, typeof(StopCommandHandler)),
        new("keyboardShortcuts", "Keyboard shortcuts", "Help", "help", [], Everywhere, null, typeof(KeyboardShortcutsCommandHandler)),
        new("startPage", "Start page", "Help", "help", [], Everywhere, null, null),
        new("about", "About NetPrints", "Help", "about", [], Everywhere, null, typeof(AboutCommandHandler)),
    ];

    /// <summary>
    /// The rows whose feature lands later, with the task that registers them. Shrink-only: delete an entry when its
    /// task registers the command (a stale entry fails).
    /// </summary>
    private static readonly Dictionary<string, string> PendingCommandIds = new()
    {
        ["theme.dark"] = "T093",
        ["theme.light"] = "T093",
        ["theme.system"] = "T093",
        ["commandPalette"] = "T075",
        ["goToAnything"] = "T076",
        ["navigateBack"] = "T074",
        ["navigateForward"] = "T074",
        ["goToSource"] = "T077",
        ["goToTarget"] = "T077",
        ["startPage"] = "T066",
    };

    private static readonly string[] MenuOrder = ["File", "Edit", "View", "Go", "Build", "Help"];

    private static string FullId(string id) => ContributionIds.CommandPrefix + id;

    private static ContributionRegistry Registered(CollectingLogger<ContributionRegistry>? logger = null)
    {
        var registry = new ContributionRegistry(logger ?? new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        return registry;
    }

    private static string Canonical(string gesture) =>
        CommandGesture.TryParse(gesture, out var parsed) ? parsed.ToString() : gesture;

    public static TheoryData<string> RegisteredRows() => [.. Table.Where(row => !PendingCommandIds.ContainsKey(row.Id)).Select(row => row.Id)];

    [Theory]
    [MemberData(nameof(RegisteredRows))]
    public void ARegisteredCommandMatchesItsRow(string id)
    {
        Row row = Table.Single(candidate => candidate.Id == id);
        CommandDescriptor command = Assert.Single(Registered().Commands, candidate => candidate.Id == FullId(id));

        Assert.Equal(row.Label, command.Label);
        Assert.Equal(row.Menu, command.Menu?.Path);
        Assert.Equal(row.Group, command.Menu?.Group);
        Assert.Equal(row.Gestures.Select(Canonical), (command.DefaultGestures ?? []).Select(Canonical));
        Assert.Equal(row.Scope, command.Scope);
        Assert.Equal(row.Bar, command.CommandBarOrder);
        Assert.Equal(row.Handler, command.Handler.GetType());
    }

    [Fact]
    public void EveryRegisteredBuiltInIsARow()
    {
        string[] known = [.. Table.Select(row => FullId(row.Id))];

        string[] unknown = [.. Registered().Commands.Select(command => command.Id).Where(id => !known.Contains(id))];

        Assert.Empty(unknown);
    }

    [Fact]
    public void MenuEntriesFollowTheTableOrderWithinTheirGroup()
    {
        var registry = Registered();
        var rows = Table.Where(row => !PendingCommandIds.ContainsKey(row.Id) && row.Menu is not null).ToList();

        foreach (var group in rows.GroupBy(row => (row.Menu, row.Group)))
        {
            int[] orders = [.. group.Select(row => Assert.Single(registry.Commands, command => command.Id == FullId(row.Id)).Menu?.Order ?? -1)];
            Assert.Equal(orders.Order(), orders);
            Assert.Equal(orders.Distinct().Count(), orders.Length);
        }
    }

    [Fact]
    public void MenuPathsAreTheSixMenus() =>
        Assert.All(Registered().Commands.Select(command => command.Menu?.Path).OfType<string>(), path => Assert.Contains(path, MenuOrder));

    [Fact]
    public void ExitsAltF4BelongsToTheOsAndIsNotARegisteredGesture() =>
        Assert.DoesNotContain(Registered().Commands.SelectMany(command => command.DefaultGestures ?? []), gesture => Canonical(gesture) == "Alt+F4");

    [Fact]
    public void ThePendingListIsShrinkOnlyAndEachEntryIsStillPending()
    {
        string[] registered = [.. Registered().Commands.Select(command => command.Id)];

        string[] notInTheTable = [.. PendingCommandIds.Keys.Where(id => Table.All(row => row.Id != id))];
        string[] stale = [.. PendingCommandIds.Keys.Where(id => registered.Contains(FullId(id)))];
        string[] withoutATask = [.. PendingCommandIds.Where(entry => !System.Text.RegularExpressions.Regex.IsMatch(entry.Value, @"^T\d{3}$")).Select(entry => entry.Key)];

        Assert.Empty(notInTheTable);
        Assert.Empty(stale);
        Assert.Empty(withoutATask);
    }

    [Fact]
    public void ARowWithoutAHandlerIsOnThePendingList() =>
        Assert.Empty(Table.Where(row => row.Handler is null && !PendingCommandIds.ContainsKey(row.Id)).Select(row => row.Id));

    [Fact]
    public void TheBuiltInsReportNoIssues()
    {
        var logger = new CollectingLogger<ContributionRegistry>();

        var registry = Registered(logger);

        Assert.Empty(registry.Issues);
    }

    [Fact]
    public void EveryIconIsAMaterialIcon()
    {
        string[] unknown = [.. Registered().Commands.Select(command => command.IconKind).OfType<string>().Where(icon => !Enum.TryParse<MaterialIconKind>(icon, out _))];

        Assert.Empty(unknown);
    }
}
