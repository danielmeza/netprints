using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Localization
{
    /// <summary>FR-027, research R13: every convention key a built-in node declaration implies is in the neutral <c>NodeStrings.resx</c>.</summary>
    public class NodeStringKeyTests
    {
        private static string[] ShippedPinLines() =>
            [.. File.ReadAllLines(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Core", "NodePins.Shipped.txt"))
                .Where(line => line.Length > 0 && line[0] != ';')];

        private static bool Missing(string key) => NodeStrings.ResourceManager.GetString(key, CultureInfo.InvariantCulture) is null;

        [Fact]
        public void EveryBuiltInKindHasATitle()
        {
            string[] kinds = [.. typeof(BuiltInNodeKinds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => Assert.IsType<string>(field.GetRawConstantValue()))];

            Assert.NotEmpty(kinds);
            Assert.Empty(kinds.Select(kind => "Node." + kind + ".Title").Where(Missing));
        }

        [Fact]
        public void EveryShippedPinHasALabel()
        {
            string[] keys = [.. ShippedPinLines().Select(line => line.Split(' ')).Select(parts => "Node." + parts[0] + ".Pin." + parts[1])];

            Assert.NotEmpty(keys);
            Assert.Empty(keys.Where(Missing));
        }
    }
}
