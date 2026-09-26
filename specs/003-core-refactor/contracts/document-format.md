# Contract: Graph documents, formats, stores and mapping

> Revised 2026-09-25: the project file is an SDK-style `.csproj` (project-system.md). This contract
> now covers only **graph documents** (`.netpc.json`); `ProjectDocument` and the `.netpp.json` file are removed.
>
> Revised 2026-09-25 (owner-approved graph-format research, `docs/research/2026-09-25-graph-format/`
> §5–§6, research.md R17): random node ids, member ids, graph keys by member id, pins by name,
> a canonical writer with one-line leaf records, integer positions, `$schema`, a tolerant reader,
> edited-only saves and a generated JSON Schema. A `netprints merge` git driver is a follow-up after P1.
>
> Revised 2026-09-26 (owner decision, research.md R21): legacy DataContract XML is not read. The
> repository's own legacy files are migrated once (tasks.md T054a) and the importer is deleted (T062a).
> Ids are strict: the reader replaces an id that does not match `IdFormat` and reports `NPD009`, and the
> schema carries the id patterns (§1.4.1, §2.6, §6).

Project: `src/NetPrints.Serialization` (new, references `src/NetPrints.Core`; `InternalsVisibleTo`
from Core so the mapper can set `Node.Id` and member ids). Tests: `tests/NetPrints.Core.Tests/Serialization/`.
Consumers: Editor, `NetPrints.Generator` (build), CLI, P3 extension host, P4/P5 hosts (VS Code workspace store).
Test obligations (`DF-Txx`) are listed at the end; `tasks.md` references them.

## 1. JSON schema v1

### 1.1 Canonical writing rules (all graph documents)

