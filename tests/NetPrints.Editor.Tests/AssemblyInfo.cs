using NetPrints.Testing;
using Xunit;

// Tests share the preloaded reflection host and write to temp folders; keep them sequential.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// samples/ pollution guard (AGENTS.md "Never leave changes under samples/"); see its own XML doc.
[assembly: AssemblyFixture(typeof(SamplesDirectoryGuardFixture))]
