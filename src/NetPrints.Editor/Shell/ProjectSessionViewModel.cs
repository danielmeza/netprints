using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Translator;

namespace NetPrints.Editor.Shell;

/// <summary>
/// The open project and what can be done with it: the per-class undo stacks, save, compile, run and stop.
/// Created when a project opens and disposed when it is unloaded. The compile and run flows are shared with
/// the class editor windows through the static overloads.
/// </summary>
public sealed class ProjectSessionViewModel : ObservableObject, IDisposable
{
    private const int ProjectKeyLength = 16;

    private readonly EditorContext context;
    private readonly Dictionary<ClassGraph, UndoRedoStack> undoStacks = [];
    private Task<bool>? saving;
    private CancellationTokenSource? runCancellation;
    private bool wasRunning;

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
    }

    /// <summary>Gets the open project.</summary>
    public Project Project { get; }

    /// <summary>Gets the path of the project's <c>.csproj</c>.</summary>
    public string ProjectFilePath => Project.Path;

    /// <summary>Gets the first <c>16</c> hex characters of the SHA-256 of the project's full path; names its per-user state.</summary>
    public string ProjectKey => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(ProjectFilePath))))[..ProjectKeyLength];

    /// <summary>Gets whether the last started program has not exited yet; raises a change (on the UI thread) when that flips.</summary>
    public bool IsRunning => context.RunState.Snapshot().Phase == RunPhase.Running;

    /// <summary>Gets the undo stack of a class; the same instance on every call.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns>The stack, created on first use.</returns>
    public UndoRedoStack UndoStackFor(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        if (!undoStacks.TryGetValue(cls, out UndoRedoStack? stack))
        {
            stack = new UndoRedoStack();
            undoStacks[cls] = stack;
        }

        return stack;
    }

    /// <summary>
    /// Saves every edited class through <see cref="ProjectPersistence"/>. A call made while a save runs returns that
    /// save's result instead of starting another. A failure is shown through <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <returns>Whether the save succeeded.</returns>
    public Task<bool> SaveAllAsync()
    {
        if (saving is { IsCompleted: false } current)
        {
            return current;
        }

        saving = SaveCoreAsync();
        return saving;
    }

    /// <summary>Saves, when needed, then compiles the project (see <see cref="CompileAsync(Project, EditorContext)"/>).</summary>
    /// <returns>Whether the build succeeded.</returns>
    public async Task<bool> CompileAsync()
    {
        await WaitForSaveAsync().ConfigureAwait(true);
        return await CompileAsync(Project, context).ConfigureAwait(true);
    }

    /// <summary>
    /// Compiles, then starts the program when the build succeeded. <see cref="Stop"/> cancels the token the program
    /// was started with, which kills it and its child processes.
    /// </summary>
    /// <returns>Whether the program was started.</returns>
    public async Task<bool> RunAsync()
    {
        await WaitForSaveAsync().ConfigureAwait(true);
        runCancellation?.Dispose();
        runCancellation = new CancellationTokenSource();
        return await CompileAndRunAsync(Project, context, runCancellation.Token).ConfigureAwait(true);
    }

    /// <summary>Stops the running program and its child processes; does nothing when none runs.</summary>
    public void Stop() => runCancellation?.Cancel();

    /// <summary>Stops following the run state and releases the run token.</summary>
    public void Dispose()
    {
        context.RunState.PhaseChanged -= OnRunPhaseChanged;
        runCancellation?.Dispose();
        runCancellation = null;
    }

    /// <summary>
    /// Saves the project's dirty classes, builds it through <see cref="IProjectSystem.BuildAsync"/>
    /// and maps the outcome onto <see cref="Core.Project.IsCompiling"/>,
    /// <see cref="Core.Project.LastCompilationSucceeded"/> and <see cref="Core.Project.LastDiagnostics"/>
    /// (shared by the main and class windows). A class that cannot be translated while saving is
    /// reported as a build error; any other failure is shown through
    /// <see cref="IEditorDialogs.ShowErrorAsync"/>.
    /// </summary>
    /// <param name="project">The project to build.</param>
    /// <param name="context">Host services.</param>
    /// <returns>Whether the build succeeded.</returns>
    public static async Task<bool> CompileAsync(Project project, EditorContext context)
    {
        project.IsCompiling = true;
        project.CompilationMessage = "Compiling...";
        context.RunState.BuildStarted();
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
            context.RunState.BuildFinished();
        }
    }

    /// <summary>
    /// Compiles a project and starts the compiled program on success (shared by the main and
    /// class windows).
    /// </summary>
    /// <param name="project">The project to run.</param>
    /// <param name="context">Host services.</param>
    /// <param name="cancellationToken">Cancelling it kills the started program.</param>
    /// <returns>Whether the program was started.</returns>
    public static async Task<bool> CompileAndRunAsync(Project project, EditorContext context, CancellationToken cancellationToken = default)
    {
        if (!await CompileAsync(project, context))
        {
            return false;
        }

        try
        {
            ProcessStartRequest request = context.Projects.GetRunCommand(project.Path);
            context.Processes.Start(request, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to run project", ex.ToString());
            return false;
        }
    }

    private void OnRunPhaseChanged(object? sender, EventArgs e) => context.Dispatcher.Post(() =>
    {
        bool running = IsRunning;
        if (running != wasRunning)
        {
            wasRunning = running;
            OnPropertyChanged(nameof(IsRunning));
        }
    });

    private Task WaitForSaveAsync() => saving is { IsCompleted: false } current ? current : Task.CompletedTask;

    private async Task<bool> SaveCoreAsync()
    {
        try
        {
            ProjectSaveResult result = await context.Persistence.SaveAsync(Project, cls => RenderGenerated(context, Project, cls), CancellationToken.None);
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

    /// <summary>
    /// Marks a translation failure during compile as one that must abort the whole build (a
    /// <see cref="ClassTranslationAbortException"/>), rather than being isolated per class by
    /// <see cref="ProjectPersistence.SaveAsync"/> (F-07).
    /// </summary>
    private sealed class ClassTranslationFailure(ClassGraph cls, Exception inner) : ClassTranslationAbortException(inner.Message, inner)
    {
        public ClassGraph Class { get; } = cls;
    }
}
