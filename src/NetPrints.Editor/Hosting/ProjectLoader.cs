using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;
using NetPrints.Serialization;
using ProjectSessionViewModel = NetPrints.Editor.Shell.ProjectSessionViewModel;
using ShellViewModel = NetPrints.Editor.Shell.ShellViewModel;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Loads, creates and closes projects for the shell: the extension trust flow, the reload of the reflection provider
/// when the project or its build changes, and the extension failure report. The open project's session lives in
/// <see cref="ShellViewModel.Session"/>; this class replaces it.
/// </summary>
internal sealed class ProjectLoader : IDisposable
{
    private readonly EditorContext context;
    private readonly ShellViewModel shell;
    private readonly ILogger<ProjectLoader> logger;
    private readonly HashSet<(string Id, string? ManifestPath, string Code)> reportedExtensionFailures = [];
    private CancellationTokenSource? warmUp;
    private Project? subscribedProject;
    private ProjectSessionViewModel? session;

    /// <summary>The extension folders currently loaded: restored when a load fails after the new project's extensions were swapped in.</summary>
    private IReadOnlyList<string> activeExtensionFolders = [];

    /// <summary>Creates the loader.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="shell">The shell state whose session this loader replaces.</param>
    public ProjectLoader(EditorContext context, ShellViewModel shell)
    {
        this.context = context;
        this.shell = shell;
        logger = context.LoggerFactory.CreateLogger<ProjectLoader>();
    }

