using System.IO;
using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>
/// The fixture library keeps a source copy of the attribute definitions the generator injects, instead of running the
/// generator: it annotates non-public members on purpose (the NPC004 cases), and the warning cannot be silenced
/// (a generator diagnostic ignores <c>#pragma</c>, and the build gates forbid <c>NoWarn</c> and per-file severities).
/// This test keeps the copy identical to the generator output.
/// </summary>
public sealed class FixtureAttributeCopyTests
{
    private const string CopyFile = GeneratorFixtures.AttributeCopyFile;

    private const string EmbeddedAttributeHint = "Microsoft.CodeAnalysis.EmbeddedAttribute.cs";

    private const string AttributesHint = "NetPrintsAttributes.g.cs";

    private static string CopyPath() =>
        Path.Combine(TestPaths.RepositoryRoot(), "tests", "Fixtures", "Catalog", GeneratorFixtures.FixtureAssemblyName, CopyFile);

    private static string Expected()
    {
        var compilation = GeneratorTestHost.Compile("Empty", "class Empty { }");
        var driver = GeneratorTestHost.CreateDriver(compilation).RunGenerators(compilation, TestContext.Current.CancellationToken);
        var sources = GeneratorTestHost.GeneratedSources(driver);
        return string.Join("\n", new[] { EmbeddedAttributeHint, AttributesHint }.Select(hint => sources.Single(s => s.HintName == hint).Text.Replace("\r\n", "\n", System.StringComparison.Ordinal)));
    }

    [Fact]
    public void TheFixtureCopyEqualsTheGeneratorOutput()
    {
        string expected = Expected();
        if (TestPaths.UpdateSnapshots)
        {
            File.WriteAllText(CopyPath(), expected);
        }

        Assert.Equal(expected, File.ReadAllText(CopyPath()).Replace("\r\n", "\n", System.StringComparison.Ordinal));
    }
}
