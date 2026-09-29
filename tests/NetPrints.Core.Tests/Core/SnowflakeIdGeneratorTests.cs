using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>DF-T18/DF-T22: <see cref="SnowflakeIdGenerator"/>'s bit layout, monotonic sequencing
    /// and <see cref="IdFormat"/>'s round trip (data-model.md §2).</summary>
    public class SnowflakeIdGeneratorTests
    {
        [Fact]
        public void IdsMatchThePublishedPattern()
        {
            var time = new FakeTimeProvider(SnowflakeIdGenerator.Epoch);
            var generator = new SnowflakeIdGenerator(time, sessionId: 7);
            var regex = new Regex(IdFormat.Pattern);

            for (int i = 0; i < 200; i++)
            {
                Assert.Matches(regex, generator.NewId('n'));
            }
        }

        [Fact]
        public void SequenceIncrementsWithinTheSameMillisecondThenAdvancesTheClock()
        {
            var time = new FakeTimeProvider(SnowflakeIdGenerator.Epoch);
            var generator = new SnowflakeIdGenerator(time, sessionId: 1);

            string first = generator.NewId('n');
            string second = generator.NewId('n'); // same fake-clock millisecond: sequence advances.

            IdFormat.TryParse(first, out _, out long firstValue);
            IdFormat.TryParse(second, out _, out long secondValue);

            Assert.Equal(firstValue + 1, secondValue);
        }

        [Fact]
        public void SequenceOverflowAdvancesALogicalMillisecondInsteadOfReusingOne()
        {
            var time = new FakeTimeProvider(SnowflakeIdGenerator.Epoch);
            var generator = new SnowflakeIdGenerator(time, sessionId: 1);

            string previous = generator.NewId('n');
            for (int i = 0; i < 64; i++) // 6-bit sequence: 64 values (0..63) exhaust one millisecond.
            {
                string next = generator.NewId('n');
                Assert.True(string.CompareOrdinal(previous, next) < 0);
                previous = next;
            }
        }

        /// <summary>A settable clock that, unlike <see cref="FakeTimeProvider"/>, allows going
        /// backwards: exactly the scenario under test (data-model.md §2's "clock goes backwards"
        /// case).</summary>
        private sealed class RewindableTimeProvider : TimeProvider
        {
            private DateTimeOffset now;

            public RewindableTimeProvider(DateTimeOffset now) => this.now = now;

            public void Set(DateTimeOffset value) => now = value;

            public override DateTimeOffset GetUtcNow() => now;
        }

        [Fact]
        public void AClockThatGoesBackwardsNeverReusesOrLowersAnId()
        {
            var time = new RewindableTimeProvider(SnowflakeIdGenerator.Epoch + TimeSpan.FromMilliseconds(100));
            var generator = new SnowflakeIdGenerator(time, sessionId: 1);

            string beforeRewind = generator.NewId('n');
            time.Set(SnowflakeIdGenerator.Epoch); // clock jumps backwards.
            string afterRewind = generator.NewId('n');

            Assert.True(string.CompareOrdinal(beforeRewind, afterRewind) < 0);
        }

        [Fact]
        public void TwoGeneratorsWithDifferentSessionsNeverCollide()
        {
            var time = new FakeTimeProvider(SnowflakeIdGenerator.Epoch);
            var a = new SnowflakeIdGenerator(time, sessionId: 1);
            var b = new SnowflakeIdGenerator(time, sessionId: 2);

            Assert.NotEqual(a.NewId('n'), b.NewId('n'));
        }

        [Theory]
        [InlineData('n', 0L)]
        [InlineData('m', 1L)]
        [InlineData('n', 0x7FFFFFFFFFFFFFFFL)]
        public void FormatThenTryParseRoundTrips(char prefix, long value)
        {
            string id = IdFormat.Format(prefix, value);

            Assert.True(IdFormat.TryParse(id, out char parsedPrefix, out long parsedValue));
            Assert.Equal(prefix, parsedPrefix);
            Assert.Equal(value, parsedValue);
        }

        [Fact]
        public void FormatRejectsANegativeValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IdFormat.Format('n', -1));
        }

        [Fact]
        public void TryParseIsCaseInsensitiveAndAcceptsCrockfordAliases()
        {
            string canonical = IdFormat.Format('n', 12345);

            Assert.True(IdFormat.TryParse(canonical.ToUpperInvariant(), out char prefix, out long value));
            Assert.Equal('n', prefix);
            Assert.Equal(12345, value);

            // 'i'/'l' -> 1, 'o' -> 0: substituting an alias character where the canonical form has the
            // character it stands for still parses to the same value.
            string withZero = IdFormat.Format('n', 0);
            string withZeroAlias = "n" + new string('o', IdFormat.ValueDigits);
            Assert.True(IdFormat.TryParse(withZeroAlias, out _, out long aliasedValue));
            Assert.True(IdFormat.TryParse(withZero, out _, out long canonicalValue));
            Assert.Equal(canonicalValue, aliasedValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData("n123")]
        [InlineData("xaaaaaaaaaaaaaa")]
        [InlineData("naaaaaaaaaaaaau")] // 'u' is not part of the alphabet or its aliases.
        public void TryParseRejectsMalformedText(string text)
        {
            Assert.False(IdFormat.TryParse(text, out _, out _));
        }
    }
}
