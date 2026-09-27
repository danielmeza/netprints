using System;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using NetPrints.Editor;
using NetPrints.Editor.Hosting;
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
        EditorApp.HostServices = new EditorHostServices(loggerFactory, msBuildAvailable);

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            loggerFactory.Dispose();
        }
    }

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
