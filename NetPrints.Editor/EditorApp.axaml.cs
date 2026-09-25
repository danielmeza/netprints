using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace NetPrints.Editor;

/// <summary>
/// The NetPrints editor application (dark Fluent theme, emerald accent, Nodify and Material icons).
/// Hosted by NetPrints.Desktop and by the headless UI tests.
/// </summary>
public partial class EditorApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var composition = new EditorComposition();
            var window = composition.CreateMainWindow();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // A single command-line argument is a project to open (FR-016, PAR-05).
            _ = composition.MainEditor!.OpenStartupProjectAsync(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
