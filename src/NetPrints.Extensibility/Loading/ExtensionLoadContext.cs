using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Loads one extension assembly and its private dependencies in isolation. Assemblies the host provides come from the
/// Default context, assemblies of the extensions it depends on come from their contexts, everything else from the
/// extension's own folder (ADR-0010 §4). Not collectible.
/// </summary>
internal sealed class ExtensionLoadContext : AssemblyLoadContext
{
    private const string HostProvider = "the host";
    private const string DependencyProvider = "its dependency";

    private readonly AssemblyDependencyResolver resolver;
    private readonly IReadOnlyList<ExtensionLoadContext> dependencies;
    private readonly HashSet<string> hostProvided = HostAssemblies.Snapshot();
    private int shadowReported;

    /// <summary>
    /// Creates the context for the extension assembly at <paramref name="extensionAssemblyPath"/>.
    /// </summary>
    /// <param name="name">The load context name, the manifest id.</param>
    /// <param name="extensionAssemblyPath">Full path of the extension assembly.</param>
    /// <param name="dependencies">The contexts of the extensions it depends on, in declared order.</param>
    public ExtensionLoadContext(string name, string extensionAssemblyPath, IReadOnlyList<ExtensionLoadContext> dependencies)
        : base(name, isCollectible: false)
    {
        resolver = new AssemblyDependencyResolver(extensionAssemblyPath);
        this.dependencies = dependencies;
    }

    /// <summary>The contexts of the extensions this one depends on, in declared order.</summary>
    public IReadOnlyList<ExtensionLoadContext> Dependencies => dependencies;

    /// <summary>Whether the host provided the assembly called <paramref name="simpleName"/> when this context was created.</summary>
    /// <param name="simpleName">The assembly's simple name.</param>
    /// <returns><see langword="true"/> when the extension must use the host's copy.</returns>
    public bool IsHostProvided(string simpleName) => HostAssemblies.IsMSBuild(simpleName) || hostProvided.Contains(simpleName);

    /// <summary>Claims the one shadow report this context gets.</summary>
    /// <returns><see langword="true"/> for the first caller only.</returns>
    public bool TryClaimShadowReport() => Interlocked.Exchange(ref shadowReported, 1) == 0;

    /// <summary>The first context in the dependency chain that provides <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="name">The assembly to look for.</param>
    /// <returns>The owning context.</returns>
    public ExtensionLoadContext? FindDependencyOwner(AssemblyName name)
    {
        var visited = new HashSet<ExtensionLoadContext> { this };
        foreach (ExtensionLoadContext dependency in dependencies)
        {
            ExtensionLoadContext? owner = dependency.FindOwner(name, visited);
            if (owner is not null)
            {
                return owner;
            }
        }

        return null;
    }

    /// <summary>The version of the copy the host or a dependency provides for <paramref name="name"/>, or <see langword="null"/> when neither does.</summary>
    /// <param name="name">The referenced assembly.</param>
    /// <returns>The version the extension would get and who provides it ("the host" or "its dependency"), or <see langword="null"/>.</returns>
    public (Version Version, string Provider)? FindProvided(AssemblyName name)
    {
        if (name.Name is null)
        {
            return null;
        }

        if (IsHostProvided(name.Name))
        {
            Assembly? hostCopy = Default.Assemblies.FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, name.Name, StringComparison.Ordinal));
            return hostCopy?.GetName().Version is { } hostVersion ? (hostVersion, HostProvider) : null;
        }

        return FindDependencyOwner(name)?.LoadOwned(name)?.GetName().Version is { } version ? (version, DependencyProvider) : null;
    }

    /// <summary>The assemblies that the private copy of <paramref name="name"/> in this extension's folder references.</summary>
    /// <param name="name">The assembly to look up in the extension's own folder.</param>
    /// <returns>Its references, or an empty list when the extension does not ship it or it cannot be read.</returns>
    public IReadOnlyList<AssemblyName> ReadPrivateReferences(AssemblyName name)
    {
        string? path = resolver.ResolveAssemblyToPath(name);
        if (path is null)
        {
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            MetadataReader metadata = peReader.GetMetadataReader();
            return [.. metadata.AssemblyReferences.Select(handle =>
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                return new AssemblyName { Name = metadata.GetString(reference.Name), Version = reference.Version };
            })];
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (name is null || IsHostProvided(name))
        {
            return null;
        }

        ExtensionLoadContext? owner = FindDependencyOwner(assemblyName);
        if (owner is not null)
        {
            return owner.LoadOwned(assemblyName);
        }

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private ExtensionLoadContext? FindOwner(AssemblyName name, HashSet<ExtensionLoadContext> visited)
    {
        if (!visited.Add(this))
        {
            return null;
        }

        if (FindLoaded(name) is not null || resolver.ResolveAssemblyToPath(name) is not null)
        {
            return this;
        }

        foreach (ExtensionLoadContext dependency in dependencies)
        {
            ExtensionLoadContext? owner = dependency.FindOwner(name, visited);
            if (owner is not null)
            {
                return owner;
            }
        }

        return null;
    }

    private Assembly? LoadOwned(AssemblyName name)
    {
        Assembly? loaded = FindLoaded(name);
        if (loaded is not null)
        {
            return loaded;
        }

        string? path = resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    private Assembly? FindLoaded(AssemblyName name) =>
        Assemblies.FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, name.Name, StringComparison.Ordinal));
}
