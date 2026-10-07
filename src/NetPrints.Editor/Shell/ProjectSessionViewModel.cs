using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.State;
using NetPrints.Editor.UndoRedo;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.Shell;

/// <summary>
/// The open project and what can be done with it: the per-class undo stacks, save, compile, run and stop.
/// Created when a project opens and disposed when it is unloaded. It owns every compile and run, including the
/// class editor windows', so Stop reaches them all.
/// </summary>
public sealed class ProjectSessionViewModel : ObservableObject, IDisposable
{
    private const string BuildSucceededText = "Build succeeded";

    private readonly EditorContext context;
    private readonly Dictionary<ClassGraph, UndoRedoStack> undoStacks = [];
    private readonly Dictionary<ClassGraph, string> classPaths = [];
    private readonly Dictionary<ClassGraph, ClassContext> contexts = [];
    private Task<bool>? saving;
    private bool saveRequested;
    private Task<bool>? flow;
    private CancellationTokenSource? runCancellation;
    private bool wasRunning;
    private bool building;
    private EventHandler? commandStatesChanged;
    private EventHandler<SessionStatus>? statusReported;

    /// <summary>Creates the session of <paramref name="project"/>.</summary>
    /// <param name="project">The open project.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public ProjectSessionViewModel(Project project, EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(context);
        Project = project;
        this.context = context;
        wasRunning = IsRunning;
        context.RunState.PhaseChanged += OnRunPhaseChanged;
        project.PropertyChanged += OnProjectPropertyChanged;
        project.Classes.CollectionChanged += OnClassesChanged;
        Unsaved = new UnsavedChangesTracker(this);
        context.CodeAnalysis.RequestAnalysis(project);
    }

    /// <summary>Gets which files of the project have unsaved changes.</summary>
    public UnsavedChangesTracker Unsaved { get; }

    /// <summary>Gets the open project.</summary>
    public Project Project { get; }

    /// <summary>Gets the path of the project's <c>.csproj</c>.</summary>
    public string ProjectFilePath => Project.Path;

    /// <summary>Gets the first <c>16</c> hex characters of the SHA-256 of the project's full path; names its per-user state.</summary>
    public string ProjectKey => EditorDataPaths.ProjectKey(ProjectFilePath);

    /// <summary>Gets whether the last started program has not exited yet; raises a change (on the UI thread) when that flips.</summary>
    public bool IsRunning => context.RunState.Snapshot().Phase == RunPhase.Running;

    /// <summary>Gets whether a compile or run is in flight (waiting for a save, building) and has not started its program yet.</summary>
    public bool IsBuilding => building;

    /// <summary>Raised, on the UI thread, when a command's enabled state or label may have changed: building or running starts or ends, the project starts or stops compiling, or an undo history changes.</summary>
    public event EventHandler? CommandStatesChanged
    {
        add => commandStatesChanged += value;
        remove => commandStatesChanged -= value;
    }

    /// <summary>Raised, on the UI thread, when an undo, a redo, a build or the program has something to say in the status bar.</summary>
    public event EventHandler<SessionStatus>? StatusReported
    {
        add => statusReported += value;
        remove => statusReported -= value;
    }

    /// <summary>Raised, on the UI thread, when a member asks for one of the class's graphs to be opened (a variable's getter, setter or type graph).</summary>
    public event EventHandler<NodeGraph>? GraphOpenRequested;

    /// <summary>Raised after the members or accessor graphs of any class of the session changed, by an edit, an undo or a redo.</summary>
    public event EventHandler? MembersChanged;

    /// <summary>
    /// Gets the path of a class's graph file relative to the project, with <c>/</c> separators (the class path of its <see cref="DocumentId"/>s).
    /// The path is fixed the first time it is asked for, so renaming a class that has never been saved, or changing its namespace, does not change
    /// the ids of its open documents.
    /// </summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>The relative path.</returns>
    public string ClassPathOf(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        if (!classPaths.TryGetValue(cls, out string? path))
        {
            path = ClassPaths.Of(Project, cls);
            classPaths[cls] = path;
        }

        return path;
    }

    /// <summary>Finds the class whose <see cref="ClassPathOf"/> is <paramref name="classPath"/>.</summary>
    /// <param name="classPath">The class path of a document id.</param>
    /// <returns>The class, or null when the project has none at that path.</returns>
    public ClassGraph? FindClass(string classPath) =>
        Project.Classes.FirstOrDefault(cls => string.Equals(ClassPathOf(cls), classPath, StringComparison.Ordinal));

    /// <summary>Gets the undo stack of a class; the same instance on every call.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>The stack, created on first use.</returns>
    public UndoRedoStack UndoStackFor(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        if (!undoStacks.TryGetValue(cls, out UndoRedoStack? stack))
        {
            stack = new UndoRedoStack();
            if (!cls.IsDirty)
            {
                stack.MarkSaved();
            }

            stack.Changed += OnUndoChanged;
            undoStacks[cls] = stack;
        }

        return stack;
    }

    /// <summary>Gets the context of a class: the one holder of its undo stack, member view models and inspectors, created on first use and disposed with the class or the session.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>The class's one context.</returns>
    public ClassContext ContextFor(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        if (!contexts.ContainsKey(cls))
        {
            var created = new ClassContext(cls, context, UndoStackFor(cls), () => Project.Classes);
            created.MembersChanged += OnContextMembersChanged;
            created.DirtyChanged += OnContextDirtyChanged;
            created.Edited += OnContextEdited;
            created.Messenger.Register<ProjectSessionViewModel, OpenGraphMessage>(this, static (session, message) => session.GraphOpenRequested?.Invoke(session, message.Graph));
            contexts[cls] = created;
        }

        return contexts[cls];
    }

    /// <summary>
    /// Saves every edited class through <see cref="ProjectPersistence"/>. A call made while a save runs makes
    /// that save run once more when it ends, so an edit made meanwhile is written; every such call gets the task of the
    /// running save, which completes after the follow-up. Each class's undo stack is marked saved at the position it had when its save started.
    /// A failure is shown through <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <returns>Whether the save succeeded.</returns>
    public Task<bool> SaveAllAsync() => StartSaveAsync(null);

    /// <summary>
    /// Saves the graph file of one class when it is unsaved. A call made while a save runs makes that save run once more
    /// for every unsaved file when it ends, as <see cref="SaveAllAsync"/> does.
    /// </summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>Whether the save succeeded.</returns>
    public Task<bool> SaveAsync(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        return StartSaveAsync(cls);
    }

    /// <summary>Undoes the last command of a class and reports <c>Undid: &lt;action&gt;</c>.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>Whether a command was undone.</returns>
    public bool Undo(ClassGraph cls)
    {
        UndoRedoStack stack = UndoStackFor(cls);
        string? name = stack.UndoName;
        bool undone = stack.Undo();
        if (undone)
        {
            Report($"Undid: {name}", SessionStatus.TransientLifetime);
        }

        return undone;
    }

    /// <summary>Redoes the last undone command of a class and reports <c>Redid: &lt;action&gt;</c>.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>Whether a command was redone.</returns>
    public bool Redo(ClassGraph cls)
    {
        UndoRedoStack stack = UndoStackFor(cls);
        string? name = stack.RedoName;
        bool redone = stack.Redo();
        if (redone)
        {
            Report($"Redid: {name}", SessionStatus.TransientLifetime);
        }

        return redone;
    }

    /// <summary>Raised, on the UI thread, after every edit, undo or redo that leaves a class unsaved, with that class.</summary>
    public event EventHandler<ClassGraph>? ClassEdited;

    /// <summary>Raised, on the UI thread, after a save succeeded, with the number of files it wrote.</summary>
    public event EventHandler<int>? Saved;

    /// <summary>
    /// Saves, when needed, then builds the project through <see cref="IProjectSystem.BuildAsync"/> and maps the outcome
    /// onto the project's compile state. One compile or run is in flight per session: a call made meanwhile returns
    /// that flow's task instead of starting another. A class that cannot be translated is reported as a build error;
    /// any other failure is shown through <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <returns>Whether the build succeeded.</returns>
    public Task<bool> CompileAsync()
    {
        if (flow is { IsCompleted: false } current)
        {
            return current;
        }

        flow = CompileFlowAsync();
        return flow;
    }

    /// <summary>
    /// Compiles, then starts the program when the build succeeded. One run per session: a call made while a compile
    /// or run is in flight returns that flow's task, and one made while the program runs does nothing and returns
    /// false. <see cref="Stop"/> cancels the token the program was started with, which kills it and its child processes.
    /// </summary>
    /// <returns>Whether the program was started.</returns>
    public Task<bool> RunAsync()
    {
        if (flow is { IsCompleted: false } current)
        {
            return current;
        }

        if (IsRunning)
        {
            return Task.FromResult(false);
        }

        flow = RunFlowAsync();
        return flow;
    }

    /// <summary>Gets a task that completes when the compile or run in flight has built the project, or is already complete when none is.</summary>
    /// <returns>The task to await.</returns>
    public Task WaitForBuildAsync() => flow is { IsCompleted: false } current ? current : Task.CompletedTask;

    /// <summary>Stops the running program and its child processes; does nothing when none runs.</summary>
    public void Stop() => runCancellation?.Cancel();

    /// <summary>Stops following the run state, kills the running program (an unloaded project leaves none behind) and releases the run token.</summary>
    public void Dispose()
    {
        context.RunState.PhaseChanged -= OnRunPhaseChanged;
        Project.PropertyChanged -= OnProjectPropertyChanged;
        Project.Classes.CollectionChanged -= OnClassesChanged;
        foreach (ClassContext classContext in contexts.Values)
        {
            classContext.Dispose();
        }

        contexts.Clear();
        foreach (UndoRedoStack stack in undoStacks.Values)
        {
            stack.Changed -= OnUndoChanged;
        }

        runCancellation?.Cancel();
        runCancellation?.Dispose();
        runCancellation = null;
    }

    private async Task<bool> CompileFlowAsync()
    {
        SetBuilding(true);
        try
        {
            await WaitForSaveAsync().ConfigureAwait(true);
            return await BuildAsync().ConfigureAwait(true);
        }
        finally
        {
            SetBuilding(false);
        }
    }

    private async Task<bool> RunFlowAsync()
    {
        SetBuilding(true);
        try
        {
            await WaitForSaveAsync().ConfigureAwait(true);
            runCancellation?.Dispose();
            runCancellation = new CancellationTokenSource();
            return await BuildAndStartAsync(runCancellation.Token).ConfigureAwait(true);
        }
        finally
        {
            SetBuilding(false);
        }
    }

    private void SetBuilding(bool value)
    {
        building = value;
        OnPropertyChanged(nameof(IsBuilding));
        RaiseCommandStatesChanged();
    }

    private void RaiseCommandStatesChanged() => commandStatesChanged?.Invoke(this, EventArgs.Empty);

    private void Report(string text, TimeSpan? expiry = null) => statusReported?.Invoke(this, new SessionStatus(text, expiry));

    private void OnUndoChanged(object? sender, EventArgs e) => RaiseCommandStatesChanged();

    private void OnContextMembersChanged(object? sender, EventArgs e) => MembersChanged?.Invoke(this, EventArgs.Empty);

    private void OnContextEdited(object? sender, EventArgs e)
    {
        if (sender is ClassContext edited)
        {
            ClassEdited?.Invoke(this, edited.Class);
        }
    }

    private void OnContextDirtyChanged(object? sender, EventArgs e) => RaiseCommandStatesChanged();

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (ClassGraph removed in contexts.Keys.Where(cls => !Project.Classes.Contains(cls)).ToList())
        {
            contexts.Remove(removed, out ClassContext? classContext);
            classContext?.Dispose();
        }

        foreach (ClassGraph removed in classPaths.Keys.Where(cls => !Project.Classes.Contains(cls)).ToList())
        {
            classPaths.Remove(removed);
        }

        context.CodeAnalysis.RequestAnalysis(Project);
        RaiseCommandStatesChanged();
    }

    private void OnProjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Core.Project.IsCompiling) or nameof(Core.Project.OutputBinaryType))
        {
            RaiseCommandStatesChanged();
        }
    }

    /// <summary>
    /// Saves the project's dirty classes, builds it through <see cref="IProjectSystem.BuildAsync"/>
    /// and maps the outcome onto <see cref="Core.Project.IsCompiling"/>,
    /// <see cref="Core.Project.LastCompilationSucceeded"/> and <see cref="Core.Project.LastDiagnostics"/>
    /// A class that cannot be translated while saving is reported as a build error; any other
    /// failure is shown through <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <returns>Whether the build succeeded.</returns>
    private async Task<bool> BuildAsync()
    {
        Project project = Project;
        project.IsCompiling = true;
        project.CompilationMessage = "Compiling...";
        context.RunState.BuildStarted();
        try
        {
            int unsavedFiles = Unsaved.CountUnsaved(null);
            await SaveClassesAsync(project.Classes, cls => RenderForBuild(context, project, cls)).ConfigureAwait(true);
            Saved?.Invoke(this, unsavedFiles);
            BuildResult result = await context.Projects.BuildAsync(project.Path, CancellationToken.None);
            var classesByGeneratedPath = BuildClassesByGeneratedPath(context, project);
            ReportBuild(result.Success, SetBuildOutcome(project, DiagnosticMapper.FromBuild(result.Messages, classesByGeneratedPath), result.Success, result.OutputAssemblyPath));
            return result.Success;
        }
        catch (ClassTranslationFailure failure)
        {
            var diagnostic = new CodeDiagnostic(CodeDiagnosticSeverity.Error, TranslationDiagnosticCodes.Unclassified,
                $"{failure.Class.FullName}: {failure.Message}", failure.Class.FullName, null, null, null, null);
            ReportBuild(false, SetBuildOutcome(project, [diagnostic], false, null));
            return false;
        }
        catch (Exception ex)
        {
            project.LastCompilationSucceeded = false;
            project.CompilationMessage = "Build failed";
            Report("Build failed");
            await context.Dialogs.ShowErrorAsync("Failed to build project", ex.ToString());
            return false;
        }
        finally
        {
            project.IsCompiling = false;
            context.RunState.BuildFinished();
        }
    }

    /// <summary>
    /// Compiles the project and starts the compiled program on success, unless the token was cancelled meanwhile.
    /// </summary>
    /// <param name="cancellationToken">Cancelling it kills the started program.</param>
    /// <returns>Whether the program was started.</returns>
    private async Task<bool> BuildAndStartAsync(CancellationToken cancellationToken = default)
    {
        if (!await BuildAsync() || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            ProcessStartRequest request = context.Projects.GetRunCommand(Project.Path);
            context.Processes.Start(request, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to run project", ex.ToString());
            return false;
        }
    }

    private void ReportBuild(bool success, int errors) => Report(success ? BuildSucceededText : $"Build failed: {errors} error(s)");

    private void OnRunPhaseChanged(object? sender, EventArgs e) => context.Dispatcher.Post(() =>
    {
        bool running = IsRunning;
        if (running != wasRunning)
        {
            wasRunning = running;
            OnPropertyChanged(nameof(IsRunning));
            RaiseCommandStatesChanged();
            ReportRunPhase(running);
        }
    });

    private void ReportRunPhase(bool running)
    {
        if (running)
        {
            Report("Running…");
        }
        else if (context.RunState.Snapshot() is { Phase: RunPhase.Exited, ExitCode: { } code })
        {
            Report($"Exited with code {code}");
        }
    }

    private Task WaitForSaveAsync() => saving is { IsCompleted: false } current ? current : Task.CompletedTask;

    private Task<bool> StartSaveAsync(ClassGraph? only)
    {
        if (saving is { IsCompleted: false } current)
        {
            saveRequested = true;
            return current;
        }

        saving = SaveCoreAsync(only);
        return saving;
    }

    private async Task<bool> SaveCoreAsync(ClassGraph? only)
    {
        bool saved;
        int files = 0;
        do
        {
            saveRequested = false;
            int before = Unsaved.CountUnsaved(only);
            saved = await SaveOnceAsync(only).ConfigureAwait(true);
            files += saved ? before : 0;
            only = null;
        }
        while (saved && saveRequested);

        if (saved)
        {
            Saved?.Invoke(this, files);
        }

        RaiseCommandStatesChanged();
        return saved;
    }

    private async Task<bool> SaveOnceAsync(ClassGraph? only)
    {
        IReadOnlyCollection<ClassGraph> classes = only is null ? Project.Classes : [only];
        try
        {
            ProjectSaveResult result = await SaveClassesAsync(classes, cls => RenderGenerated(context, Project, cls)).ConfigureAwait(true);
            if (only is null)
            {
                Unsaved.ClearProjectChangePending();
            }

            if (result.Diagnostics.Count > 0)
            {
                Project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(result.Diagnostics);
            }

            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to save project", ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// The one path every save takes: captures each class's undo position before writing and, once the write succeeds,
    /// moves the stack's saved marker there. <see cref="ProjectPersistence"/> marks a class clean only if it was not edited meanwhile.
    /// </summary>
    private async Task<ProjectSaveResult> SaveClassesAsync(IReadOnlyCollection<ClassGraph> classes, Func<ClassGraph, string> render)
    {
        var points = undoStacks.Where(pair => classes.Contains(pair.Key)).Select(pair => (pair.Value, Point: pair.Value.CapturePosition())).ToList();
        ProjectSaveResult result = await context.Persistence.SaveAsync(Project, classes, render, CancellationToken.None).ConfigureAwait(true);
        points.ForEach(entry => entry.Value.MarkSaved(entry.Point));
        return result;
    }

    /// <summary>Renders a class's generated C# file the same way a build would (project-system.md §3).</summary>
    private static string RenderGenerated(EditorContext context, Project project, ClassGraph cls) =>
        GraphCodeGenerator.RenderFile(new ClassTranslator(context.Extensions.Current.Translation).Translate(cls), Path.GetFileName(project.GetGraphFilePath(cls)));

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

    private static int SetBuildOutcome(Project project, IReadOnlyList<CodeDiagnostic> diagnostics, bool success, string? assemblyPath)
    {
        int errors = diagnostics.Count(d => d.Severity == CodeDiagnosticSeverity.Error);
        project.LastDiagnostics = new ObservableRangeCollection<CodeDiagnostic>(diagnostics);
        project.LastCompilationSucceeded = success;
        project.LastCompiledAssemblyPath = success ? assemblyPath : null;
        project.CompilationMessage = success ? BuildSucceededText : $"Build failed with {errors} error(s)";
        return errors;
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

    /// <summary>
    /// Marks a translation failure during compile as one that must abort the whole build (a
    /// <see cref="ClassTranslationAbortException"/>), rather than being isolated per class by
    /// <c>ProjectPersistence.SaveAsync</c> (F-07).
    /// </summary>
    private sealed class ClassTranslationFailure(ClassGraph cls, Exception inner) : ClassTranslationAbortException(inner.Message, inner)
    {
        public ClassGraph Class { get; } = cls;
    }
}
