# NetPrints.Extensibility.Testing.Xunit

The xUnit adapter of the [NetPrints extension test kit](https://www.nuget.org/packages/NetPrints.Extensibility.Testing). Derive from `ConformanceTests` with your extension type and every conformance check of the kit becomes one test.

```csharp
public sealed class MyExtensionConformance : ConformanceTests<MyExtension>;
```

The base class looks for the folder that holds `netprints-extension.json` next to the extension assembly (or in a parent folder) and loads the extension in-process under the manifest found there. Override `CreateOptions` to point at another folder or to give the source directory. A check that cannot run is reported as skipped, and a failed check names its code, the rule and the offending item.

This package needs xunit.v3.

## See also

- [Extensions guide](https://danielmeza.github.io/netprints/guide/extensions)
- [Diagnostics](https://danielmeza.github.io/netprints/guide/diagnostics) — the `NPK` codes
