using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Translator;

namespace NetPrints.Editor.Diagnostics;

/// <summary>
/// <see cref="ICodeAnalysisHost"/> (editor-services.md §2). A <see cref="CodeAnalysisSession"/> is
/// recreated from <see cref="IReflectionHost.Snapshot"/> whenever the injected reflection host raises
/// <see cref="IReflectionHost.Reloaded"/> (Sub-phase I, batch I2 decision: it holds no unmanaged
/// resources per compilation-and-diagnostics.md §3, so recreating it needs no disposal, and reusing a
/// stale one across a snapshot change would analyze against the wrong references); a session already
/// loaded when this host is constructed is adopted immediately, not only on the next reload.
/// Batch I3 decision: construction does real work (loading every reference's metadata and
/// documentation, parsing the project's other sources) but it is small — measured ~30 ms for a
/// realistic 167-reference set — and moving it to a background <c>Task.Run</c> was tried and reverted:
/// with the session recreated on every <see cref="IReflectionHost.Reloaded"/>, back-to-back reloads
/// (already exercised by the test suite's shared fixtures running in parallel) race the background
/// build against the next reload, and a version guard that drops a superseded build can leave
/// <see cref="session"/> stale or briefly <see langword="null"/> instead of always reflecting the
/// latest snapshot synchronously. Kept synchronous on the calling (UI) thread.
/// </summary>
public sealed class CodeAnalysisHost : ICodeAnalysisHost
{
    /// <summary>Debounce window between the last <see cref="RequestAnalysis"/> call and the start of analysis (SC-006).</summary>
    public static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(500);

    private readonly IReflectionHost reflection;
    private readonly IExtensionHost extensions;
    private readonly IUiDispatcher dispatcher;
    private readonly ILogger<CodeAnalysisHost> logger;
    private readonly Subject<Project> requests = new();
    private readonly BehaviorSubject<CodeAnalysisSnapshot> snapshots = new(CodeAnalysisSnapshot.Empty);
    private readonly IDisposable pipeline;
    private volatile CodeAnalysisSession? session;
    private CancellationTokenSource? analysisCts;
    private bool disposed;

    /// <summary>
    /// Creates a host that translates <paramref name="reflection"/>'s project with
    /// <paramref name="extensions"/>' current translation environment, debounces analysis on
    /// <paramref name="scheduler"/> (virtual time in tests, ED-T02) and publishes snapshots through
    /// <paramref name="dispatcher"/>.
    /// </summary>
    /// <param name="reflection">Source of the project's current snapshot and reload notifications.</param>
    /// <param name="extensions">Source of the loaded extensions' translation environment.</param>
    /// <param name="scheduler">Scheduler the 500 ms debounce runs on.</param>
    /// <param name="dispatcher">Dispatcher used to translate on, and publish snapshots on, the UI thread.</param>
    /// <param name="logger">Logger for analysis failures (event 1060).</param>
    public CodeAnalysisHost(IReflectionHost reflection, IExtensionHost extensions, IScheduler scheduler, IUiDispatcher dispatcher, ILogger<CodeAnalysisHost> logger)
    {
        ArgumentNullException.ThrowIfNull(reflection);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        this.reflection = reflection;
        this.extensions = extensions;
        this.dispatcher = dispatcher;
        this.logger = logger;

        reflection.Reloaded += OnReflectionReloaded;
        OnReflectionReloaded(reflection, EventArgs.Empty);
        pipeline = requests.Throttle(DebounceWindow, scheduler).Subscribe(OnDebounced);
    }

    /// <inheritdoc/>
    public IObservable<CodeAnalysisSnapshot> Snapshots => snapshots;

    /// <inheritdoc/>
    public void RequestAnalysis(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ObjectDisposedException.ThrowIf(disposed, this);

        requests.OnNext(project);
    }

    /// <inheritdoc/>
    public async Task<QuickInfo?> GetQuickInfoAsync(string classFullName, int position, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(classFullName);

        CodeAnalysisSession? current = session;
        return current is null
            ? null
            : await current.GetQuickInfoAsync(classFullName, position, cancellationToken).ConfigureAwait(false);
    }

    private void OnReflectionReloaded(object? sender, EventArgs e)
    {
        ProjectSnapshot? snapshot = reflection.Snapshot;
        session = snapshot is null ? null : new CodeAnalysisSession(snapshot.References, snapshot.OtherSources, snapshot.CompilationOptionsJson);
    }

    private void OnDebounced(Project project)
    {
        analysisCts?.Cancel();
        analysisCts?.Dispose();
        var cts = new CancellationTokenSource();
        analysisCts = cts;
        RunAnalysisAsync(project, cts.Token).Forget(logger);
    }

    private async Task RunAnalysisAsync(Project project, CancellationToken cancellationToken)
    {
        try
        {
            ProjectTranslationResult? translation = null;
            await dispatcher.InvokeAsync(() => translation = ProjectTranslation.TranslateAll(project, extensions.Current.Translation)).ConfigureAwait(false);
            if (translation is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var diagnostics = new List<CodeDiagnostic>(translation.Diagnostics);
            if (session is { } currentSession)
            {
                IReadOnlyList<CodeDiagnostic> analyzed = await Task.Run(
                    () => currentSession.AnalyzeAsync([.. translation.Classes.Values], cancellationToken), cancellationToken).ConfigureAwait(false);
                diagnostics.AddRange(analyzed);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new CodeAnalysisSnapshot(translation.Classes, diagnostics);
            await dispatcher.InvokeAsync(() => snapshots.OnNext(snapshot)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.CodeAnalysisFailed(logger, ex, project.Name);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        reflection.Reloaded -= OnReflectionReloaded;
        pipeline.Dispose();
        requests.Dispose();
        snapshots.Dispose();
        analysisCts?.Cancel();
        analysisCts?.Dispose();
    }
}
