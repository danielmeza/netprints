using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Testing.Ui.Driving;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.UITests.ProjectTree;

/// <summary>The project tree and inspector panels render in the shell and their gestures reach the view models (T036, T037).</summary>
public class ProjectTreeWiringTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public bool CheckAccess() => true;
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly HeadlessApp app;
        private readonly SampleCopy sample;

        public Rig(HeadlessApp app, SampleCopy sample, ProjectSessionViewModel session)
        {
            this.app = app;
            this.sample = sample;
            Session = session;
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            BuiltInContributions.Register(registry);
            registry.Freeze();
            Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new ImmediateDispatcher());
            Adapter = new DockShellAdapter(Shell, new NoProjectActions(), id => new TestDocumentViewModel(id, id.GraphKey ?? id.ToString()), NullLogger.Instance);
            Shell.Layout = Adapter;
            Invoker = new CommandInvoker(registry, new ShellCommandContextProvider(Shell, Adapter), Faults.Add);
            Shell.AttachCommands(Invoker);
            Shell.AttachPanels(Adapter, Invoker, app.Composition.Context);
            Shell.Session = session;
            Ui = HeadlessUi.Create();
            Window = Ui.Show(new ShellWindow { DataContext = Shell, Width = ShellRig.Width, Height = ShellRig.Height });
        }

        public ProjectSessionViewModel Session { get; }

        public ShellViewModel Shell { get; }

        public DockShellAdapter Adapter { get; }

        public CommandInvoker Invoker { get; }

        public List<Exception> Faults { get; } = [];

        public HeadlessUi Ui { get; }

        public ShellWindow Window { get; }

        public ProjectTreePanelViewModel Tree => Assert.IsType<ProjectTreePanelViewModel>(Shell.FindPanel(PanelContributions.ProjectTreeId)?.Content);

        public AutomationElement? Find(string automationId) => Ui.Tree.Find(new AutomationQuery(automationId)).FirstOrDefault();

        public UiTarget Row(string automationId) =>
            Ui.Driver.At(Find(automationId) ?? throw new InvalidOperationException($"No element {automationId}."), 0.5, 0.5);

        public async ValueTask DisposeAsync()
        {
            Ui.Dispose();
            Shell.Dispose();
            await app.DisposeAsync();
            sample.Dispose();
        }
    }

    private static async Task<Rig> CreateAsync()
    {
        var app = HeadlessApp.Start();
        var sample = new SampleCopy();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        return new Rig(app, sample, Assert.IsType<ProjectSessionViewModel>(app.Session));
    }

    private static string RowId(string kind, string name) => AutomationIds.TreePrefix + kind + "." + name;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheTreePanelShowsTheProjectAndItsClassesWithAutomationIds()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();

        HeadlessDriver.Pump();

        Assert.NotNull(rig.Find(AutomationIds.TreeView));
        Assert.NotNull(rig.Find(RowId(AutomationIds.TreeKindProject, rig.Session.Project.Name)));
        Assert.NotNull(rig.Find(RowId(AutomationIds.TreeKindClass, cls.Name)));
        Assert.NotNull(rig.Find(AutomationIds.InspectorEmpty));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingAMethodRowOpensItsDocumentAndTheInspectorShowsTheMethod()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(method));
        HeadlessDriver.Pump();

        await rig.Ui.Driver.ClickAsync(rig.Row(RowId(AutomationIds.TreeKindMethod, method.Name)), UiButton.Left, 2, Token);

        Assert.Equal(DocumentId.Graph(rig.Session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), Assert.Single(rig.Adapter.OpenDocuments));
        Assert.IsType<MethodViewModel>(rig.Shell.FindPanel(PanelContributions.InspectorId) is { Content: InspectorPanelViewModel inspector } ? inspector.Content : null);
        Assert.NotNull(rig.Find(AutomationIds.InspectorContent));
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClickingARowSelectsItAndEnterOpensItThroughTheTreeKeyScope()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(method));
        rig.Tree.SelectedItem = null;
        HeadlessDriver.Pump();

        await rig.Ui.Driver.ClickAsync(rig.Row(RowId(AutomationIds.TreeKindMethod, method.Name)), UiButton.Left, 1, Token);
        Assert.Same(method, rig.Shell.TreeSelection);
        await rig.Ui.Driver.PressAsync("Enter", Token);

        Assert.Equal(DocumentId.Graph(rig.Session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), Assert.Single(rig.Adapter.OpenDocuments));
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickingARowSelectsItSoItsContextMenuHasItsEntries()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(cls));
        Assert.True(rig.Tree.Select(method));
        rig.Tree.SelectedItem = rig.Tree.Roots[0];
        HeadlessDriver.Pump();

        await rig.Ui.Driver.ClickAsync(rig.Row(RowId(AutomationIds.TreeKindMethod, method.Name)), UiButton.Right, 1, Token);

        Assert.Same(method, rig.Shell.TreeSelection);
        Assert.NotEmpty(rig.Tree.SelectedItem?.MenuEntries ?? []);
    }

    private static UiTarget RowIndent(Rig rig, string rowId)
    {
        var row = rig.Find(rowId) ?? throw new InvalidOperationException($"No element {rowId}.");
        var tree = rig.Find(AutomationIds.TreeView) ?? throw new InvalidOperationException("No tree.");
        return new UiTarget(row.Window, tree.Bounds.X + 4, row.Bounds.Y + Math.Min(row.Bounds.Height / 2, 12));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingTheEmptyPartOfARowOpensIt()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(method));
        HeadlessDriver.Pump();

        await rig.Ui.Driver.ClickAsync(RowIndent(rig, RowId(AutomationIds.TreeKindMethod, method.Name)), UiButton.Left, 2, Token);

        Assert.Equal(DocumentId.Graph(rig.Session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), Assert.Single(rig.Adapter.OpenDocuments));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task RightClickingTheEmptyPartOfARowOpensItsContextMenu()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(cls));
        Assert.True(rig.Tree.Select(method));
        HeadlessDriver.Pump();
        string rowId = RowId(AutomationIds.TreeKindMethod, method.Name);

        await rig.Ui.Driver.ClickAsync(RowIndent(rig, rowId), UiButton.Right, 1, Token);

        TreeViewItem item = rig.Window.GetVisualDescendants().OfType<TreeViewItem>().Single(i => AutomationProperties.GetAutomationId(i) == rowId);
        Assert.True(item.ContextMenu is { IsOpen: true, ItemCount: > 0 }, rig.Ui.Driver.InputTrace);
        string classId = RowId(AutomationIds.TreeKindClass, cls.Name);
        TreeViewItem classItem = rig.Window.GetVisualDescendants().OfType<TreeViewItem>().Single(i => AutomationProperties.GetAutomationId(i) == classId);
        Assert.Same(rig.Tree.SelectedItem?.MenuEntries, item.ContextMenu.ItemsSource);
        await rig.Ui.Driver.ClickAsync(RowIndent(rig, classId), UiButton.Right, 1, Token);
        Assert.Equal(cls, rig.Tree.SelectedItem?.Model);
        Assert.Same(rig.Tree.SelectedItem?.MenuEntries, classItem.ContextMenu?.ItemsSource);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ASlowSelectionDoesNotSplitADoubleClickIntoTwoSingleClicks()
    {
        await using Rig rig = await CreateAsync();
        ClassGraph cls = rig.Session.Project.Classes.Single();
        MethodGraph method = cls.Methods.First();
        Assert.True(rig.Tree.Select(method));
        rig.Tree.SelectedItem = null;
        HeadlessDriver.Pump();
        rig.Tree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProjectTreePanelViewModel.SelectedItem))
            {
                Thread.Sleep(SlowFirstPressMilliseconds);
            }
        };

        await rig.Ui.Driver.ClickAsync(rig.Row(RowId(AutomationIds.TreeKindMethod, method.Name)), UiButton.Left, 2, Token);

        Assert.True(rig.Adapter.OpenDocuments.Count == 1, rig.Ui.Driver.InputTrace);
    }

    private const int SlowFirstPressMilliseconds = 600;
}
