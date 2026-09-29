using System;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary>DF-T07: <see cref="TypedValueConverter"/> round trips for every supported value type.</summary>
    public class TypedValueConverterTests
    {
        private static void RoundTrips(object value, string expectedType, string expectedValue)
        {
            TypedValue typed = TypedValueConverter.ToTypedValue(value, "n0/in.data.value");

            Assert.Equal(expectedType, typed.Type);
            Assert.Equal(expectedValue, typed.Value);
            Assert.Equal(value, TypedValueConverter.FromTypedValue(typed));
        }

        [Fact]
        public void Bool() => RoundTrips(true, "System.Boolean", "true");

        [Fact]
        public void BoolFalse() => RoundTrips(false, "System.Boolean", "false");

        [Fact]
        public void Char() => RoundTrips('x', "System.Char", "x");

        [Fact]
        public void StringWithQuotesAndNewlines() => RoundTrips("Quote \" and\nnewline", "System.String", "Quote \" and\nnewline");

        [Fact]
        public void Byte() => RoundTrips((byte)200, "System.Byte", "200");

        [Fact]
        public void SByte() => RoundTrips((sbyte)-100, "System.SByte", "-100");

        [Fact]
        public void Int16() => RoundTrips((short)-1234, "System.Int16", "-1234");

        [Fact]
        public void UInt16() => RoundTrips((ushort)1234, "System.UInt16", "1234");

        [Fact]
        public void Int32() => RoundTrips(-123456, "System.Int32", "-123456");

        [Fact]
        public void UInt32() => RoundTrips(123456u, "System.UInt32", "123456");

        [Fact]
        public void Int64() => RoundTrips(-123456789012L, "System.Int64", "-123456789012");

        [Fact]
        public void UInt64() => RoundTrips(123456789012UL, "System.UInt64", "123456789012");

        [Fact]
        public void Double() => RoundTrips(3.14, "System.Double", 3.14.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

        [Fact]
        public void DoubleNaN() => RoundTripsNonEqualBySameString(double.NaN, "System.Double", "NaN");

        [Fact]
        public void DoublePositiveInfinity() => RoundTrips(double.PositiveInfinity, "System.Double", "Infinity");

        [Fact]
        public void DoubleNegativeInfinity() => RoundTrips(double.NegativeInfinity, "System.Double", "-Infinity");

        [Fact]
        public void DoubleNegativeZero()
        {
            TypedValue typed = TypedValueConverter.ToTypedValue(-0.0, "n0/in.data.value");
            Assert.Equal("-0", typed.Value);

            object? parsed = TypedValueConverter.FromTypedValue(typed);
            Assert.NotNull(parsed);
            var result = (double)parsed;
            Assert.Equal(0.0, result);
            Assert.True(double.IsNegative(result));
        }

        [Fact]
        public void Single() => RoundTrips(2.5f, "System.Single", "2.5");

        [Fact]
        public void Decimal() => RoundTrips(1234.5678m, "System.Decimal", "1234.5678");

        [Fact]
        public void Enum() => RoundTrips(DayOfWeek.Monday, "System.DayOfWeek", "Monday");

        [Fact]
        public void FlagsEnum() => RoundTrips(System.IO.FileAccess.Read | System.IO.FileAccess.Write, "System.IO.FileAccess", "ReadWrite");

        [Fact]
        public void UnsupportedTypeThrows()
        {
            var ex = Assert.Throws<DocumentFormatException>(() => TypedValueConverter.ToTypedValue(new object(), "n0/in.data.value"));
            Assert.Contains("n0/in.data.value", ex.Message);
        }

        [Fact]
        public void NullValueFromTypedValueIsNull()
        {
            Assert.Null(TypedValueConverter.FromTypedValue(new TypedValue("System.String", null)));
        }

        [Fact]
        public void UnresolvableEnumTypeThrows()
        {
            Assert.Throws<DocumentFormatException>(() => TypedValueConverter.FromTypedValue(new TypedValue("Some.Unknown.Enum", "X")));
        }

        private static void RoundTripsNonEqualBySameString(double value, string expectedType, string expectedValue)
        {
            TypedValue typed = TypedValueConverter.ToTypedValue(value, "n0/in.data.value");
            Assert.Equal(expectedType, typed.Type);
            Assert.Equal(expectedValue, typed.Value);
            object? converted = TypedValueConverter.FromTypedValue(typed);
            Assert.NotNull(converted);
            var parsed = (double)converted;
            Assert.True(double.IsNaN(parsed));
        }
    }
}
