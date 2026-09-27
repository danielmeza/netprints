using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Builds the reflection provider for a project off the UI thread and publishes it on the UI
/// thread (PAR-15). References and other sources come directly from <see cref="Project.Snapshot"/>
/// (project-system.md §4: <c>IProjectSystem.LoadAsync</c> already resolved them, MSBuild and NuGet
/// included), so this host itself never touches the file system for a reference.
/// </summary>
public sealed class ReflectionHost : IReflectionHost
{
    private readonly IUiDispatcher dispatcher;
    private readonly IExtensionHost extensions;
    private readonly ILogger<ReflectionHost> logger;
    private readonly ObservableRangeCollection<TypeSpecifier> nonStaticTypes = [];
    private readonly TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReflectionProvider? provider;
    private int reloadVersion;

    /// <summary>
    /// Creates a reflection host that publishes reloaded providers through <paramref name="dispatcher"/>.
    /// </summary>
    /// <param name="dispatcher">Dispatcher used to publish a reloaded provider on the UI thread.</param>
    /// <param name="extensions">Source of the type catalogs and the translation environment of the loaded extensions.</param>
    /// <param name="logger">Logger for reload start/completion/failure (events 1010-1013).</param>
    public ReflectionHost(IUiDispatcher dispatcher, IExtensionHost extensions, ILogger<ReflectionHost> logger)
    {
        this.dispatcher = dispatcher;
        this.extensions = extensions;
        this.logger = logger;
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
    public ProjectSnapshot? Snapshot { get; private set; }

    /// <inheritdoc/>
    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> LastWarnings { get; private set; } = [];

    /// <inheritdoc/>
    public event EventHandler? Reloaded;

    /// <summary>
    /// Snapshots the project's generated class sources on the calling thread, then rebuilds and
    /// warms up a <see cref="MemoizedReflectionProvider"/>-wrapped <see cref="ReflectionProvider"/>
    /// on a background thread from <see cref="Project.Snapshot"/>'s already-resolved references and
    /// other sources, and publishes it on the UI thread via the constructor's dispatcher. If another
    /// <see cref="ReloadAsync"/> call started after this one, this call's result is silently dropped
    /// instead of published.
    /// </summary>
    /// <param name="project">Project to build a reflection provider for; must have been created
    /// through <see cref="Project.FromSnapshot"/>.</param>
    /// <param name="cancellationToken">Cancels the background build.</param>
    /// <returns>A task that completes once this reload has either published its result or been superseded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="project"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="project"/> has no <see cref="Project.Snapshot"/>.</exception>
    public async Task ReloadAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        ProjectSnapshot snapshot = project.Snapshot
            ?? throw new InvalidOperationException(
                $"'{nameof(ReflectionHost)}.{nameof(ReloadAsync)}' requires a project created through '{nameof(Core.Project)}.{nameof(Core.Project.FromSnapshot)}'.");

        int version = Interlocked.Increment(ref reloadVersion);
        Log.ReflectionReloadStarted(logger, project.Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Snapshot the model on the calling (UI) thread; everything else runs in the background.
            var references = snapshot.References;
            var otherSources = snapshot.OtherSources;
            ExtensionRegistry registry = extensions.Current;
            var generatedSources = project.GenerateClassSources(registry.Translation, out var translationWarnings).ToList();
            IReadOnlyList<ITypeCatalog> catalogs = registry.TypeCatalogs;
            IReadOnlySet<string> excludedAssemblyNames = catalogs.SelectMany(catalog => catalog.Info.CoveredAssemblyNames).ToHashSet(StringComparer.Ordinal);

            var (newProvider, types) = await Task.Run(() =>
            {
                var sourceFiles = new List<SourceFile>(otherSources);
                int generatedIndex = 0;
                foreach (string generated in generatedSources)
                {
                    sourceFiles.Add(new SourceFile($"<generated>/{generatedIndex++}.cs", generated));
                }

                cancellationToken.ThrowIfCancellationRequested();
                IReflectionProvider live = new ReflectionProvider(references, sourceFiles, excludedAssemblyNames);
                IReflectionProvider composed = catalogs.Count == 0 ? live : new CompositeReflectionProvider([.. catalogs, live]);
                IReflectionProvider built = new MemoizedReflectionProvider(composed);
                var types = built.GetNonStaticTypes().ToList();

                // Warm-up (SC-005): Roslyn binds member symbols lazily, and the first enumeration of
                // all static members (~120k methods on .NET 10) costs about 1.5 s. Doing it here, in
                // the background load, keeps the first node search after a project opens well under 2 s.
                cancellationToken.ThrowIfCancellationRequested();
                _ = built.GetMethods(new ReflectionProviderMethodQuery().WithStatic(true)).Count();
                _ = built.GetVariables(new ReflectionProviderVariableQuery().WithStatic(true)).Count();
                return (built, types);
            }, cancellationToken).ConfigureAwait(false);

            await dispatcher.InvokeAsync(() =>
            {
                // A newer reload started meanwhile: drop this result.
                if (version != Volatile.Read(ref reloadVersion))
                {
                    return;
                }

                provider = newProvider;
                Snapshot = snapshot;
                LastWarnings = translationWarnings;
                nonStaticTypes.ReplaceRange(types);
                loaded.TrySetResult();
                Reloaded?.Invoke(this, EventArgs.Empty);
            }).ConfigureAwait(false);

            foreach (ProjectMessage message in snapshot.Messages)
            {
                Log.ProjectMessage(logger, message.Code, message.Message);
            }

            Log.ReflectionReloadCompleted(logger, project.Name, stopwatch.ElapsedMilliseconds, references.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.ReflectionReloadFailed(logger, project.Name, ex);
            throw;
        }
    }
}
