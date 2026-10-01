using System.Xml.Linq;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>
/// contracts/editor-services.md §7 rule A3, extended for this batch: the model, reflection,
/// serialization, extensibility, workspace, generation, generator, SDK, catalog and annotations projects never reference an
/// assembly named <c>Avalonia*</c>, and the generation, generator, catalog and annotations projects additionally never reference
/// <c>Microsoft.Build*</c> (ED-T10). A project-reference/package scan of each project's own
/// <c>.csproj</c>, not a transitive build-output scan.
/// </summary>
public class AssemblyReferenceGateTests
{
    private static readonly string[] NeverReferencesAvalonia =
    [
        "NetPrints.Core",
        "NetPrints.Reflection",
        "NetPrints.Serialization",
        "NetPrints.Extensibility",
        "NetPrints.Workspace",
        "NetPrints.Generation",
        "NetPrints.Generator",
        "NetPrints.Sdk",
        "NetPrints.Catalog",
        "NetPrints.Annotations",
    ];

    private static readonly string[] NeverReferencesMicrosoftBuild =
    [
        "NetPrints.Generation",
        "NetPrints.Generator",
        "NetPrints.Catalog",
        "NetPrints.Annotations",
    ];

    [Fact]
    public void ModelAndToolingProjectsNeverReferenceAvalonia()
    {
        string src = Path.Combine(RepositoryPaths.Root(), "src");
        var offenders = NeverReferencesAvalonia
            .SelectMany(project => ReferenceIncludes(src, project)
                .Where(include => include.StartsWith("Avalonia", StringComparison.Ordinal))
                .Select(include => $"{project}: {include}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void GeneratorNeverReferencesMicrosoftBuild()
    {
        string src = Path.Combine(RepositoryPaths.Root(), "src");
        var offenders = NeverReferencesMicrosoftBuild
            .SelectMany(project => ReferenceIncludes(src, project)
                .Where(include => include.StartsWith("Microsoft.Build", StringComparison.Ordinal))
                .Select(include => $"{project}: {include}"))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>Every <c>ProjectReference</c> (by project name) and <c>PackageReference</c> (by package id) <paramref name="project"/> declares.</summary>
    private static IEnumerable<string> ReferenceIncludes(string src, string project)
    {
        string csprojPath = Path.Combine(src, project, $"{project}.csproj");
        XDocument document = XDocument.Load(csprojPath);

        foreach (XElement element in document.Descendants())
        {
            string? include = element.Attribute("Include")?.Value;
            if (include is null)
            {
                continue;
            }

            if (element.Name.LocalName == "ProjectReference")
            {
                yield return Path.GetFileNameWithoutExtension(include);
            }
            else if (element.Name.LocalName == "PackageReference")
            {
                yield return include;
            }
        }
    }
}
