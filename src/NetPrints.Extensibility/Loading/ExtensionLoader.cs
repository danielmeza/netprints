using System.Reflection;
using Microsoft.Extensions.Logging;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Discovers, validates, orders and loads extensions, and builds the <see cref="ExtensionRegistry"/>
/// (extension-points.md §8.1).
/// </summary>
public sealed class ExtensionLoader
{
    private readonly ExtensionLoaderOptions options;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;
    private readonly ExtensionLoadContextCache cache;

    /// <summary>
    /// Creates a loader.
    /// </summary>
    /// <param name="options">What to load.</param>
    /// <param name="loggerFactory">Logger factory; also handed to the extensions.</param>
    public ExtensionLoader(ExtensionLoaderOptions options, ILoggerFactory loggerFactory)
        : this(options, loggerFactory, new ExtensionLoadContextCache())
    {
    }

    internal ExtensionLoader(ExtensionLoaderOptions options, ILoggerFactory loggerFactory, ExtensionLoadContextCache cache)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        this.options = options;
        this.loggerFactory = loggerFactory;
        this.cache = cache;
        logger = loggerFactory.CreateLogger<ExtensionLoader>();
    }

    /// <summary>
    /// Loads every extension. Problems with an extension never throw: they become
    /// <see cref="ExtensionLoadResult.Failed"/> entries of <see cref="ExtensionRegistry.Results"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load between extensions.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ExtensionRegistry Load(CancellationToken cancellationToken)
    {
        var failures = new List<ExtensionLoadResult.Failed>();
        var candidates = Discover(failures, cancellationToken);
        var ordered = Order(candidates, failures);

        var registryBuilder = new RegistryBuilder(loggerFactory.CreateLogger<ExtensionRegistry>());
        var loaded = new List<ExtensionLoadResult>();
        var loadedIds = new HashSet<string>(StringComparer.Ordinal);
        var contexts = new Dictionary<string, ExtensionLoadContext>(StringComparer.Ordinal);

        foreach (Candidate candidate in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? unmet = candidate.Manifest.DependsOn.FirstOrDefault(dependency => !loadedIds.Contains(dependency));
            if (unmet is not null)
            {
                failures.Add(Fail(candidate, ExtensionDiagnosticCodes.Dependency, $"dependency '{unmet}' did not load.", null));
                continue;
            }

            ExtensionLoadResult result = LoadOne(candidate, registryBuilder, contexts);
            if (result is ExtensionLoadResult.Failed failed)
            {
                failures.Add(failed);
            }
            else
            {
                loaded.Add(result);
                loadedIds.Add(candidate.Manifest.Id);
                Log.ExtensionLoaded(logger, candidate.Manifest.Id, candidate.Manifest.Version);
            }
        }

        return registryBuilder.Build([.. loaded, .. failures], loggerFactory);
    }

    private List<Candidate> Discover(List<ExtensionLoadResult.Failed> failures, CancellationToken cancellationToken)
    {
        var candidates = new List<Candidate>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < options.InProcess.Count; index++)
        {
            (ExtensionManifest manifest, INetPrintsExtension extension) = options.InProcess[index];
            if (!seenIds.Add(manifest.Id))
            {
                failures.Add(Duplicate(manifest.Id, null));
                continue;
            }

            candidates.Add(new Candidate(manifest, null, extension, index));
        }

        foreach (string folder in options.ExtensionFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAdd(Path.Combine(folder, ExtensionManifest.FileName), mustExist: true);
        }

        foreach (string searchDirectory in options.SearchDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(searchDirectory))
            {
                Log.SearchDirectoryMissing(logger, searchDirectory);
                continue;
            }

            foreach (string subdirectory in Directory.EnumerateDirectories(searchDirectory).Order(StringComparer.Ordinal))
            {
                string manifestPath = Path.Combine(subdirectory, ExtensionManifest.FileName);
                if (File.Exists(manifestPath))
                {
                    TryAdd(manifestPath, mustExist: false);
                }
            }
        }

        return candidates;

        void TryAdd(string manifestPath, bool mustExist)
        {
            string fullPath = Path.GetFullPath(manifestPath);
            if (!seenPaths.Add(fullPath))
            {
                return;
            }

            string folderName = new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath).Name;
            ExtensionManifest manifest;
            try
            {
                if (mustExist && !File.Exists(fullPath))
                {
                    throw new ExtensionManifestException(fullPath, $"{ExtensionManifest.FileName} not found.");
                }

                using FileStream stream = File.OpenRead(fullPath);
                manifest = ExtensionManifest.Parse(stream, fullPath);
            }
            catch (Exception ex) when (ex is ExtensionManifestException or IOException or UnauthorizedAccessException)
            {
                failures.Add(RecordFailure(folderName, fullPath, ExtensionDiagnosticCodes.InvalidManifest, ex.Message, ex));
                return;
            }

            Log.ExtensionDiscovered(logger, manifest.Id, fullPath);

            if (!seenIds.Add(manifest.Id))
            {
                failures.Add(Duplicate(manifest.Id, fullPath));
                return;
            }

            System.Version? api = manifest.ApiVersion;
            System.Version host = ExtensionApi.Version;
            if (api is null || api.Major != host.Major || api.Minor > host.Minor)
            {
                failures.Add(RecordFailure(manifest.Id, fullPath, ExtensionDiagnosticCodes.ApiVersion,
                    $"the extension needs netprintsApi {manifest.NetprintsApi}, this host provides {host.Major}.{host.Minor}.", null));
                return;
            }

            candidates.Add(new Candidate(manifest, fullPath, null, int.MaxValue));
        }
    }

    private List<Candidate> Order(List<Candidate> candidates, List<ExtensionLoadResult.Failed> failures)
    {
        var alive = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        foreach (Candidate candidate in candidates)
        {
            alive.Add(candidate.Manifest.Id, candidate);
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (Candidate candidate in alive.Values.ToList())
            {
                string? missing = candidate.Manifest.DependsOn.FirstOrDefault(dependency => !alive.ContainsKey(dependency));
                if (missing is not null)
                {
                    alive.Remove(candidate.Manifest.Id);
                    failures.Add(Fail(candidate, ExtensionDiagnosticCodes.Dependency, $"dependency '{missing}' is missing or failed.", null));
                    changed = true;
                }
            }
        }

        var remaining = alive.Values.ToDictionary(candidate => candidate.Manifest.Id, candidate => candidate.Manifest.DependsOn.Distinct().Count(), StringComparer.Ordinal);
        var ready = new SortedSet<Candidate>(Comparer<Candidate>.Create(CompareCandidates));
        foreach (Candidate candidate in alive.Values.Where(candidate => remaining[candidate.Manifest.Id] == 0))
        {
            ready.Add(candidate);
        }

        var ordered = new List<Candidate>();
        while (ready.Count > 0)
        {
            Candidate next = ready.Min ?? throw new InvalidOperationException("Empty ready set.");
            ready.Remove(next);
            ordered.Add(next);
            foreach (Candidate dependent in alive.Values.Where(candidate => candidate.Manifest.DependsOn.Contains(next.Manifest.Id)))
            {
                if (--remaining[dependent.Manifest.Id] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        foreach (Candidate candidate in alive.Values.Except(ordered))
        {
            failures.Add(Fail(candidate, ExtensionDiagnosticCodes.Dependency, "the extension is part of, or depends on, a dependency cycle.", null));
        }

        return ordered;
    }

    private static int CompareCandidates(Candidate? left, Candidate? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        int byGroup = left.InProcessIndex.CompareTo(right.InProcessIndex);
        return byGroup != 0 ? byGroup : string.CompareOrdinal(left.Manifest.Id, right.Manifest.Id);
    }

    private ExtensionLoadResult LoadOne(Candidate candidate, RegistryBuilder registryBuilder, Dictionary<string, ExtensionLoadContext> contexts)
    {
        ExtensionManifest manifest = candidate.Manifest;
        INetPrintsExtension extension;

        if (candidate.InProcess is not null)
        {
            extension = candidate.InProcess;
        }
        else
        {
            string manifestPath = candidate.ManifestPath ?? string.Empty;
            string folder = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            string assemblyPath = Path.GetFullPath(Path.Combine(folder, manifest.Assembly));
            if (!File.Exists(assemblyPath))
            {
                return Fail(candidate, ExtensionDiagnosticCodes.AssemblyLoadFailed, $"assembly '{manifest.Assembly}' not found.", null);
            }

            Type[] extensionTypes;
            try
            {
                ExtensionLoadContext[] dependencies = [.. manifest.DependsOn.Distinct().Select(id => contexts.GetValueOrDefault(id)).OfType<ExtensionLoadContext>()];
                ExtensionLoadContext context = cache.GetOrCreate(manifestPath, manifest.Id, assemblyPath, dependencies);
                contexts[manifest.Id] = context;
                ReportShadowedAssemblies(manifest.Id, folder, assemblyPath, context);
                Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
                if (OlderDependencyCopy(assembly, context) is { } older)
                {
                    return Fail(candidate, ExtensionDiagnosticCodes.DependencyVersion, older, null);
                }

                extensionTypes = [.. assembly.GetExportedTypes().Where(IsExtensionType)];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Fail(candidate, ExtensionDiagnosticCodes.AssemblyLoadFailed, $"assembly '{manifest.Assembly}' could not be loaded: {Describe(ex)}", ex);
            }

            if (extensionTypes.Length != 1)
            {
                return Fail(candidate, ExtensionDiagnosticCodes.InvalidManifest,
                    extensionTypes.Length == 0
                        ? $"assembly '{manifest.Assembly}' has no public INetPrintsExtension with a public parameterless constructor."
                        : $"assembly '{manifest.Assembly}' has {extensionTypes.Length} INetPrintsExtension implementations; exactly one is allowed.",
                    null);
            }

            try
            {
                extension = (INetPrintsExtension)(Activator.CreateInstance(extensionTypes[0])
                    ?? throw new InvalidOperationException("The extension type could not be instantiated."));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Fail(candidate, ExtensionDiagnosticCodes.RegisterFailed, $"creating the extension failed: {Describe(ex)}", ex);
            }
        }

        var builder = new ExtensionBuilder(manifest, loggerFactory);
        ExtensionContributions contributions;
        try
        {
            extension.Register(builder);
        }
        catch (Exception ex)
        {
            builder.Seal();
            return Fail(candidate, ExtensionDiagnosticCodes.RegisterFailed, $"Register failed: {Describe(ex)}", ex);
        }

        contributions = builder.Seal();
        registryBuilder.Commit(manifest, contributions);
        return new ExtensionLoadResult.Loaded(manifest.Id, candidate.ManifestPath, manifest);
    }

    private static string? OlderDependencyCopy(Assembly assembly, ExtensionLoadContext context)
    {
        if (context.Dependencies.Count == 0)
        {
            return null;
        }

        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
        {
            if (reference.Version is { } required && context.FindDependencyVersion(reference) is { } provided && provided < required)
            {
                return $"it was built against {reference.Name} {required}, but its dependency provides {provided}.";
            }
        }

        return null;
    }

    private void ReportShadowedAssemblies(string id, string folder, string assemblyPath, ExtensionLoadContext context)
    {
        if (!context.TryClaimShadowReport())
        {
            return;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.dll").Order(StringComparer.Ordinal))
            {
                if (string.Equals(Path.GetFullPath(file), assemblyPath, StringComparison.Ordinal))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(file);
                if (context.IsHostProvided(name))
                {
                    Log.HostAssemblyShadowed(logger, id, name, file);
                }
                else if (context.FindDependencyOwner(new AssemblyName { Name = name }) is { } owner)
                {
                    Log.DependencyAssemblyShadowed(logger, id, name, owner.Name ?? string.Empty);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.ShadowCheckFailed(logger, ex, id, folder);
        }
    }

    private static bool IsExtensionType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsPublic: true }
        && typeof(INetPrintsExtension).IsAssignableFrom(type)
        && type.GetConstructor(Type.EmptyTypes) is not null;

    private static string Describe(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? $"{inner.GetType().Name}: {inner.Message}" : $"{ex.GetType().Name}: {ex.Message}";

    private ExtensionLoadResult.Failed Duplicate(string id, string? manifestPath)
    {
        Log.ExtensionDuplicate(logger, id, manifestPath ?? "<in-process>");
        return new ExtensionLoadResult.Failed(id, manifestPath, ExtensionDiagnosticCodes.DuplicateId, $"an extension with id '{id}' was already found.", null);
    }

    private ExtensionLoadResult.Failed Fail(Candidate candidate, string code, string reason, Exception? exception) =>
        RecordFailure(candidate.Manifest.Id, candidate.ManifestPath, code, reason, exception);

    private ExtensionLoadResult.Failed RecordFailure(string id, string? manifestPath, string code, string reason, Exception? exception)
    {
        Log.ExtensionLoadFailed(logger, exception, id, code, reason);
        return new ExtensionLoadResult.Failed(id, manifestPath, code, reason, exception);
    }

    private sealed record Candidate(ExtensionManifest Manifest, string? ManifestPath, INetPrintsExtension? InProcess, int InProcessIndex);
}
