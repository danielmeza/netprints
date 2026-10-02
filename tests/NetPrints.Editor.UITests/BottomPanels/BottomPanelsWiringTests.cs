using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Output;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Projects;
using NetPrints.Testing.Ui.Driving;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.UITests.BottomPanels;

/// <summary>The Errors, Output and C# panels render in the shell and follow the project, the run and the active document (T038).</summary>
public class BottomPanelsWiringTests
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

        public Rig(HeadlessApp app, SampleCopy sample, ProjectSessionViewModel session)
        {
            this.app = app;
            this.sample = sample;
            Session = session;
            Class = session.Project.Classes.Single();
            Method = Class.Methods.First();
            editor = new ClassEditorViewModel(Class, app.Composition.Context);
            editor.OpenGraph(Method);
            MethodDocument = DocumentId.Graph(session.ClassPathOf(Class), DocumentId.MethodKeyPrefix + Method.Id);
            ClassDocument = DocumentId.Graph(session.ClassPathOf(Class), DocumentId.ClassGraphKey);
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
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

        public EditorContext Context => app.Composition.Context;

        public OutputPanelViewModel Output => Assert.IsType<OutputPanelViewModel>(Shell.FindPanel(PanelContributions.OutputId)?.Content);

        public NodeGraphViewModel Graph => editor.OpenedGraph ?? throw new InvalidOperationException("No graph.");

        public IReadOnlyList<AutomationElement> Find(string automationId) => [.. Ui.Tree.Find(new AutomationQuery(automationId))];

        public UiTarget Target(string automationId) =>
            Ui.Driver.At(Find(automationId).FirstOrDefault() ?? throw new InvalidOperationException($"No element {automationId}."), 0.5, 0.5);

        public async Task WaitAsync(Func<bool> condition)
        {
            for (int i = 0; i < 400 && !condition(); i++)
            {
                HeadlessDriver.Pump();
                await Task.Delay(50, Token);
            }

            HeadlessDriver.Pump();
        }

        public async ValueTask DisposeAsync()
        {
            Ui.Dispose();
            Shell.Dispose();
            editor.Dispose();
            await app.DisposeAsync();
            sample.Dispose();
        }

        private DocumentViewModel CreateDocument(DocumentId id) => id == MethodDocument
            ? new GraphDocumentViewModel(id, Graph, Class, Session)
            : new TestDocumentViewModel(id, id.GraphKey ?? id.ToString());
    }

    private static async Task<Rig> CreateAsync()
    {
        var app = HeadlessApp.Start();
        var sample = new SampleCopy();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        return new Rig(app, sample, Assert.IsType<ProjectSessionViewModel>(app.ViewModel.Session));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingAnErrorRowOpensItsGraphTabAndSelectsTheNode()
    {
        await using Rig rig = await CreateAsync();
        string nodeId = rig.Method.Nodes.First().Id;
        rig.Adapter.OpenDocument(rig.ClassDocument);
        rig.Session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(
            [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1503", "boom", rig.Class.FullName, GraphKeys.For(rig.Method), nodeId, null, null)]);
        HeadlessDriver.Pump();
        Assert.Single(rig.Find(AutomationIds.ErrorsRow));

        await rig.Ui.Driver.ClickAsync(rig.Target(AutomationIds.ErrorsRow), UiButton.Left, 2, Token);

        Assert.Contains(rig.MethodDocument, rig.Adapter.OpenDocuments);
        Assert.Equal(nodeId, rig.Graph.SelectedNodes.Single().Node.Id);
        Assert.Empty(rig.Faults);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOutputPanelListsTheBuildAndTheProgramsOutput()
    {
        await using Rig rig = await CreateAsync();
        rig.Session.Project.CompilationMessage = "Build succeeded";
        rig.Adapter.ShowPanel(PanelContributions.OutputId);

        rig.Context.RunState.BuildStarted();
        rig.Context.RunState.BuildFinished();
        rig.Context.Processes.Start(new ProcessStartRequest("dotnet", ["--version"], Path.GetTempPath()), Token);
        await rig.WaitAsync(() => rig.Context.RunState.Snapshot().Phase == RunPhase.Exited);
        string version = Assert.Single(rig.Context.RunState.Snapshot().Stdout);
        await rig.WaitAsync(() => rig.Find(AutomationIds.OutputLine).Count >= 3);

        Assert.Equal(["Build started.", "Build succeeded", version], rig.Find(AutomationIds.OutputLine).Select(line => line.Text));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOutputPanelFollowsTheNewestLine()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.ShowPanel(PanelContributions.OutputId);

        rig.Context.Processes.Start(new ProcessStartRequest("dotnet", ["--info"], Path.GetTempPath()), Token);
        await rig.WaitAsync(() => rig.Context.RunState.Snapshot().Phase == RunPhase.Exited);
        int expected = rig.Context.RunState.Snapshot().Stdout.Count;
        await rig.WaitAsync(() => rig.Output.Lines.Count == expected);
        HeadlessDriver.Pump();

        ListBox list = rig.Window.GetVisualDescendants().OfType<ListBox>().Single(box => AutomationProperties.GetAutomationId(box) == AutomationIds.OutputLines);
        ScrollViewer scroll = Assert.IsType<ScrollViewer>(list.Scroll);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "The output must be longer than the panel for the check to mean anything.");
        Assert.True(scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 1, $"The list is at {scroll.Offset.Y} of {scroll.Extent.Height}.");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheCSharpPanelShowsTheGeneratedCodeOfTheActiveDocumentsClass()
    {
        await using Rig rig = await CreateAsync();
        rig.Adapter.ShowPanel(PanelContributions.CSharpId);
        HeadlessDriver.Pump();
        Assert.NotEmpty(rig.Find(AutomationIds.CSharpEmpty));

        rig.Adapter.OpenDocument(rig.ClassDocument);
        rig.Context.CodeAnalysis.RequestAnalysis(rig.Session.Project);
        var panel = Assert.IsType<NetPrints.Editor.CodeView.CSharpPanelViewModel>(rig.Shell.FindPanel(PanelContributions.CSharpId)?.Content);
        await rig.WaitAsync(() => panel.Current?.Code.Contains("class", StringComparison.Ordinal) == true);

        Assert.Contains("class", panel.Current?.Code, StringComparison.Ordinal);
        Assert.Empty(rig.Find(AutomationIds.CSharpEmpty));
        Assert.NotEmpty(rig.Find(AutomationIds.CSharpCode));
    }
}
