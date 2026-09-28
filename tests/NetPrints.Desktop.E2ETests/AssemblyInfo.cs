using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing;

// Linux (X11) only.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

// Collections run in parallel (xunit.runner.json maxParallelThreads); each test rents its own
// worker (private Xvfb display + a fresh, never-reused editor) from DesktopWorkerPool, so no
// cross-test resource is shared and no test needs a serial collection — see the pool's own XML
// doc and docs/adr/0006-parallel-desktop-e2e.md (batch D2).
[assembly: AssemblyFixture(typeof(DesktopWorkerPool))]

// samples/ pollution guard (AGENTS.md "Never leave changes under samples/"); see its own XML doc.
[assembly: AssemblyFixture(typeof(SamplesDirectoryGuardFixture))]
