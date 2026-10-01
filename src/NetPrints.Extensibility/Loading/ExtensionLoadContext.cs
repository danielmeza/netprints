using System.Reflection;
using System.Runtime.Loader;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Loads one extension assembly and its private dependencies in isolation. Assemblies the host provides come from the
/// Default context, assemblies of the extensions it depends on come from their contexts, everything else from the
/// extension's own folder (ADR-0010 §4). Not collectible.
/// </summary>
internal sealed class ExtensionLoadContext : AssemblyLoadContext
{
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

    /// <summary>The version of the copy a dependency provides for <paramref name="name"/>, or <see langword="null"/> when none does.</summary>
    /// <param name="name">The referenced assembly.</param>
    /// <returns>The version the extension would get.</returns>
    public Version? FindDependencyVersion(AssemblyName name)
    {
        if (name.Name is null || IsHostProvided(name.Name))
        {
            return null;
        }

        return FindDependencyOwner(name)?.LoadOwned(name)?.GetName().Version;
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
