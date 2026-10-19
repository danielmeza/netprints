# Testing an extension

The extension test kit loads your extension the way the editor does and checks it against the rules of the extension API. It needs no editor and no UI, and it runs in an ordinary test project. Two packages carry it:

- `NetPrints.Extensibility.Testing` is framework-agnostic: the `ExtensionHarness`, the declarative `ExtensionTest`, and the `ConformanceSuite` with its checks.
- `NetPrints.Extensibility.Testing.Xunit` adds `ConformanceTests`, a base class with one xUnit test per check.

The checks are the `NPK` codes of the [diagnostics guide](diagnostics.md#npk-extension-conformance-checks). A failed check names its code, the rule it enforces and the item that breaks it, for example `NPK015 contribution ids start with the extension id: command other.hello: the id must start with my.extension.`

## The harness

`ExtensionHarness` loads the built-in extension, any extension folders you give it (a folder with a `netprints-extension.json` and the extension's assemblies) and any extensions created in the test process. It then translates graphs to C#, writes them as `.netpc.json` documents and reads them back with whatever the extensions contributed.

```csharp
await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], cancellationToken);

ClassGraph graph = harness.BuildClass("Demo", "Subject", "my.extension/Ping");
string code = harness.Translate(graph);
byte[] document = await harness.WriteAsync(graph, cancellationToken);
```

The harness also keeps the editor contributions the extensions made (`EditorContributions`) and the warnings and errors the host logged (`Log`), so a test can check them without an editor.

## A declarative test

`ExtensionTest` loads one extension from a state you declare, runs the pipeline and hands you the result:

```csharp
public sealed class PingTests : ExtensionTest<MyExtension>
{
    [Fact]
    public async Task PingTranslatesAndRoundTrips()
    {
        State.ExtensionId = "my.extension";
        State.NodeKinds.Add("my.extension/Ping");

        ExtensionTestResult result = await RunAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.LoadFailures);
        Assert.Contains("ping", result.TranslatedCode, StringComparison.Ordinal);
        Assert.Equal(result.Document, result.RoundTrippedDocument);
    }
}
```

## The conformance suite

With xUnit, derive a class and the checks run as tests:

```csharp
public sealed class MyExtensionConformanceTests : ConformanceTests<MyExtension>
{
    protected override string? ExtensionFolder => MyPaths.BuildOutput;

    protected override string? SourceDirectory => MyPaths.ProjectFolder;
}
```

`ExtensionFolder` is the extension's build output: `netprints-extension.json`, the extension assembly and nothing the host supplies. NPK001 to NPK003 read it and are skipped while it is `null`, which is the default. `SourceDirectory` is where your `.axaml` files are; it defaults to the folder that holds `netprints-extension.json` next to the extension assembly or above it. Without xUnit, call `ConformanceSuite.RunAsync` and assert on `ConformanceReport.Passed`; `ConformanceOptions` takes the same folders plus the factories a few checks use to load the extension again:

```csharp
ConformanceReport report = await ConformanceSuite.RunAsync(harness, options, cancellationToken);
Assert.True(report.Passed, report.Format());
```

A check that has nothing to look at passes with a note, and a check that cannot run reports `Skipped` with the reason.

## The checks

### NPK001

The manifest is valid, its id is well formed and its `netprintsApi` is a version the host implements. Fix: correct the field the message names; the API version is in the [extensions guide](extensions.md#api-stability).

### NPK002

The folder has the loader's layout and holds no copy of an assembly the host supplies. Fix: reference the NetPrints packages with `Private="false"` / `ExcludeAssets="runtime"` so they are not copied to the output.

### NPK003

Every NetPrints assembly the extension references is part of the extension contract, so its types are the host's. Fix: remove the reference to an internal assembly such as `NetPrints.Generation`.

### NPK004

Registering twice gives the same registry. Needs a `HarnessFactory`. Fix: keep `Register` free of counters, clocks, random values and other process state.

### NPK005

The extension loads and its contributions raise no registry issue (duplicate ids, a kind outside the extension's prefix, conflicting gestures). Service registrations that the host rejects are reported by NPK013.

### NPK006

Every node kind that offers a node-search entry is written to a document, read back and written again to the same bytes. Fix: make the node's document converter keep everything it writes. A kind without a node-search entry cannot be built, so the check is Skipped and names it; give the kind an entry to have it checked.

### NPK007

The C# generated for each node kind compiles. The kit puts one node of the kind in a class, gives input pins without a connection a literal, and compiles the translation. Fix: the translator must emit valid C#. A kind without a node-search entry cannot be built, so the check is Skipped and names it; give the kind an entry to have it checked.

### NPK008

Two translations of the same graph produce identical text. Fix: no timestamps, unordered sets or random names in emitted code.

### NPK009

Each settings section serializes its default, accepts it as valid, writes it back unchanged and exports a JSON schema. Fix: make the default pass the section's own validator and use only types the source-generated JSON context can describe.

### NPK010

Each host channel is open or connecting when it is created, takes and releases a subscriber, and is closed after disposal, which can be repeated. It then checks that `Messages` completed and that `SendAsync` on the closed channel throws `InvalidOperationException`, as `IHostChannel` documents. Fix: set the state to closed in `DisposeAsync` and make a second call do nothing.

### NPK011

Loading the extension changes nothing outside it, and nothing a neighbour does changes the extension. The suite loads it a second time next to an extension that throws in `Register` and one that claims the same project properties, and compares what the extension contributes with the first load. It also reports a changed environment variable or current directory. Needs a `NeighbourHarnessFactory`, which `ConformanceTests` provides. Fix: do not touch process-wide state in `Register`.

### NPK012

A freshly loaded extension is disposed with a session still open, and then disposed again; nothing it contributed may throw. Needs a `HarnessFactory`. Fix: make `Dispose` of a contribution idempotent and free of exceptions. Load contexts are not collectible yet, so the kit does not test that the context is released.

### NPK013

The extension's service provider builds and validates (no missing registration, no singleton that takes a scoped service), holds only the extension's own types and does not replace a host export (see the host exports in the [extensions guide](extensions.md)). The leak test then opens a session, resolves each scoped or transient service, disposes the session and checks that the instance is no longer reachable. Fix: do not keep scoped services in static fields or singletons.

### NPK015

Editor contributions follow the editor's rules: ids start with the extension id, labels have a key with a default text or a string resource, gestures parse back to themselves and a global command has no gesture without a modifier (use a function key, a modifier or a narrower scope), and an icon id is the extension id, a dot and a name that a view resource in `SourceDirectory` defines as `Icon.<id>`.

### NPK016

The extension's `.axaml` views hold no colour literal outside a theme dictionary and no `FontSize` or `FontFamily` literal, the E2 and E8 rules of the editor's own XAML tests. Use theme tokens (`{DynamicResource ...}`), style classes and the type ramp, so the views follow the theme. Every `.axaml` file is scanned, `Styles` and `ResourceDictionary` files included; only an element inside `ThemeDictionaries` may hold a colour.

### NPK017

Every NetPrints type and member the extension references exists in the host. The suite does not run this check: [`netprints extension verify`](cli.md#extension-verify--check-an-extension-without-loading-it) does, by reading the extension assembly's metadata and looking each reference up in the host's assemblies by name and signature, so it needs no test project and loads nothing. `NpkDiagnosticCodes.ApiCompatibility` names it. The fix is to build the extension against the NetPrints packages of the host that will load it, or to stop using the member the report names.

NPK014 (graph rules) arrives with the graph rules.

## CI workflow

`.github/workflows/netprints-extension.yml` is a reusable workflow (`workflow_call`) that builds an extension, runs its kit tests, packs it and runs `netprints extension verify` on the package. Call it from your extension's repository:

```yaml
jobs:
  extension:
    uses: danielmeza/netprints/.github/workflows/netprints-extension.yml@master
    with:
      extension-folder: .
```

| Input | Meaning |
|---|---|
| `extension-folder` | Required. The folder that holds the extension project (package type `NetPrintsExtension`) and its `*.Tests.csproj`, as the `netprints-extension` template lays them out. |
| `packages-artifact` | The name of an artifact that holds the packed NetPrints packages (`*.nupkg`). They are used as a local feed for restore and for the `NetPrints.Cli` tool. Without it, the packages come from the configured NuGet sources. |
| `netprints-version` | The version of the `NetPrints.Cli` tool that verifies the package; the latest release by default. |
| `dotnet-version` | The .NET SDK to install; `10.0.x` by default. |

The workflow uploads the extension package as the artifact named in its `package-artifact` output. Pin the `uses:` reference to a release tag or a commit once one exists.
