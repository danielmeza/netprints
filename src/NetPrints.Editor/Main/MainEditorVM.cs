using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Generator;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.Main;

/// <summary>
/// View model of the main window: project lifecycle, settings, references and the class list
/// (PAR-01..15).
/// </summary>
public sealed partial class MainEditorVM : ObservableObject, IDisposable
{
    private readonly EditorContext context;
    private readonly ILogger<MainEditorVM> logger;
    private readonly HashSet<(string Id, string? ManifestPath, string Code)> reportedExtensionFailures = [];
    private readonly HostChannelBridge hostChannelBridge;
    private Project? subscribedProject;

    /// <summary>
    /// Creates the main window's view model, optionally with a project already open.
    /// </summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="project">Initially open project, or <see langword="null"/>.</param>
    public MainEditorVM(EditorContext context, Project? project = null)
    {
        this.context = context;
        logger = context.LoggerFactory.CreateLogger<MainEditorVM>();
        hostChannelBridge = new HostChannelBridge(context.HostChannel, context.Dispatcher, ReloadReflectionAsync, FocusDocument,
            context.LoggerFactory.CreateLogger<HostChannelBridge>());
        Project = project;
    }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context => context;

    /// <summary>The open project, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectOpen), nameof(CanCompile), nameof(CanCompileAndRun), nameof(Title), nameof(Classes))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(CompileCommand), nameof(RunCommand),
        nameof(ShowReferencesCommand), nameof(NewClassCommand), nameof(AddExistingClassCommand), nameof(ToggleSettingsPaneCommand))]
    public partial Project? Project { get; set; }

    /// <summary>
    /// The open project's classes, or empty with no project open (batch D1): the class list's
    /// <c>ItemsSource</c> binds this instead of the two-segment <c>Project.Classes</c>, which logged a
    /// "Value is null" binding warning while no project was open yet (the null <see cref="Project"/>
    /// intermediate). Still the live collection itself, so additions and removals keep updating the list.
    /// </summary>
    public IReadOnlyList<ClassGraph> Classes => Project?.Classes ?? [];

    /// <summary>Whether the Project pane (Create/Open/Save) is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    public partial bool IsProjectPaneOpen { get; set; }

    /// <summary>Whether the Settings pane (output, binary type) is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    public partial bool IsSettingsPaneOpen { get; set; }

    /// <summary>Whether one of the side panes is open.</summary>
    public bool IsPaneOpen => IsProjectPaneOpen || IsSettingsPaneOpen;

    /// <summary>Whether a background operation shows the progress overlay.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyTitle { get; set; } = "";

    [ObservableProperty]
    public partial string BusyMessage { get; set; } = "";

    /// <summary>Whether a project is open.</summary>
    public bool IsProjectOpen => Project is not null;

    /// <summary>Whether the open project can currently be compiled; <see langword="false"/> with no project open.</summary>
    public bool CanCompile => Project?.CanCompile ?? false;

    /// <summary>Whether the open project can currently be compiled and run; <see langword="false"/> with no project open.</summary>
    public bool CanCompileAndRun => Project?.CanCompileAndRun ?? false;

    /// <summary>Window title: the project name (PAR-01).</summary>
    public string Title => Project?.Name is { Length: > 0 } name ? name : "NetPrints";

    /// <summary>The values offered by the binary-type chooser.</summary>
    public IReadOnlyList<BinaryType> BinaryTypes { get; } = Enum.GetValues<BinaryType>();

    /// <summary>
    /// The open project's output binary type, or <see cref="BinaryType.SharedLibrary"/> with no
    /// project open. Setting it edits the <c>.csproj</c> through
    /// <see cref="IProjectSystem.ApplyAsync"/> (<see cref="ProjectEdit.SetOutputType"/>,
    /// project-system.md §4) and replaces <see cref="Core.Project.Snapshot"/> once that completes.
    /// </summary>
    public BinaryType OutputBinaryType
    {
        get => Project?.OutputBinaryType ?? BinaryType.SharedLibrary;
        set
        {
            if (Project is { } project && project.OutputBinaryType != value)
            {
                ApplyOutputTypeAsync(project, value).Forget(logger);
            }
        }
    }

    private async Task ApplyOutputTypeAsync(Project project, BinaryType value)
    {
        try
        {
            ProjectSnapshot snapshot = await context.Projects.ApplyAsync(
                project.Path, [new ProjectEdit.SetOutputType(value)], CancellationToken.None);
            project.Snapshot = snapshot;
            project.OutputBinaryType = snapshot.OutputType;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to change the binary type", ex.ToString());
        }
        finally
        {
            OnPropertyChanged(nameof(OutputBinaryType));
        }
    }

    // The former Fody hook OnProjectChanged: wire project events and reload reflection (PAR-15).
    partial void OnProjectChanged(Project? value)
    {
        if (subscribedProject is not null)
        {
            ((INotifyPropertyChanged)subscribedProject).PropertyChanged -= OnProjectPropertyChanged;
        }

        subscribedProject = value;

        if (value is not null)
        {
            ((INotifyPropertyChanged)value).PropertyChanged += OnProjectPropertyChanged;
            ReloadReflectionAsync().Forget(logger);
        }

        OnPropertyChanged(nameof(OutputBinaryType));

        if (value is null)
        {
            IsSettingsPaneOpen = false;
        }
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Core.Project.IsCompiling):
                RefreshCompileState();
                if (Project is { IsCompiling: false })
                {
                    // Reload after a compilation finished (PAR-15).
                    ReloadReflectionAsync().Forget(logger);
                }
                break;
            case nameof(Core.Project.OutputBinaryType):
                RefreshCompileState();
                OnPropertyChanged(nameof(OutputBinaryType));
                break;
            case nameof(Core.Project.Snapshot):
                // References or other build settings may have changed (the References dialog, the
                // binary-type chooser): reload the reflection provider from the new snapshot.
                OnPropertyChanged(nameof(OutputBinaryType));
                ReloadReflectionAsync().Forget(logger);
                break;
            case nameof(Core.Project.Name):
                OnPropertyChanged(nameof(Title));
                break;
        }
    }

    private void RefreshCompileState()
    {
        OnPropertyChanged(nameof(CanCompile));
        OnPropertyChanged(nameof(CanCompileAndRun));
        CompileCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Reloads the reflection provider for the open project.</summary>
    public async Task ReloadReflectionAsync()
    {
        if (Project is null)
        {
            return;
        }

        try
        {
            await context.Reflection.ReloadAsync(Project);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to load references", ex.ToString());
        }
    }

    // Project and Settings panes are mutually exclusive toggles (PAR-02).
    [RelayCommand]
    private void ToggleProjectPane()
    {
        IsSettingsPaneOpen = false;
        IsProjectPaneOpen = !IsProjectPaneOpen;
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void ToggleSettingsPane()
    {
        IsProjectPaneOpen = false;
        IsSettingsPaneOpen = !IsSettingsPaneOpen;
    }

    /// <summary>
    /// Opens the project passed as the only command-line argument (PAR-05, FR-016).
    /// </summary>
    public Task OpenStartupProjectAsync(IReadOnlyList<string>? args)
    {
        if (args is { Count: 1 } && !string.IsNullOrWhiteSpace(args[0]))
        {
            return LoadProjectAsync(args[0]);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new project in a folder and name chosen by the user and opens it (PAR-03,
    /// project-system.md §6): <see cref="IFilePickerService.SaveFileAsync"/> supplies the directory
    /// and project name, then <see cref="IProjectSystem.CreateAsync"/> writes the <c>.csproj</c>.
    /// Cancelling, or a failure, restores the previous project.
    /// </summary>
    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        Project? oldProject = Project;

        string? path = await context.FilePicker.SaveFileAsync("Create Project", "MyProject.csproj", "csproj",
            [FileFilter.ProjectFiles]);

        if (path is null)
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            await context.Dialogs.ShowErrorAsync("Failed to create project", $"'{path}' has no directory.");
            return;
        }

        string projectName = Path.GetFileNameWithoutExtension(path);

        try
        {
            string csprojPath = await context.Projects.CreateAsync(
                directory, projectName, DefaultProjectProfile.Instance, projectName, CancellationToken.None);
            context.Windows.CloseAllClassEditors();
            await LoadProjectAsync(csprojPath);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create project", $"Failed to create the project at {path}.\n\n{ex}");
            Project = oldProject;
            return;
        }

        IsProjectPaneOpen = false;
    }

    /// <summary>Opens a project chosen with a *.csproj picker (PAR-04).</summary>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        string? path = await context.FilePicker.OpenFileAsync("Open Project", [FileFilter.ProjectFiles]);
        if (path is not null)
        {
            IsProjectPaneOpen = false;
            await LoadProjectAsync(path);
        }
    }

    /// <summary>
    /// Loads a project in the background with the progress overlay, through
    /// <see cref="ProjectPersistence"/>. Only a <c>.csproj</c> is accepted: any other path (for
    /// example a legacy project file) shows a message and nothing is opened or written
    /// (research.md R21). On a load failure the exception is shown in an error dialog and copied to
    /// the clipboard (PAR-04); non-fatal issues found while loading classes are shown but do not
    /// keep the project from opening (document-format.md §2.8).
    /// </summary>
    public async Task LoadProjectAsync(string path)
    {
        if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            await context.Dialogs.ShowErrorAsync("Unsupported project file",
                $"'{path}' is not a NetPrints project: only '.csproj' projects are supported. Nothing was opened.");
            return;
        }

        BusyTitle = "Loading project";
        BusyMessage = path;
        IsBusy = true;

        try
        {
            IReadOnlyList<string> requestedProperties = context.Extensions.Current.ProjectProperties;
            ProjectSnapshot snapshot = await context.Projects.LoadAsync(path, CancellationToken.None);
            DocumentIssue? notTrusted = await LoadExtensionsForProjectAsync(snapshot);
            if (context.Extensions.Current.ProjectProperties.Any(name => !requestedProperties.Contains(name, StringComparer.Ordinal)))
            {
                // The project's own extensions add MSBuild properties the first evaluation did not request (FR-025).
                snapshot = await context.Projects.LoadAsync(path, CancellationToken.None);
            }

            DocumentIssue? unknownProfile = context.Extensions.Current.FindProfile(snapshot.ProfileId) is null
                ? new DocumentIssue(DocumentIssueSeverity.Warning, DocumentIssue.UnknownProfile,
                    $"The project profile '{snapshot.ProfileId}' is not provided by any loaded extension: the default profile is used. The project file is unchanged.",
                    new DocumentId(Path.GetFileName(snapshot.ProjectFilePath)))
                : null;
            ProjectLoadResult loaded = await context.Persistence.LoadAsync(snapshot, CancellationToken.None);

            context.Windows.CloseAllClassEditors();
            Project = loaded.Project;
            IsBusy = false;

            await ReportExtensionFailuresAsync();

            IReadOnlyList<DocumentIssue> issues = [.. new[] { notTrusted, unknownProfile }.OfType<DocumentIssue>(), .. loaded.Issues];
            if (issues.Count > 0)
            {
                await context.Dialogs.ShowErrorAsync("Project loaded with issues",
                    string.Join("\n\n", issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            }
        }
        catch (Exception ex)
        {
            IsBusy = false;
            await context.Clipboard.SetTextAsync(ex.ToString());
            await context.Dialogs.ShowErrorAsync("Failed to load project",
                $"Failed to load project at path {path}. The exception has been copied to your clipboard.\n\n{ex}");
        }
        finally
        {
            IsBusy = false;
        }
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
            if (await context.Dialogs.ConfirmTrustAsync(snapshot.ProjectFilePath, folders))
            {
                NetPrintsSettings current = context.Settings.Get(NetPrintsSettings.Descriptor);
                await context.Settings.SetAsync(NetPrintsSettings.Descriptor,
                    current with { TrustedProjects = [.. current.TrustedProjects, snapshot.ProjectFilePath] }, CancellationToken.None);
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

        await context.Extensions.LoadForProjectAsync(folders, CancellationToken.None);
        return notTrusted;
    }

    private bool IsTrusted(string projectFilePath)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return context.Settings.Get(NetPrintsSettings.Descriptor).TrustedProjects
            .Any(trusted => string.Equals(trusted, projectFilePath, comparison));
    }

    /// <summary>
    /// Shows one dialog listing the extensions that failed to load (<c>NPX001</c> to <c>NPX007</c>) and the
    /// contributions the registry rejected, skipping what an earlier call already showed. The editor stays usable
    /// without them (ED-T11, editor-services.md §5).
    /// </summary>
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

    /// <summary>
    /// Saves every edited class of the open project through <see cref="ProjectPersistence"/> (PAR-06).
    /// Returns whether the project was open (and therefore saved).
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task<bool> SaveProjectAsync()
    {
        IsProjectPaneOpen = false;
        return await PromptProjectSaveAsync();
    }

    internal async Task<bool> PromptProjectSaveAsync()
    {
        if (Project is not { } project)
        {
            return false;
        }

        try
        {
            await context.Persistence.SaveAsync(project, cls => RenderGenerated(context, project, cls), CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
            return false;
        }
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private static string RenderGenerated(EditorContext context, Project project, ClassGraph cls) =>
        GraphCodeGenerator.RenderFile(new ClassTranslator(context.Extensions.Current.Translation).Translate(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

    /// <summary>Compiles the project in the background (PAR-09).</summary>
    [RelayCommand(CanExecute = nameof(CanCompile))]
    private Task CompileAsync() => Project is null ? Task.CompletedTask : CompileAsync(Project, context);

    /// <summary>Compiles, then runs the program when the build succeeded (PAR-10).</summary>
    [RelayCommand(CanExecute = nameof(CanCompileAndRun))]
    private Task RunAsync() => Project is null ? Task.CompletedTask : CompileAndRunAsync(Project, context);

    /// <summary>
    /// Saves the project's dirty classes, builds it through <see cref="IProjectSystem.BuildAsync"/>
    /// and maps the outcome onto <see cref="Core.Project.IsCompiling"/>,
    /// <see cref="Core.Project.LastCompilationSucceeded"/> and <see cref="Core.Project.LastDiagnostics"/>
    /// (shared by the main and class windows). A class that cannot be translated while saving is
    /// reported as a build error; any other failure is shown through
    /// <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <returns>Whether the build succeeded.</returns>
    public static async Task<bool> CompileAsync(Project project, EditorContext context)
    {
        project.IsCompiling = true;
        project.CompilationMessage = "Compiling...";
        try
        {
            await context.Persistence.SaveAsync(project, cls => RenderForBuild(context, project, cls), CancellationToken.None);
            BuildResult result = await context.Projects.BuildAsync(project.Path, CancellationToken.None);
            var classesByGeneratedPath = BuildClassesByGeneratedPath(context, project);
            SetBuildOutcome(project, DiagnosticMapper.FromBuild(result.Messages, classesByGeneratedPath), result.Success, result.OutputAssemblyPath);
            return result.Success;
        }
        catch (ClassTranslationFailure failure)
        {
            var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, TranslationDiagnosticCodes.Unclassified,
                $"{failure.Class.FullName}: {failure.Message}", failure.Class.FullName, null, null, null, null);
            SetBuildOutcome(project, [diagnostic], false, null);
            return false;
        }
        catch (Exception ex)
        {
            project.LastCompilationSucceeded = false;
            project.CompilationMessage = "Build failed";
            await context.Dialogs.ShowErrorAsync("Failed to build project", ex.ToString());
            return false;
        }
        finally
        {
            project.IsCompiling = false;
        }
    }

    /// <summary>
    /// Re-translates every class, keyed by its generated file's full path, so
    /// <see cref="DiagnosticMapper.FromBuild"/> can map a build error back to its node (ED-T04).
    /// </summary>
    private static IReadOnlyDictionary<string, (ClassGraph Class, TranslatedClass Translated)> BuildClassesByGeneratedPath(EditorContext context, Project project)
    {
        var classesByGeneratedPath = new Dictionary<string, (ClassGraph, TranslatedClass)>(StringComparer.Ordinal);
        foreach (ClassGraph cls in project.Classes)
        {
            try
            {
                TranslatedClass translated = new ClassTranslator(context.Extensions.Current.Translation).Translate(cls);
                classesByGeneratedPath[ProjectFiles.GetGeneratedFilePath(project.GetGraphFilePath(cls))] = (cls, translated);
            }
            catch (TranslationException)
            {
                // Already translated once for the preceding save; left unmapped here is harmless.
            }
        }

        return classesByGeneratedPath;
    }

    private static void SetBuildOutcome(Project project, IReadOnlyList<CodeDiagnostic> diagnostics, bool success, string? assemblyPath)
    {
        project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(diagnostics);
        project.LastCompilationSucceeded = success;
        project.LastCompiledAssemblyPath = success ? assemblyPath : null;
        project.CompilationMessage = success
            ? "Build succeeded"
            : $"Build failed with {diagnostics.Count(d => d.Severity == CodeDiagnosticSeverity.Error)} error(s)";
    }

    private static string RenderForBuild(EditorContext context, Project project, ClassGraph cls)
    {
        try
        {
            return RenderGenerated(context, project, cls);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ClassTranslationFailure(cls, ex);
        }
    }

    private sealed class ClassTranslationFailure(ClassGraph cls, Exception inner) : Exception(inner.Message, inner)
    {
        public ClassGraph Class { get; } = cls;
    }

    /// <summary>
    /// Compiles a project and starts the compiled program on success (shared by the main and
    /// class windows).
    /// </summary>
    public static async Task CompileAndRunAsync(Project project, EditorContext context)
    {
        if (!await CompileAsync(project, context))
        {
            return;
        }

        try
        {
            ProcessStartRequest request = context.Projects.GetRunCommand(project.Path);
            context.Processes.Start(request);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to run project", ex.ToString());
        }
    }

    /// <summary>Adds a uniquely named class (MyClass, MyClass1, ...) (PAR-12).</summary>
    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NewClassAsync()
    {
        try
        {
            if (Project is { } project)
            {
                project.CreateNewClass(ResolveProfile(project));
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create class", ex.ToString());
        }
    }

    /// <summary>
    /// The profile the project's <c>NetPrintsProfile</c> names, or the default profile when no loaded extension provides it
    /// (the <c>NPD005</c> warning was shown on load, extension-points.md §5).
    /// </summary>
    private IProjectProfile ResolveProfile(Project project) =>
        project.Snapshot is { } snapshot && context.Extensions.Current.FindProfile(snapshot.ProfileId) is { } profile
            ? profile
            : DefaultProjectProfile.Instance;

    /// <summary>Adds an existing *.netpc.json class, copied into the project folder (PAR-13).</summary>
    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task AddExistingClassAsync()
    {
        if (Project is null)
        {
            return;
        }

        string? path = await context.FilePicker.OpenFileAsync("Add Existing Class", [FileFilter.ClassFiles]);
        if (path is null)
        {
            return;
        }

        try
        {
            await context.Persistence.AddGraphAsync(Project, path, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to load existing class",
                $"Failed to load existing class at path {path}:\n\n{ex}");
        }
    }

    /// <summary>Opens the editor window of a class, reusing an open one (PAR-11).</summary>
    [RelayCommand]
    private void OpenClass(ClassGraph? cls)
    {
        if (cls is null || context.Windows.TryActivateClassEditor(cls))
        {
            return;
        }

        context.Windows.OpenClassEditor(cls, context);
    }

    /// <summary>Closes the window of a class and removes it from the project (PAR-11, fixes the WPF defect).</summary>
    [RelayCommand]
    private void RemoveClass(ClassGraph? cls)
    {
        if (cls is null || Project is null)
        {
            return;
        }

        context.Windows.CloseClassEditor(cls);
        Project.Classes.Remove(cls);
    }

    /// <summary>Opens the References dialog (PAR-08).</summary>
    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task ShowReferencesAsync()
    {
        if (Project is null)
        {
            return;
        }

        using var references = new ReferenceListVM(Project, context);
        await context.Dialogs.ShowReferencesAsync(references);
    }

    /// <summary>
    /// Opens the class whose graph file is <paramref name="path"/> (project-relative or absolute), reusing an open window.
    /// The node is not selected: navigating to a node arrives with the error list (sub-phase I).
    /// </summary>
    private bool FocusDocument(string path, string? nodeId)
    {
        if (Project is not { } project)
        {
            return false;
        }

        string projectDirectory = Path.GetDirectoryName(project.Path) ?? "";
        string fullPath = Path.GetFullPath(path, projectDirectory);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        ClassGraph? cls = project.Classes.FirstOrDefault(c => string.Equals(Path.GetFullPath(project.GetGraphFilePath(c)), fullPath, comparison));
        if (cls is null)
        {
            return false;
        }

        OpenClass(cls);
        return true;
    }

    /// <summary>Called when the main window closes (PAR-14).</summary>
    public void OnMainWindowClosed()
    {
        Dispose();
        context.Windows.CloseAllClassEditors();
    }

    /// <summary>Unsubscribes <see cref="hostChannelBridge"/> from the host channel.</summary>
    public void Dispose() => hostChannelBridge.Dispose();
}
