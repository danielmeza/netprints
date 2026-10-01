using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Main;
using NetPrints.Editor.References;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;
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
public sealed class HeadlessApp : IAsyncDisposable
{
    /// <summary>The E2E screen size (Xvfb), used as the size of maximized windows.</summary>
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 1000;

    private readonly IDisposable exceptionHandler;
    private readonly IDisposable classWindowSizer;

    private readonly ExtensionHost extensions;
    private readonly string settingsDirectory;

    private HeadlessApp(IReadOnlyList<string> extensionFolders)
    {
        // A real extension host (the built-in extension plus the given folders) and a real settings file in a
        // temp folder, so the composition is the production one apart from the recording dialogs.
        extensions = new ExtensionHost(new ExtensionLoaderOptions([], extensionFolders, [BuiltInExtension.InProcessEntry]), NullLoggerFactory.Instance);
        settingsDirectory = Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));
        Settings = new JsonFileSettingsStore(Path.Combine(settingsDirectory, "settings.json"), NullLogger<JsonFileSettingsStore>.Instance);
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
            // The real (non-modal) issues dialog, so tests can drive it.
            ShowIssues = (title, issues) =>
            {
                var dialog = new IssuesDialog(title, issues);
                dialog.Show();
                HeadlessDriver.Pump();
                return Task.CompletedTask;
            },
        };
        Processes = new CapturingProcessLauncher();
        FilePicker = new QueuedFilePicker();
        Composition = new TestComposition(new EditorHostServices(NullLoggerFactory.Instance, extensions, Settings, NullHostChannel.Instance, HostChannelError: null,
            MsBuildAvailable: true, DisposeOwnedResources: () => ValueTask.CompletedTask), Dialogs, FilePicker, Processes);
        exceptionHandler = Composition.InstallUnhandledExceptionHandler(); // as EditorApp does on the desktop
        Tree = new AutomationTree();

        // Headless maximized windows keep their size: use the E2E screen size.
        classWindowSizer = Avalonia.Controls.Window.WindowOpenedEvent.AddClassHandler(typeof(ClassEditorWindow), (sender, _) =>
        {
            if (sender is ClassEditorWindow window)
            {
                window.Width = ScreenWidth;
                window.Height = ScreenHeight;
            }
        });
        Driver = new HeadlessDriver(Tree, () => Processes.Output);
        Window = Composition.CreateMainWindow();
        Window.Show();
        Tree.Track(Window);
        HeadlessDriver.Pump();
        Main = new MainWindowPage(Driver);
        Actor = Actor.Named("Ada").WhoCan(UseNetPrints.With(Driver, FilePicker));
    }

    public ISettingsStore Settings { get; }
    public TestComposition Composition { get; }
    public MainWindow Window { get; }
    public RecordingDialogs Dialogs { get; }
    public CapturingProcessLauncher Processes { get; }
    public QueuedFilePicker FilePicker { get; }
    public AutomationTree Tree { get; }
    public HeadlessDriver Driver { get; }
    public MainWindowPage Main { get; }
    public Actor Actor { get; }
    public MainEditorViewModel ViewModel => Composition.MainEditor
        ?? throw new InvalidOperationException($"{nameof(Composition.MainEditor)} has not been created yet.");

    public static HeadlessApp Start() => new([]);

    /// <summary>A fresh editor whose extension host also loads the given folders (each must hold a manifest).</summary>
    public static HeadlessApp Start(IReadOnlyList<string> extensionFolders) => new(extensionFolders);

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
        Composition.Windows.ClassEditorWindows.Single(w => (w.DataContext as ClassEditorViewModel)?.Class.FullName == fullName);

    /// <summary>
    /// Saves a screenshot of every open window and a dump of the automation tree for the current
    /// test (CI artifact; the last state of a failing test).
    /// </summary>
    private async Task SaveDiagnosticsAsync()
    {
        try
        {
            string test = TestContext.Current.Test?.TestDisplayName ?? "unknown";
            string folder = Path.Combine(UiArtifacts.Directory, "diagnostics", UiArtifacts.SafeName(test));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "tree.txt"), Tree.Dump());
            foreach (var window in Tree.Windows.Where(w => w.IsVisible).ToList())
            {
                var image = await Driver.ScreenshotAsync(Tree.KeyOf(window), CancellationToken.None);
                image.Save(Path.Combine(folder, Tree.KeyOf(window) + ".png"));
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Diagnostics are best effort.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await SaveDiagnosticsAsync();
        foreach (var window in Tree.Windows.Reverse().ToList())
        {
            window.Close();
        }

        exceptionHandler.Dispose();
        classWindowSizer.Dispose();
        Tree.Dispose();
        Processes.Dispose();
        Composition.Dispose();
        await extensions.DisposeAsync();
        try
        {
            Directory.Delete(settingsDirectory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The settings file was never written.
        }
    }
}

/// <summary>Where UI test artifacts go: snapshots (actual and diff images) and diagnostics.</summary>
public static class UiArtifacts
{
    /// <summary><c>NETPRINTS_UI_ARTIFACTS</c> (CI: TestResults/ui), else a folder next to the tests.</summary>
    public static string Directory { get; } =
        Environment.GetEnvironmentVariable(TestEnvironment.UiArtifactsVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "ui-artifacts");

    /// <summary>Turns a test display name into a file or folder name that actions/upload-artifact accepts.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>The name with every rejected character replaced by an underscore; <c>unknown</c> when blank.</returns>
    public static string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "unknown";
        }

        var builder = new System.Text.StringBuilder(name.Length);
        foreach (char character in name)
        {
            builder.Append(char.IsWhiteSpace(character) || character is '"' or ':' or '<' or '>' or '|' or '*' or '?' or '\\' or '/' || char.IsControl(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static readonly string Baselines = typeof(UiArtifacts).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
        .Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a => a.Key == "SnapshotBaselines").Value
        ?? throw new InvalidOperationException("The 'SnapshotBaselines' assembly metadata has no value.");

    /// <summary>The snapshot baselines committed in Snapshots/Baselines.</summary>
    public static SnapshotStore Snapshots { get; } = new(Baselines, Path.Combine(Directory, "snapshots"));
}
