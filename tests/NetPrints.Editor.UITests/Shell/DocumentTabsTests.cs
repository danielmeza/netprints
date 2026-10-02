using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>Documents and layout commands over the shell window: tabs, the Project settings document, floated graphs and the View menu (T039).</summary>
public class DocumentTabsTests
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
        private readonly ClassEditorViewModel editor;
        private readonly ContributionRegistry registry;

        public Rig(HeadlessApp app, SampleCopy sample, ProjectSessionViewModel session)
        {
            this.app = app;
            this.sample = sample;
            Session = session;
            Class = session.Project.Classes.Single();
            Method = Class.Methods.First();
            editor = new ClassEditorViewModel(Class, app.Composition.Context);
            MethodDocument = DocumentId.Graph(session.ClassPathOf(Class), DocumentId.MethodKeyPrefix + Method.Id);
            ClassDocument = DocumentId.Graph(session.ClassPathOf(Class), DocumentId.ClassGraphKey);
            registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            BuiltInContributions.Register(registry);
            registry.Freeze();
            Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new ImmediateDispatcher());
            Adapter = new DockShellAdapter(Shell, new NoProjectActions(), CreateDocument);
            Shell.Layout = Adapter;
            Invoker = new CommandInvoker(registry, new ShellCommandContextProvider(Shell, Adapter), Faults.Add);
            Shell.AttachCommands(Invoker);
            Shell.AttachPanels(Adapter, Invoker, app.Composition.Context);
            Shell.Session = session;
            Ui = HeadlessUi.Create();
            Window = Ui.Show(new ShellWindow { DataContext = Shell, Width = ShellRig.Width, Height = ShellRig.Height });
        }

        public ProjectSessionViewModel Session { get; }

        public ClassGraph Class { get; }

        public MethodGraph Method { get; }

        public DocumentId MethodDocument { get; }

        public DocumentId ClassDocument { get; }

        public ShellViewModel Shell { get; }

        public DockShellAdapter Adapter { get; }

        public CommandInvoker Invoker { get; }

        public List<Exception> Faults { get; } = [];

        public HeadlessUi Ui { get; }

        public ShellWindow Window { get; }

        public NodeGraphViewModel MethodGraphView => ((GraphDocumentViewModel)(Shell.FindDocument(MethodDocument) ?? throw new InvalidOperationException("Not open."))).Graph;

        public IEnumerable<DocumentId> Tabs => Adapter.OpenDocuments;

        public CommandDescriptor Command(string name) => registry.Commands.Single(command => command.Id == ContributionIds.CommandPrefix + name);

        public Task RunAsync(string name) => Command(name).Handler.ExecuteAsync(Invoker.CreateContext(), Token);

        public Control Tab(DocumentId id) => Window.GetVisualDescendants().OfType<Control>()
            .Single(control => control.GetType().Name == "DocumentTabStripItem" && control.DataContext is ShellDocument { } document && document.Id == id.ToString());

        public UiTarget Center(Control control, Window? host = null)
        {
            Window window = host ?? Window;
            Point point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? throw new InvalidOperationException("Not in the window.");
            return new UiTarget(Ui.Tree.KeyOf(window), point.X, point.Y);
        }

        public Control CloseButton(DocumentId id) => Tab(id).GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Bounds.Width > 0);

        public void Settle() => HeadlessDriver.Pump();

        public async ValueTask DisposeAsync()
        {
            Ui.Dispose();
            Shell.Dispose();
            editor.Dispose();
            await app.DisposeAsync();
            sample.Dispose();
        }

        private DocumentViewModel? CreateDocument(DocumentId id) => id switch
        {
            _ when id == DocumentId.ProjectSettings => new ProjectSettingsDocumentViewModel(Session, app.Composition.Context),
            _ when id == MethodDocument => new GraphDocumentViewModel(id, new NodeGraphViewModel(Method, editor.Services), Class, Session),
            _ when id == ClassDocument => new GraphDocumentViewModel(id, new NodeGraphViewModel(Class, editor.Services), Class, Session),
            _ => null,
        };
    }

    private static async Task<Rig> CreateAsync()
    {
        var app = HeadlessApp.Start();
        var sample = new SampleCopy();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        return new Rig(app, sample, Assert.IsType<ProjectSessionViewModel>(app.ViewModel.Session));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheProjectSettingsDocumentShowsTheBinaryTypeAndChoosingOneEditsTheProject()
    {
        await using Rig rig = await CreateAsync();
        BinaryType other = Enum.GetValues<BinaryType>().First(type => type != rig.Session.Project.OutputBinaryType);

        await rig.RunAsync("projectSettings");
        rig.Settle();

        Assert.Equal([DocumentId.ProjectSettings], rig.Tabs);
        var chooser = rig.Window.GetVisualDescendants().OfType<ComboBox>().Single(box => Avalonia.Automation.AutomationProperties.GetAutomationId(box) == AutomationIds.ProjectSettingsBinaryTypeChooser);
        Assert.Equal(rig.Session.Project.OutputBinaryType, chooser.SelectedItem);

        chooser.SelectedItem = other;
        for (int i = 0; i < 200 && rig.Session.Project.OutputBinaryType != other; i++)
        {
            rig.Settle();
            await Task.Delay(50, Token);
        }

        Assert.Equal(other, rig.Session.Project.OutputBinaryType);
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ATabClosesWithItsButtonAndWithAMiddleClick()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.OpenDocument(rig.MethodDocument);
        rig.Adapter.OpenDocument(rig.ClassDocument);
        rig.Adapter.OpenDocument(DocumentId.ProjectSettings);
        rig.Settle();

        await rig.Ui.Driver.ClickAsync(rig.Center(rig.CloseButton(rig.ClassDocument)), UiButton.Left, 1, Token);
        Assert.Equal([rig.MethodDocument, DocumentId.ProjectSettings], rig.Tabs);

        await rig.Ui.Driver.ClickAsync(rig.Center(rig.Tab(rig.MethodDocument)), UiButton.Middle, 1, Token);
        Assert.Equal([DocumentId.ProjectSettings], rig.Tabs);
        Assert.Equal(DocumentId.ProjectSettings, rig.Adapter.ActiveDocument);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TabsReorderByDraggingOneOverAnother()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.OpenDocument(rig.MethodDocument);
        rig.Adapter.OpenDocument(rig.ClassDocument);
        rig.Adapter.OpenDocument(DocumentId.ProjectSettings);
        rig.Settle();
        Assert.Equal([rig.MethodDocument, rig.ClassDocument, DocumentId.ProjectSettings], rig.Tabs);

        await rig.Ui.Driver.DragAsync(rig.Center(rig.Tab(DocumentId.ProjectSettings)), rig.Center(rig.Tab(rig.MethodDocument)).Offset(-10, 0), UiButton.Left, Token);
        rig.Settle();

        Assert.Equal([DocumentId.ProjectSettings, rig.MethodDocument, rig.ClassDocument], rig.Tabs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CloseTabNextTabAndPreviousTabWorkOnTheActiveDocument()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.OpenDocument(rig.MethodDocument);
        rig.Adapter.OpenDocument(rig.ClassDocument);
        rig.Adapter.OpenDocument(DocumentId.ProjectSettings);
        rig.Settle();

        await rig.RunAsync("nextTab");
        Assert.Equal(rig.MethodDocument, rig.Adapter.ActiveDocument);
        await rig.RunAsync("previousTab");
        Assert.Equal(DocumentId.ProjectSettings, rig.Adapter.ActiveDocument);
        await rig.RunAsync("previousTab");
        Assert.Equal(rig.ClassDocument, rig.Adapter.ActiveDocument);

        await rig.RunAsync("closeTab");
        rig.Settle();

        Assert.Equal([rig.MethodDocument, DocumentId.ProjectSettings], rig.Tabs);
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AFloatedGraphKeepsEditingUndoAndSave()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.OpenDocument(rig.MethodDocument);
        rig.Adapter.OpenDocument(rig.ClassDocument);
        await rig.RunAsync("floatDocument");
        rig.Settle();
        Assert.True(rig.Adapter.IsFloating(rig.ClassDocument));
        rig.Adapter.ActivateDocument(rig.MethodDocument);
        await rig.RunAsync("floatDocument");
        rig.Settle();
        Assert.True(rig.Adapter.IsFloating(rig.MethodDocument));
        Assert.Equal(rig.MethodDocument, rig.Adapter.ActiveDocument);
        NodeGraphViewModel graph = rig.MethodGraphView;
        Node added = graph.AddNode<IfElseNode>(new GraphPoint(300, 300));
        Assert.Contains(graph.Nodes, vm => vm.Node == added);
        rig.Session.UseUndoStack(rig.Class, graph.Services.UndoRedo);
        int edits = 0;
        graph.Services.UndoRedo.Do(new DelegateUndoableCommand("Edit", () => edits++, () => edits--));
        Assert.False(rig.Session.UndoStackFor(rig.Class).IsAtSavedState);

        await rig.RunAsync("undo");
        Assert.Equal(0, edits);
        await rig.RunAsync("redo");
        Assert.Equal(1, edits);

        await rig.RunAsync("save");
        Assert.True(rig.Session.UndoStackFor(rig.Class).IsAtSavedState);
        Assert.False(rig.Class.IsDirty);
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ClosingAPaneHidesItAndTheViewMenuShowsItAgain()
    {
        await using Rig rig = await CreateAsync();
        rig.Settle();
        Assert.True(rig.Adapter.IsPanelVisible(PanelContributions.InspectorId));
        rig.Adapter.HidePanel(PanelContributions.InspectorId);
        rig.Settle();
        Assert.False(rig.Adapter.IsPanelVisible(PanelContributions.InspectorId));

        MenuViewModel view = rig.Shell.MenuBar?.Menus.Single(menu => menu.Header == "View") ?? throw new InvalidOperationException("No menu bar.");
        CommandEntryViewModel entry = view.Items.Single(item => item.Id == ContributionIds.CommandPrefix + "showPanel.inspector");
        entry.RunCommand.Execute(null);
        rig.Settle();

        Assert.True(rig.Adapter.IsPanelVisible(PanelContributions.InspectorId));
        Assert.Contains(rig.Ui.Tree.Find(ShellRig.Panel(PanelContributions.InspectorId)), _ => true);
    }
}
