using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>SC-004: the real test extension's host channel is selected by id and its messages drive the editor.</summary>
public sealed class TestExtensionHostChannelTests : IDisposable
{
    private const string TestHostChannelId = "test";

    private static readonly JsonElement NoPayload = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly ExtensionHost extensions = TestExtensionFolder.CreateHost();
    private readonly string csproj = TestPaths.CopyHelloWorldSample();

    public void Dispose()
    {
        extensions.Dispose();
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
        var vm = new MainEditorVM(editor.Context);
        try
        {
            await vm.LoadProjectAsync(csproj);

            int reloads = 0;
            var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            editor.Reflection.Reloaded += (_, _) =>
            {
                Interlocked.Increment(ref reloads);
                reloaded.TrySetResult();
            };

            await hostEnd.SendAsync(new HostMessage(HostMessageTypes.TypesChanged, NoPayload), TestContext.Current.CancellationToken);
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.Equal(1, Volatile.Read(ref reloads));
        }
        finally
        {
            vm.OnMainWindowClosed();
            await selection.Channel.DisposeAsync();
        }
    }
}