    /// <summary>
    /// Reloads the reflection provider for the open project, then warms the overload lists of its graphs' nodes off the UI
    /// thread; both show the busy indicator. A newer reload cancels the warm-up of the older one.
    /// </summary>
    /// <returns>A task that completes when the provider was reloaded and warmed; a failure is shown in the error dialog.</returns>
    public async Task ReloadReflectionAsync()
    {
        if (shell.Session?.Project is not { } project)
        {
            return;
        }

        if (warmUp is not null)
        {
            await warmUp.CancelAsync().ConfigureAwait(true);
            warmUp.Dispose();
        }

        warmUp = new CancellationTokenSource();
        CancellationToken warmUpToken = warmUp.Token;

        try
        {
            using (shell.StatusBar.BeginBusy("Loading references…"))
            {
                await context.Reflection.ReloadAsync(project).ConfigureAwait(true);
            }

            using (shell.StatusBar.BeginBusy("Preparing graphs…"))
            {
                await OverloadWarmUp.WarmAsync(context.Reflection, project, warmUpToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (warmUpToken.IsCancellationRequested)
        {
            // Superseded by a newer reload.
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to load references", ex.ToString()).ConfigureAwait(true);
        }
    }

    /// <summary>Opens the project passed as the only command-line argument.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A task that completes when the project is open, or at once with no project argument.</returns>
    public Task OpenStartupProjectAsync(IReadOnlyList<string>? args)
    {
        if (args is { Count: 1 } && !string.IsNullOrWhiteSpace(args[0]))
        {
            return LoadProjectAsync(args[0]);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new project in a folder and name chosen by the user and opens it:
    /// <see cref="IFilePickerService.SaveFileAsync"/> supplies the directory and project name, then
    /// <see cref="IProjectSystem.CreateAsync"/> writes the <c>.csproj</c>. Cancelling, or a failure, keeps the previous project.
    /// </summary>
    /// <returns>A task that completes when the project is open, or the user cancelled.</returns>
    public async Task CreateProjectAsync()
    {
        string? path = await context.FilePicker.SaveFileAsync("Create Project", "MyProject.csproj", "csproj",
            [FileFilter.ProjectFiles]).ConfigureAwait(true);

        if (path is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            await context.Dialogs.ShowErrorAsync("Failed to create project", $"'{path}' has no directory.").ConfigureAwait(true);
            return;
        }

        string projectName = Path.GetFileNameWithoutExtension(path);

        try
        {
            string csprojPath = await context.Projects.CreateAsync(
                directory, projectName, DefaultProjectProfile.Instance, projectName, CancellationToken.None).ConfigureAwait(true);
            await LoadProjectAsync(csprojPath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create project", $"Failed to create the project at {path}.\n\n{ex}").ConfigureAwait(true);
        }
    }

    /// <summary>Opens a project, asking for it with a *.csproj picker unless a path is given.</summary>
    /// <param name="path">The <c>.csproj</c> path, or null to ask the user.</param>
    /// <returns>A task that completes when the project is open, or the user cancelled.</returns>
    public async Task OpenProjectAsync(string? path)
    {
        path ??= await context.FilePicker.OpenFileAsync("Open Project", [FileFilter.ProjectFiles]).ConfigureAwait(true);
        if (path is not null)
        {
            await LoadProjectAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Replaces the open project's session with none.</summary>
    public void CloseProject() => SetProject(null);

    /// <summary>
    /// Loads a project through <see cref="ProjectPersistence"/>. Only a <c>.csproj</c> is accepted: any other path (for
    /// example a legacy project file) shows a message and nothing is opened or written (research.md R21). On a load
    /// failure the exception is shown in an error dialog and copied to the clipboard; non-fatal issues found while
    /// loading classes are shown but do not keep the project from opening (document-format.md §2.8).
    /// </summary>
    /// <param name="path">The <c>.csproj</c> path.</param>
    /// <returns>A task that completes when the project is open or the failure was shown.</returns>
    public async Task LoadProjectAsync(string path)
    {
        if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            await context.Dialogs.ShowErrorAsync("Unsupported project file",
                $"'{path}' is not a NetPrints project: only '.csproj' projects are supported. Nothing was opened.").ConfigureAwait(true);
            return;
        }

        shell.StatusBar.Show($"Loading project {path}");
        IReadOnlyList<string> previousExtensionFolders = activeExtensionFolders;

        try
        {
            IReadOnlyList<DocumentIssue> issues;
            Project project;
            using (shell.StatusBar.BeginBusy("Loading project…"))
            {
                IReadOnlyList<string> requestedProperties = context.Extensions.Current.ProjectProperties;
                ProjectSnapshot snapshot = await context.Projects.LoadAsync(path, CancellationToken.None).ConfigureAwait(true);
                DocumentIssue? notTrusted = await LoadExtensionsForProjectAsync(snapshot).ConfigureAwait(true);
                if (context.Extensions.Current.ProjectProperties.Any(name => !requestedProperties.Contains(name, StringComparer.Ordinal)))
                {
                    // The project's own extensions add MSBuild properties the first evaluation did not request (FR-025).
                    snapshot = await context.Projects.LoadAsync(path, CancellationToken.None).ConfigureAwait(true);
                }

                DocumentIssue? unknownProfile = context.Extensions.Current.FindProfile(snapshot.ProfileId) is null
                    ? new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.UnknownProfile,
                        $"The project profile '{snapshot.ProfileId}' is not provided by any loaded extension: the default profile is used. The project file is unchanged.",
                        new DocumentId(Path.GetFileName(snapshot.ProjectFilePath)))
                    : null;
                ProjectLoadResult loaded = await context.Persistence.LoadAsync(snapshot, CancellationToken.None).ConfigureAwait(true);

                SetProject(loaded.Project);
                project = loaded.Project;
                issues = [.. new[] { notTrusted, unknownProfile }.OfType<DocumentIssue>(), .. loaded.Issues];
            }

            shell.StatusBar.Show($"Loaded project {project.Name}", TimeSpan.FromSeconds(5));

            await ReportExtensionFailuresAsync().ConfigureAwait(true);

            if (issues.Count > 0)
            {
                await context.Dialogs.ShowErrorAsync("Project loaded with issues",
                    string.Join("\n\n", issues.Select(issue => $"{issue.Code}: {issue.Message}"))).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            // R2-11: the new project's extensions were already swapped in (LoadExtensionsForProjectAsync
            // runs before its graphs are mapped), but its graphs never finished mapping: restore the
            // previous project's extensions, or its own nodes and translation stop working until it is
            // reopened.
            if (!activeExtensionFolders.SequenceEqual(previousExtensionFolders))
            {
                try
                {
                    await context.Extensions.LoadForProjectAsync(previousExtensionFolders, CancellationToken.None).ConfigureAwait(true);
                    activeExtensionFolders = previousExtensionFolders;
                }
                catch (Exception rollbackEx)
                {
                    // The original load failure below must still reach the user even if the rollback
                    // itself fails (R2-22).
                    Log.ExtensionRollbackFailed(logger, rollbackEx);
                }
            }

            shell.StatusBar.Show($"Failed to load project {path}", TimeSpan.FromSeconds(5));
            await context.Clipboard.SetTextAsync(ex.ToString()).ConfigureAwait(true);
            await context.Dialogs.ShowErrorAsync("Failed to load project",
                $"Failed to load project at path {path}. The exception has been copied to your clipboard.\n\n{ex}").ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Shows one dialog listing the extensions that failed to load (<c>NPX001</c> to <c>NPX007</c>) and the
    /// contributions the registry rejected, skipping what an earlier call already showed. The editor stays usable
    /// without them (ED-T11, editor-services.md §5).
    /// </summary>
    /// <returns>A task that completes when the dialog was closed, or at once with nothing new to show.</returns>
    public Task ReportExtensionFailuresAsync()
    {
        ExtensionRegistry registry = context.Extensions.Current;
        List<CodeDiagnostic> diagnostics = [];

        foreach (ExtensionLoadResult.Failed failed in registry.Results.OfType<ExtensionLoadResult.Failed>())
        {
            if (reportedExtensionFailures.Add((failed.Id, failed.ManifestPath, failed.Code)))
            {
                diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, failed.Code,
                    $"Extension '{failed.Id}' was not loaded: {failed.Reason}", null, null, null, failed.ManifestPath, null));
            }
        }

        foreach (ExtensionContributionIssue issue in registry.Issues)
        {
            if (reportedExtensionFailures.Add((issue.ExtensionId, issue.Contribution, issue.Code)))
            {
                diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, issue.Code,
                    $"Extension '{issue.ExtensionId}': {issue.Contribution} was rejected: {issue.Reason}", null, null, null, null, null));
            }
        }

        return diagnostics.Count == 0
            ? Task.CompletedTask
            : context.Dialogs.ShowIssuesAsync("Extensions failed to load", diagnostics);
    }

    /// <summary>Stops following the open project and disposes its session.</summary>
    public void Dispose()
    {
        Unsubscribe();
        warmUp?.Dispose();
        session?.Dispose();
    }

    /// <summary>
    /// Loads the extensions of the project that is about to open (extension-points.md §8.3), before its graphs are
    /// mapped: the project's <c>NetPrintsExtension</c> folders are loaded only if its full path is in
    /// <see cref="NetPrintsSettings.TrustedProjects"/>; otherwise the user is asked once, "Trust" records the path in
    /// the settings, and "Don't load" opens the project with those extensions' nodes preserved but inactive. A project
    /// with no extension folders drops the previous project's.
    /// </summary>
    /// <returns>The <c>NPD006</c> issue when the user declined; otherwise <see langword="null"/>.</returns>
    private async Task<DocumentIssue?> LoadExtensionsForProjectAsync(ProjectSnapshot snapshot)
    {
        IReadOnlyList<string> folders = snapshot.ExtensionFolders;
        DocumentIssue? notTrusted = null;

        if (folders.Count > 0 && !IsTrusted(snapshot.ProjectFilePath))
        {
            if (await context.Dialogs.ConfirmTrustAsync(snapshot.ProjectFilePath, folders).ConfigureAwait(true))
            {
                NetPrintsSettings current = context.Settings.Get(NetPrintsSettings.Descriptor);
                await context.Settings.SetAsync(NetPrintsSettings.Descriptor,
                    current with { TrustedProjects = [.. current.TrustedProjects, snapshot.ProjectFilePath] }, CancellationToken.None).ConfigureAwait(true);
            }
            else
            {
                notTrusted = new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.ExtensionNotTrusted,
                    $"The extensions of '{snapshot.ProjectFilePath}' were not loaded because the project is not trusted: " +
                    "the nodes they provide are preserved but inactive, and they are saved unchanged.",
                    new DocumentId(Path.GetFileName(snapshot.ProjectFilePath)));
                folders = [];
            }
        }

        await context.Extensions.LoadForProjectAsync(folders, CancellationToken.None).ConfigureAwait(true);
        activeExtensionFolders = folders;
        return notTrusted;
    }

    private bool IsTrusted(string projectFilePath)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return context.Settings.Get(NetPrintsSettings.Descriptor).TrustedProjects
            .Any(trusted => string.Equals(trusted, projectFilePath, comparison));
    }

    // Swaps the shell's session first (it closes the documents), then disposes the old one; reloads reflection (PAR-15).
    private void SetProject(Project? project)
    {
        Unsubscribe();
        ProjectSessionViewModel? previous = session;
        session = project is null ? null : new ProjectSessionViewModel(project, context);
        shell.Session = session;
        previous?.Dispose();

        if (project is not null)
        {
            subscribedProject = project;
            ((INotifyPropertyChanged)project).PropertyChanged += OnProjectPropertyChanged;
            ReloadReflectionAsync().Forget(logger);
        }
    }

    private void Unsubscribe()
    {
        if (subscribedProject is not null)
        {
            ((INotifyPropertyChanged)subscribedProject).PropertyChanged -= OnProjectPropertyChanged;
            subscribedProject = null;
        }
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Core.Project.IsCompiling) when subscribedProject is { IsCompiling: false }:
                // Reload after a compilation finished (PAR-15).
                ReloadReflectionAsync().Forget(logger);
                break;
            case nameof(Core.Project.Snapshot):
                // References or other build settings may have changed (the References dialog, the binary-type chooser).
                ReloadReflectionAsync().Forget(logger);
                break;
        }
    }
}
