using System.Runtime.Loader;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Decides which assemblies the host provides to every extension (ADR-0010 §4): the host application's trusted platform
/// assemblies, assemblies already loaded in the Default context and the MSBuildLocator family. A name prefix never makes an
/// assembly shared.
/// </summary>
internal static class HostAssemblies
{
    private const string MSBuildPrefix = "Microsoft.Build";

    private static readonly HashSet<string> PlatformAssemblies = ReadPlatformAssemblies();

    /// <summary>Whether the Default context supplies the assembly called <paramref name="simpleName"/>.</summary>
    /// <param name="simpleName">The assembly's simple name.</param>
    /// <returns><see langword="true"/> when an extension must use the host's copy.</returns>
    public static bool IsProvided(string simpleName)
    {
        ArgumentNullException.ThrowIfNull(simpleName);
        if (simpleName.StartsWith(MSBuildPrefix, StringComparison.Ordinal) || PlatformAssemblies.Contains(simpleName))
        {
            return true;
        }

        foreach (System.Reflection.Assembly assembly in AssemblyLoadContext.Default.Assemblies)
        {
            if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
