using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.UndoRedo;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.ProjectTree;

public sealed class ProjectTreePanelViewModelTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private static string[] Names(IEnumerable<ProjectTreeItemViewModel> items) => [.. items.Select(item => item.Name)];

    [Fact]
    public async Task TheTreeShowsTheProjectItsClassesAndTheFourGroupsOfEachClass()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        cls.Methods.Add(new MethodGraph("Greet") { Class = cls });
        cls.Constructors.Add(new ConstructorGraph { Class = cls });
        cls.EventGraphs.Add(new EventGraph("Ticks") { Class = cls });
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));

        ProjectTreeItemViewModel root = Assert.Single(rig.Tree.Roots);
        Assert.Equal(TreeItemKind.Project, root.Kind);
        Assert.Equal(session.Project.Name, root.Name);
        ProjectTreeItemViewModel classItem = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.Same(cls, classItem.Model);
        Assert.Equal(["Methods", "Constructors", "Variables", "Event graphs"], Names(classItem.Children));
        Assert.All(classItem.Children, group => Assert.Equal(TreeItemKind.Group, group.Kind));
        Assert.Contains("Greet", Names(classItem.Children[0].Children));
        Assert.Single(classItem.Children[1].Children);
        Assert.Equal(TreeItemKind.Constructor, classItem.Children[1].Children[0].Kind);
        Assert.Equal(["count"], Names(classItem.Children[2].Children));
        Assert.Equal(["Ticks"], Names(classItem.Children[3].Children));
        Assert.Equal("Tree.method.Greet", rig.Item(TreeItemKind.Method, "Greet").AutomationId);
        Assert.Equal("Tree.eventgraph.Ticks", rig.Item(TreeItemKind.EventGraph, "Ticks").AutomationId);
    }

    [Fact]
    public async Task NoProjectOpenMeansAnEmptyTreeAndClosingTheProjectEmptiesIt()
    {
        Assert.Empty(rig.Tree.Roots);
        await rig.OpenSessionAsync();
        Assert.Single(rig.Tree.Roots);

        rig.Shell.Session = null;

        Assert.Empty(rig.Tree.Roots);
        Assert.Null(rig.Tree.SelectedItem);
    }

    [Fact]
    public async Task TheTreeFollowsAddsRemovesAndRenamesOfMembersAndClasses()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        var undo = new UndoRedoStack();
        undo.Do(EditorCommands.AddVariable(cls, "count"));
        var eventGraph = new EventGraph("Ticks") { Class = cls };
        undo.Do(EditorCommands.AddEventGraph(cls, eventGraph));

        method.Name = "Hello";
        cls.Variables[0].Name = "total";
        Assert.Equal("Tree.method.Hello", rig.Item(TreeItemKind.Method, "Hello").AutomationId);
        Assert.Equal("total", Assert.Single(rig.Item(TreeItemKind.Class, cls.Name).Children[2].Children).Name);

        cls.Methods.Remove(method);
        undo.Do(EditorCommands.RemoveEventGraph(cls, eventGraph));
        Assert.Empty(rig.Item(TreeItemKind.Class, cls.Name).Children[0].Children.Where(item => item.Name == "Hello"));
        Assert.Empty(rig.Item(TreeItemKind.Class, cls.Name).Children[3].Children);

        var other = new ClassGraph { Name = "Other", Namespace = cls.Namespace };
        session.Project.Classes.Add(other);
        Assert.Equal(TreeItemKind.Class, rig.Item(TreeItemKind.Class, "Other").Kind);
        other.Name = "Renamed";
        rig.Shell.NotifyModelRenamed();
        Assert.Equal("Tree.class.Renamed", rig.Item(TreeItemKind.Class, "Renamed").AutomationId);
        session.Project.Classes.Remove(other);
        Assert.DoesNotContain(rig.Flatten(rig.Tree.Roots), item => ReferenceEquals(item.Model, other));
    }

    [Fact]
    public async Task OpeningAGraphItemOpensItsDocumentThroughTheShellAndOtherItemsDoNothing()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        string classPath = session.ClassPathOf(cls);

        rig.Item(TreeItemKind.Method, "Greet").OpenCommand.Execute(null);
        rig.Item(TreeItemKind.Class, cls.Name).OpenCommand.Execute(null);

        Assert.Equal([DocumentId.Graph(classPath, $"method:{method.Id}"), DocumentId.Graph(classPath, "class")], rig.Api.OpenDocuments);
        Assert.False(rig.Item(TreeItemKind.Variable, "count").OpenCommand.CanExecute(null));
        Assert.False(rig.Tree.Roots[0].OpenCommand.CanExecute(null));
    }

    [Fact]
    public async Task AClassRowShowsTheUnsavedMarkWhileItsFileIsUnsaved()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        ProjectTreeItemViewModel item = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.False(item.IsUnsaved);
        Assert.Equal(cls.Name, item.DisplayName);

        session.ContextFor(cls).CreateVariable();
        Assert.True(item.IsUnsaved);
        Assert.Equal(cls.Name + "*", item.DisplayName);
        Assert.Equal("Tree.class." + cls.Name, item.AutomationId);

        session.UndoStackFor(cls).Undo();
        Assert.False(item.IsUnsaved);

        session.ContextFor(cls).CreateVariable();
        Assert.True(await session.SaveAllAsync());
        Assert.False(item.IsUnsaved);
        Assert.Equal(cls.Name, item.DisplayName);
    }

    [Fact]
    public async Task SelectingARowSetsTheShellTreeSelectionAndPulsesTheCommandStates()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        int pulses = 0;
        rig.Provider.CommandStatesChanged += (_, _) => pulses++;

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");

        Assert.Same(method, rig.Shell.TreeSelection);
        Assert.Same(method, rig.Provider.Create().Selection.TreeItem);
        Assert.True(pulses > 0, "a selection change pulses CommandStatesChanged");

        rig.Tree.SelectedItem = rig.Tree.Roots[0];
        Assert.Null(rig.Shell.TreeSelection);
        rig.Tree.SelectedItem = null;
        Assert.Null(rig.Provider.Create().Selection.TreeItem);
    }

    [Fact]
    public async Task RenameAndDeleteActOnTheSelectedRowAndRenameIsOffForConstructors()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        cls.Constructors.Add(new ConstructorGraph { Class = cls });
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        CommandDescriptor rename = rig.Command("rename");
        CommandDescriptor delete = rig.Command("delete");

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        Assert.True(rig.Invoker.CanRun(rename, CommandScope.ProjectTree));
        Assert.True(rig.Invoker.CanRun(delete, CommandScope.ProjectTree));
        rig.Tree.SelectedItem = rig.Tree.Roots[0].Children[0].Children[1].Children[0];
        Assert.Equal(TreeItemKind.Constructor, rig.Tree.SelectedItem.Kind);
        Assert.False(rig.Invoker.CanRun(rename, CommandScope.ProjectTree));
        Assert.True(rig.Invoker.CanRun(delete, CommandScope.ProjectTree));
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Variable, "count");
        Assert.True(rig.Invoker.CanRun(rename, CommandScope.ProjectTree));
        Assert.True(rig.Invoker.CanRun(delete, CommandScope.ProjectTree));

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        Assert.True(rig.Invoker.TryRun(rename, CommandScope.ProjectTree));
        Assert.True(SpinWait.SpinUntil(() => rig.Api.Project.Calls.Contains("RenameItem"), TimeSpan.FromSeconds(10)));
        Assert.Same(method, rig.Api.Project.LastItem);
    }

    [Fact]
    public async Task TheEnterKeyCommandOpensTheSelectedGraphInTheProjectTreeScope()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);
        CommandDescriptor open = Assert.Single(rig.Invoker.CommandsIn(CommandScope.ProjectTree), command => command.Id == ContributionIds.CommandPrefix + "openGraph");
        Assert.Contains("Enter", open.DefaultGestures ?? []);

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        Assert.True(rig.Invoker.TryRun(open));
        Assert.True(SpinWait.SpinUntil(() => rig.Api.OpenDocuments.Count == 1, TimeSpan.FromSeconds(10)));
        Assert.Equal(DocumentId.Graph(session.ClassPathOf(cls), $"method:{method.Id}"), rig.Api.ActiveDocument);

        rig.Tree.SelectedItem = rig.Tree.Roots[0];
        Assert.False(rig.Invoker.CanRun(open));
    }

    [Fact]
    public async Task ContextMenusListTheRegistryItemsOfTheTargetAndHideTheOnesThatCannotRun()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        cls.Methods.Add(new MethodGraph("Greet") { Class = cls });
        cls.Constructors.Add(new ConstructorGraph { Class = cls });
        cls.EventGraphs.Add(new EventGraph("Ticks") { Class = cls });
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        Assert.Equal(
            ["Open", "Rename", "Add method", "Add constructor", "Add variable", "Add event graph", "Override method…", "Class settings", "Delete"],
            rig.Item(TreeItemKind.Class, cls.Name).MenuEntries.Select(entry => entry.Label));

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Method, "Greet");
        Assert.Equal(["Open", "Rename", "Delete"], rig.Item(TreeItemKind.Method, "Greet").MenuEntries.Select(entry => entry.Label));

        rig.Tree.SelectedItem = rig.Tree.Roots[0].Children[0].Children[1].Children[0];
        Assert.Equal(["Open", "Delete"], rig.Tree.SelectedItem.MenuEntries.Select(entry => entry.Label));

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Variable, "count");
        Assert.Equal(["Rename", "Delete"], rig.Tree.SelectedItem.MenuEntries.Select(entry => entry.Label));

        rig.Tree.SelectedItem = rig.Item(TreeItemKind.EventGraph, "Ticks");
        Assert.Equal(["Open", "Rename", "Delete"], rig.Tree.SelectedItem.MenuEntries.Select(entry => entry.Label));

        rig.Tree.SelectedItem = rig.Tree.Roots[0];
        Assert.Empty(rig.Tree.Roots[0].MenuEntries);
    }

    [Fact]
    public async Task APulseKeepsTheEntriesOfAnOpenContextMenu()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);
        ProjectTreeItemViewModel item = rig.Tree.SelectedItem;
        CommandEntryViewModel[] before = [.. item.MenuEntries];
        Assert.NotEmpty(before);
        int changes = 0;
        item.MenuEntries.CollectionChanged += (_, _) => changes++;

        session.UndoStackFor(cls).Do(EditorCommands.AddVariable(cls, "count"));

        Assert.Equal(before, item.MenuEntries);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task AContextMenuEntryRunsItsCommandOnTheSelectedRow()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        rig.Tree.SelectedItem = rig.Item(TreeItemKind.Class, cls.Name);

        CommandEntryViewModel addMethod = rig.Tree.SelectedItem.MenuEntries.Single(entry => entry.Label == "Add method");
        addMethod.RunCommand.Execute(null);

        Assert.True(SpinWait.SpinUntil(() => rig.Api.Project.Calls.Any(call => call.StartsWith("AddMethod", StringComparison.Ordinal)), TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task TheSelectionFollowsTheActiveGraphDocumentAndSelectFindsRowsByModel()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var method = new MethodGraph("Greet") { Class = cls };
        cls.Methods.Add(method);

        Assert.True(rig.Tree.Select(method));
        Assert.Equal("Greet", rig.Tree.SelectedItem?.Name);
        Assert.False(rig.Tree.Select(new MethodGraph("Missing")));

        rig.Tree.SelectedItem = null;
        rig.Shell.AddDocument(new TestDocumentViewModel(DocumentId.Graph(session.ClassPathOf(cls), $"method:{method.Id}"), "Greet"));
        rig.Shell.ActiveDocument = rig.Shell.Documents[0];
        Assert.Same(method, rig.Shell.TreeSelection);
        rig.Shell.ActiveDocument = null;
        Assert.Same(method, rig.Shell.TreeSelection);
    }
}
