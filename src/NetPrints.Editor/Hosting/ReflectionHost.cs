using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Builds the reflection provider for a project off the UI thread and publishes it on the UI
/// thread (PAR-15). References are resolved with <see cref="ReferenceAssemblyResolver"/>, so
/// .NET Framework references fall back to the running runtime's assemblies on Linux/macOS.
/// </summary>
public sealed class ReflectionHost : IReflectionHost
{
    private readonly IUiDispatcher dispatcher;
    private readonly ObservableRangeCollection<TypeSpecifier> nonStaticTypes = [];
    private readonly TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReflectionProvider? provider;
    private int reloadVersion;

    /// <summary>
    /// Creates a reflection host that publishes reloaded providers through <paramref name="dispatcher"/>.
    /// </summary>
    /// <param name="dispatcher">Dispatcher used to publish a reloaded provider on the UI thread.</param>
    public ReflectionHost(IUiDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        NonStaticTypes = new ReadOnlyObservableCollection<TypeSpecifier>(nonStaticTypes);
    }

    /// <inheritdoc/>
    public bool IsLoaded => provider is not null;

    /// <inheritdoc/>
    public Task Loaded => loaded.Task;

    /// <inheritdoc/>
    public IReflectionProvider Provider =>
        provider ?? throw new InvalidOperationException("The reflection provider has not been loaded yet; check IsLoaded or await Loaded.");

    /// <inheritdoc/>
    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> LastWarnings { get; private set; } = [];

    /// <inheritdoc/>
    public event EventHandler? Reloaded;

    /// <summary>
    /// Snapshots the project's references and generated class sources on the calling thread, then
    /// rebuilds and warms up a <see cref="MemoizedReflectionProvider"/>-wrapped <see cref="ReflectionProvider"/>
    /// on a background thread (resolving assembly paths with a fresh <see cref="ReferenceAssemblyResolver"/>,
    /// skipping an unreadable source directory with a warning instead of throwing), and publishes it
    /// on the UI thread via the constructor's dispatcher. If another <see cref="ReloadAsync"/> call
    /// started after this one, this call's result is silently dropped instead of published.
    /// </summary>
    /// <param name="project">Project to build a reflection provider for.</param>
    /// <param name="cancellationToken">Cancels the background build.</param>
    /// <returns>A task that completes once this reload has either published its result or been superseded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="project"/> is <see langword="null"/>.</exception>
    public async Task ReloadAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        int version = Interlocked.Increment(ref reloadVersion);

        // Snapshot the model on the calling (UI) thread; everything else runs in the background.
        var references = project.References.ToList();
        var sources = project.GenerateClassSources(out var translationWarnings).ToList();

        var (newProvider, types, warnings) = await Task.Run(() =>
        {
            var warnings = new List<string>(translationWarnings);
            var assemblyPaths = new ReferenceAssemblyResolver()
                .ResolveAssemblyPaths(references.OfType<AssemblyReference>(), warnings);

            var sourcePaths = new List<string>();
            foreach (var sourceReference in references.OfType<SourceDirectoryReference>())
            {
                try
                {
                    sourcePaths.AddRange(sourceReference.SourceFilePaths);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Warning: source directory skipped: {sourceReference.SourceDirectory} ({ex.Message})");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            IReflectionProvider built = new MemoizedReflectionProvider(new ReflectionProvider(assemblyPaths, sourcePaths, sources));
            var types = built.GetNonStaticTypes().ToList();

            // Warm-up (SC-005): Roslyn binds member symbols lazily, and the first enumeration of all
            // static members (~120k methods on .NET 10) costs about 1.5 s. Doing it here, in the
            // background load, keeps the first node search after a project opens well under 2 s.
            cancellationToken.ThrowIfCancellationRequested();
            _ = built.GetMethods(new ReflectionProviderMethodQuery().WithStatic(true)).Count();
            _ = built.GetVariables(new ReflectionProviderVariableQuery().WithStatic(true)).Count();
            return (built, types, warnings);
        }, cancellationToken).ConfigureAwait(false);

        await dispatcher.InvokeAsync(() =>
        {
            // A newer reload started meanwhile: drop this result.
            if (version != Volatile.Read(ref reloadVersion))
            {
                return;
            }

            provider = newProvider;
            LastWarnings = warnings;
            nonStaticTypes.ReplaceRange(types);
            loaded.TrySetResult();
            Reloaded?.Invoke(this, EventArgs.Empty);
        }).ConfigureAwait(false);
    }
}
