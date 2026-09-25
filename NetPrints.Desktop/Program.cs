using System;
using Avalonia;
using Avalonia.Media;
using NetPrints.Editor;

namespace NetPrints.Desktop;

internal static class Program
{
    /// <summary>
    /// Starts the NetPrints editor. A single argument is the path of a project (.netpp) to open.
    /// </summary>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Avalonia configuration; also used by the visual designer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EditorApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = EditorApp.DefaultFontFamily })
            .LogToTrace();
}
