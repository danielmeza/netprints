using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Main;

namespace NetPrints.Editor;

/// <summary>
/// The NetPrints editor application (dark Fluent theme, emerald accent, Nodify and Material icons).
/// Hosted by NetPrints.Desktop and by the headless UI tests.
/// </summary>
public partial class EditorApp : Application
{
    /// <summary>The embedded Inter font (Avalonia.Fonts.Inter), used as the default font family.</summary>
    public const string DefaultFontFamily = "avares://Avalonia.Fonts.Inter/Assets#Inter";

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Removes all transitions (theme animations), so screenshots and pixel checks are taken in a
    /// settled state. Used by the UI tests and by the desktop host in automation mode.
    /// </summary>
    public void DisableTransitions() =>
        Styles.Add(new Style(x => x.Is<Control>())
        {
            Setters = { new Setter(Animatable.TransitionsProperty, null) },
        });

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var composition = new EditorComposition();
            var exceptionHandler = composition.InstallUnhandledExceptionHandler();
            desktop.Exit += (_, _) => exceptionHandler.Dispose();
            var window = composition.CreateMainWindow();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Automation mode (E2E tests only): settled screenshots and a read-only agent.
            if (AutomationAgent.IsEnabled(out string pipeName))
            {
                DisableTransitions();
                var tree = new AutomationTree();
                tree.Track(window);
                string? startupProject = desktop.Args is [var single] ? single : null;
                AutomationAgent agent;
                try
                {
                    agent = new AutomationAgent(pipeName, tree, () => new AutomationStatus(
                        window.IsVisible,
                        composition.MainEditor!.Project is not null && !composition.MainEditor.IsBusy,
                        composition.Context.Reflection.IsLoaded,
                        startupProject,
                        Environment.ProcessId));
                }
                catch (Exception e)
                {
                    // NETPRINTS_AUTOMATION was asked for and could not be honored: fail loudly and
                    // fast (stderr, non-zero exit) instead of leaving a caller waiting on a pipe
                    // that will never accept a connection. This runs before the dispatcher loop
                    // starts, so it never reaches the unhandled-exception dialog.
                    Console.Error.WriteLine($"[NetPrints automation] Could not start the automation agent on '{pipeName}': {e}");
                    throw;
                }

                desktop.Exit += (_, _) =>
                {
                    agent.Dispose();
                    tree.Dispose();
                };
            }

            // A single command-line argument is a project to open (FR-016, PAR-05).
            _ = composition.MainEditor!.OpenStartupProjectAsync(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
