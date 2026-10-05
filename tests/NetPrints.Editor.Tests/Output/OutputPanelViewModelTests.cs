using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Output;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Projects;

namespace NetPrints.Editor.Tests.Output;

public sealed class OutputPanelViewModelTests : IAsyncDisposable
{
    private static readonly ProcessStartRequest Request = new("dotnet", ["run"], "/work");

    private readonly QueueDispatcher queue = new();
    private readonly ShellPanelRig rig;

    public OutputPanelViewModelTests() => rig = new ShellPanelRig(context => context with { Dispatcher = queue });

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private IReadOnlyList<string> Texts
    {
        get
        {
            queue.Flush();
            return [.. rig.Output.Lines.Select(line => line.Text)];
        }
    }

    private void Build(ProjectSessionViewModel session, bool succeeded, params CodeDiagnostic[] diagnostics)
    {
        rig.Context.RunState.BuildStarted();
        session.Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(diagnostics);
        session.Project.CompilationMessage = succeeded ? "Build succeeded" : "Build failed";
        rig.Context.RunState.BuildFinished();
    }

    [Fact]
    public async Task TheBuildOutputComesFirstThenTheProgramsStdoutAndStderr()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        var warning = new CodeDiagnostic(CodeDiagnosticSeverity.Warning, "CS0168", "unused", "N.C", null, null, "C.cs", null);

        Build(session, succeeded: true, warning);
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        rig.Processes.RaiseLine(ProcessStream.Output, "hello");
        rig.Processes.RaiseLine(ProcessStream.Error, "oops");

        Assert.Equal(["Build started.", CodeDiagnosticFormat.ToCanonicalLine(warning), "Build succeeded", "hello", "oops"], Texts);
        Assert.Equal([OutputLineKind.Build, OutputLineKind.Build, OutputLineKind.Build, OutputLineKind.Output, OutputLineKind.Error], rig.Output.Lines.Select(line => line.Kind));
        Assert.True(rig.Output.Lines[^1].IsError);
    }

    [Fact]
    public async Task EachCompileClearsWhatWasShown()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        Build(session, succeeded: true);
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        rig.Processes.RaiseLine(ProcessStream.Output, "hello");
        Assert.Contains("hello", Texts);

        rig.Context.RunState.BuildStarted();

        Assert.Equal(["Build started."], Texts);
    }

    [Fact]
    public async Task EachRunClearsTheProgramOutputOfTheRunBeforeButKeepsTheBuild()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        Build(session, succeeded: true);
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        rig.Processes.RaiseLine(ProcessStream.Output, "first run");
        rig.Processes.RaiseExited(0);
        Assert.Contains("first run", Texts);

        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        rig.Processes.RaiseLine(ProcessStream.Output, "second run");

        Assert.Equal(["Build started.", "Build succeeded", "second run"], Texts);
    }

    [Fact]
    public async Task RunBringsOutputForwardOnceNotOnEveryLine()
    {
        await rig.OpenSessionAsync();
        string show = $"ShowPanel:{PanelContributions.OutputId}";

        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        queue.Flush();
        Assert.Equal(1, rig.Api.Calls.Count(call => call == show));

        rig.Api.Calls.Clear();
        rig.Processes.RaiseLine(ProcessStream.Output, "still printing");
        rig.Processes.RaiseLine(ProcessStream.Error, "and again");
        queue.Flush();
        Assert.DoesNotContain(show, rig.Api.Calls);
    }

    [Fact]
    public async Task LinesFromAnotherThreadAreAddedOnlyWhenTheDispatcherRunsThem()
    {
        await rig.OpenSessionAsync();
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        queue.Flush();

        await Task.Run(() => rig.Processes.RaiseLine(ProcessStream.Output, "from a worker"), TestContext.Current.CancellationToken);

        Assert.Empty(rig.Output.Lines);
        Assert.Equal(["from a worker"], Texts);
    }

    [Fact]
    public async Task OnlyTheNewestLinesAreKept()
    {
        await rig.OpenSessionAsync();
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);

        for (int i = 0; i < OutputPanelViewModel.MaxLines + 5; i++)
        {
            rig.Processes.RaiseLine(ProcessStream.Output, "line " + i);
        }

        IReadOnlyList<string> texts = Texts;
        Assert.Equal(OutputPanelViewModel.MaxLines, texts.Count);
        Assert.Equal("line " + (OutputPanelViewModel.MaxLines + 4), texts[^1]);
        Assert.Equal("line 5", texts[0]);
    }

    [Fact]
    public async Task AfterTheShellIsDisposedTheTrackerNoLongerReachesThePanel()
    {
        await rig.OpenSessionAsync();
        rig.Processes.Start(Request, TestContext.Current.CancellationToken);
        OutputPanelViewModel output = rig.Output;
        rig.Shell.Dispose();

        rig.Processes.RaiseLine(ProcessStream.Output, "late");
        queue.Flush();

        Assert.Empty(output.Lines);
    }
}
