using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>Pins the primitive table: its rows, and that <c>sbyte</c> and <c>decimal</c> are full primitives (US2 scenario 7).</summary>
    public class PrimitiveKindsTests
    {
        private const string GoldenFileName = "PrimitiveKinds.golden.txt";

        private static readonly TypeSpecifier SByteType = TypeSpecifier.FromType<sbyte>();

        private static readonly TypeSpecifier DecimalType = TypeSpecifier.FromType<decimal>();

        [Fact]
        public void TheTableMatchesTheGolden()
        {
            string[] lines = PrimitiveKinds.All
                .Select(kind => string.Join(
                    '|',
                    kind.FullName,
                    kind.Keyword,
                    kind.Category,
                    kind.DefaultValue.GetType().Name,
                    kind.DefaultValue is char nul ? "U+" + ((int)nul).ToString("X4", CultureInfo.InvariantCulture) : kind.Format(kind.DefaultValue),
                    kind.LiteralSuffix))
                .ToArray();

            string goldenPath = Path.Combine(
                SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", GoldenFileName);

            if (Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1")
            {
                File.WriteAllLines(goldenPath, lines);
            }

            Assert.True(File.Exists(goldenPath), $"Missing {goldenPath}; regenerate with {GoldenCSharpTests.UpdateSnapshotsVariable}=1");
            Assert.Equal(File.ReadAllLines(goldenPath), lines);
        }

        [Theory]
        [InlineData("System.SByte", "-5", typeof(sbyte))]
        [InlineData("System.Decimal", "12.50", typeof(decimal))]
        [InlineData("System.Int32", "7", typeof(int))]
        [InlineData("System.Double", "0.1", typeof(double))]
        public void ParseAndFormatRoundTripInTheInvariantCulture(string fullName, string text, Type expected)
        {
            Assert.True(PrimitiveKinds.TryGet(fullName, out PrimitiveKind? kind));

            object parsed = kind.Parse(text);

            Assert.IsType(expected, parsed);
            Assert.Equal(text, kind.Format(parsed));
        }

        [Fact]
        public void SByteAndDecimalAreFullPrimitives()
        {
            Assert.True(SByteType.IsPrimitive);
            Assert.True(DecimalType.IsPrimitive);
            Assert.Equal((sbyte)0, PrimitiveKinds.TryGet(SByteType.Name, out PrimitiveKind? s) ? s.DefaultValue : null);
            Assert.Equal(0m, PrimitiveKinds.TryGet(DecimalType.Name, out PrimitiveKind? d) ? d.DefaultValue : null);
        }

        [Fact]
        public void UnconnectedSByteAndDecimalInputsUseTheirValueAndEmitValidCSharp()
        {
            var call = new CallMethodNode(new MethodGraph("M"), Method(SByteType, DecimalType));

            NodeInputDataPin sbytePin = call.InputDataPins[0];
            NodeInputDataPin decimalPin = call.InputDataPins[1];

            Assert.True(sbytePin.UsesUnconnectedValue);
            Assert.True(decimalPin.UsesUnconnectedValue);

            sbytePin.UnconnectedValue = (sbyte)-5;
            decimalPin.UnconnectedValue = 12.5m;

            Assert.Equal("-5", TranslatorUtil.ObjectToLiteral(sbytePin.UnconnectedValue, SByteType));
            Assert.Equal("12.5M", TranslatorUtil.ObjectToLiteral(decimalPin.UnconnectedValue, DecimalType));
        }

        private static MethodSpecifier Method(params TypeSpecifier[] parameterTypes) =>
            new(
                "Take",
                parameterTypes.Select((type, index) => new MethodParameter("p" + index, type, MethodParameterPassType.Default, false, null)).ToArray(),
                [],
                MethodModifiers.Static,
                MemberVisibility.Public,
                TypeSpecifier.FromType(typeof(Math)),
                []);
    }
}
