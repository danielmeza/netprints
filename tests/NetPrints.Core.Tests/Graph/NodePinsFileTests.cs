using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NetPrints.Serialization.Documents;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>The seed of <c>NodePins.Shipped.txt</c> (FR-011): well-formed lines of known kinds whose pins are in the S0 pin-layout golden.</summary>
    public class NodePinsFileTests
    {
        private static string[] ShippedLines() =>
            [.. File.ReadAllLines(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Core", "NodePins.Shipped.txt"))
                .Where(line => line.Length > 0 && line[0] != ';')];

        [Fact]
        public void ShippedLinesAreSortedUniqueAndOfKnownKinds()
        {
            string[] lines = ShippedLines();
            HashSet<string> kinds = [.. typeof(BuiltInNodeKinds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => Assert.IsType<string>(field.GetRawConstantValue()))];

            Assert.NotEmpty(lines);
            Assert.Equal(lines.Order(StringComparer.Ordinal), lines);
            Assert.Equal(lines.Length, lines.Distinct(StringComparer.Ordinal).Count());
            Assert.All(lines, line => Assert.Contains(line.Split(' ')[0], kinds));
        }

        [Fact]
        public void EveryShippedKeyIsInTheGolden()
        {
            string golden = File.ReadAllText(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", "NodePinLayout.golden.txt"));

            Assert.All(ShippedLines(), line => Assert.Contains(" " + line.Split(' ')[1] + " ", golden, StringComparison.Ordinal));
        }
    }
}
