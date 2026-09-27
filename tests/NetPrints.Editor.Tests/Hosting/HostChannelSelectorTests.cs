using System.Text;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

public class HostChannelSelectorTests
{
    private const string FactoryId = "test.channel";

    private static ExtensionRegistry RegistryWith(RecordingFactory factory)
    {
        var manifest = ExtensionManifest.Parse(
            new MemoryStream(Encoding.UTF8.GetBytes("""{ "id": "test.host", "name": "Host test", "version": "1.0.0", "netprintsApi": "1.0", "assembly": "x.dll" }""")),
            "/x/netprints-extension.json");
        var extension = new HostExtension(factory);
        var options = new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, extension)]);
        return new ExtensionLoader(options, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).Load(CancellationToken.None);
    }

    private static Dictionary<string, string> Environment(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);

    [Fact]
    public async Task UnsetGivesTheNullChannel()
    {
        await using ExtensionRegistry registry = RegistryWith(new RecordingFactory());
        var logger = new CollectingLogger();

        HostChannelSelection selection = HostChannelSelector.Select(registry, Environment(("NETPRINTS_HOST_OTHER", "1")), logger);

        Assert.Same(NullHostChannel.Instance, selection.Channel);
        Assert.Null(selection.Error);
        Assert.Empty(logger.Ids);
    }

    [Fact]
    public async Task ANamedFactoryCreatesTheChannelFromTheHostVariables()
    {
        var factory = new RecordingFactory();
        await using ExtensionRegistry registry = RegistryWith(factory);

        HostChannelSelection selection = HostChannelSelector.Select(
            registry, Environment(("NETPRINTS_HOST_CHANNEL", FactoryId), ("NETPRINTS_HOST_PIPE", "p1"), ("PATH", "/bin")), new CollectingLogger());

        Assert.Same(factory.Created, selection.Channel);
        Assert.Null(selection.Error);
        Assert.Equal("p1", factory.Context?.Settings["PIPE"]);
        Assert.Equal(FactoryId, factory.Context?.Settings["CHANNEL"]);
        Assert.DoesNotContain("PATH", factory.Context?.Settings.Keys ?? []);
    }

    [Fact]
    public async Task AnUnknownFactoryFallsBackToTheNullChannelWithAnErrorAndLog1021()
    {
        await using ExtensionRegistry registry = RegistryWith(new RecordingFactory());
        var logger = new CollectingLogger();

        HostChannelSelection selection = HostChannelSelector.Select(registry, Environment(("NETPRINTS_HOST_CHANNEL", "nope")), logger);

        Assert.Same(NullHostChannel.Instance, selection.Channel);
        Assert.Contains("nope", selection.Error);
        Assert.Equal([1021], logger.Ids);
    }

    [Fact]
    public async Task AFactoryThatThrowsFallsBackToTheNullChannelWithAnErrorAndLog1023()
    {
        await using ExtensionRegistry registry = RegistryWith(new RecordingFactory { Throw = true });
        var logger = new CollectingLogger();

        HostChannelSelection selection = HostChannelSelector.Select(registry, Environment(("NETPRINTS_HOST_CHANNEL", FactoryId)), logger);

        Assert.Same(NullHostChannel.Instance, selection.Channel);
        Assert.Contains("boom", selection.Error);
        Assert.Equal([1023], logger.Ids);
    }

    private sealed class RecordingFactory : IHostChannelFactory
    {
        public string Id => FactoryId;
        public bool Throw { get; init; }
        public HostLaunchContext? Context { get; private set; }
        public IHostChannel? Created { get; private set; }

        public IHostChannel Create(HostLaunchContext context)
        {
            Context = context;
            return Throw ? throw new InvalidOperationException("boom") : Created = InMemoryHostChannel.CreatePair(FactoryId).Editor;
        }
    }

    private sealed class HostExtension(IHostChannelFactory factory) : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddHostChannel(factory);
    }

    private sealed class CollectingLogger : ILogger
    {
        public List<int> Ids { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Ids.Add(eventId.Id);
    }
}
