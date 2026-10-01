using System.Runtime.Loader;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Decides which assemblies the host provides to every extension (ADR-0010 §4): the host application's trusted platform
/// assemblies, assemblies loaded in the Default context when an extension's context is created and the MSBuildLocator family. A name prefix never makes an
/// assembly shared.
/// </summary>
internal static class HostAssemblies
{
    private const string MSBuildName = "Microsoft.Build";
    private const string MSBuildPrefix = "Microsoft.Build.";

    private static readonly HashSet<string> PlatformAssemblies = ReadPlatformAssemblies();

    /// <summary>The names the host provides right now: its platform assemblies and what the Default context has loaded.</summary>
    /// <returns>A snapshot an extension's load context keeps for its lifetime.</returns>
    public static HashSet<string> Snapshot()
    {
        var names = new HashSet<string>(PlatformAssemblies, StringComparer.Ordinal);
        foreach (System.Reflection.Assembly assembly in AssemblyLoadContext.Default.Assemblies)
        {
            if (assembly.GetName().Name is { } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>Whether <paramref name="simpleName"/> belongs to the MSBuildLocator family, which the host always provides.</summary>
    /// <param name="simpleName">The assembly's simple name.</param>
    /// <returns><see langword="true"/> for <c>Microsoft.Build</c> and <c>Microsoft.Build.*</c>.</returns>
    public static bool IsMSBuild(string simpleName)
    {
        ArgumentNullException.ThrowIfNull(simpleName);
        return simpleName == MSBuildName || simpleName.StartsWith(MSBuildPrefix, StringComparison.Ordinal);
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
