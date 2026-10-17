using System.IO;
using System.Linq;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    public partial class SourceHygieneTests
    {
        /// <summary>AB-19: a node class in Core names its kind through <see cref="NodeKindIds"/>, never a repeated literal.</summary>
        [Fact]
        public void NodeKindAttributesInCoreUseTheKindIdConstants()
        {
            string graph = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Core", "Graph");

            string[] offenders =
            [
                .. EnumerateSourceFiles(graph, "*.cs")
                    .Where(path => File.ReadAllText(path).Contains("[NodeKind(\"", System.StringComparison.Ordinal))
                    .Select(Path.GetFileName)
                    .OfType<string>(),
            ];

            Assert.True(offenders.Length == 0, $"[NodeKind] with a string literal: {string.Join(", ", offenders)}");
        }

        /// <summary>AB-19: the built-in kinds of the document layer forward the constants of Core.</summary>
        [Fact]
        public void BuiltInNodeKindsForwardTheCoreKindIds()
        {
            Assert.Equal(NodeKindIds.ForLoop, BuiltInNodeKinds.ForLoop);
            Assert.Equal(NodeKindIds.MakeArrayType, BuiltInNodeKinds.MakeArrayType);
        }
    }
}
