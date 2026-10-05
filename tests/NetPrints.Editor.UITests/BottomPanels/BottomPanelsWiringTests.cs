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

    private sealed class Rig(ShellApp app) : IAsyncDisposable
    {
        public ProjectSessionViewModel Session => app.Session;

        public ClassGraph Class => Session.Project.Classes.Single();

        public MethodGraph Method => Class.Methods.First();

        public DocumentId MethodDocument => DocumentId.Graph(Session.ClassPathOf(Class), DocumentId.MethodKeyPrefix + Method.Id);

        public DocumentId ClassDocument => DocumentId.Graph(Session.ClassPathOf(Class), DocumentId.ClassGraphKey);

        public RecordingDialogs Dialogs => app.Dialogs;

        public ShellViewModel Shell => app.Shell;

        public IShell Api => app.Api;

        public HeadlessUi Ui => app.Ui;

        public ShellWindow Window => app.Window;

        public EditorContext Context => app.Composition.Context;

        public OutputPanelViewModel Output => Assert.IsType<OutputPanelViewModel>(Shell.FindPanel(PanelContributions.OutputId)?.Content);

        public NodeGraphViewModel Graph => Assert.IsType<GraphDocumentViewModel>(Shell.FindDocument(MethodDocument)).Graph;

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

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }

    private static async Task<Rig> CreateAsync()
    {
        var app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        return new Rig(app);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DoubleClickingAnErrorRowOpensItsGraphTabAndSelectsTheNode()
    {
        await using Rig rig = await CreateAsync();
        string nodeId = rig.Method.Nodes.First().Id;
        rig.Api.OpenDocument(rig.ClassDocument);
        rig.Session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(
            [new CodeDiagnostic(CodeDiagnosticSeverity.Error, "CS1503", "boom", rig.Class.FullName, GraphKeys.For(rig.Method), nodeId, null, null)]);
        HeadlessDriver.Pump();
        Assert.Single(rig.Find(AutomationIds.ErrorsRow));

        await rig.Ui.Driver.ClickAsync(rig.Target(AutomationIds.ErrorsRow), UiButton.Left, 2, Token);

        Assert.Contains(rig.MethodDocument, rig.Api.OpenDocuments);
        Assert.Equal(nodeId, rig.Graph.SelectedNodes.Single().Node.Id);
        Assert.Empty(rig.Dialogs.Errors);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOutputPanelListsTheBuildAndTheProgramsOutput()
    {
        await using Rig rig = await CreateAsync();
        rig.Session.Project.CompilationMessage = "Build succeeded";
        rig.Api.ShowPanel(PanelContributions.OutputId);

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
        rig.Api.ShowPanel(PanelContributions.OutputId);

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
        rig.Api.ShowPanel(PanelContributions.CSharpId);
        HeadlessDriver.Pump();
        Assert.NotEmpty(rig.Find(AutomationIds.CSharpEmpty));

        rig.Api.OpenDocument(rig.ClassDocument);
        rig.Context.CodeAnalysis.RequestAnalysis(rig.Session.Project);
        var panel = Assert.IsType<NetPrints.Editor.CodeView.CSharpPanelViewModel>(rig.Shell.FindPanel(PanelContributions.CSharpId)?.Content);
        await rig.WaitAsync(() => panel.Current?.Code.Contains("class Program", StringComparison.Ordinal) == true);

        Assert.Contains("class Program", panel.Current?.Code, StringComparison.Ordinal);
        Assert.Empty(rig.Find(AutomationIds.CSharpEmpty));
        Assert.NotEmpty(rig.Find(AutomationIds.CSharpCode));
    }
}
