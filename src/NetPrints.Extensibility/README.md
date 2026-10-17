# NetPrints.Extensibility

The NetPrints extension host: discovery of extension folders, manifest validation, isolated loading, the extension registry and the contribution registries that the editor and the command line read.

The NetPrints editor and CLI use this package; an application that embeds NetPrints and wants to load extensions can do the same. Extension authors reference [NetPrints.Extensibility.Abstractions](https://www.nuget.org/packages/NetPrints.Extensibility.Abstractions) instead and do not need this package.

Problems in an extension (a bad manifest, a duplicate id, a contribution without the manifest prefix) are reported as `NPX` diagnostics with stable codes, and a failing extension never stops the host.

## See also

- [Extensions guide](https://danielmeza.github.io/netprints/guide/extensions)
- [NetPrints.Extensibility.Abstractions](https://www.nuget.org/packages/NetPrints.Extensibility.Abstractions) — the contracts
