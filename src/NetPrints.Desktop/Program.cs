using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using NetPrints.Editor;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Workspace;

namespace NetPrints.Desktop;

internal static class Program
{
    /// <summary>The first argument that switches <see cref="Main"/> into <see cref="ProjectCheck"/> instead of starting the editor.</summary>
    private const string CheckProjectArgument = "--check-project";

    /// <summary>
    /// Starts the NetPrints editor. A single argument is the path of a project (.csproj) to open.
    /// <c>--check-project &lt;path.csproj&gt; [--run]</c> instead runs <see cref="ProjectCheck"/> and
    /// exits without starting Avalonia (release contract §5). <see cref="Main"/> must stay synchronous
    /// for <see cref="STAThreadAttribute"/> to land on the real entry point (an <c>async Task&lt;int&gt;
    /// Main</c> compiles to a synthesized wrapper entry point that does not carry it, so the thread is
    /// never actually STA): <see cref="RunCheckProject"/> pumps that one await on this thread instead
    /// of blocking on the task, so there is no sync-over-async.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        var (factory, explicitLogLevel) = CreateLoggerFactory();
        using ILoggerFactory loggerFactory = factory;
        Logger.Sink = new AvaloniaLogSink(loggerFactory, explicitLogLevel);

        // Must run before any Microsoft.Build-namespace type is loaded (project-system.md §4).
        bool msBuildAvailable = MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration)));

        if (args is [CheckProjectArgument, ..])
        {
            return RunCheckProject(args.ElementAtOrDefault(1), args.Contains("--run", StringComparer.Ordinal), loggerFactory, msBuildAvailable);
        }

        var settings = new JsonFileSettingsStore(JsonFileSettingsStore.DefaultFilePath(), loggerFactory.CreateLogger<JsonFileSettingsStore>());
        var extensions = new ExtensionHost(
            new ExtensionLoaderOptions(GetExtensionSearchDirectories(settings), [], [BuiltInExtension.InProcessEntry]), loggerFactory);
        HostChannelSelection channel = HostChannelSelector.Select(
            extensions.Current, GetEnvironment(), loggerFactory.CreateLogger(nameof(HostChannelSelector)));
        EditorApp.HostServices = new EditorHostServices(loggerFactory, extensions, settings, channel.Channel, channel.Error, msBuildAvailable,
            async () =>
            {
                await extensions.DisposeAsync().ConfigureAwait(false);
                await channel.Channel.DisposeAsync().ConfigureAwait(false);
            });

        // EditorApp's ShutdownRequested handler awaits HostServices.DisposeAsync() before the app exits.
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Runs <see cref="ProjectCheck.RunAsync(string?, bool, TextWriter, ILoggerFactory, bool, CancellationToken)"/>,
    /// reusing this process's already-registered MSBuild instance and already-built logger factory, to
    /// completion on the calling (STA) thread by pumping a private <see
    /// cref="SingleThreadSynchronizationContext"/>, instead of blocking on the resulting task
    /// (<c>--check-project</c> never touches Avalonia, so no display is needed either way).
    /// </summary>
    private static int RunCheckProject(string? projectPath, bool run, ILoggerFactory loggerFactory, bool msBuildAvailable)
    {
        var pump = new SingleThreadSynchronizationContext();
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(pump);
        try
        {
            int exitCode = ProjectCheck.ExitBadArguments;
            Exception? failure = null;

            async Task RunAsync()
            {
                try
                {
                    exitCode = await ProjectCheck.RunAsync(projectPath, run, Console.Out, loggerFactory, msBuildAvailable, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    pump.Complete();
                }
            }

            Task execution = RunAsync();
            pump.RunOnCurrentThread();
            Debug.Assert(execution.IsCompleted, "RunOnCurrentThread only returns once RunAsync's finally has run.");

            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return exitCode;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>
    /// A minimal single-threaded dispatcher (the classic "AsyncPump"): runs an <see langword="async"/>
    /// method to completion on the calling thread by draining its posted continuations one at a time,
    /// so <see cref="RunCheckProject"/> never has to block on the resulting <see cref="Task"/>.
    /// </summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();

        public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException($"{nameof(SingleThreadSynchronizationContext)} does not support synchronous {nameof(Send)}.");

        /// <summary>Runs every posted continuation on this thread until <see cref="Complete"/> is called.</summary>
        public void RunOnCurrentThread()
        {
            foreach ((SendOrPostCallback callback, object? state) in queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        /// <summary>Lets <see cref="RunOnCurrentThread"/> return once every already-posted continuation has run.</summary>
        public void Complete() => queue.CompleteAdding();
    }

    /// <summary>
    /// The directories searched for extensions: the settings' <c>netprints.extensionPaths</c>, then the
    /// <c>NETPRINTS_EXTENSION_PATH</c> entries (separated like <c>PATH</c>).
    /// </summary>
    private static string[] GetExtensionSearchDirectories(ISettingsStore settings)
    {
        string[] fromEnvironment = Environment.GetEnvironmentVariable("NETPRINTS_EXTENSION_PATH")
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        return [.. settings.Get(NetPrintsSettings.Descriptor).ExtensionPaths, .. fromEnvironment];
    }

    private static Dictionary<string, string> GetEnvironment() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Select(entry => (Key: entry.Key as string, Value: entry.Value as string))
            .Where(entry => entry.Key is not null && entry.Value is not null)
            .ToDictionary(entry => entry.Key ?? string.Empty, entry => entry.Value ?? string.Empty, StringComparer.Ordinal);

    /// <summary>
    /// Builds the process-wide <see cref="ILoggerFactory"/>: a simple console logger at
    /// <see cref="LogLevel.Information"/>, or the level named by <c>NETPRINTS_LOG_LEVEL</c>
    /// (a <see cref="LogLevel"/> member name, case-insensitive) if it is set and valid. Also returns
    /// that explicit level (or <see langword="null"/> if the variable was unset/invalid), so
    /// <see cref="AvaloniaLogSink"/> knows whether to floor its own chatty areas to
    /// <see cref="LogLevel.Warning"/>.
    /// </summary>
    private static (ILoggerFactory Factory, LogLevel? ExplicitMinimumLevel) CreateLoggerFactory()
    {
        LogLevel minimumLevel = LogLevel.Information;
        string? levelName = Environment.GetEnvironmentVariable("NETPRINTS_LOG_LEVEL");
        LogLevel? explicitLevel = null;
        if (levelName is { Length: > 0 } && Enum.TryParse(levelName, ignoreCase: true, out LogLevel overridden))
        {
            minimumLevel = overridden;
            explicitLevel = overridden;
        }

        ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddSimpleConsole().SetMinimumLevel(minimumLevel));
        return (factory, explicitLevel);
    }

    /// <summary>
    /// Avalonia configuration; also used by the visual designer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EditorApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = EditorApp.DefaultFontFamily });
}
