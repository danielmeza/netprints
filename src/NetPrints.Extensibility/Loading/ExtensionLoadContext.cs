using System.Reflection;
using System.Runtime.Loader;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Loads one extension assembly and its private dependencies in isolation, and shares the host's copy of
/// everything the extension API exposes (extension-points.md §8.2, research R8). Not collectible.
/// </summary>
internal sealed class ExtensionLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedPrefixes =
    [
        "NetPrints",
        "Microsoft.Build",
        "Microsoft.CodeAnalysis",
        "CommunityToolkit.Mvvm",
        "System.Reactive",
        "DynamicData",
        "Avalonia",
    ];

    private static readonly HashSet<string> PlatformAssemblies = ReadPlatformAssemblies();

    private readonly AssemblyDependencyResolver resolver;

    /// <summary>
    /// Creates the context for the extension assembly at <paramref name="extensionAssemblyPath"/>.
    /// </summary>
    /// <param name="name">The load context name, the manifest id.</param>
    /// <param name="extensionAssemblyPath">Full path of the extension assembly.</param>
    public ExtensionLoadContext(string name, string extensionAssemblyPath)
        : base(name, isCollectible: false)
    {
        resolver = new AssemblyDependencyResolver(extensionAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (name is null || IsShared(name))
        {
            return null;
        }

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsShared(string name)
    {
        foreach (string prefix in SharedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) && name.EndsWith(".Abstractions", StringComparison.Ordinal))
        {
            return true;
        }

        return PlatformAssemblies.Contains(name);
    }

    private static HashSet<string> ReadPlatformAssemblies()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string list)
        {
            foreach (string path in list.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                names.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        return names;
    }
}
