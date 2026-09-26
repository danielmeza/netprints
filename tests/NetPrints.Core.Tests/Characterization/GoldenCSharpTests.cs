using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Legacy;
using NetPrints.Serialization.Mapping;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// DF-T01: the C# the new importer (<see cref="LegacyXmlDocumentFormat"/> + <see cref="DocumentMapper"/>)
    /// produces for the legacy fixtures, compared to the golden files recorded before P1 (T004) from the
    /// unmodified (pre-P1) <see cref="ClassTranslator"/>/<c>Project.LoadFromPath</c> pipeline. Set
    /// NETPRINTS_UPDATE_SNAPSHOTS=1 to (re)write them.
    /// </summary>
    public class GoldenCSharpTests
    {
        public const string UpdateSnapshotsVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

        public static IEnumerable<object[]> LegacyFixtures()
        {
            yield return new object[] { "HelloWorld", "HelloWorld.Program.netpc" };
            yield return new object[] { "AllNodes", "AllNodes.Everything.netpc" };
        }

        [Theory]
        [MemberData(nameof(LegacyFixtures))]
        public async Task TranslatedClassesMatchGoldenFiles(string fixtureName, string classFileName)
        {
            string fixtureDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", fixtureName);
            string goldenDir = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");
            string classPath = Path.Combine(fixtureDir, classFileName);
            var id = new DocumentId(classFileName + ".json");

            var mapper = new DocumentMapper(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []));
            var format = new LegacyXmlDocumentFormat(mapper);

            ClassDocument document;
            using (FileStream stream = File.OpenRead(classPath))
            {
                document = await format.ReadClassAsync(stream, id, TestContext.Current.CancellationToken);
            }

            var issues = new List<DocumentIssue>();
            Project project = Project.CreateNew(fixtureName, fixtureName);
            ClassGraph cls = mapper.FromDocument(document, project, issues, id);
            Assert.Empty(issues);

            var translator = new ClassTranslator();
            string translated = translator.TranslateClass(cls);
            string goldenPath = Path.Combine(goldenDir, $"{cls.FullName}.cs");
            bool update = Environment.GetEnvironmentVariable(UpdateSnapshotsVariable) == "1";

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
