using System;
using System.Globalization;
using NetPrints.Core;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests
{
    /// <summary>Unconnected pin values and literals become valid C# literals.</summary>
    public class LiteralTests
    {
        [Fact]
        public void BooleansAreLowerCase()
        {
            Assert.Equal("true", TranslatorUtil.ObjectToLiteral(true, TypeSpecifier.FromType<bool>()));
            Assert.Equal("false", TranslatorUtil.ObjectToLiteral(false, TypeSpecifier.FromType<bool>()));
        }

        [Fact]
        public void NumbersIgnoreTheCulture()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("1.5D", TranslatorUtil.ObjectToLiteral(1.5, TypeSpecifier.FromType<double>()));
                Assert.Equal("2.25F", TranslatorUtil.ObjectToLiteral(2.25f, TypeSpecifier.FromType<float>()));
                Assert.Equal("3.5M", TranslatorUtil.ObjectToLiteral(3.5m, TypeSpecifier.FromType<decimal>()));
                Assert.Equal("-1234567", TranslatorUtil.ObjectToLiteral(-1234567, TypeSpecifier.FromType<int>()));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [Fact]
        public void StringsAndCharsAreEscaped()
        {
            Assert.Equal("\"C:\\\\temp \\\"quoted\\\"\\n\"", TranslatorUtil.ObjectToLiteral("C:\\temp \"quoted\"\n", TypeSpecifier.FromType<string>()));
            Assert.Equal("'\\''", TranslatorUtil.ObjectToLiteral('\'', TypeSpecifier.FromType<char>()));
        }

        /// <summary>
        /// NaN and the infinities have no C# numeric literal ("NaND", "InfinityD" do not compile);
        /// they must come out as the static field instead. Convert.ChangeType (NodePinVM's pin
        /// editor path) accepts all three from user text, including an overflowing value like
        /// "1e400", which double.Parse clamps to PositiveInfinity rather than throwing.
        /// </summary>
        [Theory]
        [InlineData(double.NaN, "double.NaN")]
        [InlineData(double.PositiveInfinity, "double.PositiveInfinity")]
        [InlineData(double.NegativeInfinity, "double.NegativeInfinity")]
        public void NonFiniteDoublesUseTheStaticField(double value, string expected) =>
            Assert.Equal(expected, TranslatorUtil.ObjectToLiteral(value, TypeSpecifier.FromType<double>()));

        [Theory]
        [InlineData(float.NaN, "float.NaN")]
        [InlineData(float.PositiveInfinity, "float.PositiveInfinity")]
        [InlineData(float.NegativeInfinity, "float.NegativeInfinity")]
        public void NonFiniteFloatsUseTheStaticField(float value, string expected) =>
            Assert.Equal(expected, TranslatorUtil.ObjectToLiteral(value, TypeSpecifier.FromType<float>()));

        [Fact]
        public void PinEditorTextForNonFiniteAndOverflowingDoublesTranslates()
        {
            Assert.Equal("double.NaN", TranslatorUtil.ObjectToLiteral(
                Convert.ChangeType("NaN", typeof(double), CultureInfo.InvariantCulture), TypeSpecifier.FromType<double>()));
            Assert.Equal("double.PositiveInfinity", TranslatorUtil.ObjectToLiteral(
                Convert.ChangeType("1e400", typeof(double), CultureInfo.InvariantCulture), TypeSpecifier.FromType<double>()));
        }
    }
}
