using Microsoft.Extensions.Logging;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// The <see cref="IExtensionHost"/>: loads the initial registry from its options and reloads it for a project,
/// reusing the load contexts of folders it has already loaded.
/// </summary>
public sealed class ExtensionHost : IExtensionHost, IDisposable
{
    private readonly object gate = new();
    private readonly ExtensionLoaderOptions baseOptions;
    private readonly ILoggerFactory loggerFactory;
    private readonly ExtensionLoadContextCache cache = new();
    private IReadOnlyList<string> projectFolders = [];
    private ExtensionRegistry current;

    /// <summary>
    /// Creates the host and loads the initial registry.
    /// </summary>
    /// <param name="options">The loader options without project folders.</param>
    /// <param name="loggerFactory">Logger factory for the loader and the extensions.</param>
    public ExtensionHost(ExtensionLoaderOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        baseOptions = options;
        this.loggerFactory = loggerFactory;
        current = new ExtensionLoader(options, loggerFactory, cache).Load(CancellationToken.None);
    }

    /// <inheritdoc />
    public event EventHandler<ExtensionRegistry>? RegistryChanged;

    /// <inheritdoc />
    public ExtensionRegistry Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    /// <inheritdoc />
    public ExtensionRegistry LoadForProject(IReadOnlyList<string> projectExtensionFolders, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectExtensionFolders);

        string[] folders = [.. projectExtensionFolders.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal)];
        ExtensionRegistry previous;
        lock (gate)
        {
            if (folders.SequenceEqual(projectFolders, StringComparer.Ordinal))
            {
                return current;
            }

            previous = current;
        }

        var options = baseOptions with { ExtensionFolders = [.. baseOptions.ExtensionFolders, .. folders] };
        ExtensionRegistry next = new ExtensionLoader(options, loggerFactory, cache).Load(cancellationToken);

        lock (gate)
        {
            projectFolders = folders;
            current = next;
        }

        RegistryChanged?.Invoke(this, next);
        previous.Dispose();
        return next;
    }

    /// <summary>
    /// Disposes the registry in use.
    /// </summary>
    public void Dispose() => Current.Dispose();
}
