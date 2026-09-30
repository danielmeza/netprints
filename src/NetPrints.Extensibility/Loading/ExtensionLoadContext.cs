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

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (name is null || HostAssemblies.IsProvided(name))
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
