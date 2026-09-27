namespace NetPrints.Editor.Hosting;

/// <summary>
/// A named set of file patterns for file pickers, for example <c>new("Project Files", ["*.csproj"])</c>.
/// </summary>
public sealed record FileFilter(string Name, IReadOnlyList<string> Patterns)
{
    /// <summary>Filter for NetPrints project files (<c>*.csproj</c>; research.md R21).</summary>
    public static readonly FileFilter ProjectFiles = new("Project Files", ["*.csproj"]);

    /// <summary>Filter for NetPrints class files (<c>*.netpc.json</c>).</summary>
    public static readonly FileFilter ClassFiles = new("Class Files", ["*.netpc.json"]);

    /// <summary>Filter for .NET assemblies (<c>*.dll</c>, <c>*.exe</c>).</summary>
    public static readonly FileFilter Assemblies = new("Assemblies", ["*.dll", "*.exe"]);

    /// <summary>Filter matching every file.</summary>
    public static readonly FileFilter AllFiles = new("All Files", ["*"]);
}
