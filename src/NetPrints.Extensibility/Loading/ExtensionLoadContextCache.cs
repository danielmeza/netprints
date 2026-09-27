namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Holds the load contexts of extension folders by manifest path, so re-loading never loads an assembly twice
/// (extension-points.md §8, <see cref="IExtensionHost"/>).
/// </summary>
internal sealed class ExtensionLoadContextCache
{
    private readonly object gate = new();
    private readonly Dictionary<string, ExtensionLoadContext> contexts = new(StringComparer.Ordinal);

    public ExtensionLoadContext GetOrCreate(string manifestPath, string extensionId, string assemblyPath)
    {
        lock (gate)
        {
            if (!contexts.TryGetValue(manifestPath, out ExtensionLoadContext? context))
            {
                context = new ExtensionLoadContext(extensionId, assemblyPath);
                contexts.Add(manifestPath, context);
            }

            return context;
        }
    }
}
