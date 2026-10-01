using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// The diagnostic files of an editor that is gone and of one that is still running (contracts/ci.md §3),
/// over a fake editor: no display or real editor is needed.
/// </summary>
public sealed class DiagnosticPartsTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string directory = Directory.CreateTempSubdirectory("netprints-parts-").FullName;

    [Fact]
    public async Task AnExitedEditorLeavesItsExitCodeInsteadOfAUiTree()
    {
        var editor = new FakeEditor { Exited = true, Code = 137 };

        await WriteAsync(editor, "process.txt", "ui-tree.json", "run-state.json");

        Assert.Equal("exited 137", await File.ReadAllTextAsync(Path.Combine(directory, "process.txt"), Token));
        using var tree = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "ui-tree.json"), Token));
        Assert.Equal(137, tree.RootElement.GetProperty("editorExitCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, tree.RootElement.GetProperty("focused").ValueKind);
        Assert.Equal(0, tree.RootElement.GetProperty("windows").GetArrayLength());
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "run-state.json"), Token));
        Assert.Equal(137, state.RootElement.GetProperty("editorExitCode").GetInt32());
        Assert.Equal(0, editor.Connections);
    }

    [Fact]
    public async Task ARunningEditorIsReadThroughConnectionsOfTheirOwn()
    {
        string pipe = Path.Combine(Path.GetTempPath(), "netprints-parts-" + Guid.NewGuid().ToString("N"));
        var editor = new FakeEditor { Pipe = pipe };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var serving = ServeAsync(pipe, 2, stop.Token);

        await WriteAsync(editor, "process.txt", "ui-tree.json", "run-state.json");
        await stop.CancelAsync();
        await serving;

        Assert.Equal("running", await File.ReadAllTextAsync(Path.Combine(directory, "process.txt"), Token));
        using var tree = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "ui-tree.json"), Token));
        Assert.False(tree.RootElement.TryGetProperty("editorExitCode", out _));
        Assert.Equal(0, tree.RootElement.GetProperty("windows").GetArrayLength());
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "run-state.json"), Token));
        Assert.True(state.RootElement.TryGetProperty("phase", out _) || state.RootElement.TryGetProperty("Phase", out _));
        Assert.Equal(2, editor.Connections);
    }

    private async Task WriteAsync(FakeEditor editor, params string[] files)
    {
        var parts = DiagnosticParts.Create("editor exited", "SomeTests", new StepTimer("test"), new FailureMoment("step", TimeSpan.Zero, []),
            ":99", editor, _ => Task.FromResult(Array.Empty<byte>()));
        foreach (var part in parts.Where(p => files.Contains(p.FileName)))
        {
            await part.WriteAsync(Path.Combine(directory, part.FileName), Token);
        }
    }

    private static async Task ServeAsync(string pipe, int connections, CancellationToken token)
    {
        for (int i = 0; i < connections; i++)
        {
            await using var server = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(token);
            using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
            await using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            var request = JsonSerializer.Deserialize(await reader.ReadLineAsync(token) ?? "", AutomationJsonContext.Default.AutomationRequest);
            var response = request?.Op == "tree"
                ? new AutomationResponse(true) { Elements = [] }
                : new AutomationResponse(true) { RunState = new RunStateSnapshot(default, null, [], []) };
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, AutomationJsonContext.Default.AutomationResponse));
            await reader.ReadLineAsync(token);
        }
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private sealed class FakeEditor : ICapturedEditor
    {
        public bool Exited { get; init; }

        public int Code { get; init; }

        public string Pipe { get; init; } = "";

        public int Connections { get; private set; }

        public int ProcessId => 1;

        public bool HasExited => Exited;

        public int ExitCode => Code;

        public string Output => "";

        public string Errors => "";

        public Task<AutomationClient> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            Connections++;
            return AutomationClient.ConnectAsync(Pipe, timeout, cancellationToken);
        }
    }
}
