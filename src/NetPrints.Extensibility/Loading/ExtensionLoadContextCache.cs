namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Holds the load contexts of extension folders by manifest path, so re-loading never loads an assembly twice
/// (extension-points.md §8, <see cref="IExtensionHost"/>). A cached context is reused only while its dependency contexts are the same ones; otherwise a new one replaces it.
/// </summary>
internal sealed class ExtensionLoadContextCache
{
    private readonly object gate = new();
    private readonly Dictionary<string, ExtensionLoadContext> contexts = new(StringComparer.Ordinal);

    public ExtensionLoadContext GetOrCreate(string manifestPath, string extensionId, string assemblyPath, IReadOnlyList<ExtensionLoadContext> dependencies)
    {
        lock (gate)
        {
            if (!contexts.TryGetValue(manifestPath, out ExtensionLoadContext? context) || !context.Dependencies.SequenceEqual(dependencies))
            {
                context = new ExtensionLoadContext(extensionId, assemblyPath, dependencies);
                contexts[manifestPath] = context;
            }

            return context;
        }
    }
}
