using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>SC-004: the real test extension's host channel is selected by id and its messages drive the editor.</summary>
public sealed class TestExtensionHostChannelTests : IAsyncLifetime
{
    private const string TestHostChannelId = "test";

    private static readonly JsonElement NoPayload = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly ExtensionHost extensions = TestExtensionFolder.CreateHost();
    private readonly string csproj = TestPaths.CopyHelloWorldSample();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await extensions.DisposeAsync();
        TestPaths.TryDelete(csproj);
    }

    [Fact(Timeout = 120000)]
    public async Task ATypesChangedMessageFromTheTestExtensionsChannelReloadsReflectionExactlyOnce()
    {
        var environment = new Dictionary<string, string> { [HostChannelSelector.ChannelVariable] = TestHostChannelId };
        HostChannelSelection selection = HostChannelSelector.Select(extensions.Current, environment, NullLogger.Instance);
        Assert.Null(selection.Error);
        Assert.NotSame(NullHostChannel.Instance, selection.Channel);

        IHostChannelFactory factory = extensions.Current.FindHostChannel(TestHostChannelId) ?? throw new InvalidOperationException("Channel factory missing.");
        IHostChannel hostEnd = factory.GetType().GetProperty("LastHost")?.GetValue(factory) as IHostChannel
            ?? throw new InvalidOperationException("The factory did not keep its host end.");

        var editor = TestEditor.Create(TestEditor.CreateReflectionHost, hostChannel: selection.Channel);
        var rig = new ProjectRig(editor.Context);
        try
        {
            await rig.LoadProjectAsync(csproj);

            // Settle the reload that opening the project itself triggers (ProjectLoader.SetProject)
            // before sending the channel message below: otherwise the two reloads race, and the count
            // observed afterward depends on which one the ReflectionHost version guard lets publish last
            // (R2-22). Loaded is the host's own signal for "a reload has published"; no sleep involved.
            await editor.Reflection.Loaded.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            int reloads = 0;
            var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            editor.Reflection.Reloaded += (_, _) =>
            {
                Interlocked.Increment(ref reloads);
                reloaded.TrySetResult();
            };

            await hostEnd.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

            // R2-22: no further reload can be in flight to arrive late and inflate this count. The
            // project-load reload already settled above, so the message is the only other trigger in
            // this test, and the version guard means a stale reload can only be dropped, never publish
            // a second time.
            Assert.Equal(1, Volatile.Read(ref reloads));
        }
        finally
        {
            rig.Dispose();
            await selection.Channel.DisposeAsync();
        }
    }
}
