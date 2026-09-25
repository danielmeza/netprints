using Avalonia.Controls;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Main;
using NetPrints.Editor.References;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Testing.Ui.Main;
using NetPrints.Testing.Ui.Screenplay;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>
/// A fresh editor on the headless platform for one test: the real composition with recording
/// dialogs, a primed file picker and a capturing process launcher, the automation tree, the
/// headless driver, the root page object and a Screenplay actor. The test arranges through the
/// API (<see cref="Composition"/>) and acts through the page objects.
/// </summary>
public sealed class HeadlessApp : IDisposable
{
    /// <summary>The E2E screen size (Xvfb), used as the size of maximized windows.</summary>
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 1000;

    private readonly IDisposable exceptionHandler;
    private readonly IDisposable classWindowSizer;

    private HeadlessApp()
    {
        Dialogs = new RecordingDialogs
        {
            // The real (non-modal) references dialog, so tests can drive it.
            ShowReferences = references =>
            {
                var dialog = new ReferencesDialog { DataContext = references };
                dialog.Closed += (_, _) => references.Dispose();
                dialog.Show();
                return Task.CompletedTask;
            },
        };
        Processes = new CapturingProcessLauncher();
        FilePicker = new QueuedFilePicker();
        Composition = new EditorComposition(c => c with { Dialogs = Dialogs, Processes = Processes, FilePicker = FilePicker });
        exceptionHandler = Composition.InstallUnhandledExceptionHandler(); // as EditorApp does on the desktop
        Tree = new AutomationTree();

        // Headless windows do not grow when maximized: give class windows the size a maximized
        // window has on the E2E screen (1600x1000), so layouts and coordinates match.
        classWindowSizer = Avalonia.Controls.Window.WindowOpenedEvent.AddClassHandler(typeof(ClassEditorWindow), (sender, _) =>
        {
            var window = (ClassEditorWindow)sender!;
            window.Width = ScreenWidth;
            window.Height = ScreenHeight;
        });
        Driver = new HeadlessDriver(Tree, () => Processes.Output);
        Window = Composition.CreateMainWindow();
        Window.Show();
        Tree.Track(Window);
        HeadlessDriver.Pump();
        Main = new MainWindowPage(Driver);
        Actor = Actor.Named("Ada").WhoCan(UseNetPrints.With(Driver, FilePicker));
    }

    public EditorComposition Composition { get; }
    public MainWindow Window { get; }
    public RecordingDialogs Dialogs { get; }
    public CapturingProcessLauncher Processes { get; }
    public QueuedFilePicker FilePicker { get; }
    public AutomationTree Tree { get; }
    public HeadlessDriver Driver { get; }
    public MainWindowPage Main { get; }
    public Actor Actor { get; }
    public MainEditorVM ViewModel => Composition.MainEditor!;

    public static HeadlessApp Start() => new();

    /// <summary>Opens a project the way the command line does (PAR-05) and waits for its types.</summary>
    public async Task OpenStartupProjectAsync(string path, CancellationToken cancellationToken)
    {
        await ViewModel.OpenStartupProjectAsync([path]);
        await Testing.Ui.Driving.UiWait.UntilAsync(Driver, () => Task.FromResult(ViewModel.Project is not null), "project loaded", cancellationToken);
        await Testing.Ui.Driving.UiWait.UntilAsync(Driver, () => Task.FromResult(Composition.Context.Reflection.NonStaticTypes.Count > 0),
            "reflection loaded", cancellationToken, TimeSpan.FromSeconds(60));
    }

    /// <summary>The window of an open class editor (for arranging and asserting through the API).</summary>
    public ClassEditorWindow ClassWindow(string fullName) =>
        Composition.Windows.ClassEditorWindows.Single(w => ((ClassEditorVM)w.DataContext!).Class.FullName == fullName);

    /// <summary>
    /// Saves a screenshot of every open window and a dump of the automation tree for the current
    /// test (CI artifact; the last state of a failing test).
    /// </summary>
    private void SaveDiagnostics()
    {
        try
        {
            string test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
            string folder = Path.Combine(UiArtifacts.Directory, "diagnostics",
                string.Concat(test.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_'));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "tree.txt"), Tree.Dump());
            foreach (var window in Tree.Windows.Where(w => w.IsVisible).ToList())
            {
                var image = Driver.ScreenshotAsync(Tree.KeyOf(window), CancellationToken.None).GetAwaiter().GetResult();
                image.Save(Path.Combine(folder, Tree.KeyOf(window) + ".png"));
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Diagnostics are best effort.
        }
    }

    public void Dispose()
    {
        SaveDiagnostics();
        foreach (var window in Tree.Windows.Reverse().ToList())
        {
            window.Close();
        }

        exceptionHandler.Dispose();
        classWindowSizer.Dispose();
        Tree.Dispose();
        Processes.Dispose();
    }
}

/// <summary>Where UI test artifacts go: snapshots (actual and diff images) and diagnostics.</summary>
public static class UiArtifacts
{
    /// <summary><c>NETPRINTS_UI_ARTIFACTS</c> (CI: TestResults/ui), else a folder next to the tests.</summary>
    public static string Directory { get; } =
        Environment.GetEnvironmentVariable("NETPRINTS_UI_ARTIFACTS") is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "ui-artifacts");

    private static readonly string Baselines = typeof(UiArtifacts).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "SnapshotBaselines").Value!;

    /// <summary>The snapshot baselines committed in Snapshots/Baselines.</summary>
    public static SnapshotStore Snapshots { get; } = new(Baselines, Path.Combine(Directory, "snapshots"));
}
