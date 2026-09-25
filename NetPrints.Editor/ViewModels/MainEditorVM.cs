using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Services;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// View model of the main window: project lifecycle, settings, references and the class list
/// (PAR-01..15).
/// </summary>
public sealed partial class MainEditorVM : ObservableObject
{
    private readonly EditorContext context;
    private Project? subscribedProject;

    public MainEditorVM(EditorContext context, Project? project = null)
    {
        this.context = context;
        Project = project;
    }

    public EditorContext Context => context;

    /// <summary>The open project, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectOpen), nameof(CanCompile), nameof(CanCompileAndRun), nameof(Title))]
    [NotifyCanExecuteChangedFor(nameof(SaveProjectCommand), nameof(CompileCommand), nameof(RunCommand),
        nameof(ShowReferencesCommand), nameof(NewClassCommand), nameof(AddExistingClassCommand), nameof(ToggleSettingsPaneCommand))]
    private Project? project;

    /// <summary>Whether the Project pane (Create/Open/Save) is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    private bool isProjectPaneOpen;

    /// <summary>Whether the Settings pane (output, binary type) is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaneOpen))]
    private bool isSettingsPaneOpen;

    /// <summary>Whether one of the side panes is open; setting false closes both (light dismiss).</summary>
    public bool IsPaneOpen
    {
        get => IsProjectPaneOpen || IsSettingsPaneOpen;
        set
        {
            if (!value)
            {
                IsProjectPaneOpen = false;
                IsSettingsPaneOpen = false;
            }
        }
    }

    /// <summary>Whether a background operation shows the progress overlay.</summary>
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string busyTitle = "";

    [ObservableProperty]
    private string busyMessage = "";

    public bool IsProjectOpen => Project is not null;

    public bool CanCompile => Project?.CanCompile ?? false;

    public bool CanCompileAndRun => Project?.CanCompileAndRun ?? false;

    /// <summary>Window title: the project name (PAR-01).</summary>
    public string Title => Project?.Name is { Length: > 0 } name ? name : "NetPrints";

    public IReadOnlyList<ProjectCompilationOutput> CompilationOutputs { get; } = Enum.GetValues<ProjectCompilationOutput>();

    public IReadOnlyList<BinaryType> BinaryTypes { get; } = Enum.GetValues<BinaryType>();

    public ProjectCompilationOutput CompilationOutput
    {
        get => Project?.CompilationOutput ?? ProjectCompilationOutput.Nothing;
        set
        {
            if (Project is not null && Project.CompilationOutput != value)
            {
                Project.CompilationOutput = value;
                OnPropertyChanged();
            }
        }
    }

    public BinaryType OutputBinaryType
    {
        get => Project?.OutputBinaryType ?? BinaryType.SharedLibrary;
        set
        {
            if (Project is not null && Project.OutputBinaryType != value)
            {
                Project.OutputBinaryType = value;
                OnPropertyChanged();
            }
        }
    }

    // The former Fody hook OnProjectChanged: wire project events and reload reflection (PAR-15).
    partial void OnProjectChanged(Project? value)
    {
        if (subscribedProject is not null)
        {
            subscribedProject.References.CollectionChanged -= OnReferencesChanged;
            ((INotifyPropertyChanged)subscribedProject).PropertyChanged -= OnProjectPropertyChanged;
        }

        subscribedProject = value;

        if (value is not null)
        {
            value.References.CollectionChanged += OnReferencesChanged;
            ((INotifyPropertyChanged)value).PropertyChanged += OnProjectPropertyChanged;
            _ = ReloadReflectionAsync();
        }

        OnPropertyChanged(nameof(CompilationOutput));
        OnPropertyChanged(nameof(OutputBinaryType));

        if (value is null)
        {
            IsSettingsPaneOpen = false;
        }
    }

