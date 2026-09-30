using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// Every product and test project builds and tests with the solution, so it must be listed in <c>NetPrints.slnx</c>.
    /// <c>samples/</c> (user-style projects that load the Generator from <c>bin/</c>), <c>legacy/</c> and <c>docs/</c> are out of scope,
    /// and so are the fixture projects that tests copy and build on demand.
    /// </summary>
    public class SolutionHygieneTests
    {
        private const string SolutionFile = "NetPrints.slnx";

        [Fact]
        public void EverySourceAndTestProjectIsInTheSolution()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string[] listed =
            [
                .. XDocument.Load(Path.Combine(root, SolutionFile)).Descendants("Project")
                    .Select(project => (string?)project.Attribute("Path"))
                    .OfType<string>()
                    .Select(path => path.Replace('\\', '/')),
            ];

            string[] missing =
            [
                .. new[] { "src", "tests" }
                    .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories))
                    .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                    .Where(path => !path.Contains("/obj/", StringComparison.Ordinal)
                        && !path.Contains("/bin/", StringComparison.Ordinal)
                        && !IsFixture(path))
                    .Where(path => !listed.Contains(path, StringComparer.Ordinal))
                    .Order(StringComparer.Ordinal),
            ];

            Assert.NotEmpty(listed);
            Assert.Empty(missing);
        }

        private static bool IsFixture(string relativePath) =>
            relativePath.StartsWith("tests/", StringComparison.Ordinal)
            && relativePath.Contains("/Fixtures/", StringComparison.Ordinal);
    }
}
