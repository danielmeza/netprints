using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.References;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Screenplay;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The production composition with the shell window instead of the legacy windows, over a copy of the HelloWorld sample, on the headless platform.</summary>
internal sealed class ShellApp : IAsyncDisposable
{
    private readonly ExtensionHost extensions;
    private readonly SampleCopy sample = new();
    private readonly string settingsDirectory = Path.Combine(Path.GetTempPath(), "netprints-ui-tests", Guid.NewGuid().ToString("N"));
    private readonly IDisposable exceptionHandler;

    private ShellApp(IReadOnlyList<string> extensionFolders)
    {
        extensions = new ExtensionHost(new ExtensionLoaderOptions([], extensionFolders, [BuiltInExtension.InProcessEntry]), NullLoggerFactory.Instance);
        var settings = new JsonFileSettingsStore(Path.Combine(settingsDirectory, "settings.json"), NullLogger<JsonFileSettingsStore>.Instance);
        Dialogs = new RecordingDialogs();
        Processes = new CapturingProcessLauncher();
        FilePicker = new QueuedFilePicker();
        Composition = new TestComposition(new EditorHostServices(NullLoggerFactory.Instance, extensions, settings, NullHostChannel.Instance, HostChannelError: null,
            MsBuildAvailable: true, DisposeOwnedResources: () => ValueTask.CompletedTask), Dialogs, FilePicker, Processes);
        exceptionHandler = Composition.InstallUnhandledExceptionHandler();
        Ui = HeadlessUi.Create();
        Dialogs.ShowIssues = (title, issues) =>
        {
            Ui.Show(new IssuesDialog(title, issues));
            return Task.CompletedTask;
        };
        Dialogs.ShowReferences = references =>
        {
            var dialog = new ReferencesDialog { DataContext = references };
            dialog.Closed += (_, _) => references.Dispose();
            Ui.Show(dialog);
            return Task.CompletedTask;
        };
        Window = Ui.Show(Composition.CreateShellWindow());
        Driver = Ui.Driver;
        Actor = Actor.Named("Ada").WhoCan(UseNetPrints.With(Driver, FilePicker));
    }

    public TestComposition Composition { get; }

    public RecordingDialogs Dialogs { get; }

    public CapturingProcessLauncher Processes { get; }

    public QueuedFilePicker FilePicker { get; }

    public HeadlessUi Ui { get; }

    public HeadlessDriver Driver { get; }

    public Actor Actor { get; }

    public ShellWindow Window { get; }

    public ShellViewModel Shell => Composition.Shell ?? throw new InvalidOperationException("No shell.");

    public IShell Api => Composition.ShellApi ?? throw new InvalidOperationException("No shell.");

    public CommandInvoker Commands => Composition.Commands ?? throw new InvalidOperationException("No shell.");

    public string ProjectPath => sample.ProjectPath;

    public InspectorPanelViewModel Inspector => Assert.IsType<InspectorPanelViewModel>(Shell.FindPanel(PanelContributions.InspectorId)?.Content);

    public ProjectSessionViewModel Session => Shell.Session ?? throw new InvalidOperationException("No project open.");

    public static ShellApp Start() => new([]);

    /// <summary>A fresh editor whose extension host also loads the given folders (each must hold a manifest).</summary>
    public static ShellApp Start(IReadOnlyList<string> extensionFolders) => new(extensionFolders);

    /// <summary>Opens the sample the way the command line does and waits for its types.</summary>
    public async Task OpenSampleAsync(CancellationToken cancellationToken)
    {
        await Composition.StartAsync([sample.ProjectPath]);
        await Testing.Ui.Driving.UiWait.UntilAsync(Driver, () => Task.FromResult(Shell.Session is not null), "project loaded", cancellationToken);
        await Testing.Ui.Driving.UiWait.UntilAsync(Driver, () => Task.FromResult(Composition.Context.Reflection.NonStaticTypes.Count > 0),
            "reflection loaded", cancellationToken, TimeSpan.FromSeconds(60));
    }

    public IContributionRegistry Registry => Composition.Registry ?? throw new InvalidOperationException("No shell.");

    public CommandDescriptor Command(string name) => Registry.Commands.Single(command => command.Id == ContributionIds.CommandPrefix + name);

    public async ValueTask DisposeAsync()
    {
        Ui.Dispose();
        exceptionHandler.Dispose();
        Processes.Dispose();
        Composition.Dispose();
        await extensions.DisposeAsync();
        sample.Dispose();
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