| Rule | Value |
|---|---|
| Encoding | UTF-8, no BOM |
| Line endings | `\n` only; the file ends with exactly one `\n`. New projects (and the migrated sample) get a `.gitattributes` with `*.netpc.json text eol=lf` and `*.netpc.g.cs text eol=lf` (project-system.md §1.1) |
| Writer | `CanonicalJsonWriter` (§2.3.1), a custom pass over the `JsonNode` tree STJ produces. STJ `WriteIndented` alone cannot write inline records, so it is not used for graph files |
| Indentation | 2 spaces per level for block values |
| Line layout | **Inline records** are written on one line each, whatever their length: connections, layout positions, pin states, typed values, type references, parameter references, local variables, and nodes that have only common fields. Everything else is block-indented, one property or element per line. The rule depends on the property name (§2.3.1), never on line width |
| First property | `"$schema": "<NetPrintsSchema.V1Url>"` (§6), then `schemaVersion`. `$schema` is informational: written on every save, ignored on read |
| Property names | camelCase (`JsonKnownNamingPolicy.CamelCase`), order = `[JsonPropertyOrder]` as declared below; in nodes `$kind` first, `id` second |
| Nulls / defaults | omitted globally (`DefaultIgnoreCondition = WhenWritingDefault`); every member marked **Req. = yes** carries `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]` so default values such as `SharedLibrary` (0) are still written. Node `name` is omitted when it equals the node's default name (§1.5) |
| Enums | strings (`UseStringEnumConverter = true`); `[Flags]` enums as `"Static, Async"` (STJ format); `None` omitted |
| Maps | `SortedDictionary<string, …>` with `StringComparer.Ordinal` |
| Numbers | layout coordinates are JSON integers (model `double`s rounded with `MidpointRounding.AwayFromZero` on write); `TypedValue.value` is always a string |
| String escaping | `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`: non-ASCII characters and `<`, `>`, `&`, `'`, `+` are written literally; `"`, `\` and control characters are escaped |
| Ids | node ids and member ids per §1.4.1; no sequential counters |
| Forbidden | timestamps, absolute paths, machine names, counters (`lastNodeId`-style), content hashes, derived data, editor view state (zoom, scroll, selection, collapsed state), `SaveVersion`, `LastCompiledAssemblyPath` |
| Reading | tolerant (§2.3): comments, trailing commas, any property order (including `$kind` not first) and any whitespace are accepted. Comments are not preserved when the class is written again |
| When a file is written | only when the user edited that class (§2.8); a clean class is never rewritten, even if its file is not canonical |

### 1.2 File names

| Document | Name | Location |
|---|---|---|
| Graph (one class) | `<Namespace>.<ClassName>.netpc.json` for new classes (any name matching `*.netpc.json` is accepted) | anywhere under the project; found by the `NetPrintsGraph` items (project-system.md §2) |
| Generated C# | `<same name without .json>.g.cs`, i.e. `<…>.netpc.g.cs` | next to the graph; committed, visible in PR diffs (not `linguist-generated`); regenerated, never hand-merged |
| JSON Schema | `netpc.v1.schema.json` | `schemas/` at the repository root (§6) |
| Project | `<Name>.csproj` | project-system.md §1 |

### 1.3 (removed) `ProjectDocument`

Superseded by the `.csproj` (project-system.md §1). Project settings are MSBuild properties.

### 1.4 `ClassDocument` (the graph file)

| JSON name | C# member | Type | Req. | Notes |
|---|---|---|---|---|
| `$schema` | — (added by the writer, stripped by the reader) | string | yes (written) | `NetPrintsSchema.V1Url`; not validated on read |
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
| `layout` | `Layout` | map graphKey → map nodeId → `[x, y]` (integers) | no | the only place positions live; both maps ordinal-sorted; every entry optional (a node without an entry is auto-placed, §2.6); omitted when empty |

`MethodDocument`: `id`, `name`, `visibility`, `modifiers` (omit if `None`), `graph`.
`ConstructorDocument`: `id`, `visibility`, `graph`.
`VariableDocument`: `id`, `name`, `visibility`, `modifiers` (omit if `None`), `typeGraph`, `getter?` (`AccessorDocument`: `visibility`, `graph`), `setter?`.
`EventGraphDocument`: `id`, `name`, `graph`.
`id` is required and is the first property of every member document; accessors have no id of their own.
`GraphDocument`: `nodes` (`NodeDocument[]`, graph order), `connections` (`ConnectionDocument[]`, ordinal-sorted by `from` then `to`), `locals` (`LocalVariableDocument[]`, method/constructor graphs only, omit if empty).
`LocalVariableDocument`: `name`, `type` (`TypeRef`).
`ConnectionDocument`: `from` = `"<nodeId>/<pin>"` of an **output** pin, `to` = same for an **input** pin.

#### 1.4.1 Identifiers and graph keys

| Id | Shape | Scope of uniqueness | On read |
|---|---|---|---|
| Node id | `n` + 13 lowercase Crockford base32 digits (`IdFormat`, data-model.md §2: a 63-bit Snowflake-style value, zero-padded so string order equals numeric order), from `IdGeneration.Current` — not retried, the generator guarantees uniqueness within its session | one graph | must match `IdFormat.PatternFor('n')` (`^n[0-9a-hjkmnp-tv-z]{13}$`, exact ordinal match, lowercase); missing → `DocumentFormatException`; any other shape → replaced by a fresh id, the graph's connections and layout entries that name it follow, `NPD009` (§2.6); duplicate in a graph → the later one is reassigned a fresh id and reported as `NPD007` (§2.6) |
| Member id | `m` + 13 digits of the same alphabet | the class (methods, constructors, variables and event graphs share one namespace) | must match `IdFormat.PatternFor('m')`; missing → `DocumentFormatException`; any other shape → replaced by a fresh id, its `layout` keys follow, `NPD009`; duplicate in a class → the later one is reassigned a fresh id and reported as `NPD007` (§2.6) |

Revised 2026-09-26 (research.md R21): the former "Legacy import" column (`n<index>` node ids, seeded
member ids at conversion) and the looser "accepted on read" rule (any string without `/` or whitespace)
are gone; every id in a v1 file has the shape above.

Ids never change once assigned: renaming a member or node, reordering members, or changing a node's
kind data keeps its id. Removing a node or member drops its id; a new one never reuses it on purpose
(random ids make reuse improbable, and uniqueness is checked).

Graph keys (`GraphKeys.For`, Core): `class`; `<memberId>` for a method, constructor or event graph;
`<variableId>/type`, `<variableId>/get`, `<variableId>/set` for a variable's type graph and accessors.
Keys are used in `layout`, in diagnostics (`CodeDiagnostic.GraphKey`) and in generator messages
(`(graph <key>, node <id>)`). Reordering or inserting members never changes an existing key.

#### 1.4.2 Pin references

```text
pin       = direction "." kind "." key
direction = "in" | "out"
kind      = "exec" | "data" | "type"
key       = keyName | keyName "~" n        ; n = 2, 3, … for the 2nd, 3rd, … pin with the same keyName
endpoint  = nodeId "/" pin                 ; used by ConnectionDocument.from/to
```

- `keyName` = `Node.GetPinKeyName(pin)` (data-model.md §2): `Input<i>` for the i-th output data pin of
  `MethodEntryNode`, `ConstructorEntryNode` and `EventEntryNode` (argument pins, user-renamable);
  `Output<i>` for the i-th input data pin of `ReturnNode` (return pins, user-renamable); the pin's
  `Name` for every other pin (names set by the node's constructor or by its kind data, e.g. the called
  method's parameter names). User renames never change a key; they are stored in `pins[].name`.
- Within one (direction, kind) collection of a node, keys are assigned in collection order: the first
  pin with a given `keyName` gets `keyName`, the second `keyName~2`, and so on (e.g. two `Int32`
  return values of a called method are `out.data.Int32` and `out.data.Int32~2`).
- Parsing: the node id is the text before the first `/`; direction and kind are the first two
  `.`-separated segments of the rest; the key is the remainder and may contain `.`, `<`, `>`, `,` or
  spaces. Resolution is an exact ordinal match against the keys computed for the node's current pins
  (`PinKeys.Find`, data-model.md §2).
- The index form of earlier drafts (`in.data.0`) is **not** accepted: it resolves like any unknown key
  (a connection is dropped with `NPD002`, a pin state with `NPD003`). No v1 file uses it, and a second
  grammar would make a pin named with digits only ambiguous.
- Examples (ids of §1.7): `n00000000057k3/in.exec.Exec`, `n00000000057k3/in.data.value` (the `value`
  parameter of `Console.WriteLine`), `n00000000057k1/out.data.Input0` (first method argument, whatever its
  display name), `n0c7hx3kq2m4a0/out.exec.Catch`.

### 1.5 `NodeDocument` (polymorphic, `"$kind"`)

Common fields (in this order after `$kind`): `id` (string, §1.4.1), `name` (node `Name`, omitted when
it equals `Node.DefaultName`, which is the node's runtime type name, e.g. `CallMethodNode`; a missing
`name` is read as `DefaultName`), `pins` (`PinStateDocument[]`, omit if empty). `PinStateDocument`:
`pin` (pin reference without node id, §1.4.2), `name` (only when the pin's `Name` differs from its
`keyName`, i.e. a user-renamed argument or return pin), `value` (`TypedValue`, unconnected input
value, only if set). Pin *counts* of dynamic nodes are given by the kind fields below; the mapper
creates the pins and then applies `pins`.

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
| any unknown | `UnknownNodeDocument` | content preserved: `$kind` and `id` first, the other properties in source order, re-emitted through the canonical writer (byte-identical when the input was canonical) |

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

### 1.7 Complete example: `HelloWorld.Program.netpc.json` (the sample)

This is the exact canonical output (no whitespace is compacted). The ids stand for the seeded values the
one-time migration writes (tasks.md T054a, research.md R21): `n0000000004g00` `ClassReturnNode`,
`m00000000057k0` method `Main`, `n00000000057k1` `MethodEntryNode`, `n00000000057k2` `ReturnNode`,
`n00000000057k3` `CallMethodNode`; the committed sample file is the reviewed golden output. A node added
later in the editor gets an id like `n0c7hx3kq2m4a0`. Default node names are omitted.

```json
{
  "$schema": "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json",
  "schemaVersion": 1,
  "namespace": "HelloWorld",
  "name": "Program",
  "visibility": "Public",
  "classGraph": {
    "nodes": [
      { "$kind": "classReturn", "id": "n0000000004g00" }
    ]
  },
  "methods": [
    {
      "id": "m00000000057k0",
      "name": "Main",
      "visibility": "Public",
      "modifiers": "Static",
      "graph": {
        "nodes": [
          { "$kind": "methodEntry", "id": "n00000000057k1" },
          { "$kind": "return", "id": "n00000000057k2" },
          {
            "$kind": "callMethod",
            "id": "n00000000057k3",
            "pins": [
              { "pin": "in.data.value", "value": { "type": "System.String", "value": "Hello, World!" } }
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
          { "from": "n00000000057k1/out.exec.Exec", "to": "n00000000057k3/in.exec.Exec" },
          { "from": "n00000000057k3/out.exec.Exec", "to": "n00000000057k2/in.exec.Exec" }
        ]
      }
    }
  ],
  "layout": {
    "class": {
      "n0000000004g00": [112, 112]
    },
    "m00000000057k0": {
      "n00000000057k1": [112, 112],
      "n00000000057k2": [840, 112],
      "n00000000057k3": [420, 112]
    }
  }
}
```

What review diffs look like: moving `n00000000057k3` changes one line (`"n00000000057k3": [420, 112]` →
`"n00000000057k3": [460, 140]`);
adding a node adds one node block (or one line), one line per new connection and one layout line.

The matching `HelloWorld.csproj` is shown in project-system.md §1 (with `OutputType` `Exe` and
`RootNamespace` `HelloWorld`); it has no references beyond the SDK's defaults.

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

Document issue codes (`DocumentIssue.Code`):

| Code | Severity | Meaning |
|---|---|---|
| `NPD001` | Warning | node of unknown kind (missing or untrusted extension) preserved |
| `NPD002` | Warning | connection dropped: missing node, unknown pin key, or incompatible pins |
| `NPD003` | Warning | pin state dropped: unknown pin key |
| `NPD004` | Info | layout entry ignored: unknown graph key or node id |
| `NPD005` | Warning | unknown `NetPrintsProfile`; default profile used (extension-points.md §5) |
| `NPD006` | Warning | project extension not trusted; its nodes preserved but inactive (extension-points.md §8.3) |
| `NPD007` | Warning | a node id (within its graph) or member id (within its class) was duplicated — a merge, or a hand-edited or copy-pasted file; the later occurrence (document order) was reassigned a fresh id and the document still loaded |
| `NPD008` | Error | a graph document could not be read at all (malformed content, or a deserialization failure); a caller that keeps working after skipping it reports this instead of propagating the `DocumentFormatException` (added T044, `GraphCodeGenerator`) |
| `NPD009` | Warning | a node id or member id does not match `IdFormat` for its prefix (a hand or AI edit, e.g. `"n0"` or `"start"`); it was replaced by a fresh id, references to it in the same document (connection endpoints, `layout` keys) follow, and the document still loaded (added T054b, research.md R21) |

### 2.2 Formats — `IDocumentFormat.cs`, `DocumentFormatRegistry.cs`

```csharp
namespace NetPrints.Serialization;

public interface IDocumentFormat
{
    string Id { get; }                              // "json"
    IReadOnlyList<string> ClassExtensions { get; }   // ".netpc.json"
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
| `Find` | Longest matching suffix, `OrdinalIgnoreCase`; `null` if none. P1 registers only the JSON format; the registry stays the seam for later formats (constitution VII). |
| Thread-safety | Formats and the registry are immutable and thread-safe. |

`JsonDocumentFormat` (`Json/JsonDocumentFormat.cs`): `public JsonDocumentFormat(NetPrintsJsonOptions options, DocumentMigrator migrator)`.
- Read: `JsonNode.Parse(input, nodeOptions: null, NetPrintsJsonOptions.DocumentOptions)` (comments
  skipped, trailing commas allowed); the root must be an object; a `$schema` property is removed (it must
  be a string if present, its value is not checked); `DocumentMigrator.Upgrade`; then
  `JsonSerializer.Deserialize(root, SerializerOptions.GetTypeInfo(typeof(ClassDocument)))`. Syntax
  errors carry line and byte position; errors found after parsing carry the JSON path
  (`JsonException.Path`) in the message.
- Write: `JsonSerializer.SerializeToNode(document, …)`, a new root `JsonObject` with `$schema` first
  followed by the serialized properties in order, then `CanonicalJsonWriter.Write(root, output)` (§2.3.1).
`LegacyXmlDocumentFormat` (`Legacy/LegacyXmlDocumentFormat.cs`, T038) is never registered in a registry a
user reaches (editor, generator, CLI). It exists only for the one-time migration of the repository's own
files (tasks.md T054a) and is deleted in T062a (research.md R21).

### 2.3 JSON options, node polymorphism and the canonical writer — `Json/NetPrintsJsonContext.cs`, `Json/NetPrintsJsonOptions.cs`, `Json/NodeListConverter.cs`, `Json/CanonicalJsonWriter.cs`

```csharp
namespace NetPrints.Serialization.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(ClassDocument))]
internal sealed partial class NetPrintsJsonContext : JsonSerializerContext;

public sealed class NetPrintsJsonOptions
{
    public NetPrintsJsonOptions(NodeDocumentConverterRegistry nodes);
    public JsonSerializerOptions SerializerOptions { get; } // frozen (MakeReadOnly) after construction
    public static JsonDocumentOptions DocumentOptions { get; } // CommentHandling = Skip, AllowTrailingCommas = true
}

internal sealed class NodeListConverter : JsonConverter<IReadOnlyList<NodeDocument>>; // research R5

public static class CanonicalJsonWriter
{
    public static void Write(JsonObject root, Stream output);   // §2.3.1; ArgumentException for a non-finite number
}

public static class NetPrintsSchema
{
    public const string V1Url = "https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json";
}
```

| Rule | Contract |
|---|---|
| Resolver | `JsonTypeInfoResolver.Combine(NetPrintsJsonContext.Default, <each extension resolver in registry order>)` + one `WithAddedModifier` that adds every extension `(Kind, DocumentType)` to `PolymorphismOptions.DerivedTypes` of `NodeDocument`. |
| Serializer options | Built at runtime on top of the resolver: the context's naming/enum/ignore settings, plus `AllowOutOfOrderMetadataProperties = true` (`$kind` need not be first), `ReadCommentHandling = JsonCommentHandling.Skip`, `AllowTrailingCommas = true`, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, `WriteIndented = false` (formatting is the canonical writer's job). |
| Unknown properties | ignored on read (STJ default); they are dropped the next time that class is saved. |
| Built-in kinds | `[JsonDerivedType(typeof(XNodeDocument), "<kind>")]` on `NodeDocument` for every row of §1.5. |
| Unknown kinds | `NodeListConverter` reads each element as `JsonElement`, finds `$kind` and `id` wherever they appear in the object; unknown `$kind` → `UnknownNodeDocument(Id, Kind, Raw)` and an issue `NPD001`; on write it emits `Raw` as a `JsonObject` with `$kind` and `id` first and the other properties in source order. Missing `$kind` or `id` → `DocumentFormatException`. |

#### 2.3.1 Canonical writer

`CanonicalJsonWriter` walks the `JsonNode` tree and writes UTF-8 bytes; it never reorders properties
(order comes from the DTOs). A value is written **inline** (on one line) when it is an object or an array
and at least one of these holds:

1. it is an element of an array held by a property named `connections`, `pins`, `locals`,
   `parameters`, `args`, `returnTypes`, `genericArgs` or `dataTypes`;
2. it is the value of a property named `declaringType`, `type`, `literalType`, `value` or `default`;
3. it is a value of one of the inner maps of the root `layout` object (the `[x, y]` arrays);
4. it is an element of a `nodes` array and has no properties other than `$kind`, `id` and `name`;
5. it is nested inside an inline value.

Every other object or array is a **block**. The rules match by property name only, so they apply
unchanged to extension node documents (an extension property named `type` holding an object is
written inline).

| Form | Output |
|---|---|
| Block object | `{`, then one line per property at indent + 2: `"name": <value>`, a `,` after every property but the last, then `}` at the parent's indent |
| Block array | `[`, one line per element at indent + 2, `,` after every element but the last, `]` at the parent's indent |
| Inline object | `{ "a": 1, "b": { "c": 2 } }`: one space after `{` and before `}`, `, ` between properties, `": "` after names |
| Inline array | `[112, 112]`, `[{ "name": "T" }]`: no space inside brackets, `, ` between elements |
| Empty object / array | `{}` / `[]` (DTOs omit empty collections, so this appears only in unknown or extension content) |
| Strings | escaped with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (the value written by a `Utf8JsonWriter` with that encoder) |
| Numbers, `true`, `false`, `null` | as STJ writes them; integers without a fraction |
| File end | exactly one `\n` after the root `}` |

Implementation note: the writer is about 200 lines. Tests always re-parse the written bytes (DF-T03, DF-T04).

### 2.4 DTO records — `Documents/*.cs`

```csharp
namespace NetPrints.Serialization.Documents;

public sealed record ClassDocument(
    int SchemaVersion, string? Namespace, string Name, MemberVisibility Visibility, ClassModifiers Modifiers,
    IReadOnlyList<string>? GenericArguments, GraphDocument ClassGraph,
    IReadOnlyList<VariableDocument>? Variables, IReadOnlyList<MethodDocument>? Methods,
    IReadOnlyList<ConstructorDocument>? Constructors, IReadOnlyList<EventGraphDocument>? EventGraphs,
    SortedDictionary<string, SortedDictionary<string, int[]>>? Layout);   // JsonPropertyOrder 0..11 in this order; each int[] has exactly 2 elements (else DocumentFormatException on read)

public sealed record GraphDocument(
    [property: JsonConverter(typeof(NodeListConverter))] IReadOnlyList<NodeDocument> Nodes,
    IReadOnlyList<ConnectionDocument>? Connections,
    IReadOnlyList<LocalVariableDocument>? Locals);
public sealed record ConnectionDocument(string From, string To);
public sealed record MethodDocument(string Id, string Name, MemberVisibility Visibility, MethodModifiers Modifiers, GraphDocument Graph);
public sealed record ConstructorDocument(string Id, MemberVisibility Visibility, GraphDocument Graph);
public sealed record AccessorDocument(MemberVisibility Visibility, GraphDocument Graph);
public sealed record VariableDocument(string Id, string Name, MemberVisibility Visibility, VariableModifiers Modifiers,
    GraphDocument TypeGraph, AccessorDocument? Getter, AccessorDocument? Setter);
public sealed record EventGraphDocument(string Id, string Name, GraphDocument Graph);   // Id is JsonPropertyOrder 0 in every member record
public sealed record LocalVariableDocument(string Name, TypeRef Type);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(MethodEntryNodeDocument), "methodEntry")]
// … one [JsonDerivedType] per row of §1.5 …
public abstract record NodeDocument(
    [property: JsonPropertyOrder(-3)] string Id,
    [property: JsonPropertyOrder(-2)] string? Name,          // null = Node.DefaultName
    [property: JsonPropertyOrder(-1)] IReadOnlyList<PinStateDocument>? Pins);
public sealed record PinStateDocument(string Pin, string? Name, TypedValue? Value);
public sealed record UnknownNodeDocument(string Id, string Kind, JsonElement Raw) : NodeDocument(Id, "", null); // never serialized by STJ directly

public sealed record MethodEntryNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
    int ArgumentCount, IReadOnlyList<string>? GenericArguments) : NodeDocument(Id, Name, Pins);
public sealed record CallMethodNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins,
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

DTOs are immutable. Record equality is not structural for the list members, so tests compare
documents by their canonical bytes (or `JsonNode.DeepEquals` of the serialized trees).

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
log `DocumentMigrated` (3001). P1 ships no migration (v1 is the first JSON schema); there is no
"version 0": DataContract XML is not read (research.md R21).

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
| `ToDocument(ClassGraph)` | Pure (no model mutation). Node without converter → `InvalidOperationException` naming the type. `NodeGraph.PreservedDocumentState` (unknown nodes and their connections) appended after known nodes. Pin references and pin states use `PinKeys` (§1.4.2); `name` omitted when `Node.Name == Node.DefaultName`; `pins[].name` only for pins whose `Name` differs from their `keyName`. Member ids from the model; duplicate member ids → `InvalidOperationException` (callers run `ClassGraph.EnsureUniqueMemberIds()` first, §2.8). Connections enumerated from output pins, sorted (§1.4). Positions only in `Layout`: one entry per node of every graph, coordinates rounded to integers (`MidpointRounding.AwayFromZero`). Deterministic. |
| `FromDocument(ClassDocument, …)` | Order: header → members (member id missing → `DocumentFormatException`; present but not matching `IdFormat.PatternFor('m')` → replaced by a fresh id, the `layout` keys that name it follow, `NPD009`) → graphs; per graph: node ids not matching `IdFormat.PatternFor('n')` are replaced first (a fresh id per distinct invalid text; every connection endpoint and layout entry of that graph naming it is rewritten; `NPD009`), then create nodes via converters (constructor allocates an id, then `Node.Id = document.Id` and `graph.ReindexNode`; `Name = document.Name ?? node.DefaultName`), apply `pins` by key (unknown key → drop + `NPD003`), connect edges by key with `GraphUtil.Connect*Pins` (missing node, unknown key or incompatible pins → drop + `NPD002`), restore positions from `Layout`, auto-place nodes that have no entry (`GraphAutoLayout.PlaceUnpositioned`, data-model.md §2; never `(0,0)` by default), ignore layout entries for unknown graph keys or node ids (`NPD004`), then `GraphTypeInference.Relax(graph)`. A duplicate node id (within its graph) or member id (within the class) does not throw: the later occurrence (document order) is reassigned a fresh id (`StableIds.AllocateUnique`) and reported as `NPD007` (merge safety) — the fix is local, so an id that was unique in the original document is never disturbed. Unknown node documents → kept in `PreservedDocumentState` + issue `NPD001`. Sets `Class`/`Project` back-references. The returned class has `IsDirty == false`. |
| Round trip | `ToDocument(FromDocument(d))` serializes to the same bytes as `d` for every valid canonical `d` with a layout entry for every node (DF-T03). |

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
| `SaveAsync` | For each class in project order with `ClassGraph.IsDirty` (data-model.md §2): `EnsureUniqueMemberIds()`, `ToDocument`, write the graph to `Project.GetGraphFilePath(cls)` and, next to it, `renderGenerated(cls)` (the `.netpc.g.cs`, `GraphCodeGenerator.RenderFile`), each only when its bytes differ from the file on disk; then `MarkClean()`. A clean class is neither mapped nor written, even if mapping it would now give different bytes (auto-placed nodes, a non-canonical hand edit, re-resolved references): no ripple saves. Never writes the `.csproj` (settings go through `IProjectSystem.ApplyAsync`). Returns written files in write order. |
| `AddGraphAsync` | Copies a `.netpc.json` byte for byte into the project folder, loads and adds it clean (PAR-13). A legacy `.netpc` is not accepted (research.md R21). |
| Thread-safety | Stateless; safe to share. The returned model is owned by the UI thread. |

## 3. (removed 2026-09-26) Legacy XML → v1 rules

Legacy `.netpp`/`.netpc` files are not read (research.md R21), so there is no project conversion
(project-system.md §5, removed) and no graph import. The one-time migration of the repository's own files
(tasks.md T054a) reuses the T038 importer as it stands and then rewrites its positional `n<index>` node
ids to seeded Snowflake ids; the importer is deleted afterwards (T062a).

### 3.1 DataContract `[OnDeserialized]` hooks

The JSON path never runs these hooks: `DocumentMapper.FromDocument` builds the model through the normal
constructors, which already do what each hook repairs after DataContract (which skips constructors). The
hooks stay only while something still deserializes the model with DataContract: `LegacyXmlDocumentFormat`
(until T062a) and `Project.LoadFromPath`/`SerializationHelper` (until T063). T063 deletes them together
with the model's `[DataContract]`/`[DataMember]` attributes.

| Hook (on `master`) | What it does | JSON path | Until T063 |
|---|---|---|---|
| `Node.OnDeserializing` (`[OnDeserialized]`, `Graph/Node.cs`) | Re-subscribes `InferredType.OnValueChanged` and `IncomingPinChanged` on every input type pin | Constructors subscribe (`AddInputTypePin`); nothing extra | Kept |
| `MethodGraph.OnDeserialized` | Relaxation loop calling `Node.OnMethodDeserialized` until inferred types settle (max 20 iterations) | `GraphTypeInference.Relax(graph)` at the end of each graph in `FromDocument`, for method, constructor and event graphs, same loop and limit | Hook body replaced by a call to `GraphTypeInference.Relax(this)` (T020) |
| `Variable.OnDeserialized` | Creates `TypeGraph` when it is null | The mapper always builds the type graph from the document | Kept |
| `Project.FixDefaults` | Resets `Classes` (not serialized) | Not applicable: projects are `.csproj` (project-system.md) | Kept for `Project.LoadFromPath` |

New members that DataContract would leave null (`LocalVariables`, `EventGraphs`, `VariableSpecifier.Scope`, ids)
are initialized in an `[OnDeserializing]` method until T063 (data-model.md §1). The JSON path gets them from
the constructors or the document. Test **DF-T27** covers the JSON path.

## 4. Mapping old → new

| Old (`master`) | New |
|---|---|
| `src/NetPrints.Core/Serialization/SerializationHelper.cs` (`SaveClass`, `LoadClass`) | deleted (T063) → `JsonDocumentFormat` (read/write); DataContract XML is not read (research.md R21) |
| `Project.LoadFromPath(path)` (`.netpp`) | `ProjectPersistence.LoadAsync(csprojPath, ct)`; a `.netpp` is not opened |
| `Project.Save()`, `SaveClassInProjectDirectory(cls)` | `ProjectPersistence.SaveAsync(project, render, ct)` (edited graphs + their `.g.cs`) |
| project `DataContract` XML (`.netpp`) | `.csproj` via `IProjectSystem` (project-system.md) |
| `Project.AddExistingClass(path)` | `ProjectPersistence.AddGraphAsync` |
| `Project.GetClassStoragePath(cls)` → `X.netpc` | `Project.GetGraphFilePath(cls)` → `<project dir>/X.netpc.json` (existing path kept for loaded classes) |
| `MethodGraph.OnDeserialized` relaxation loop | `GraphTypeInference.Relax(NodeGraph)` (Core, `Graph/GraphTypeInference.cs`) |
| `FileFilter.ProjectFiles` `["*.netpp"]` | `["*.csproj"]`; `ClassFiles` `["*.netpc.json"]` |
| `samples/HelloWorld/*.netpp`, `*.netpc` | `samples/HelloWorld/HelloWorld.csproj`, `HelloWorld.Program.netpc.json`, `HelloWorld.Program.netpc.g.cs`, migrated once (T054a); the legacy files are deleted (T057 in `samples/`, T062a in `tests/NetPrints.Core.Tests/Fixtures/Legacy/`) |

## 5. Extension node kinds

An extension contributes a `NodeKindDescriptor` (extension-points.md §2) whose `Converter.Kind` is
`"<extension id>/<name>"` (lowercase id, `[a-z0-9.-]+`, name `[A-Za-z0-9]+`), whose `DocumentType`
derives from `NodeDocument`, and an `IJsonTypeInfoResolver` (its own `JsonSerializerContext`) that
covers `DocumentType`. Registration order = extension load order; conflicts → `NPX006`, the later
registration is rejected.

Extension nodes follow the same file rules as built-ins: pin keys come from `Node.GetPinKeyName`
(override it for user-renamable pins), `name` is omitted when it equals `Node.DefaultName`, and the
inline rules of §2.3.1 apply by property name. The published schema (§6) accepts any node whose `$kind`
contains `/` without checking its fields.

## 6. Schema publication — `Json/NetPrintsJsonSchema.cs`, `schemas/netpc.v1.schema.json`

```csharp
namespace NetPrints.Serialization.Json;

public static class NetPrintsJsonSchema
{
    public static string GenerateV1();   // the committed file's exact text
}
```

| Rule | Contract |
|---|---|
| Source | `JsonSchemaExporter.GetJsonSchemaAsNode` over `ClassDocument` with serializer options built from `NetPrintsJsonContext.Default` only (built-in kinds; loaded extensions never change the file), `JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true, TransformSchemaNode = … }`. |
| Transform | Root: add `"$schema": "https://json-schema.org/draft/2020-12/schema"`, `"$id": NetPrintsSchema.V1Url`, `"title": "NetPrints class graph (schema v1)"`, and a root property `"$schema": { "type": "string" }`; `schemaVersion` gets `"const": 1`; every layout position array gets `"minItems": 2, "maxItems": 2`; the `NodeDocument` schema gets one more `anyOf` branch for extension kinds: `{ "type": "object", "required": ["$kind", "id"], "properties": { "$kind": { "type": "string", "pattern": "/" } } }`; `required` lists exactly the Req. = yes members of §1.4–§1.6 (fix what the exporter emits). No `additionalProperties: false` anywhere (the reader is tolerant). **Id patterns** (revised 2026-09-26, research.md R21, T054b): every node `id` (each built-in branch and the extension branch) gets `"pattern": IdFormat.PatternFor('n')`, every member `id` `IdFormat.PatternFor('m')`; `ConnectionDocument.from`/`to` get `^n[0-9a-hjkmnp-tv-z]{13}/(in|out)\.(exec|data|type)\..+$`; the root `layout` object gets `"propertyNames": { "pattern": "^(class|m[0-9a-hjkmnp-tv-z]{13}(/(type|get|set))?)$" }` and each inner map `"propertyNames"` with the node pattern. All of these are built from `IdFormat.Alphabet`/`ValueDigits`/`PatternFor`, never typed twice. (Until T054b the schema had no id patterns, because legacy-imported `n0`-style ids were accepted on read.) |
| Output | `JsonNode.ToJsonString` with `WriteIndented = true`, `IndentSize = 2`, `NewLine = "\n"`, plus a final `\n`. Committed at `schemas/netpc.v1.schema.json`; `NETPRINTS_UPDATE_SNAPSHOTS=1` rewrites it (DF-T24). |
| Versioning | A schema version bump adds `schemas/netpc.v2.schema.json` and a new `V2Url`; old files stay published. |
| Publication | The docs workflow copies `schemas/*.schema.json` to `/schemas/` of the GitHub Pages site (release-and-docs.md §8, §10), so `V1Url` resolves once the owner has enabled Pages and the first deployment ran; the committed file stays the source. The reader never fetches the URL. |
| Editors | VS Code and Rider validate and complete through the `$schema` URL once it resolves. SchemaStore registration (`fileMatch: ["*.netpc.json"]`) is a follow-up after P1. |
| Open item | How the exporter renders STJ polymorphism (`anyOf` with `$kind` `const`) is verified in T041; any gap is fixed in `TransformSchemaNode` and recorded in research.md R17. Validating documents against the schema with a validator library is not part of P1. |

## 7. Test obligations

| ID | Case |
|---|---|
| DF-T01 | Golden C#: each class of the characterization fixtures (`HelloWorld`, `AllNodes`), read from its migrated `.netpc.json` (T054a; before that, imported from the legacy XML) → C# equals the golden file recorded before P1 (T004); the goldens are never regenerated by the migration |
| DF-T02 | JSON fixture → load → mark dirty → save → load → C# equals golden (byte-identical) (revised 2026-09-26: was legacy → JSON → load) |
| DF-T03 | JSON round trip: load → mark dirty → save → bytes identical, for every fixture and the sample; the written bytes re-parse |
| DF-T04 | Canonical form on a small handcrafted document, compared with an expected text literal: `$schema` first, 2-space indent, `\n`, final newline, no BOM, `$kind` then `id` first, sorted maps and connections, omitted defaults and default node names, each inline record kind of §2.3.1 on one line (connection, layout position, pin state, typed value, type ref, parameter ref, local variable, common-fields-only node), integer coordinates (a model position of 420.5 is written as 421), relaxed escaping (`List<T>`, `é` literal; `"` and newline escaped) |
| DF-T05 | Moving one node changes exactly one line, inside `layout` (line diff of two saves) |
| DF-T06 | Every built-in kind of §1.5 round-trips, including dynamic pin counts and renamed pins |
| DF-T07 | `TypedValue`: bool, char, string with quotes/newlines, all integral types, float/double incl. `NaN`/`±Infinity`/`-0`, decimal, enum, flags enum; unsupported type → `DocumentFormatException` |
| DF-T08 | Unknown `$kind` (canonical input, `$kind` and `id` first) preserved byte-identically after load, an edit elsewhere in the class, and save; issue `NPD001`; connections to it preserved |
| DF-T09 | Malformed JSON → `DocumentFormatException` with line and position; nothing written |
| DF-T10 | `schemaVersion` 2 → `DocumentVersionException(2, 1)`; missing → `DocumentFormatException`; a synthetic test migration v1→v2 registered in a test migrator is applied |
| DF-T11 | Malformed graph file among others → project opens without it, issue with line/position; incompatible connection → dropped, issue `NPD002` |
| DF-T12 | `FileSystemDocumentStore.WriteAsync`: exception inside `write` leaves the old file intact and no temp file; cancellation likewise |
| DF-T13 | `Changes`: external edit raises one `Changed` (virtual time); own write raises none; `Dispose` completes the stream |
| DF-T14 | `InMemoryDocumentStore` passes the same store tests (shared abstract test class) |
| DF-T15 | `ProjectPersistence.SaveAsync` writes only dirty classes' graphs and their `.netpc.g.cs`: a project of three classes with one edited → exactly 2 files written; a clean class whose file is non-canonical (comments, other order) or has an unpositioned node is not written; never the `.csproj`; unchanged project → 0 files; all classes clean afterwards |
| DF-T16 | Registry: duplicate format id / extension → `ArgumentException`; `Find` prefers the longest matching suffix (a fake second format claiming a shorter suffix of `.netpc.json`; it is not a legacy format) |
| DF-T17 | Extension node kind from a second `JsonSerializerContext` round-trips (in-process test extension) |
| DF-T18 | Ids: new nodes get ids matching `IdFormat.Pattern` (`^n[0-9a-hjkmnp-tv-z]{13}$`), unique in the graph; `AllocateNodeId` is not retried (a generator stub that returns an already-used id is trusted, not searched around — `NodeIdTests.AllocateNodeIdDoesNotRetryOrSearch`); `IdGeneration.Use(new SeededIdGenerator(42))` gives the same ids on every run; node and member ids survive round trips, renames and reordering; a document with two nodes sharing an id loads (not a `DocumentFormatException`), the later one gets a fresh id, is indexed under it (`FindNode` resolves it), and an `NPD007` issue is reported |
| DF-T19 | Pin references by name: golden list `Fixtures/Golden/PinKeys.golden.txt` of every pin reference of every node of AllNodes (an accidental built-in pin rename fails it); two same-named pins get `~2`; a renamed method argument keeps key `out.data.Input0` and writes `name`; hand-editing a `callMethod`'s `MethodRef` from `M(int a, int b)` to `M(int a, string inserted, int b)` keeps the connection on `in.data.b` and drops nothing else; an index-form reference (`in.data.0`) → `NPD002` / `NPD003` |
| DF-T20 | Auto-placement: a node without a layout entry is placed per data-model.md §2 (expected coordinates asserted for an upstream neighbour, a downstream neighbour, no neighbour, and a collision); a document with no `layout` loads with no node at `(0,0)` unless computed there; placement is deterministic |
| DF-T21 | Tolerant read: a document with `//` and `/* */` comments, trailing commas, `$kind` after other properties, `$schema` missing or present, members and properties in another order, and arbitrary whitespace loads to the same model as its canonical form; saving it after an edit writes the canonical form; `$schema` that is not a string → `DocumentFormatException` |
| DF-T22 | Member ids: layout and diagnostics keys use member ids; reordering two methods in the model keeps each method's layout entries; a document with a missing member id → `DocumentFormatException`; a malformed member id is covered by DF-T28; a document with a duplicate member id loads, the later one is reassigned a fresh id, and an `NPD007` issue is reported; `EnsureUniqueMemberIds` renames only the later duplicate |
| DF-T23 | Merge (plain git), ids from a queue generator stub: base = a method graph with exec chain `nb00000` (entry) → `nk00000` (call) → `nr00000` (call) → `nz00000` (return) and layout entries for all four; branch A adds a `literal` `nd00000` connected to a new `callMethod` `nf00000` (`Console.WriteLine(string)`), branch B adds `nm00000` → `np00000` the same way, each with layout entries (their connection and layout lines fall into different interior gaps of the sorted sets); `git merge-file` of the three saved files reports exactly one conflict hunk, at the tail of `nodes`; connections and layout merge cleanly; resolving the hunk by keeping both sides (plus the missing `,`) loads with 8 nodes and 5 connections. Skipped with a reason when `git` is not on `PATH` (CI has it). The test documents the limit too: appends at the end of a sorted set, or two inserts into the same gap, still conflict until the P2 merge driver |
| DF-T24 | Schema: `NetPrintsJsonSchema.GenerateV1()` equals `schemas/netpc.v1.schema.json` (rewritten with `NETPRINTS_UPDATE_SNAPSHOTS=1`); structural asserts: root `$id` = `V1Url`, one `anyOf` branch per built-in `$kind` (`const`) plus the extension branch, `schemaVersion` `const` 1, layout arrays `minItems`/`maxItems` 2, `required` of `MethodDocument` = `["id", "name", "visibility", "graph"]`; the id patterns of §6 on a node `id`, a member `id`, `from`/`to` and both `layout` levels, and each equals the text built from `IdFormat` |
| DF-T25 | Default node names: a node named `CallMethodNode` is written without `name` and reads back as `CallMethodNode`; a node named `CallMethodNode2` or `Greeting` is written with `name` |
| DF-T26 | Committed samples: every `samples/**/*.netpc.json` is canonical (load → mark dirty → write equals the file bytes) and every `samples/**/*.netpc.g.cs` equals `GraphCodeGenerator.RenderFile` of its graph (stale-file guard; runs in CI with the rest of the suite) |
| DF-T27 | Post-load wiring (JSON path; the legacy-import half is deleted with the importer in T062a): load the HelloWorld sample from JSON; on the result, change a type-pin input of a generic node (e.g. connect a different type to a `MakeArray` type pin) and assert the dependent pins' inferred types update and the generated C# changes accordingly; assert `Variable.TypeGraph` is non-null for every variable, and that the inferred types after load equal the ones before save (relaxation ran) |
| DF-T28 | Strict ids (§1.4.1, §2.6): a document whose node ids are `n0`, `start` and `N00000000057K3` (upper case) and whose method id is `m1` loads; each gets a fresh id matching `IdFormat.PatternFor`, one `NPD009` per replaced id names the old text, every connection and layout entry that named it now names the new id (C# unchanged, no `NPD002`/`NPD004`); an invalid id that also appears twice ends with two distinct valid ids (`NPD009` then `NPD007`); a missing id still throws `DocumentFormatException`; the migrated fixtures and the sample load with no `NPD009` |
