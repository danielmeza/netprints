using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <summary>
    /// Starts the NetPrints editor. A single argument is the path of a project (.csproj) to open.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        ILoggerFactory loggerFactory = CreateLoggerFactory();
        Logger.Sink = new AvaloniaLogSink(loggerFactory);

        // Must run before any Microsoft.Build-namespace type is loaded (project-system.md §4).
        bool msBuildAvailable = MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration)));

        var settings = new JsonFileSettingsStore(JsonFileSettingsStore.DefaultFilePath(), loggerFactory.CreateLogger<JsonFileSettingsStore>());
        using var extensions = new ExtensionHost(
            new ExtensionLoaderOptions(GetExtensionSearchDirectories(settings), [], [BuiltInExtension.InProcessEntry]), loggerFactory);
        HostChannelSelection channel = HostChannelSelector.Select(
            extensions.Current, GetEnvironment(), loggerFactory.CreateLogger(nameof(HostChannelSelector)));
        EditorApp.HostServices = new EditorHostServices(loggerFactory, extensions, settings, channel.Channel, channel.Error, msBuildAvailable);

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            channel.Channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            loggerFactory.Dispose();
        }
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
    /// (a <see cref="LogLevel"/> member name, case-insensitive) if it is set and valid.
    /// </summary>
    private static ILoggerFactory CreateLoggerFactory()
    {
        LogLevel minimumLevel = LogLevel.Information;
        string? levelName = Environment.GetEnvironmentVariable("NETPRINTS_LOG_LEVEL");
        if (levelName is { Length: > 0 } && Enum.TryParse(levelName, ignoreCase: true, out LogLevel overridden))
        {
            minimumLevel = overridden;
        }

        return LoggerFactory.Create(builder => builder.AddSimpleConsole().SetMinimumLevel(minimumLevel));
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
