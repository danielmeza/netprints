using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Composition root: creates the Avalonia service implementations and the shell window (no DI container).
/// </summary>
public sealed class EditorComposition : IDisposable
{
    private readonly EditorServices services;

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    public EditorComposition(EditorHostServices host)
    {
        EditorDataPaths paths = EditorDataPaths.Resolve();
        var fileSystem = new RealEditorFileSystem();
        new StateMigration(paths, fileSystem, new RealStateFolderMover(), host.LoggerFactory.CreateLogger<StateMigration>()).Run();
        var stateStore = new JsonEditorStateStore(paths, fileSystem, host.LoggerFactory.CreateLogger<JsonEditorStateStore>());
        services = new EditorServices(host, collection =>
        {
            collection.AddSingleton(new BackupOptions(paths, fileSystem, TimeProvider.System, BackupService.ResolveDelay(Environment.GetEnvironmentVariable)));
            collection.AddSingleton(new RecentProjects(stateStore, fileSystem, TimeProvider.System));
            collection.AddSingleton(new WindowStateService(stateStore));
            collection.AddSingleton<IEditorStateStore>(stateStore);
            collection.AddSingleton(ProjectLocations.Resolve(stateStore, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        });
    }

    /// <summary>The composed host services.</summary>
    public EditorContext Context => services.Context;

    /// <summary>The concrete window service (not just <see cref="IWindowService"/>, for callers that need <see cref="WindowService.MainWindow"/>).</summary>
    public WindowService Windows => services.Windows;

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() => services.InstallUnhandledExceptionHandler();

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateShellWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public Task StartAsync(IReadOnlyList<string> args) => services.StartAsync(args);

    /// <summary>The composed shell state, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public ShellViewModel? Shell => services.Shell;

    /// <summary>The shell API over the docking layout, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IShell? ShellApi => services.ShellApi;

    /// <summary>The invoker of the registered commands, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public CommandInvoker? Commands => services.Commands;

    /// <summary>Creates the shell window over a project service (no legacy window opens).</summary>
    public ShellWindow CreateShellWindow() => services.CreateShellWindow();

    /// <summary>
    /// Asks whether the application may exit, as closing the window does: waits for a build, asks to stop a running
    /// program and asks about unsaved files. Passes when the shell does not exist yet.
    /// </summary>
    /// <param name="cancellationToken">Cancels the prompts.</param>
    /// <returns><see langword="true"/> to go on, <see langword="false"/> to keep the application running.</returns>
    public Task<bool> ConfirmExitAsync(CancellationToken cancellationToken) => services.ConfirmExitAsync(cancellationToken);

    /// <summary>Writes the backups of the open project that are still waiting.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public Task FlushBackupsAsync() => services.FlushBackupsAsync();

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes the shell, if created, and the
    /// code analysis host (editor-services.md §6: "disposed with the main window").
    /// </summary>
    public void Dispose() => services.Dispose();
}

/// <summary>
/// Builds the Avalonia-agnostic half of the composition root (project system, persistence, reflection,
/// live analysis, the main view model and window) from the dialogs, file picker and process launcher the
/// caller supplies: <see cref="EditorComposition"/> passes the Avalonia implementations, and the headless
/// UI tests' own composition passes test doubles instead. Passing them in through the constructor is
/// ordinary dependency injection, not a customization hook (ED-T14): both callers exercise this exact
/// same startup order, so a change to it (R2-06) is caught by the headless UI tests too, not only by the
/// slow E2E suite.
/// </summary>
internal sealed class EditorServices : IDisposable
{
    /// <summary>
    /// <c>NetPrints.Sdk</c> version substituted into a new project's template (project-system.md §4):
    /// the editor's own <see cref="AssemblyInformationalVersionAttribute"/>, which MinVer stamps at
    /// build time, with the <c>+&lt;sha&gt;</c> build-metadata suffix stripped (release-and-docs.md,
    /// "Editor version").
    /// </summary>
    private static readonly string NetPrintsSdkVersion = EditorSdkVersion.Resolve(typeof(EditorServices).Assembly);

    private readonly string? hostChannelError;
    private readonly ServiceProvider provider;
    private ShellHost? shellHost;

    /// <summary>
    /// The options the editor's MSBuild project system runs with: the properties the loaded extensions ask for and the editor's SDK
    /// version. Loading a project must not write generated files (FR-085), so generation on load is off.
    /// </summary>
    /// <param name="extensions">The extension host whose loaded extensions name the properties to evaluate.</param>
    /// <returns>The options.</returns>
    internal static ProjectSystemOptions CreateProjectSystemOptions(IExtensionHost extensions) =>
        new(new ExtensionProjectProperties(extensions), NetPrintsSdkVersion, GenerateOnLoad: false);

    /// <param name="host">Process-wide services created once by the host (desktop, headless tests).</param>
    /// <param name="configure">Adds what the caller provides on top of the generated registrations: the host-only services in production
    /// (backups, recent projects, window state, state store, project locations), the test doubles in the headless UI tests (<c>Replace</c>).</param>
    public EditorServices(EditorHostServices host, Action<IServiceCollection>? configure = null)
    {
        hostChannelError = host.HostChannelError;
        var collection = new ServiceCollection();
        collection.AddEditorHost(host);
        configure?.Invoke(collection);
        provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        // The composition validates the settings sections the editor owns: a failure is logged and the section keeps its defaults.
        host.Settings.Get(NetPrintsSettings.Descriptor);
        host.Settings.Get(EditorSettings.Descriptor);
        host.Settings.Get(RunSettings.Descriptor);
        Context = provider.GetRequiredService<EditorContext>();
        Windows = provider.GetRequiredService<WindowService>();
        provider.GetRequiredService<PersistenceBinding>();
    }

    /// <summary>The container the editor was composed in.</summary>
    public IServiceProvider Services => provider;

    /// <summary>The composed host services.</summary>
    public EditorContext Context { get; }

    /// <summary>The concrete window service.</summary>
    public WindowService Windows { get; }

    /// <summary>
    /// Shows exceptions that escape to the UI thread in the error dialog instead of crashing
    /// (dispose to uninstall).
    /// </summary>
    public IDisposable InstallUnhandledExceptionHandler() =>
        new UnhandledExceptionHandler(Context.Dialogs, Context.Dispatcher, Context.LoggerFactory.CreateLogger<UnhandledExceptionHandler>(), ExtensionAttribution.For(Context.Extensions, () => shellHost?.Actions.Loader.ActiveExtensionFolders ?? []));

    /// <summary>
    /// Reports what went wrong before the window existed (a requested host channel that is not available, extensions
    /// that failed to load), then opens the project named on the command line, if any (FR-016, PAR-05). Call after
    /// <see cref="CreateShellWindow"/>.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    public async Task StartAsync(IReadOnlyList<string> args)
    {
        ProjectLoader loader = shellHost?.Actions.Loader ?? throw new InvalidOperationException($"{nameof(CreateShellWindow)} must be called first.");
        if (hostChannelError is not null)
        {
            await Context.Dialogs.ShowErrorAsync("Host channel unavailable", hostChannelError);
        }

        await loader.ReportExtensionFailuresAsync();
        await CleanUpBackupsAsync();
        await loader.OpenStartupProjectAsync(args);
    }

    // Off the UI thread: a folder on an unreachable share can take long to answer. Runs before a project opens, so nothing writes beside it.
    private Task CleanUpBackupsAsync() =>
        Context.Backups is { } options
            ? Task.Run(() => BackupService.CleanUp(options.Paths, options.FileSystem, options.Time, Context.LoggerFactory.CreateLogger<BackupService>()))
            : Task.CompletedTask;

    /// <summary>The frozen registry the shell was generated from, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IContributionRegistry? Registry => shellHost?.Registry;

    /// <summary>The composed shell state, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public ShellViewModel? Shell => shellHost?.Shell;

    /// <summary>The shell API over the docking layout, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public IShell? ShellApi => shellHost?.Adapter;

    /// <summary>The invoker of the registered commands, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    public CommandInvoker? Commands => shellHost?.Invoker;

    /// <summary>The project flows of the shell, or <see langword="null"/> before <see cref="CreateShellWindow"/> is called.</summary>
    internal IProjectActions? ProjectActions => shellHost?.Actions;

    /// <summary>Asks whether the application may exit; passes when the shell does not exist yet.</summary>
    /// <param name="cancellationToken">Cancels the prompts.</param>
    /// <returns><see langword="true"/> to go on.</returns>
    public Task<bool> ConfirmExitAsync(CancellationToken cancellationToken) =>
        shellHost?.Actions.ConfirmExitAsync(cancellationToken) ?? Task.FromResult(true);

    /// <summary>Writes the backups of the open project that are still waiting; the exit cleanup awaits it before disposing the composition.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public Task FlushBackupsAsync() => shellHost?.Actions.Loader.FlushBackupsAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Creates the shell window: the registry, <see cref="ShellViewModel"/>, the docking adapter and the project flows
    /// that open, create and close projects.
    /// </summary>
    /// <exception cref="InvalidOperationException">The shell window was already created.</exception>
    public ShellWindow CreateShellWindow()
    {
        if (shellHost is not null)
        {
            throw new InvalidOperationException("The shell window was already created.");
        }

        shellHost = provider.GetRequiredService<ShellHost>();
        Windows.MainWindow = shellHost.Window;
        return shellHost.Window;
    }

    /// <summary>
    /// Stops rebinding persistence to the extension host's registry (see
    /// <see cref="PersistenceBinding.Bind"/>), disposes the shell, if created, and the
    /// code analysis host (editor-services.md §6: "disposed with the main window").
    /// </summary>
    public void Dispose() => provider.Dispose();
}

/// <summary>
/// Derives the <c>NetPrints.Sdk</c> version a new project or sample copy references
/// (release-and-docs.md, "Editor version"; ADR-0022).
/// </summary>
internal static class EditorSdkVersion
{
    /// <summary>Used when the assembly carries no informational version, e.g. run without MinVer having stamped one.</summary>
    internal const string Fallback = "0.1.0-dev";

    /// <summary>The assembly metadata key the build stamps with the latest released version (the nearest <c>v*</c> git tag).</summary>
    internal const string LatestReleaseKey = "NetPrints.LatestRelease";

    /// <summary>The version to write: the editor's own when it is a release, else the latest published one.</summary>
    internal static string Resolve(Assembly assembly)
    {
        string? latest = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, LatestReleaseKey, StringComparison.Ordinal))?.Value;
        return Choose(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, latest);
    }

    /// <summary>
    /// A build between releases (a pre-release version other than the latest tag) is not on nuget.org, so it writes
    /// <paramref name="latestRelease"/>; a release writes its own version.
    /// </summary>
    internal static string Choose(string? informationalVersion, string? latestRelease)
    {
        string? own = informationalVersion is null ? null : StripBuildMetadata(informationalVersion);
        if (string.IsNullOrWhiteSpace(latestRelease))
        {
            return own ?? Fallback;
        }

        return own is null || (own.Contains('-', StringComparison.Ordinal) && !string.Equals(own, latestRelease, StringComparison.Ordinal))
            ? latestRelease
            : own;
    }

    /// <summary>Strips MinVer's <c>+&lt;sha&gt;</c> build-metadata suffix, if present.</summary>
    internal static string StripBuildMetadata(string version) =>
        version.Contains('+', StringComparison.Ordinal) ? version[..version.IndexOf('+', StringComparison.Ordinal)] : version;
}
