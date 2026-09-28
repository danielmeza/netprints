using NetPrints.Testing;

// One X server and real input: E2E tests run one at a time. Linux (X11) only.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

// samples/ pollution guard (AGENTS.md "Never leave changes under samples/"); see its own XML doc.
[assembly: AssemblyFixture(typeof(SamplesDirectoryGuardFixture))]
