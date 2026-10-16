using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>The editor composes from the generated registration and the host instances (research R4 step 3; US1 scenarios 3 and 5).</summary>
public sealed class CompositionTests : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> messages = [];
    private readonly ExtensionHost extensions = new(new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry]), NullLoggerFactory.Instance);

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return extensions.DisposeAsync();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnInvalidSettingsSectionIsReportedWhenTheEditorComposes()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "settings.json"), """{"extensions":{"netprints.editor":{"theme":"purple"}},"netprints":{"language":"not a culture"}}""");

        using TestComposition composition = Compose(new RecordingDialogs(), new QueuedFilePicker(), new CapturingProcessLauncher());

        Assert.Contains(messages, message => message.Contains("netprints.editor", StringComparison.Ordinal) && message.Contains("theme", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(messages, message => message.Contains("language", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(composition.Context);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheDoublesReplaceTheGeneratedRegistrations()
    {
        var dialogs = new RecordingDialogs();
        var picker = new QueuedFilePicker();
        var processes = new CapturingProcessLauncher();

        using TestComposition composition = Compose(dialogs, picker, processes);

        IServiceProvider services = composition.Services;
        Assert.Same(dialogs, services.GetRequiredService<IEditorDialogs>());
        Assert.Same(picker, services.GetRequiredService<IFilePickerService>());
        Assert.Same(processes, services.GetRequiredService<IProcessLauncher>());
        Assert.Same(dialogs, composition.Context.Dialogs);
        Assert.Same(processes, composition.Context.Processes);
        Assert.IsType<AvaloniaUiDispatcher>(services.GetRequiredService<IUiDispatcher>());
        Assert.Same(services.GetRequiredService<EditorContext>(), composition.Context);
    }

    private TestComposition Compose(RecordingDialogs dialogs, QueuedFilePicker picker, CapturingProcessLauncher processes)
    {
        var settings = new JsonFileSettingsStore(Path.Combine(directory, "settings.json"), new ListLogger<JsonFileSettingsStore>(messages));
        return new TestComposition(new EditorHostServices(NullLoggerFactory.Instance, extensions, settings, NullHostChannel.Instance, HostChannelError: null,
            MsBuildAvailable: true, DisposeOwnedResources: () => ValueTask.CompletedTask), dialogs, picker, processes);
    }

    private sealed class ListLogger<T>(List<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }
}
