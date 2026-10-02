using System.Text.Json;
using System.Text.RegularExpressions;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>ADR-0018: Dock.Avalonia stays inside <c>Shell/Docking</c>, pinned exactly, with no ReactiveUI, Newtonsoft.Json or Dock serializer package.</summary>
public partial class DockConfinementTests
{
    private const string DockingFolder = "Shell/Docking";
    private const string PinnedVersion = "[12.1.0.6]";

    private static readonly string[] PinnedPackages =
    [
        "Dock.Avalonia",
        "Dock.Model.Mvvm",
        "Dock.Avalonia.Themes.Fluent",
    ];

    [GeneratedRegex(@"(^\s*using\s+(static\s+)?Dock\.)|(\bDock\.(Avalonia|Model|Settings|Serializer|Controls)\b)|(clr-namespace:Dock\.)|(xmlns(:\w+)?=""https://github\.com/avaloniaui/dock"")|(xmlns(:\w+)?=""[^""]*Dock\.[^""]*"")", RegexOptions.Multiline)]
    private static partial Regex DockUsage();

    [Fact]
    public void DockIsUsedOnlyInsideTheDockingAdapter()
    {
        string editorSrc = Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Editor");
        string docking = Path.Combine(editorSrc, DockingFolder.Replace('/', Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar;
        string[] offenders =
        [.. Directory.EnumerateFiles(editorSrc, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".axaml", StringComparison.Ordinal))
            .Where(path => !path.StartsWith(docking, StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => DockUsage().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(editorSrc, path))];

        Assert.Empty(offenders);
    }

    [Fact]
    public void EditorAssetsListNoReactiveUiAndNoNewtonsoftJson()
    {
        string assets = Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Editor", "obj", "project.assets.json");
        Assert.True(File.Exists(assets), $"Restore the editor first: {assets} is missing.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assets));
        string[] libraries = [.. document.RootElement.GetProperty("libraries").EnumerateObject().Select(p => p.Name)];

        Assert.Contains(libraries, name => name.StartsWith("Dock.Avalonia/", StringComparison.Ordinal));
        string[] offenders =
        [.. libraries.Where(name => name.StartsWith("ReactiveUI", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Newtonsoft.Json/", StringComparison.OrdinalIgnoreCase))];
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoDockSerializerPackageIsReferenced()
    {
        string root = RepositoryPaths.Root();
        string props = File.ReadAllText(Path.Combine(root, "Directory.Packages.props"));
        string project = File.ReadAllText(Path.Combine(root, "src", "NetPrints.Editor", "NetPrints.Editor.csproj"));
        Assert.DoesNotContain("Dock.Serializer", props, StringComparison.Ordinal);
        Assert.DoesNotContain("Dock.Serializer", project, StringComparison.Ordinal);

        string assets = Path.Combine(root, "src", "NetPrints.Editor", "obj", "project.assets.json");
        Assert.True(File.Exists(assets), $"Restore the editor first: {assets} is missing.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assets));
        string[] offenders = [.. document.RootElement.GetProperty("libraries").EnumerateObject().Select(p => p.Name).Where(name => name.StartsWith("Dock.Serializer", StringComparison.Ordinal))];
        Assert.Empty(offenders);
    }

    [Fact]
    public void DirectoryPackagesPinsDockExactly()
    {
        string props = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), "Directory.Packages.props"));

        foreach (string package in PinnedPackages)
        {
            Assert.Matches($"<PackageVersion\\s+Include=\"{Regex.Escape(package)}\"\\s+Version=\"{Regex.Escape(PinnedVersion)}\"", props);
        }
    }
}
