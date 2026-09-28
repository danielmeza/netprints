using NetPrints.Testing;
using Xunit;

// Registers NetPrints.Testing.SamplesDirectoryGuardFixture (samples/ pollution guard, AGENTS.md
// "Never leave changes under samples/") for this assembly; see its own XML doc for what it checks.
[assembly: AssemblyFixture(typeof(SamplesDirectoryGuardFixture))]
