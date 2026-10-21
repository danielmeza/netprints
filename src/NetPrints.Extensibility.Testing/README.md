# NetPrints.Extensibility.Testing

The test kit for NetPrints extensions. It loads an extension the way the editor does and checks it against the rules of the extension API, with no editor and no UI.

- `ExtensionHarness` loads real extensions (created in the test process) and folder-based ones (a folder with a `netprints-extension.json`), then translates, writes and reads graphs with what they contribute.
- `ExtensionTest` is a generic base class over your extension type for tests: declare what to load and which node kinds to place in `State`, call `RunAsync`, and assert on the result.
- `ConformanceSuite` runs the conformance checks (`NPK001` and up) and returns a `ConformanceReport`. A failed check names its code, the rule and the offending item.

```csharp
await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], cancellationToken);
ConformanceReport report = await ConformanceSuite.RunAsync(harness, new ConformanceOptions { ExtensionFolder = folder }, cancellationToken);
Assert.True(report.Passed, report.Format());
```

For xUnit, [NetPrints.Extensibility.Testing.Xunit](https://www.nuget.org/packages/NetPrints.Extensibility.Testing.Xunit) adds `ConformanceTests`, a generic base class with one test per check.

## See also

- [Extensions guide](https://danielmeza.github.io/netprints/guide/extensions)
- [Diagnostics](https://danielmeza.github.io/netprints/guide/diagnostics) — the `NPK` codes
