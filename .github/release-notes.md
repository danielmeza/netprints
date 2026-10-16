## Unreleased

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
