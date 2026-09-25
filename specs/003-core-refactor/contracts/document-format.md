# Contract: Graph documents, formats, stores and mapping

> Revised 2026-09-25: the project file is an SDK-style `.csproj` (project-system.md). This contract
> now covers only **graph documents** (`.netpc.json`); `ProjectDocument` and the `.netpp.json` file are removed.

Project: `src/NetPrints.Serialization` (new, references `src/NetPrints.Core`; `InternalsVisibleTo`
from Core so the mapper can set `Node.Id`). Tests: `tests/NetPrints.Core.Tests/Serialization/`.
Consumers: Editor, `NetPrints.Generator` (build), CLI, P3 extension host, P4/P5 hosts (VS Code workspace store).
Test obligations (`DF-Txx`) are listed at the end; `tasks.md` references them.

## 1. JSON schema v1

### 1.1 Canonical writing rules (all documents)

| Rule | Value |
|---|---|
| Encoding | UTF-8, no BOM |
| Indentation | 2 spaces (`JsonSerializerOptions.IndentSize = 2`, `WriteIndented = true`) |
| Line endings | `\n` (`JsonSerializerOptions.NewLine = "\n"`), file ends with exactly one `\n` |
| Property names | camelCase (`JsonKnownNamingPolicy.CamelCase`), order = `[JsonPropertyOrder]` as declared below |
| Nulls / defaults | omitted globally (`DefaultIgnoreCondition = WhenWritingDefault`); every member marked **Req. = yes** carries `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]` so default values such as `SharedLibrary` (0) are still written |
| Enums | strings (`UseStringEnumConverter = true`); `[Flags]` enums as `"Static, Async"` (STJ format); `None` omitted |
| Maps | `SortedDictionary<string, …>` with `StringComparer.Ordinal` |
| Numbers | positions as JSON numbers (`double`, round-trip); `TypedValue.value` always a string |
| Discriminator | `"$kind"`, always the first property of a node object |
| Forbidden | timestamps, absolute paths, machine names, `SaveVersion`, `LastCompiledAssemblyPath` |

### 1.2 File names

| Document | Name | Location |
|---|---|---|
| Graph (one class) | `<Namespace>.<ClassName>.netpc.json` for new classes (any name matching `*.netpc.json` is accepted) | anywhere under the project; found by the `NetPrintsGraph` items (project-system.md §2) |
| Generated C# | `<same name without .json>.g.cs`, i.e. `<…>.netpc.g.cs` | next to the graph; committed |
| Project | `<Name>.csproj` | project-system.md §1 |
| Legacy (read-only) | `*.netpp`, `*.netpc` | unchanged, never written |

### 1.3 (removed) `ProjectDocument`

Superseded by the `.csproj` (project-system.md §1). Project settings are MSBuild properties.

### 1.4 `ClassDocument` (the graph file)

| JSON name | C# member | Type | Req. | Notes |
|---|---|---|---|---|
| `schemaVersion` | `SchemaVersion` | int | yes | `1` |
| `namespace` | `Namespace` | string | no | omit if empty |
| `name` | `Name` | string | yes | |
| `visibility` | `Visibility` | `MemberVisibility` | yes | |
| `modifiers` | `Modifiers` | `ClassModifiers` | no | omit if `None` |
| `genericArguments` | `GenericArguments` | string[] | no | declared generic parameter names |
| `classGraph` | `ClassGraph` | `GraphDocument` | yes | graph key `class` |
| `variables` | `Variables` | `VariableDocument[]` | no | class order |
| `methods` | `Methods` | `MethodDocument[]` | no | class order |
| `constructors` | `Constructors` | `ConstructorDocument[]` | no | class order |
| `eventGraphs` | `EventGraphs` | `EventGraphDocument[]` | no | class order |
| `layout` | `Layout` | map graphKey → map nodeId → `[x, y]` | yes | the only place positions live; both maps ordinal-sorted |

Graph keys (`GraphKeys.For`, Core): `class`, `methods/<i>`, `constructors/<i>`, `eventGraphs/<i>`,
`variables/<i>/type`, `variables/<i>/get`, `variables/<i>/set` (`<i>` = zero-based index).

