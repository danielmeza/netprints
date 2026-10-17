using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetPrints.Graph;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-020, research R12: every packable library validates its public surface against the last published version, and the recorded 0.4.0 breaks match the API files.</summary>
public class PackageValidationTests
{
    private const string BaselineProperty = "NetPrintsPackageBaselineVersion";

    /// <summary>The suppression ids a test below checks against the API files or the assembly; any other id has no cross-check.</summary>
    private static readonly string[] CrossCheckedIds = ["CP0002", "CP0009"];

    /// <summary>Packable projects that are not class libraries: a tool, an MSBuild SDK and an analyzer package.</summary>
    private static readonly string[] NotLibraries = ["NetPrints.Annotations", "NetPrints.Cli", "NetPrints.Sdk"];

    /// <summary>The libraries already on nuget.org. The new ones take their baseline in the release that first publishes them (docs/contributing/releasing.md).</summary>
    private static readonly string[] Published = ["NetPrints.Core", "NetPrints.Reflection", "NetPrints.Catalog"];

    /// <summary>The packages that ship the repository README, as before.</summary>
    private static readonly string[] RootReadme = ["NetPrints.Core", "NetPrints.Reflection"];

    private static readonly string[] ExpectedLibraries =
    [
        "NetPrints.Catalog",
        "NetPrints.Core",
        "NetPrints.Extensibility",
        "NetPrints.Extensibility.Abstractions",
        "NetPrints.Reflection",
        "NetPrints.Serialization",
    ];

    private static string Root => SampleProjectFactory.FindRepositoryRoot();

    private static Dictionary<string, string> Properties(string project) =>
        XDocument.Load(project).Descendants("PropertyGroup").Elements()
            .GroupBy(element => element.Name.LocalName)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim());

    private static List<(string Name, string Directory, Dictionary<string, string> Properties)> Libraries() =>
        [.. Directory.GetFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => !project.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(project => (Name: Path.GetFileNameWithoutExtension(project), Directory: Path.GetDirectoryName(project) ?? string.Empty, Properties: Properties(project)))
            .Where(project => project.Properties.GetValueOrDefault("IsPackable") == "true" && !NotLibraries.Contains(project.Name))
            .OrderBy(project => project.Name, StringComparer.Ordinal)];

    [Fact]
    public void TheLibrariesThatShipAsPackagesArePackable()
    {
        Assert.Equal(ExpectedLibraries, Libraries().Select(library => library.Name));
    }

    [Fact]
    public void EveryPackableLibraryEnablesPackageValidation()
    {
        Assert.All(Libraries(), library => Assert.Equal("true", library.Properties.GetValueOrDefault("EnablePackageValidation")));
    }

    [Fact]
    public void TheBaselineVersionIsSetOnceAndOnlyThePublishedLibrariesUseIt()
    {
        string version = Properties(Path.Combine(Root, "Directory.Build.props")).GetValueOrDefault(BaselineProperty) ?? string.Empty;
        Assert.True(Version.TryParse(version, out _), $"{BaselineProperty} in Directory.Build.props is '{version}'");

        Assert.All(Libraries(), library => Assert.Equal(
            Published.Contains(library.Name) ? $"$({BaselineProperty})" : null,
            library.Properties.GetValueOrDefault("PackageValidationBaselineVersion")));
    }

    [Fact]
    public void EveryRecordedSuppressionHasACrossCheck()
    {
        foreach ((string name, string directory, _) in Libraries())
        {
            string suppressions = Path.Combine(directory, "CompatibilitySuppressions.xml");
            if (!File.Exists(suppressions))
            {
                continue;
            }

            Assert.All(Targets(suppressions), entry => Assert.True(CrossCheckedIds.Contains(entry.Id), $"{name}: {entry.Id} {entry.Target} is a suppression no test checks"));
        }
    }

    [Fact]
    public void EveryPackableLibraryExceptTheRootReadmeOnesShipsItsOwnReadme()
    {
        Assert.All(Libraries().Where(library => !RootReadme.Contains(library.Name)), library => Assert.True(File.Exists(Path.Combine(library.Directory, "README.md")), library.Name));
    }

    [Fact]
    public void EveryRecordedRemovalIsAStarRemovedLineInTheApiFile()
    {
        foreach ((string name, string directory, _) in Libraries())
        {
            string suppressions = Path.Combine(directory, "CompatibilitySuppressions.xml");
            if (!File.Exists(suppressions))
            {
                continue;
            }

            string[] removed = [.. File.ReadAllLines(Path.Combine(directory, "PublicAPI.Unshipped.txt")).Where(line => line.StartsWith("*REMOVED*", StringComparison.Ordinal))];
            foreach ((string id, string target) in Targets(suppressions).Where(entry => entry.Id == "CP0002"))
            {
                string member = target[2..];
                int split = member.LastIndexOf('.');
                string accessor = member[(split + 1)..];
                string line = accessor.StartsWith("get_", StringComparison.Ordinal)
                    ? $"{member[..split]}.{accessor[4..]}.get"
                    : $"{member[..split]}.{accessor}";
                Assert.True(
                    removed.Any(candidate => candidate.Contains(line, StringComparison.Ordinal)),
                    $"{name}: {id} {target} has no *REMOVED* line in PublicAPI.Unshipped.txt");
            }
        }
    }

    [Fact]
    public void EveryRecordedSealingIsASealedTypeInTheAssembly()
    {
        string suppressions = Path.Combine(Root, "src", "NetPrints.Core", "CompatibilitySuppressions.xml");

        foreach ((_, string target) in Targets(suppressions).Where(entry => entry.Id == "CP0009"))
        {
            Type type = Assert.IsAssignableFrom<Type>(typeof(Node).Assembly.GetType(target[2..]));
            Assert.True(type.IsSealed, target);
        }
    }

    [Fact]
    public void EveryRecordedBreakIsFromTheBaselineVersionOnly()
    {
        string suppressions = Path.Combine(Root, "src", "NetPrints.Core", "CompatibilitySuppressions.xml");
        if (!File.Exists(suppressions))
        {
            return;
        }

        Assert.All(
            XDocument.Load(suppressions).Descendants("Suppression"),
            suppression => Assert.Equal("true", suppression.Element("IsBaselineSuppression")?.Value));
    }

    /// <summary>The suppressions of a file; a library right after a release has none (docs/contributing/releasing.md).</summary>
    private static IEnumerable<(string Id, string Target)> Targets(string suppressions) =>
        !File.Exists(suppressions) ? [] : XDocument.Load(suppressions).Descendants("Suppression")
            .Select(suppression => (Id: suppression.Element("DiagnosticId")?.Value ?? string.Empty, Target: suppression.Element("Target")?.Value ?? string.Empty));
}
