using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NetPrints.Core;
using Xunit;

namespace NetPrints.Tests.Core
{
    public class IdGenerationTests
    {
        [Fact]
        public void RandomIdsMatchShapeAndAlphabet()
        {
            var regex = new Regex(IdFormat.Pattern);
            string previous = "";

            for (int i = 0; i < 10_000; i++)
            {
                string id = RandomIdGenerator.Instance.NewId('m');
                Assert.Matches(regex, id);

                // SnowflakeIdGenerator is monotonic: string order must equal generation order.
                Assert.True(string.CompareOrdinal(previous, id) < 0);
                previous = id;
            }
        }

        [Fact]
        public void SeededGeneratorIsDeterministic()
        {
            var first = new SeededIdGenerator(42);
            var second = new SeededIdGenerator(42);

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(first.NewId('n'), second.NewId('n'));
            }
        }

        [Fact]
        public void DifferentSeedsUsuallyGiveDifferentIds()
        {
            var a = new SeededIdGenerator(1);
            var b = new SeededIdGenerator(2);

            Assert.NotEqual(a.NewId('n'), b.NewId('n'));
        }

        [Fact]
        public void UseRestoresThePreviousGeneratorOnDispose()
        {
            var outer = new SeededIdGenerator(1);
            var inner = new SeededIdGenerator(2);

            using (IdGeneration.Use(outer))
            {
                Assert.Same(outer, IdGeneration.Current);

                using (IdGeneration.Use(inner))
                {
                    Assert.Same(inner, IdGeneration.Current);
                }

                Assert.Same(outer, IdGeneration.Current);
            }

            Assert.Same(RandomIdGenerator.Instance, IdGeneration.Current);
        }

        [Fact]
        public async Task UseFlowsAcrossAwait()
        {
            var generator = new SeededIdGenerator(3);

            using (IdGeneration.Use(generator))
            {
                await Task.Yield();
                Assert.Same(generator, IdGeneration.Current);
            }
        }

        [Fact(Timeout = 10000)]
        public async Task UseInAParallelTaskDoesNotLeakBackToTheCaller()
        {
            var outer = new SeededIdGenerator(4);
            var inner = new SeededIdGenerator(5);

            using (IdGeneration.Use(outer))
            {
                bool innerSawItsOwnScope = await Task.Run(() =>
                {
                    using (IdGeneration.Use(inner))
                    {
                        return ReferenceEquals(IdGeneration.Current, inner);
                    }
                });

                Assert.True(innerSawItsOwnScope);

                // The parallel task's scope does not propagate back to the caller.
                Assert.Same(outer, IdGeneration.Current);
            }
        }

        [Theory]
        [InlineData("", unchecked((int)2166136261))]
        [InlineData("a", unchecked((int)0xE40C292C))]
        public void SeedForMatchesFnv1a(string text, int expected)
        {
            Assert.Equal(expected, StableIds.SeedFor(text));
        }

        [Theory]
        [InlineData("")]
        [InlineData("a/b")]
        [InlineData("a b")]
        public void IsValidDocumentIdRejectsMalformedIds(string id)
        {
            Assert.False(StableIds.IsValidDocumentId(id));
        }

        [Fact]
        public void IsValidDocumentIdAcceptsAnOrdinaryId()
        {
            Assert.True(StableIds.IsValidDocumentId("n1a2b3c"));
        }

        [Fact]
        public void IsValidDocumentIdRejectsNull()
        {
            Assert.False(StableIds.IsValidDocumentId(null));
        }
    }
}
