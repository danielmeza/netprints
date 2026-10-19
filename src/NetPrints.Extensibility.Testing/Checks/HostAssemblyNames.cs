namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>The assemblies the host provides to every extension, by simple name.</summary>
internal static class HostAssemblyNames
{
    private const string NetPrintsPrefix = "NetPrints.";

    /// <summary>The NetPrints assemblies an extension may reference (FR-020); the host shares each with it.</summary>
    public static IReadOnlySet<string> Contract { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "NetPrints.Annotations",
        "NetPrints.Catalog",
        "NetPrints.Core",
        "NetPrints.Extensibility",
        "NetPrints.Extensibility.Abstractions",
        "NetPrints.Reflection",
        "NetPrints.Serialization",
    };

    private static readonly HashSet<string> Platform = ReadPlatformAssemblies();

    /// <summary>Whether a copy of <paramref name="simpleName"/> in an extension folder would shadow what the host provides.</summary>
    /// <param name="simpleName">The assembly's simple name.</param>
    /// <returns><see langword="true"/> for the NetPrints contract assemblies and the host's platform assemblies.</returns>
    public static bool IsProvidedByHost(string simpleName) => Contract.Contains(simpleName) || Platform.Contains(simpleName);

    /// <summary>Whether <paramref name="simpleName"/> is a NetPrints assembly.</summary>
    /// <param name="simpleName">The assembly's simple name.</param>
    /// <returns><see langword="true"/> when it starts with <c>NetPrints.</c>.</returns>
    public static bool IsNetPrints(string simpleName) => simpleName.StartsWith(NetPrintsPrefix, StringComparison.Ordinal);

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
