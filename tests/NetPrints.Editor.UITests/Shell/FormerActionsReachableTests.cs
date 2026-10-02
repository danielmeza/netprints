using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>FR-017, US2 scenario 8: every action the launcher and the class window offered is reachable in the shell.</summary>
public class FormerActionsReachableTests
{
    private enum Surface
    {
        MenuBar,
        CommandBar,
        ClassRowMenu,
        MethodRowMenu,
        ConstructorRowMenu,
        VariableRowMenu,
        EventGraphRowMenu,
        InspectorElement,
    }

    /// <summary>One former action, the surface that offers it now and what to find there: a command id, or an automation id for the inspector.</summary>
    private sealed record Former(string Action, Surface Surface, string Target);

    private static readonly Former[] Table =
    [
        new("Create project", Surface.MenuBar, "newProject"),
        new("Open project", Surface.MenuBar, "openProject"),
        new("Close project", Surface.MenuBar, "closeProject"),
        new("New class", Surface.MenuBar, "newClass"),
        new("Existing class", Surface.MenuBar, "addExistingClass"),
        new("Save", Surface.MenuBar, "save"),
        new("Save", Surface.MenuBar, "saveAll"),
        new("Project settings", Surface.MenuBar, "projectSettings"),
        new("References", Surface.MenuBar, "references"),
        new("Exit", Surface.MenuBar, "exit"),
        new("Compile", Surface.CommandBar, "compile"),
        new("Run", Surface.CommandBar, "run"),
        new("Class settings", Surface.ClassRowMenu, "classSettings"),
        new("Class settings", Surface.InspectorElement, AutomationIds.ClassInspectorName),
        new("Open class graph", Surface.ClassRowMenu, "openGraph"),
        new("Rename class", Surface.ClassRowMenu, "rename"),
        new("Remove class", Surface.ClassRowMenu, "delete"),
        new("Add method", Surface.ClassRowMenu, "addMethod"),
        new("Add constructor", Surface.ClassRowMenu, "addConstructor"),
        new("Add variable", Surface.ClassRowMenu, "addVariable"),
        new("Add event graph", Surface.ClassRowMenu, "addEventGraph"),
        new("Open method", Surface.MethodRowMenu, "openGraph"),
        new("Rename method", Surface.MethodRowMenu, "rename"),
        new("Remove method", Surface.MethodRowMenu, "delete"),
        new("Open constructor", Surface.ConstructorRowMenu, "openGraph"),
        new("Remove constructor", Surface.ConstructorRowMenu, "delete"),
        new("Rename variable", Surface.VariableRowMenu, "rename"),
        new("Remove variable", Surface.VariableRowMenu, "delete"),
        new("Open event graph", Surface.EventGraphRowMenu, "openGraph"),
        new("Rename event graph", Surface.EventGraphRowMenu, "rename"),
        new("Remove event graph", Surface.EventGraphRowMenu, "delete"),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string IdOf(string name) => ContributionIds.CommandPrefix + name;

    private static async Task<ShellApp> StartWithEveryMemberAsync()
    {
        ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        ClassGraph cls = app.Session.Project.Classes[0];
        app.Shell.TreeSelection = cls;
        foreach (string add in new[] { "addMethod", "addConstructor", "addVariable", "addEventGraph" })
        {
            await Run(app, add);
        }

        HeadlessDriver.Pump();
        return app;
    }

    private static Task Run(ShellApp app, string name) => app.Command(name).Handler.ExecuteAsync(app.Commands.CreateContext(), Token);

    private static ProjectTreePanelViewModel Tree(ShellApp app) =>
        Assert.IsType<ProjectTreePanelViewModel>(app.Shell.FindPanel(PanelContributions.ProjectTreeId)?.Content);

    private static IEnumerable<ProjectTreeItemViewModel> Flatten(IEnumerable<ProjectTreeItemViewModel> items) =>
        items.SelectMany(item => new[] { item }.Concat(Flatten(item.Children)));

    private static IEnumerable<string> MenuIds(ShellApp app, Surface surface)
    {
        switch (surface)
        {
            case Surface.MenuBar:
                return app.Shell.MenuBar?.Menus.SelectMany(menu => menu.Items).Select(entry => entry.Id) ?? [];
            case Surface.CommandBar:
                return app.Shell.CommandBar?.Slots.SelectMany(slot => slot.Items).Select(entry => entry.Id) ?? [];
            default:
                TreeItemKind kind = surface switch
                {
                    Surface.ClassRowMenu => TreeItemKind.Class,
                    Surface.MethodRowMenu => TreeItemKind.Method,
                    Surface.ConstructorRowMenu => TreeItemKind.Constructor,
                    Surface.VariableRowMenu => TreeItemKind.Variable,
                    _ => TreeItemKind.EventGraph,
                };
                ProjectTreeItemViewModel row = Flatten(Tree(app).Roots).First(item => item.Kind == kind);
                Tree(app).SelectedItem = row;
                return row.MenuEntries.Select(entry => entry.Id);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryFormerActionIsOfferedByAShellSurface()
    {
        await using ShellApp app = await StartWithEveryMemberAsync();
        app.Shell.TreeSelection = app.Session.Project.Classes[0];
        HeadlessDriver.Pump();
        var missing = new List<string>();

        foreach (Former former in Table)
        {
            bool found = former.Surface == Surface.InspectorElement
                ? app.Ui.Tree.Find(new AutomationQuery(former.Target)).Count > 0
                : MenuIds(app, former.Surface).Contains(IdOf(former.Target), StringComparer.Ordinal);
            if (!found)
            {
                missing.Add($"{former.Action} ({former.Surface}: {former.Target})");
            }
        }

        Assert.Empty(missing);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryCommandTheTableNamesIsRegistered()
    {
        await using ShellApp app = ShellApp.Start();

        foreach (Former former in Table.Where(entry => entry.Surface != Surface.InspectorElement))
        {
            Assert.Contains(app.Registry.Commands, command => command.Id == IdOf(former.Target));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheTreeCommandsAddAndRemoveMembersAndOpenTheirGraphs()
    {
        await using ShellApp app = await StartWithEveryMemberAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        MethodGraph method = cls.Methods[^1];
        ConstructorGraph constructor = cls.Constructors[^1];
        Variable variable = cls.Variables[^1];
        EventGraph eventGraph = cls.EventGraphs[^1];

        Assert.Contains(CommandTargets.GraphDocumentOf(app.Session, method), app.Api.OpenDocuments);
        Assert.Contains(CommandTargets.GraphDocumentOf(app.Session, constructor), app.Api.OpenDocuments);
        Assert.Contains(CommandTargets.GraphDocumentOf(app.Session, eventGraph), app.Api.OpenDocuments);
        Assert.Same(eventGraph, app.Shell.TreeSelection);

        foreach (object item in new object[] { method, constructor, variable, eventGraph })
        {
            app.Shell.TreeSelection = item;
            await Run(app, "delete");
        }

        HeadlessDriver.Pump();
        Assert.DoesNotContain(method, cls.Methods);
        Assert.DoesNotContain(constructor, cls.Constructors);
        Assert.DoesNotContain(variable, cls.Variables);
        Assert.DoesNotContain(eventGraph, cls.EventGraphs);
        Assert.DoesNotContain(app.Api.OpenDocuments, id => id.GraphKey is { } key && key != DocumentId.ClassGraphKey);

        app.Shell.TreeSelection = cls;
        await Run(app, "classSettings");
        Assert.True(app.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.Same(cls, app.Shell.TreeSelection);
    }
}