`MethodDocument`: `name`, `visibility`, `modifiers` (omit if `None`), `graph`.
`ConstructorDocument`: `visibility`, `graph`.
`VariableDocument`: `name`, `visibility`, `modifiers` (omit if `None`), `typeGraph`, `getter?` (`AccessorDocument`: `visibility`, `graph`), `setter?`.
`EventGraphDocument`: `name`, `graph`.
`GraphDocument`: `nodes` (`NodeDocument[]`, graph order), `connections` (`ConnectionDocument[]`, ordinal-sorted by `from` then `to`), `locals` (`LocalVariableDocument[]`, method/constructor graphs only, omit if empty).
`LocalVariableDocument`: `name`, `type` (`TypeRef`).
`ConnectionDocument`: `from` = `"<nodeId>/<pin>"` of an **output** pin, `to` = same for an **input** pin.

Pin reference `<pin>` = `<direction>.<kind>.<index>`: direction `in`|`out`, kind `exec`|`data`|`type`,
index = position in the node's corresponding pin collection (e.g. `n2/in.data.0` = `InputDataPins[0]`).

### 1.5 `NodeDocument` (polymorphic, `"$kind"`)

Common fields (in this order after `$kind`): `id` (string, `n<k>`), `name` (node `Name`), `pins`
(`PinStateDocument[]`, omit if empty). `PinStateDocument`: `pin` (pin reference without node id),
`name` (only when the pin name differs from the one the node's constructor creates), `value`
(`TypedValue`, unconnected input value, only if set). Pin *counts* of dynamic nodes are given by the
kind fields below; the mapper creates the pins and then applies `pins`.

| `$kind` | Node type (`NetPrints.Graph`) | Kind fields |
|---|---|---|
| `methodEntry` | `MethodEntryNode` | `argumentCount` (int, omit 0), `genericArguments` (string[], omit empty) |
| `constructorEntry` | `ConstructorEntryNode` | `argumentCount` |
| `return` | `ReturnNode` | `returnCount` (int, omit 0) |
| `classReturn` | `ClassReturnNode` | `interfaceCount` (int, omit 0) |
| `typeReturn` | `TypeReturnNode` | — |
| `eventEntry` | `EventEntryNode` (new) | `eventName`, `visibility`, `modifiers` (omit None), `overrides` (`MethodRef`, optional), `argumentCount` |
| `callMethod` | `CallMethodNode` | `method` (`MethodRef`), `genericArgumentCount` (omit 0) |
| `constructor` | `ConstructorNode` | `constructor` (`ConstructorRef`) |
| `makeDelegate` | `MakeDelegateNode` | `method` (`MethodRef`) |
| `variableGetter` | `VariableGetterNode` | `variable` (`VariableRef`) |
| `variableSetter` | `VariableSetterNode` | `variable` (`VariableRef`) |
| `literal` | `LiteralNode` | `literalType` (`TypeRef`) |
| `type` | `TypeNode` | `type` (`TypeRef`) |
| `makeArrayType` | `MakeArrayTypeNode` | — |
| `makeArray` | `MakeArrayNode` | `usePredefinedSize` (bool), `elementCount` (int, omit 0) |
| `explicitCast` | `ExplicitCastNode` | — |
| `typeOf` | `TypeOfNode` | — |
| `ifElse` | `IfElseNode` | — |
| `forLoop` | `ForLoopNode` | — |
| `ternary` | `TernaryNode` | — |
| `await` | `AwaitNode` | — |
| `throw` | `ThrowNode` | — |
| `default` | `DefaultNode` | — |
| `reroute` | `RerouteNode` | `pinKind` (`exec`\|`data`\|`type`), `count` (int); for `data`: `dataTypes` (`[TypeRef, TypeRef][]`) |
| `<extensionId>/<name>` | extension node | declared by the extension (§5) |
| any unknown | `UnknownNodeDocument` | raw JSON kept and rewritten verbatim |

### 1.6 Reference and value DTOs

| DTO | JSON fields |
|---|---|
| `TypeRef` | `name` (full name, e.g. `System.String`), `generic` (bool, `true` = generic parameter, omit false), `isEnum`, `isInterface` (omit false), `args` (`TypeRef[]`, omit empty) |
| `MethodRef` | `name`, `declaringType` (`TypeRef`), `parameters` (`ParameterRef[]`), `returnTypes` (`TypeRef[]`), `modifiers`, `visibility`, `genericArgs` (`TypeRef[]`) — empty arrays omitted |
| `ParameterRef` | `name`, `type` (`TypeRef`), `passType` (`MethodParameterPassType`, omit `Default`), `default` (`TypedValue`, only when the parameter has an explicit default) |
| `ConstructorRef` | `declaringType`, `parameters` |
| `VariableRef` | `name`, `type`, `declaringType` (omit for locals), `getterVisibility`, `setterVisibility`, `visibility`, `modifiers`, `scope` (`Member`\|`Local`, omit `Member`) |
| `TypedValue` | `type` (full CLR name), `value` (string or JSON `null`) |

`TypedValue` conversion (`TypedValueConverter`, invariant culture): `System.Boolean` → `"true"`/`"false"`;
integral types → `ToString(CultureInfo.InvariantCulture)`; `System.Single`/`Double` → `ToString("R")`,
`NaN`, `Infinity`, `-Infinity` verbatim; `System.Decimal` → invariant; `System.Char` → the character;
`System.String` → the string; enum values → member name (flags: `"A, B"`); any other runtime type →
`DocumentFormatException("Unsupported value type <T> in <node>/<pin>")`.

### 1.7 Complete example: `HelloWorld.Program.netpc.json` (converted legacy sample)

Whitespace is compacted here for reading; the canonical output (verified with STJ 10, `p1spike/`)
puts every property and every array element on its own line, e.g. `"n0": [\n  112,\n  112\n]`.
The committed sample file is the reviewed golden output. Node ids come from legacy import (graph order). `n0` `MethodEntryNode`, `n1` `ReturnNode`, `n2`
`CallMethodNode` (the legacy node order).

```json
{
  "schemaVersion": 1,
  "namespace": "HelloWorld",
  "name": "Program",
  "visibility": "Public",
  "classGraph": {
    "nodes": [
      { "$kind": "classReturn", "id": "n0", "name": "ClassReturnNode" }
    ]
  },
  "methods": [
    {
      "name": "Main",
      "visibility": "Public",
      "modifiers": "Static",
      "graph": {
        "nodes": [
          { "$kind": "methodEntry", "id": "n0", "name": "MethodEntryNode" },
          { "$kind": "return", "id": "n1", "name": "ReturnNode" },
          {
            "$kind": "callMethod",
            "id": "n2",
            "name": "CallMethodNode",
            "pins": [
              { "pin": "in.data.0", "value": { "type": "System.String", "value": "Hello, World!" } }
            ],
            "method": {
              "name": "WriteLine",
              "declaringType": { "name": "System.Console" },
              "parameters": [
                { "name": "value", "type": { "name": "System.String" } }
              ],
              "modifiers": "Static",
              "visibility": "Public"
            }
          }
        ],
        "connections": [
          { "from": "n0/out.exec.0", "to": "n2/in.exec.0" },
          { "from": "n2/out.exec.0", "to": "n1/in.exec.0" }
        ]
      }
    }
  ],
  "layout": {
    "class": { "n0": [112, 112] },
    "methods/0": { "n0": [112, 112], "n1": [840, 112], "n2": [420, 112] }
  }
}
```

The matching `HelloWorld.csproj` is shown in project-system.md §1 (with `OutputType` `Exe` and
`RootNamespace` `HelloWorld`); the converted project has no legacy references (they are dropped with
`NPM001`).

## 2. C# API

### 2.1 Identifiers, issues, exceptions — `src/NetPrints.Serialization/DocumentId.cs`, `DocumentIssue.cs`, `DocumentExceptions.cs`

```csharp
namespace NetPrints.Serialization;

/// Relative, '/'-separated path inside a store. Ordinal equality; case preserved.
public readonly record struct DocumentId
{
    public DocumentId(string path);          // ArgumentException: empty, rooted, contains '\\', "..", or "." segments
    public string Path { get; }
    public string FileName { get; }          // last segment
    public DocumentId Sibling(string fileName); // same directory, other file
    public override string ToString();       // Path
}

public enum DocumentKind { Project, Class }

public enum DocumentIssueSeverity { Info, Warning, Error }

public sealed record DocumentIssue(DocumentIssueSeverity Severity, string Code, string Message, DocumentId? Document);

public class DocumentFormatException : Exception
{
    public DocumentFormatException(string message, DocumentId? document = null, long? line = null, long? bytePosition = null, Exception? inner = null);
    public DocumentId? Document { get; }
    public long? Line { get; }
    public long? BytePosition { get; }
}

public sealed class DocumentVersionException : DocumentFormatException
{
    public DocumentVersionException(int found, int supported, DocumentId? document = null);
    public int Found { get; }
    public int Supported { get; }
}

public sealed class DocumentNotFoundException : Exception
{
    public DocumentNotFoundException(DocumentId document);
    public DocumentId Document { get; }
}
```

### 2.2 Formats — `IDocumentFormat.cs`, `DocumentFormatRegistry.cs`

```csharp
namespace NetPrints.Serialization;

public interface IDocumentFormat
{
    string Id { get; }                              // "json" | "legacy-xml"
    IReadOnlyList<string> ClassExtensions { get; }   // ".netpc.json" | ".netpc"
    bool CanWrite { get; }

    ValueTask<ClassDocument> ReadClassAsync(Stream input, DocumentId id, CancellationToken cancellationToken);
    ValueTask WriteClassAsync(ClassDocument document, Stream output, CancellationToken cancellationToken);
}

public sealed class DocumentFormatRegistry
{
    public DocumentFormatRegistry(IReadOnlyList<IDocumentFormat> formats);
    public IReadOnlyList<IDocumentFormat> Formats { get; }
    public IDocumentFormat Default { get; }          // the JSON format
    public IDocumentFormat? Find(DocumentId id, DocumentKind kind);
}
```

| Member | Contract |
|---|---|
| `Read*Async` | Does not dispose `input`. Throws `DocumentFormatException` (with line/position from `JsonException`) on malformed content, `DocumentVersionException` when `schemaVersion` > supported. Migrates older versions first (§2.5). Honors cancellation between reads. |
| `Write*Async` | Writes the canonical form (§1.1). Does not dispose `output`. `NotSupportedException` when `CanWrite` is false. Deterministic: same document → same bytes. |
| Registry ctor | `ArgumentException` on duplicate `Id` or an extension claimed by two formats; exactly one format with `Id == "json"` must exist (`Default`). |
| `Find` | Longest matching suffix, `OrdinalIgnoreCase` (so `.netpc.json` wins over `.netpc`); `null` if none. |
| Thread-safety | Formats and the registry are immutable and thread-safe. |

`JsonDocumentFormat` (`Json/JsonDocumentFormat.cs`): `public JsonDocumentFormat(NetPrintsJsonOptions options, DocumentMigrator migrator)`.
`LegacyXmlDocumentFormat` (`Legacy/LegacyXmlDocumentFormat.cs`): `public LegacyXmlDocumentFormat(IDocumentMapper mapper)`; `CanWrite = false`; reads with the existing `DataContractSerializer` settings (`PreserveObjectReferences = true`, `MaxItemsInObjectGraph = int.MaxValue`), calls `NodeGraph.AssignLegacyNodeIds()` on every graph, runs `GraphTypeInference.Relax`, then `mapper.ToDocument`.

### 2.3 JSON options and node polymorphism — `Json/NetPrintsJsonContext.cs`, `Json/NetPrintsJsonOptions.cs`, `Json/NodeListConverter.cs`

```csharp
namespace NetPrints.Serialization.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    WriteIndented = true, IndentSize = 2, NewLine = "\n", DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(ClassDocument))]
internal sealed partial class NetPrintsJsonContext : JsonSerializerContext;

public sealed class NetPrintsJsonOptions
{
    public NetPrintsJsonOptions(NodeDocumentConverterRegistry nodes);
    public JsonSerializerOptions SerializerOptions { get; } // frozen (MakeReadOnly) after construction
}

internal sealed class NodeListConverter : JsonConverter<IReadOnlyList<NodeDocument>>; // research R5
```

| Rule | Contract |
|---|---|
| Resolver | `JsonTypeInfoResolver.Combine(NetPrintsJsonContext.Default, <each extension resolver in registry order>)` + one `WithAddedModifier` that adds every extension `(Kind, DocumentType)` to `PolymorphismOptions.DerivedTypes` of `NodeDocument`. |
| Built-in kinds | `[JsonDerivedType(typeof(XNodeDocument), "<kind>")]` on `NodeDocument` for every row of §1.5. |
| Unknown kinds | `NodeListConverter` reads each element as `JsonElement`; unknown `$kind` → `UnknownNodeDocument(Id, Kind, Raw)` and an issue `NPD001`; writes `Raw` back verbatim. Missing `$kind` or `id` → `DocumentFormatException`. |

### 2.4 DTO records — `Documents/*.cs`

```csharp
namespace NetPrints.Serialization.Documents;

public sealed record ClassDocument(
    int SchemaVersion, string? Namespace, string Name, MemberVisibility Visibility, ClassModifiers Modifiers,
    IReadOnlyList<string>? GenericArguments, GraphDocument ClassGraph,
    IReadOnlyList<VariableDocument>? Variables, IReadOnlyList<MethodDocument>? Methods,
    IReadOnlyList<ConstructorDocument>? Constructors, IReadOnlyList<EventGraphDocument>? EventGraphs,
    SortedDictionary<string, SortedDictionary<string, double[]>> Layout);   // JsonPropertyOrder 0..11 in this order

public sealed record GraphDocument(
    [property: JsonConverter(typeof(NodeListConverter))] IReadOnlyList<NodeDocument> Nodes,
    IReadOnlyList<ConnectionDocument>? Connections,
    IReadOnlyList<LocalVariableDocument>? Locals);
public sealed record ConnectionDocument(string From, string To);
public sealed record MethodDocument(string Name, MemberVisibility Visibility, MethodModifiers Modifiers, GraphDocument Graph);
public sealed record ConstructorDocument(MemberVisibility Visibility, GraphDocument Graph);
public sealed record AccessorDocument(MemberVisibility Visibility, GraphDocument Graph);
public sealed record VariableDocument(string Name, MemberVisibility Visibility, VariableModifiers Modifiers,
    GraphDocument TypeGraph, AccessorDocument? Getter, AccessorDocument? Setter);
public sealed record EventGraphDocument(string Name, GraphDocument Graph);
public sealed record LocalVariableDocument(string Name, TypeRef Type);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(MethodEntryNodeDocument), "methodEntry")]
// … one [JsonDerivedType] per row of §1.5 …
public abstract record NodeDocument(
    [property: JsonPropertyOrder(-3)] string Id,
    [property: JsonPropertyOrder(-2)] string Name,
    [property: JsonPropertyOrder(-1)] IReadOnlyList<PinStateDocument>? Pins);
public sealed record PinStateDocument(string Pin, string? Name, TypedValue? Value);
public sealed record UnknownNodeDocument(string Id, string Kind, JsonElement Raw) : NodeDocument(Id, "", null); // never serialized by STJ directly

public sealed record MethodEntryNodeDocument(string Id, string Name, IReadOnlyList<PinStateDocument>? Pins,
    int ArgumentCount, IReadOnlyList<string>? GenericArguments) : NodeDocument(Id, Name, Pins);
public sealed record CallMethodNodeDocument(string Id, string Name, IReadOnlyList<PinStateDocument>? Pins,
    MethodRef Method, int GenericArgumentCount) : NodeDocument(Id, Name, Pins);
// … remaining built-in node documents: exactly the kind fields of §1.5, same pattern …

public sealed record TypeRef(string Name, bool Generic = false, bool IsEnum = false, bool IsInterface = false, IReadOnlyList<TypeRef>? Args = null);
public sealed record ParameterRef(string Name, TypeRef Type, MethodParameterPassType PassType = MethodParameterPassType.Default, TypedValue? Default = null);
public sealed record MethodRef(string Name, TypeRef DeclaringType, IReadOnlyList<ParameterRef>? Parameters,
    IReadOnlyList<TypeRef>? ReturnTypes, MethodModifiers Modifiers, MemberVisibility Visibility, IReadOnlyList<TypeRef>? GenericArgs);
public sealed record ConstructorRef(TypeRef DeclaringType, IReadOnlyList<ParameterRef>? Parameters);
public sealed record VariableRef(string Name, TypeRef Type, TypeRef? DeclaringType, MemberVisibility GetterVisibility,
    MemberVisibility SetterVisibility, MemberVisibility Visibility, VariableModifiers Modifiers, VariableScope Scope = VariableScope.Member);
public sealed record TypedValue(string Type, string? Value);
```

DTOs are immutable; equality is structural except `JsonElement` (compare with `JsonElement.DeepEquals`).

### 2.5 Migrations — `Migrations/DocumentMigrator.cs`

```csharp
namespace NetPrints.Serialization.Migrations;

public interface IDocumentMigration
{
    DocumentKind Kind { get; }
    int FromVersion { get; }            // migrates FromVersion → FromVersion + 1
    void Migrate(JsonObject document);  // mutates in place; must set "schemaVersion"
}

public sealed class DocumentMigrator
{
    public const int CurrentSchemaVersion = 1;
    public DocumentMigrator(IReadOnlyList<IDocumentMigration> migrations); // ArgumentException on duplicate (Kind, FromVersion) or gaps
    public int Supported { get; }                                         // CurrentSchemaVersion
    public JsonObject Upgrade(JsonObject document, DocumentKind kind, DocumentId id); // returns same instance
}
```

Rules: missing `schemaVersion` → `DocumentFormatException`; `schemaVersion` > `Supported` →
`DocumentVersionException`; < 1 → `DocumentFormatException`; otherwise apply migrations in order and
log `DocumentMigrated` (3001). P1 ships no migration (v1 is the first JSON schema); the legacy XML is
"version 0" handled by the importer, not by migrations.

### 2.6 Mapping — `Mapping/IDocumentMapper.cs`, `Mapping/INodeDocumentConverter.cs`, `Mapping/NodeDocumentConverterRegistry.cs`

```csharp
namespace NetPrints.Serialization.Mapping;

public interface IDocumentMapper
{
    ClassDocument ToDocument(ClassGraph cls);
    ClassGraph FromDocument(ClassDocument document, Project project, ICollection<DocumentIssue> issues, DocumentId id);
}

public interface INodeDocumentConverter
{
    string Kind { get; }                 // "$kind" value
    Type NodeType { get; }               // exact runtime type of the node
    Type DocumentType { get; }           // concrete NodeDocument subtype
    NodeDocument ToDocument(Node node, NodeMappingContext context);
    Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context);
}

public sealed class NodeMappingContext
{
    public ClassGraph Class { get; }
    public TypeRef ToRef(BaseType type);
    public BaseType FromRef(TypeRef type);
    public MethodRef ToRef(MethodSpecifier method);
    public MethodSpecifier FromRef(MethodRef method);
    public ConstructorRef ToRef(ConstructorSpecifier constructor);
    public ConstructorSpecifier FromRef(ConstructorRef constructor);
    public VariableRef ToRef(VariableSpecifier variable);
    public VariableSpecifier FromRef(VariableRef variable);
    public TypedValue? ToValue(object? value, string where);   // DocumentFormatException for unsupported types
    public object? FromValue(TypedValue? value);
}

public sealed class NodeDocumentConverterRegistry
{
    public NodeDocumentConverterRegistry(IReadOnlyList<INodeDocumentConverter> converters, IReadOnlyList<IJsonTypeInfoResolver> extensionResolvers);
    public static IReadOnlyList<INodeDocumentConverter> BuiltIn { get; }   // the 24 kinds of §1.5
    public IReadOnlyList<INodeDocumentConverter> Converters { get; }
    public IReadOnlyList<IJsonTypeInfoResolver> ExtensionResolvers { get; }
    public INodeDocumentConverter? FindByKind(string kind);
    public INodeDocumentConverter? FindByNodeType(Type nodeType);
}

public sealed class DocumentMapper : IDocumentMapper
{
    public DocumentMapper(NodeDocumentConverterRegistry nodes);
}
```

| Member | Contract |
|---|---|
| Registry ctor | `ArgumentException` on duplicate `Kind` or `NodeType`; extension kinds must contain `/`, built-ins must not. |
| `ToDocument(ClassGraph)` | Pure (no model mutation). Node without converter → `InvalidOperationException` naming the type. `NodeGraph.PreservedDocumentState` (unknown nodes and their connections) appended after known nodes. Connections enumerated from output pins, sorted (§1.4). Positions only in `Layout`. Deterministic. |
| `FromDocument(ClassDocument, …)` | Order: header → graphs; per graph: create nodes via converters (constructor allocates an id, then `Node.Id = document.Id`), apply `pins` (names, values), connect edges with `GraphUtil.Connect*Pins` (incompatible or missing endpoint → drop + issue `NPD002`), restore positions from `Layout` (missing → `(0,0)`), then `GraphTypeInference.Relax(graph)`. Duplicate node id → `DocumentFormatException`. Unknown node documents → kept in `PreservedDocumentState` + issue `NPD001`. Sets `Class`/`Project` back-references. |
| Round trip | `ToDocument(FromDocument(d)) == d` for every valid `d` (DF-T03). |

### 2.7 Stores — `Stores/IDocumentStore.cs`, `Stores/FileSystemDocumentStore.cs`, `Stores/InMemoryDocumentStore.cs`

```csharp
namespace NetPrints.Serialization.Stores;

public enum DocumentChangeKind { Created, Changed, Deleted }
public sealed record DocumentChange(DocumentId Id, DocumentChangeKind Kind);

public interface IDocumentStore : IDisposable
{
    string DisplayName { get; }
    ValueTask<bool> ExistsAsync(DocumentId id, CancellationToken cancellationToken);
    ValueTask<Stream> OpenReadAsync(DocumentId id, CancellationToken cancellationToken);
    ValueTask WriteAsync(DocumentId id, Func<Stream, CancellationToken, ValueTask> write, CancellationToken cancellationToken);
    IAsyncEnumerable<DocumentId> ListAsync(string prefix, CancellationToken cancellationToken);
    IObservable<DocumentChange> Changes { get; }
}

public sealed class FileSystemDocumentStore : IDocumentStore
{
    public FileSystemDocumentStore(string rootDirectory, IScheduler scheduler, ILogger<FileSystemDocumentStore> logger);
    public string RootDirectory { get; }
    public string GetFullPath(DocumentId id);
    public static DocumentId ToDocumentId(string rootDirectory, string fullPath); // ArgumentException if outside root
}

public sealed class InMemoryDocumentStore : IDocumentStore
{
    public InMemoryDocumentStore();
    public void Set(DocumentId id, byte[] content);   // raises Changed/Created
    public byte[] Get(DocumentId id);                 // DocumentNotFoundException
}
```

| Member | Contract |
|---|---|
| `OpenReadAsync` | Caller owns and disposes the stream. `DocumentNotFoundException` if absent. |
| `WriteAsync` | Atomic: `write` fills a temporary (file: `<name>.tmp-<guid>` in the same directory; memory: buffer); on success the content replaces the document in one step (`File.Move(tmp, target, overwrite: true)`); on exception or cancellation the temp is deleted and the old content is untouched; the exception propagates. Creates missing directories. |
| `ListAsync` | Ordinal-sorted ids starting with `prefix` (`""` = all), recursive. |
| `Changes` | File store: `FileSystemWatcher` on the root (recursive), throttled 200 ms per id on `scheduler`, emitted on the scheduler; changes caused by this store's own `WriteAsync` within the throttle window are suppressed. Never errors; completes on `Dispose`. Memory store: synchronous on `Set`. |
| Thread-safety | All members may be called concurrently; writes to the same id are serialized (per-id `SemaphoreSlim`). |
| Lifetime | Owner disposes the store; `Dispose` stops the watcher and completes `Changes`. |

### 2.8 Persistence facade — `ProjectPersistence.cs`

```csharp
namespace NetPrints.Serialization;

public sealed record ProjectLoadResult(Project Project, ProjectSnapshot Snapshot, IReadOnlyList<DocumentIssue> Issues);
public sealed record ProjectSaveResult(IReadOnlyList<string> WrittenFiles);

public sealed class ProjectPersistence
{
    public ProjectPersistence(IProjectSystem projects, DocumentFormatRegistry formats, IDocumentMapper mapper,
        Func<string, IDocumentStore> createStore, ILogger<ProjectPersistence> logger);   // createStore(projectDirectory)
    public Task<ProjectLoadResult> LoadAsync(string projectFilePath, CancellationToken cancellationToken);
    public Task<ProjectSaveResult> SaveAsync(Project project, Func<ClassGraph, string> renderGenerated, CancellationToken cancellationToken);
    public Task<ClassGraph> AddGraphAsync(Project project, string sourceGraphPath, CancellationToken cancellationToken); // "Existing Class"
}
```

| Member | Contract |
|---|---|
| `LoadAsync` | `IProjectSystem.LoadAsync`, then each `Snapshot.GraphFiles` entry through the JSON format; a malformed graph → issue (Error, with line/position) and the project opens without it. `Project` is built from the snapshot (data-model.md §5); class order = ordinal by file path. |
| `SaveAsync` | Writes every class whose document bytes changed to its `Project.GetGraphFilePath(cls)` and, next to it, `renderGenerated(cls)` (the `.netpc.g.cs`, `GraphCodeGenerator.RenderFile`) — only when content differs. Never writes the `.csproj` (settings go through `IProjectSystem.ApplyAsync`). Returns written files in write order. |
| `AddGraphAsync` | Copies a `.netpc.json` (or imports a legacy `.netpc`, writing `<name>.netpc.json`) into the project folder, loads and adds it (PAR-13). |
| Thread-safety | Stateless; safe to share. The returned model is owned by the UI thread. |

## 3. Legacy XML → v1 rules (graphs)

Project-level conversion (`.netpp` → `.csproj`) is in project-system.md §5.

| Legacy `.netpc` (DataContract) | v1 graph document |
|---|---|
| Node without id | `n<index>` by position in `NodeGraph.Nodes` (`NodeGraph.AssignLegacyNodeIds`) |
| `Node.PositionX/Y` | `layout[graphKey][id]` |
| Pin object references (`IncomingPin`, `OutgoingPin(s)`) | `connections` |
| `NodeInputDataPin.UnconnectedValue` (object) | `pins[].value` (`TypedValue`); unsupported runtime type → conversion error |
| `NodePin.PinType`, `InferredType` | not stored; recomputed by constructors + `GraphTypeInference.Relax` |
| (absent) locals, event graphs, `VariableSpecifier.Scope` | empty / `Member` (initialized in `[OnDeserializing]`, because DataContract skips constructors) |

## 4. Mapping old → new

| Old (`master`) | New |
|---|---|
| `NetPrints/Serialization/SerializationHelper.cs` (`SaveClass`, `LoadClass`) | deleted → `LegacyXmlDocumentFormat` (read), `JsonDocumentFormat` (read/write) |
| `Project.LoadFromPath(path)` (`.netpp`) | `ProjectPersistence.LoadAsync(csprojPath, ct)`; `.netpp` → `ProjectConverter.ConvertAsync` first |
| `Project.Save()`, `SaveClassInProjectDirectory(cls)` | `ProjectPersistence.SaveAsync(project, render, ct)` (graphs + `.g.cs`) |
| project `DataContract` XML (`.netpp`) | `.csproj` via `IProjectSystem` (project-system.md) |
| `Project.AddExistingClass(path)` | `ProjectPersistence.AddGraphAsync` |
| `Project.GetClassStoragePath(cls)` → `X.netpc` | `Project.GetGraphFilePath(cls)` → `<project dir>/X.netpc.json` (existing path kept for loaded classes) |
| `MethodGraph.OnDeserialized` relaxation loop | `GraphTypeInference.Relax(NodeGraph)` (Core, `Graph/GraphTypeInference.cs`) |
| `FileFilter.ProjectFiles` `["*.netpp"]` | `["*.csproj", "*.netpp"]`; `ClassFiles` `["*.netpc.json", "*.netpc"]` |
| `samples/HelloWorld/*.netpp`, `*.netpc` | `samples/HelloWorld/HelloWorld.csproj`, `HelloWorld.Program.netpc.json`, `HelloWorld.Program.netpc.g.cs`; legacy copies → `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/` |

## 5. Extension node kinds

An extension contributes a `NodeKindDescriptor` (extension-points.md §2) whose `Converter.Kind` is
`"<extension id>/<name>"` (lowercase id, `[a-z0-9.-]+`, name `[A-Za-z0-9]+`), whose `DocumentType`
derives from `NodeDocument`, and an `IJsonTypeInfoResolver` (its own `JsonSerializerContext`) that
covers `DocumentType`. Registration order = extension load order; conflicts → `NPX006`, the later
registration is rejected.

## 6. Test obligations

| ID | Case |
|---|---|
| DF-T01 | Golden C#: each class of the legacy fixtures (`HelloWorld`, `AllNodes`) imported → C# equals the golden file recorded before P1 (T004) |
| DF-T02 | Legacy → JSON → load → C# equals golden (byte-identical) |
| DF-T03 | JSON round trip: load → save → bytes identical, for every fixture and the converted sample |
| DF-T04 | Canonical form: 2-space indent, `\n`, final newline, no BOM, `$kind` first, sorted maps/connections, omitted defaults (assert on a small handcrafted document) |
| DF-T05 | Moving one node changes only lines inside `layout` (line diff of two saves) |
| DF-T06 | Every built-in kind of §1.5 round-trips, including dynamic pin counts and renamed pins |
| DF-T07 | `TypedValue`: bool, char, string with quotes/newlines, all integral types, float/double incl. `NaN`/`±Infinity`/`-0`, decimal, enum, flags enum; unsupported type → `DocumentFormatException` |
| DF-T08 | Unknown `$kind` preserved byte-identically after load/save; issue `NPD001`; connections to it preserved |
| DF-T09 | Malformed JSON → `DocumentFormatException` with line and position; nothing written |
| DF-T10 | `schemaVersion` 2 → `DocumentVersionException(2, 1)`; missing → `DocumentFormatException`; a synthetic test migration v1→v2 registered in a test migrator is applied |
| DF-T11 | Malformed graph file among others → project opens without it, issue with line/position; incompatible connection → dropped, issue `NPD002` |
| DF-T12 | `FileSystemDocumentStore.WriteAsync`: exception inside `write` leaves the old file intact and no temp file; cancellation likewise |
| DF-T13 | `Changes`: external edit raises one `Changed` (virtual time); own write raises none; `Dispose` completes the stream |
| DF-T14 | `InMemoryDocumentStore` passes the same store tests (shared abstract test class) |
| DF-T15 | `ProjectPersistence.SaveAsync` writes only changed graphs and their `.netpc.g.cs`; never the `.csproj`; unchanged project → 0 files written (legacy conversion: PS-T12) |
| DF-T16 | Registry: duplicate format id / extension → `ArgumentException`; `Find` prefers `.netpc.json` over `.netpc` |
| DF-T17 | Extension node kind from a second `JsonSerializerContext` round-trips (in-process test extension) |
| DF-T18 | Deterministic ids: legacy import assigns `n0…`; new nodes get `n<max+1>`; ids survive round trips |