    private void OnReferencesChanged(object? sender, NotifyCollectionChangedEventArgs e) => _ = ReloadReflectionAsync();

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
            case nameof(Core.Project.CompilationOutput):
                RefreshCompileState();
                OnPropertyChanged(nameof(CompilationOutput));
                OnPropertyChanged(nameof(OutputBinaryType));
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
    /// Creates "MyProject"/"MyNamespace" with default references and asks where to save it.
    /// Cancelling restores the previous project (PAR-03).
    /// </summary>
    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        Project? oldProject = Project;
        var newProject = Core.Project.CreateNew("MyProject", "MyNamespace");

        string? path = await context.FilePicker.SaveFileAsync("Create Project", $"{newProject.Name}.netpp", "netpp",
            [FileFilter.ProjectFiles]);

        if (path is null)
        {
            return;
        }

        newProject.Path = path;
        newProject.Name = System.IO.Path.GetFileNameWithoutExtension(path);

        try
        {
            newProject.Save();
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to save project", $"Failed to save the project at {path}.\n\n{ex}");
            Project = oldProject;
            return;
        }

        context.Windows.CloseAllClassEditors();
        Project = newProject;
        IsProjectPaneOpen = false;
    }

    /// <summary>Opens a project chosen with a *.netpp picker (PAR-04).</summary>
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
    /// Loads a project in the background with the progress overlay. On failure the exception is
    /// shown in an error dialog and copied to the clipboard (PAR-04).
    /// </summary>
    public async Task LoadProjectAsync(string path)
    {
        BusyTitle = "Loading project";
        BusyMessage = path;
        IsBusy = true;

        try
        {
            Project loaded = await Task.Run(() => Core.Project.LoadFromPath(path))
                ?? throw new InvalidDataException($"The file {path} does not contain a NetPrints project.");

            context.Windows.CloseAllClassEditors();
            Project = loaded;
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
    /// Saves the project and all classes, asking for a path first if none is set (PAR-06).
    /// Returns whether the project was saved.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task<bool> SaveProjectAsync()
    {
        IsProjectPaneOpen = false;
        return await PromptProjectSaveAsync();
    }

    internal async Task<bool> PromptProjectSaveAsync()
    {
        if (Project is null)
        {
            return false;
        }

        if (Project.Path is null)
        {
            string? path = await context.FilePicker.SaveFileAsync("Save Project", $"{Project.Name}.netpp", "netpp",
                [FileFilter.ProjectFiles]);

            if (path is null)
            {
                return false;
            }

            Project.Path = path;
        }

        try
        {
            Project.Save();
            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
            return false;
        }
    }

    /// <summary>Compiles the project in the background (PAR-09).</summary>
    [RelayCommand(CanExecute = nameof(CanCompile))]
    private void Compile() => Project?.CompileProject();

    /// <summary>Compiles, then runs the program when the build succeeded (PAR-10).</summary>
    [RelayCommand(CanExecute = nameof(CanCompileAndRun))]
    private Task RunAsync() => Project is null ? Task.CompletedTask : CompileAndRunAsync(Project, context);

    /// <summary>
    /// Compiles a project and starts the compiled program on success (shared by the main and
    /// class windows).
    /// </summary>
    public static async Task CompileAndRunAsync(Project project, EditorContext context)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Core.Project.IsCompiling) && !project.IsCompiling)
            {
                completion.TrySetResult(project.LastCompilationSucceeded);
            }
        }

        var notifier = (INotifyPropertyChanged)project;
        notifier.PropertyChanged += OnChanged;
        try
        {
            project.CompileProject();
            if (!project.IsCompiling)
            {
                // Nothing to compile (e.g. output set to Nothing).
                completion.TrySetResult(false);
            }

            if (!await completion.Task)
            {
                return;
            }
        }
        finally
        {
            notifier.PropertyChanged -= OnChanged;
        }

        try
        {
            var (fileName, arguments) = project.GetRunCommand();
            context.Processes.Start(fileName, arguments);
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
            Project?.CreateNewClass();
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create class", ex.ToString());
        }
    }

    /// <summary>Adds an existing *.netpc class, copied into the project folder (PAR-13).</summary>
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
            Project.AddExistingClass(path);
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
