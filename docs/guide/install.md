# Install

This is a short placeholder. The full install guide — the editor download table, `dotnet tool
install -g NetPrints.Cli`, `dotnet add package NetPrints.Sdk`, the .NET 10 SDK requirement,
unsigned-build steps and checksum verification — lands with the README overhaul tracked in the
[roadmap](../../.specify/memory/roadmap.md) (P1, task T120).

Until then:

- Editor builds are attached to each [GitHub release](https://github.com/danielmeza/netprints/releases).
- The CLI is a .NET tool: `dotnet tool install -g NetPrints.Cli`.
- The SDK is a NuGet package: `dotnet add package NetPrints.Sdk`.
- Building and running a compiled graph needs the .NET 10 SDK (the runtime it includes is enough to
  just run one).
