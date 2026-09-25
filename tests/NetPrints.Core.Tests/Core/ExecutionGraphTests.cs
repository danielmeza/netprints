using System;
using System.Runtime.CompilerServices;
using NetPrints.Core;
using Xunit;

namespace NetPrintsUnitTests
{
    /// <summary>
    /// Covers the guard exceptions added when replacing null-forgiving operators (T011, AGENTS.md
    /// "Nullable reference types"): paths that used to throw an opaque <see cref="NullReferenceException"/>
    /// (or worse, silently produce a wrong result) now throw a clear <see cref="InvalidOperationException"/>.
    /// </summary>
    public class ExecutionGraphTests
    {
        [Fact]
        public void EntryNodeThrowsWhenReadBeforeSet()
        {
            // Simulates what DataContractSerializer does: allocate without running any constructor,
            // so the private entryNode backing field is never assigned.
            var graph = (MethodGraph)RuntimeHelpers.GetUninitializedObject(typeof(MethodGraph));

            var ex = Assert.Throws<InvalidOperationException>(() => graph.EntryNode);
            Assert.Contains("EntryNode was read before it was set", ex.Message);
        }

        [Fact]
        public void EntryNodeIsSetByTheConstructor()
        {
            var graph = new MethodGraph("Main");

            Assert.NotNull(graph.EntryNode);
        }
    }
}
