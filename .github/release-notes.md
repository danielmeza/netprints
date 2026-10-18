## Unreleased

**Breaking (API):** the texts of the editor contributions are `LocalizedText` instead of `string`: `DialogDescriptor.Title`, `DashboardTileDescriptor.Title`, `ProjectTemplateDescriptor.DisplayName` and `Description`, and `NodeSuggestion.DisplayName`. A string converts implicitly, so callers compile; code that reads these properties as strings uses `.Text`. A plain string takes the convention key (`Dialog.<id>.Title`, `Tile.<id>.Title`, `Template.<id>.Name`, `Template.<id>.Description`); a suggestion given as a plain string is its own key, and `new LocalizedText(key, text)` separates the two. `NetPrints.Extensibility` and `NetPrints.Extensibility.Abstractions` have no published baseline, so package validation records no suppression.

**Breaking (API):** the pin properties of eight built-in nodes are named after their stored pin key. Saved graphs and generated code are unchanged. There are no `[Obsolete]` shims; rename the calls:

- `IfElseNode.ExecutionPin`, `ForLoopNode.ExecutionPin`: `ExecPin`
- `ReturnNode.ReturnPin`: `ExecPin`
- `ExecutionEntryNode.InitialExecutionPin` (so `MethodEntryNode` and `ConstructorEntryNode`), `EventEntryNode.InitialExecutionPin`: `ExecPin`
- `DefaultNode.DefaultValuePin`: `DefaultPin`
- `ClassReturnNode.SuperTypePin`: `BaseTypePin`

`TypeOfNode.InputTypePin` keeps its name, because `TypePin` is its output pin.

**Breaking (API):** six pin getters return a typed pin, which is a binary break for code compiled against 0.3.0 (recompile):

- `IfElseNode.ConditionPin`: `NodeInputDataPin<bool>`
- `ForLoopNode.MaxIndexPin`, `ForLoopNode.InitialIndexPin`: `NodeInputDataPin<int>`
- `ThrowNode.ExceptionPin`: `NodeInputDataPin<Exception>`
- `ForLoopNode.IndexPin`: `NodeOutputDataPin<int>`
- `TypeOfNode.TypePin`: `NodeOutputDataPin<Type>`

**Breaking (API):** eleven built-in node classes are `sealed`: `IfElseNode`, `ForLoopNode`, `ThrowNode`, `TypeReturnNode`, `TypeOfNode`, `DefaultNode`, `MakeArrayTypeNode`, `ClassReturnNode`, `ReturnNode`, `MethodEntryNode` and `ConstructorEntryNode`. Code that derives from one has to derive from `Node`, `ExecNode` or `ExecutionEntryNode` instead. The `HandleInputTypeChanged` overrides of `DefaultNode`, `MakeArrayTypeNode`, `MethodEntryNode` and `ReturnNode` are gone; pins now follow their type pins on their own.

**Breaking (API):** `CanSetPure` follows pin roles. A node can be made pure exactly when an output data pin has the `Result` or `OutParam` role, so the per-class overrides of `CallMethodNode`, `ConstructorNode`, `TernaryNode` and `ExplicitCastNode` are gone. `CallMethodNode.ReturnValuePins` lists the `Result` pins only.

**Breaking (extensions):** the extension API version is 0.4. An `extension.json` with `"netprintsApi": "1.0"`, the value the 0.2.0 guide published, no longer loads: the host accepts the same major and a minor up to its own, and reports NPX002. Set `netprintsApi` to `"0.4"` and rebuild against the new packages.

**Breaking (generated code):** for a project whose C# target is 13 or newer, the escape character in a generated string or char literal is written `\e` instead of `\u001b`. The output of a project on an older target, and every golden without an escape character, is unchanged.

**Added:** three packages, `NetPrints.Serialization`, `NetPrints.Extensibility.Abstractions` and `NetPrints.Extensibility`, with the extension API. The stable part of the API is tracked in `PublicAPI.Shipped.txt`; what is not yet stable is marked `[Experimental]` with an `NPXE` id. `NetPrints.Core` carries the same API tracking and the author analyzers: NPA020 to NPA024 (icon ids, gestures, contribution ids, duplicate commands and menu slots) and NPA040 to NPA042 (resource keys of node kinds); `docs/guide/diagnostics.md` lists their severities.

**Added:** a node of an extension kind may carry `kindVersion` in the graph file, the version of the data its kind wrote it with; a registered migration upgrades older data when the graph opens, and an older editor keeps the node untouched.

**Added:** `netprints.language` in the `netprints` settings section chooses the editor language (a culture name; empty follows the operating system).

**Added:** a Settings document (File › Settings, `Ctrl+,`) with the pages General, Appearance, Keyboard shortcuts, Run and Extensions. `settings.json` is described by a published schema, a value out of range falls back to its default (NPX014 for an extension's section), and an unreadable file is copied to `settings.json.unreadable-<timestamp>` before the first write.

**Added:** every command can be rebound on the Keyboard shortcuts page, with conflict resolution; your changes are stored as differences from the defaults in the `keyboard` section.

**Added:** theme presets per variant (NetPrints Dark, NetPrints Light, Classic Dark, High contrast) chosen on Settings › Appearance with `darkPreset` and `lightPreset`, applied without a restart. Every theme token is in a catalogue, and a preset that fails the contrast gate is reported.

**Added:** the status bar shows the error and warning counts, the number of selected nodes and the zoom level of the open graph. Their texts, with one form for a single item and one for several, are in the resx.

**Added:** a call to a method with `out` or `ref` parameters has an output pin for each of them (keyed `out.data.<parameterName>`) that carries the value after the call, and a void method with an `out` parameter can be made pure. Saved graphs do not change. Node capabilities (`IPinListEditor`, `IOverloadableNode`, `IPinNameEditor`) are role interfaces the editor asks for.

## Downloads

| Platform | File |
|---|---|
| Linux x64 | `NetPrints-<version>-linux-x64.tar.gz` |
| Windows x64 | `NetPrints-<version>-win-x64.zip` |
| macOS Apple silicon | `NetPrints-<version>-osx-arm64.tar.gz` |

The editor archives are self-contained: they run without a .NET runtime. Opening, building and running
NetPrints projects needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), which also
provides the runtime your compiled programs need.

Packages: `dotnet tool install -g NetPrints.Cli` (command `netprints`) and
`<PackageReference Include="NetPrints.Sdk" Version="…" PrivateAssets="all" />`.

**These builds are not code-signed.** Windows SmartScreen shows "Windows protected your PC": choose
*More info* → *Run anyway*. On macOS, open it once, then *System Settings* → *Privacy & Security* →
*Open Anyway* (or run `xattr -dr com.apple.quarantine NetPrints-<version>-osx-arm64`).

Verify a download: `sha256sum -c --ignore-missing SHA256SUMS.txt` and `gh attestation verify <file> -R danielmeza/netprints`.
