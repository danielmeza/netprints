using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>A cancelled request must not leave its reply in the pipe for the next call to read (Review A R1).</summary>
public sealed class AutomationClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACallAfterACancelledRequestNeverReadsTheCancelledRequestsReply()
    {
        string pipe = Path.Combine(Path.GetTempPath(), "netprints-client-" + Guid.NewGuid().ToString("N"));
        var firstRead = new TaskCompletionSource();
        var releaseFirstReply = new TaskCompletionSource();
        var server = new NamedPipeServerStream(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var serving = ServeAsync(server, firstRead, releaseFirstReply);
        await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var first = client.DumpAsync(cancel.Token);
        await firstRead.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        releaseFirstReply.SetResult();

        var second = await Record.ExceptionAsync(() => client.DumpAsync(Token));

        Assert.IsType<IOException>(second);
        await serving.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    private static async Task ServeAsync(NamedPipeServerStream server, TaskCompletionSource firstRead, TaskCompletionSource releaseFirstReply)
    {
        await using (server)
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
            await using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
            await reader.ReadLineAsync();
            firstRead.SetResult();
            await releaseFirstReply.Task;
            await writer.WriteLineAsync(JsonSerializer.Serialize(new AutomationResponse(true) { Text = "reply 1" }, AutomationJsonContext.Default.AutomationResponse));
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
