using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Generator;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.Main;

/// <summary>
/// View model of the main window: project lifecycle, settings, references and the class list
/// (PAR-01..15).
/// </summary>
public sealed partial class MainEditorVM : ObservableObject
{
    private readonly EditorContext context;
    private Project? subscribedProject;

    /// <summary>
    /// Creates the main window's view model, optionally with a project already open.
    /// </summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="project">Initially open project, or <see langword="null"/>.</param>
    public MainEditorVM(EditorContext context, Project? project = null)
    {
        this.context = context;
        Project = project;
    }

    /// <summary>Host services shared across the editor.</summary>
    public EditorContext Context => context;

    /// <summary>The open project, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectOpen), nameof(CanCompile), nameof(CanCompileAndRun), nameof(Title))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(CompileCommand), nameof(RunCommand),
        nameof(ShowReferencesCommand), nameof(NewClassCommand), nameof(AddExistingClassCommand), nameof(ToggleSettingsPaneCommand))]
    public partial Project? Project { get; set; }

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
                _ = ApplyOutputTypeAsync(project, value);
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
            _ = ReloadReflectionAsync();
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
                    _ = ReloadReflectionAsync();
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
                _ = ReloadReflectionAsync();
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
    /// example an old <c>.netpp</c>) shows a message and nothing is opened or written
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
            ProjectLoadResult loaded = await context.Persistence.LoadAsync(path, CancellationToken.None);

            context.Windows.CloseAllClassEditors();
            Project = loaded.Project;
            IsBusy = false;

            if (loaded.Issues.Count > 0)
            {
                await context.Dialogs.ShowErrorAsync("Project loaded with issues",
                    string.Join("\n\n", loaded.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
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
            await context.Persistence.SaveAsync(project, cls => RenderGenerated(project, cls), CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
            return false;
        }
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private static string RenderGenerated(Project project, ClassGraph cls) =>
        GraphCodeGenerator.RenderFile(new ClassTranslator().TranslateClass(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

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
            await context.Persistence.SaveAsync(project, cls => RenderForBuild(project, cls), CancellationToken.None);
            BuildResult result = await context.Projects.BuildAsync(project.Path, CancellationToken.None);
            SetBuildOutcome(project, DiagnosticMapper.FromBuild(result.Messages), result.Success, result.OutputAssemblyPath);
            return result.Success;
        }
        catch (ClassTranslationFailure failure)
        {
            // Interim id until the translator reports coded TranslationExceptions (T086).
            var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, "NPT000",
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

    private static void SetBuildOutcome(Project project, IReadOnlyList<CodeDiagnostic> diagnostics, bool success, string? assemblyPath)
    {
        project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(diagnostics);
        project.LastCompilationSucceeded = success;
        project.LastCompiledAssemblyPath = success ? assemblyPath : null;
        project.CompilationMessage = success
            ? "Build succeeded"
            : $"Build failed with {diagnostics.Count(d => d.Severity == CodeDiagnosticSeverity.Error)} error(s)";
    }

    private static string RenderForBuild(Project project, ClassGraph cls)
    {
        try
        {
            return RenderGenerated(project, cls);
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
            Project?.CreateNewClass(DefaultProjectProfile.Instance);
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create class", ex.ToString());
        }
    }

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

        context.Windows.OpenClassEditor(new ClassEditorVM(cls, context));
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
    private Task ShowReferencesAsync() =>
        Project is null ? Task.CompletedTask : context.Dialogs.ShowReferencesAsync(new ReferenceListVM(Project, context));

    /// <summary>Called when the main window closes (PAR-14).</summary>
    public void OnMainWindowClosed() => context.Windows.CloseAllClassEditors();
}
