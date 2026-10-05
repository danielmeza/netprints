using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.Variables;

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
        VariableInspectorElement,
        VariablesPanelElement,
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
        new("Override method", Surface.MenuBar, "overrideMethod"),
        new("Override method", Surface.ClassRowMenu, "overrideMethod"),
        new("Rename variable", Surface.VariableInspectorElement, AutomationIds.VariableInspectorName),
        new("Open variable getter", Surface.VariableInspectorElement, AutomationIds.VariableInspectorOpenGetter),
        new("Open variable setter", Surface.VariableInspectorElement, AutomationIds.VariableInspectorOpenSetter),
        new("Open variable type graph", Surface.VariableInspectorElement, AutomationIds.VariableInspectorOpenTypeGraph),
        new("Show the variables panel", Surface.MenuBar, "showPanel.variables"),
        new("Add member variable (panel)", Surface.VariablesPanelElement, AutomationIds.VariablesAddVariable),
        new("Member variable list (panel)", Surface.VariablesPanelElement, AutomationIds.VariablesClassGroup),
        new("Add local variable (panel)", Surface.VariablesPanelElement, AutomationIds.VariablesAddLocalVariable),
        new("Local variable list (panel)", Surface.VariablesPanelElement, AutomationIds.VariablesMethodGroup),
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

        MemberVariableViewModel variable = app.Session.ContextFor(cls).Variables[^1];
        variable.AddGetterCommand.Execute(null);
        variable.AddSetterCommand.Execute(null);
        HeadlessDriver.Pump();
        return app;
    }

    private static void ShowVariablesPanelOfTheMethod(ShellApp app)
    {
        app.Api.ShowPanel(PanelContributions.VariablesId);
        DocumentId method = CommandTargets.GraphDocumentOf(app.Session, app.Session.Project.Classes[0].Methods[^1])
            ?? throw new InvalidOperationException("The method has no document id.");
        app.Api.OpenDocument(method);
        HeadlessDriver.Pump();
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
            if (former.Surface == Surface.VariableInspectorElement)
            {
                app.Shell.TreeSelection = app.Session.Project.Classes[0].Variables[^1];
                HeadlessDriver.Pump();
            }

            if (former.Surface == Surface.VariablesPanelElement)
            {
                ShowVariablesPanelOfTheMethod(app);
            }

            bool found = former.Surface is Surface.InspectorElement or Surface.VariableInspectorElement or Surface.VariablesPanelElement
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

        foreach (Former former in Table.Where(entry => entry.Surface is not (Surface.InspectorElement or Surface.VariableInspectorElement or Surface.VariablesPanelElement)))
        {
            Assert.Contains(app.Registry.Commands, command => command.Id == IdOf(former.Target));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheVariableInspectorOpensTheGetterSetterAndTypeGraphs()
    {
        await using ShellApp app = await StartWithEveryMemberAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        Variable variable = cls.Variables[^1];
        app.Shell.TreeSelection = variable;
        HeadlessDriver.Pump();

        foreach ((string id, NodeGraph? graph) in new[]
        {
            (AutomationIds.VariableInspectorOpenGetter, (NodeGraph?)variable.GetterMethod),
            (AutomationIds.VariableInspectorOpenSetter, variable.SetterMethod),
            (AutomationIds.VariableInspectorOpenTypeGraph, variable.TypeGraph),
        })
        {
            Button button = Assert.IsType<Button>(app.Ui.Tree.FindControls(new AutomationQuery(id)).Select(pair => pair.Control).First());
            button.Command?.Execute(null);
            HeadlessDriver.Pump();
            Assert.Contains(CommandTargets.GraphDocumentOf(app.Session, graph), app.Api.OpenDocuments);
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

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheVariablesPanelAddsAndRemovesMemberAndLocalVariables()
    {
        await using ShellApp app = await StartWithEveryMemberAsync();
        ClassGraph cls = app.Session.Project.Classes[0];
        MethodGraph method = cls.Methods[^1];
        ShowVariablesPanelOfTheMethod(app);
        int members = cls.Variables.Count;

        PressButton(app, AutomationIds.VariablesAddVariable);
        PressButton(app, AutomationIds.VariablesAddLocalVariable);
        Assert.Equal(members + 1, cls.Variables.Count);
        Assert.Single(method.LocalVariables);

        var panel = Assert.IsType<ShellVariablesPanelViewModel>(app.Shell.FindPanel(PanelContributions.VariablesId)?.Content);
        panel.Current?.MethodVariables?[0].RemoveCommand.Execute(null);
        panel.Current?.ClassVariables[^1].RemoveCommand.Execute(null);
        Assert.Empty(method.LocalVariables);
        Assert.Equal(members, cls.Variables.Count);
    }

    private static void PressButton(ShellApp app, string automationId)
    {
        Button button = Assert.IsType<Button>(app.Ui.Tree.FindControls(new AutomationQuery(automationId)).Select(pair => pair.Control).First());
        button.Command?.Execute(null);
        HeadlessDriver.Pump();
    }
}
