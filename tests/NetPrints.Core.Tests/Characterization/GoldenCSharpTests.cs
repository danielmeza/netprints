using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// Characterization of the C# the unmodified (pre-P1) <see cref="ClassTranslator"/> produces for
    /// the legacy fixtures. The recorded golden files are the baseline every later phase's "identical
    /// C#" requirement (FR-008) is checked against. Set NETPRINTS_UPDATE_SNAPSHOTS=1 to (re)write them.
    /// </summary>
    public class GoldenCSharpTests
    {
        public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

        public static IEnumerable<object[]> LegacyFixtures()
        {
            yield return new object[] { "HelloWorld", "HelloWorld.netpp" };
            yield return new object[] { "AllNodes", "AllNodes.netpp" };
        }

        [Theory]
        [MemberData(nameof(LegacyFixtures))]
        public void TranslatedClassesMatchGoldenFiles(string fixtureName, string projectFileName)
        {
            string fixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", fixtureName);
            string goldenDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");

            Project? project = Project.LoadFromPath(Path.Combine(fixtureDir, projectFileName));
            var translator = new ClassTranslator();
            bool update = Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1";

            Assert.NotNull(project);
            Assert.NotEmpty(project.Classes);

            foreach (ClassGraph cls in project.Classes.OrderBy(c => c.FullName, StringComparer.Ordinal))
            {
                string translated = translator.TranslateClass(cls);
                string goldenPath = Path.Combine(goldenDir, $"{cls.FullName}.cs");

                if (update)
                {
                    Directory.CreateDirectory(goldenDir);
                    File.WriteAllText(goldenPath, translated);
                }

                Assert.True(File.Exists(goldenPath), $"Missing golden file {goldenPath}; regenerate with {UpdateSnapshotsVariable}=1");
                string golden = File.ReadAllText(goldenPath);
                Assert.Equal(golden, translated);
            }
        }
    }
}
