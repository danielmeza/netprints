using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// exits without starting Avalonia (release contract §5): no <see langword="await"/> runs before
    /// Avalonia's own synchronous startup call below, so the thread stays STA for that path.
    /// </summary>
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        var (factory, explicitLogLevel) = CreateLoggerFactory();
        using ILoggerFactory loggerFactory = factory;
        Logger.Sink = new AvaloniaLogSink(loggerFactory, explicitLogLevel);

        // Must run before any Microsoft.Build-namespace type is loaded (project-system.md §4).
        bool msBuildAvailable = MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration)));

        if (args is [CheckProjectArgument, ..])
        {
            return await ProjectCheck.RunAsync(args.ElementAtOrDefault(1), args.Contains("--run", StringComparer.Ordinal), Console.Out, CancellationToken.None);
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
