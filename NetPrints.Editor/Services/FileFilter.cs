namespace NetPrints.Editor.Services;

/// <summary>
/// A named set of file patterns for file pickers, for example <c>new("Project Files", ["*.netpp"])</c>.
/// </summary>
public sealed record FileFilter(string Name, IReadOnlyList<string> Patterns)
{
    public static readonly FileFilter ProjectFiles = new("Project Files", ["*.netpp"]);
    public static readonly FileFilter ClassFiles = new("Class Files", ["*.netpc"]);
    public static readonly FileFilter Assemblies = new("Assemblies", ["*.dll", "*.exe"]);
    public static readonly FileFilter AllFiles = new("All Files", ["*"]);
}
