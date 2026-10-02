using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Commands;

public sealed class DocumentCommandsTests
{
    private static readonly DocumentId A = DocumentId.Graph("A.cs", DocumentId.ClassGraphKey);
    private static readonly DocumentId B = DocumentId.Graph("B.cs", DocumentId.ClassGraphKey);
    private static readonly DocumentId C = DocumentId.Graph("C.cs", DocumentId.ClassGraphKey);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeShell shell = new();

    private void Open(params DocumentId[] ids)
    {
        foreach (DocumentId id in ids)
        {
            shell.OpenDocument(id);
        }

        shell.Calls.Clear();
    }

    private static Task Run(ICommandHandler handler, CommandContext context)
    {
        Assert.True(handler.CanExecute(context));
        return handler.ExecuteAsync(context, Token);
    }

    [Fact]
    public async Task CloseTabClosesTheActiveDocument()
    {
        Open(A, B);

        await Run(new CloseTabCommandHandler(), shell.Context());

        Assert.Equal([$"CloseDocument:{B}"], shell.Calls);
    }

    [Fact]
    public void TabCommandsNeedAnOpenDocumentAndCyclingNeedsTwo()
    {
        Assert.False(new CloseTabCommandHandler().CanExecute(shell.Context()));
        Assert.False(new FloatDocumentCommandHandler().CanExecute(shell.Context()));
        Assert.False(new DockDocumentCommandHandler().CanExecute(shell.Context()));
        Assert.False(new CycleTabCommandHandler(1).CanExecute(shell.Context()));
        Open(A);
        Assert.False(new CycleTabCommandHandler(1).CanExecute(shell.Context()));
        Assert.False(new CycleTabCommandHandler(-1).CanExecute(shell.Context()));
    }

    [Fact]
    public async Task NextAndPreviousTabCycleTheOpenDocumentsInTabOrderAndWrapAround()
    {
        Open(A, B, C);
        shell.ActivateDocument(C);
        shell.Calls.Clear();

        await Run(new CycleTabCommandHandler(1), shell.Context());
        await Run(new CycleTabCommandHandler(1), shell.Context());
        await Run(new CycleTabCommandHandler(-1), shell.Context());
        await Run(new CycleTabCommandHandler(-1), shell.Context());
        await Run(new CycleTabCommandHandler(-1), shell.Context());

        Assert.Equal([$"ActivateDocument:{A}", $"ActivateDocument:{B}", $"ActivateDocument:{A}", $"ActivateDocument:{C}", $"ActivateDocument:{B}"], shell.Calls);
    }

    [Fact]
    public async Task FloatAndDockActOnTheActiveDocumentOnlyWhenItCan()
    {
        Open(A);
        var float_ = new FloatDocumentCommandHandler();
        var dock = new DockDocumentCommandHandler();
        Assert.True(float_.CanExecute(shell.Context()));
        Assert.False(dock.CanExecute(shell.Context()));

        await Run(float_, shell.Context());

        Assert.Equal([$"FloatDocument:{A}"], shell.Calls);
        Assert.False(float_.CanExecute(shell.Context()));
        await Run(dock, shell.Context());
        Assert.Equal([$"FloatDocument:{A}", $"DockDocument:{A}"], shell.Calls);
    }

    [Theory]
    [InlineData("projectTree")]
    [InlineData("inspector")]
    [InlineData("errors")]
    [InlineData("output")]
    [InlineData("csharp")]
    public async Task ShowPanelShowsItsPanel(string name)
    {
        string panelId = ContributionIds.PanelPrefix + name;
        var handler = new ShowPanelCommandHandler(panelId);

        await Run(handler, shell.Context());

        Assert.Equal([$"ShowPanel:{panelId}"], shell.Calls);
        Assert.True(shell.IsPanelVisible(panelId));
    }

    [Fact]
    public async Task EveryPanelHasAShowCommandThatShowsIt()
    {
        var registry = new ContributionRegistry(new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);

        foreach (PanelDescriptor panel in registry.Panels)
        {
            string name = panel.Id[ContributionIds.PanelPrefix.Length..];
            CommandDescriptor command = Assert.Single(registry.Commands, candidate => candidate.Id == ContributionIds.CommandPrefix + "showPanel." + name);
            await Run(command.Handler, shell.Context());
        }

        Assert.Equal(registry.Panels.Select(panel => $"ShowPanel:{panel.Id}"), shell.Calls);
    }

    [Fact]
    public async Task ResetLayoutRestoresTheDefaultLayout()
    {
        await Run(new ResetLayoutCommandHandler(), shell.Context());

        Assert.Equal(["ResetLayout"], shell.Calls);
    }
}
