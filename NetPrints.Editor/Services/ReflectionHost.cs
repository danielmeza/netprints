using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Services;

/// <summary>
/// Builds the reflection provider for a project off the UI thread and publishes it on the UI
/// thread (PAR-15). References are resolved with <see cref="ReferenceAssemblyResolver"/>, so
/// .NET Framework references fall back to the running runtime's assemblies on Linux/macOS.
/// </summary>
public sealed class ReflectionHost : IReflectionHost
{
    private readonly IUiDispatcher dispatcher;
    private readonly ObservableRangeCollection<TypeSpecifier> nonStaticTypes = [];
    private IReflectionProvider? provider;
    private int reloadVersion;

    public ReflectionHost(IUiDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        NonStaticTypes = new ReadOnlyObservableCollection<TypeSpecifier>(nonStaticTypes);
    }

    public IReflectionProvider Provider => provider ??= new MemoizedReflectionProvider(new ReflectionProvider([], [], []));

    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes { get; }

    public IReadOnlyList<string> LastWarnings { get; private set; } = [];

    public event EventHandler? Reloaded;

    public async Task ReloadAsync(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        int version = Interlocked.Increment(ref reloadVersion);

        // Snapshot the model on the calling (UI) thread; everything else runs in the background.
        var references = project.References.ToList();
        var sources = project.GenerateClassSources().ToList();

        var (newProvider, types, warnings) = await Task.Run(() =>
        {
            var warnings = new List<string>();
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

            IReflectionProvider built = new MemoizedReflectionProvider(new ReflectionProvider(assemblyPaths, sourcePaths, sources));
            var types = built.GetNonStaticTypes().ToList();
            return (built, types, warnings);
        }).ConfigureAwait(false);

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
            Reloaded?.Invoke(this, EventArgs.Empty);
        }).ConfigureAwait(false);
    }
}
