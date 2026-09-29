# Data Model: Core Refactor and Extension Points (P1)

Paths use the post-reorganization layout (plan.md). Where to find the rest:

| Area | Contract |
|---|---|
| Project file (`.csproj`), `NetPrints.Sdk` targets, generator, `IProjectSystem` | [contracts/project-system.md](./contracts/project-system.md) |
| Graph documents (JSON schema v1, DTO records, formats, stores, mapper, persistence) | [contracts/document-format.md](./contracts/document-format.md) |
| Extension points, manifest, loader, registry, host channel, settings, profiles, catalogs | [contracts/extension-points.md](./contracts/extension-points.md) |
| Diagnostics, source map, translator output, quick info | [contracts/compilation-and-diagnostics.md](./contracts/compilation-and-diagnostics.md) |
| Editor context, VMs, composition/DI, error model, logging ids, architecture gate | [contracts/editor-services.md](./contracts/editor-services.md) |

This file defines the **runtime model changes** in `src/NetPrints.Core` and the **old → new mapping**.

## 1. Model base and INPC (Fody removal, research R6)

`src/NetPrints.Core/Core/ModelObject.cs`:

```csharp
namespace NetPrints.Core;

[DataContract]                                  // removed in T063 with the rest of the model's DataContract persistence (research R21)
[INotifyPropertyChanged]                        // CommunityToolkit.Mvvm; MVVMTK0032 suppressed here only
#pragma warning disable MVVMTK0032
public abstract partial class ModelObject;
#pragma warning restore MVVMTK0032
```

| Class | Base (new) | Notifying members (must match the recorded Fody map, T005) |
|---|---|---|
| `NetPrints.Graph.Node` | `ModelObject` | `Name`, `PositionX`, `PositionY` (+ the existing `OnPositionChanged` event kept), `IsPure` (computed, raised by `SetPurity` callers), subclass state properties (e.g. `MakeArrayNode.UsePredefinedSize`) |
| `NetPrints.Graph.NodePin` | `ModelObject` | `Name`; data pins `UnconnectedValue`, `IncomingPin`; exec/type pins `IncomingPin`/`OutgoingPin` |
| `NetPrints.Core.Variable` | `ModelObject` | `Name`, `Visibility`, `Modifiers`, `GetterMethod`, `SetterMethod`, `Type` (computed from the type graph) |
| `NetPrints.Core.Project` | `ModelObject` | `Name`, `DefaultNamespace`, `Path`, `OutputBinaryType`, `IsCompiling`, `CompilationMessage`, `LastCompilationSucceeded`, `LastDiagnostics`, `CanCompile`, `CanCompileAndRun`, `TargetFramework`, `ProfileId`, `Snapshot` |
| `NetPrints.Core.LocalVariable` (new) | `ModelObject` | `Name`, `Type` |

Rules:
- Auto-properties become `[ObservableProperty] public partial T X { get; set; }`; `[DataMember]` stays on
  the partial declaration (verified in `p1spike`) until T063 removes it. Computed properties use `[NotifyPropertyChangedFor]`
  on their sources or explicit `OnPropertyChanged(nameof(X))`.
- The table is indicative; the **golden notification map** recorded from the Fody build (T005,
  `tests/NetPrints.Core.Tests/Characterization/NotificationMap.golden.json`) is authoritative.
- `Node.OnInputTypeChanged` (protected virtual) is renamed `HandleInputTypeChanged` so no Fody-style
  `On<Name>Changed` convention method remains.
- DataContract deserialization runs no constructors or initializers: while anything still deserializes the
  model with DataContract (the one-time migration's importer until T062a, `Project.LoadFromPath` until
  T063), every **new** collection or defaulted member of a *graph-side* type is initialized in an
  `[OnDeserializing]` method (`LocalVariables`, `EventGraphs`). T063 deletes these methods, the
  `[OnDeserialized]` hooks (document-format.md §3.1) and the DataContract attributes (research R21). No
  `.netpp` is converted (project-system.md §5, removed).

## 2. Identity, graph keys, pin keys, layout and dirty state

Graph-format research (`docs/research/2026-09-25-graph-format/` §5.2, owner-approved; research.md R17).
File-level rules are in document-format.md §1.4.1–§1.4.2. Id format revised 2026-09-25 (owner decision):
Snowflake-style ids replace the original random-6-character scheme, implemented in T040a–T040c.

Id value: a 63-bit non-negative `long`, packed most-significant-first as 41 bits of milliseconds since
`SnowflakeIdGenerator.Epoch` (2026-01-01T00:00:00Z) | 16 bits of session id | 6 bits of per-millisecond
sequence. A generator is monotonic per instance, like ULID's monotonic mode: if the sequence would
overflow (more than 64 ids requested in the same millisecond) or the clock reads a time at or before
the last one used, a logical millisecond is advanced instead — an id is never reused and the sequence
never goes backwards. Text form (`IdFormat`): a one-character prefix (`n` node, `m` member) followed by
the value encoded as 13 lowercase Crockford base32 digits (alphabet
`0123456789abcdefghjkmnpqrstvwxyz`), most-significant digit first, zero-padded, so ordinal string order
equals numeric value order; 14 characters total. `IdFormat.Pattern` (`^[nm][0-9a-hjkmnp-tv-z]{13}$`) is
the single source of this shape: the reader's id check (T054b, `IdFormat.IsValid`) and the generated JSON
Schema's id patterns (T054b, `IdFormat.PatternFor`) both read it from there instead of duplicating the regular expression. Parsing
(`IdFormat.TryParse`) is case-insensitive and also accepts Crockford's own transcription aliases in the
value digits (`i`/`I`/`l`/`L` → 1, `o`/`O` → 0); the prefix itself must still be `n` or `m`. Rationale:
sortable (string and numeric order agree, so ids sort correctly in a directory listing, a `git log`, or
a SQL `ORDER BY`), fit a database bigint column without a lossy or oversized alternate representation,
unique by construction (a real clock and a per-instance session make a same-millisecond collision
require both the same session *and* the same sequence, which one generator instance never produces),
and — because they are unique by construction — allocation never needs an uniqueness search.

`src/NetPrints.Core/Core/Ids.cs`:

```csharp
namespace NetPrints.Core;

public interface IIdGenerator
{
    string NewId(char prefix);                                 // prefix + IdFormat's 13-digit encoding of a fresh value
}

public static class IdFormat
{
    public const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
    public const int ValueDigits = 13;
    public const string Pattern = "^[nm][0-9a-hjkmnp-tv-z]{13}$"; // either prefix
    public static string PatternFor(char prefix);                 // "^n[0-9a-hjkmnp-tv-z]{13}$" for 'n'; the schema's id patterns (T054b)
    public static bool IsValid(string? id, char prefix);          // exact ordinal match of PatternFor(prefix); the reader's check (T054b)
    public static string Format(char prefix, long value);        // throws ArgumentOutOfRangeException if value < 0
    public static bool TryParse(ReadOnlySpan<char> text, out char prefix, out long value); // case-insensitive; i/l->1, o->0
}

public sealed class SnowflakeIdGenerator : IIdGenerator         // the engine; thread-safe (internal lock)
{
    public static readonly DateTimeOffset Epoch;                // 2026-01-01T00:00:00Z
    public SnowflakeIdGenerator(TimeProvider timeProvider);      // random 16-bit session, chosen once per instance
    public SnowflakeIdGenerator(TimeProvider timeProvider, ushort sessionId);
}

public sealed class RandomIdGenerator : IIdGenerator            // SnowflakeIdGenerator(TimeProvider.System); one shared instance
{
    public static RandomIdGenerator Instance { get; }
}

public sealed class SeededIdGenerator : IIdGenerator            // SnowflakeIdGenerator(fixed clock at Epoch, session derived from seed)
{
    public SeededIdGenerator(int seed);                         // deterministic: same seed -> same id sequence; tests and the one-time migration (T054a)
}

public static class IdGeneration
{
    public static IIdGenerator Current { get; }                // AsyncLocal override, else RandomIdGenerator.Instance
    public static IDisposable Use(IIdGenerator generator);     // scope for the current async flow; Dispose restores the previous one
}

public static class StableIds
{
    public static int SeedFor(string text);                    // FNV-1a 32-bit over UTF-8 (offset 2166136261, prime 16777619), cast to int; deleted in T062a with its callers
    public static bool IsValidDocumentId(string? id);          // non-empty, no '/', no whitespace; replaced by IdFormat.IsValid in T054b (it existed for legacy "n0" ids)
    public static string AllocateUnique(char prefix, ICollection<string> existingIds); // retried allocation against a concrete, finite id set (load-time duplicate repair, ClassGraph.EnsureUniqueMemberIds); ordinary allocation (below) never retries
}
```

Node constructors have no access to services, so the generator is ambient (`IdGeneration.Current`,
scoped per async flow) instead of a constructor parameter (plan.md Complexity Tracking).

```csharp
namespace NetPrints.Graph;
public abstract partial class Node : ModelObject
{
    [DataMember] public string Id { get; internal set; }      // unique in its graph; never null after construction or import
    public virtual string DefaultName => GetType().Name;       // "CallMethodNode"; written names equal to it are omitted
    public virtual string GetPinKeyName(NodePin pin);          // document-format.md §1.4.2; base returns pin.Name
}
// overrides: MethodEntryNode, ConstructorEntryNode, EventEntryNode → "Input<i>" for OutputDataPins[i];
//            ReturnNode → "Output<i>" for InputDataPins[i]; every other pin → base

public static class PinKeys                                    // src/NetPrints.Core/Graph/PinKeys.cs
{
    public static string For(NodePin pin);                     // "<dir>.<kind>.<key>" incl. "~n" disambiguation
    public static NodePin? Find(Node node, string pinReference); // exact ordinal match; null for unknown or malformed
}

public static class GraphAutoLayout                            // src/NetPrints.Core/Graph/GraphAutoLayout.cs
{
    public const int ColumnSpacing = 300, RowSpacing = 120, CollisionWidth = 200, CollisionHeight = 100;
    public static void PlaceUnpositioned(NodeGraph graph, IReadOnlyCollection<Node> unpositioned);
}

namespace NetPrints.Core;
public abstract class NodeGraph
{
    public string AllocateNodeId();                            // IdGeneration.Current.NewId('n'); not retried — the generator guarantees uniqueness
    public Node? FindNode(string id);                          // O(1): an id -> node Dictionary index, [IgnoreDataMember], built lazily (§3.1)
    public void AssignLegacyNodeIds();                         // sets Id = "n<index>" for every node (legacy import only); deleted in T062a (research R21)
    [IgnoreDataMember] public object? PreservedDocumentState { get; set; } // owned by NetPrints.Serialization (unknown nodes); opaque to Core
}
// internal ReindexNode(Node, string? previousId) keeps the index current whenever code sets Node.Id
// after the node was added (the mapper's document-id overwrite, invalid-id and duplicate-id
// repair, document-format.md §2.6); a no-op before the index has been built (FindNode not yet called).

// Member ids ("m" + 13 digits, IdFormat): MethodGraph.Id, ConstructorGraph.Id, Variable.Id, EventGraph.Id (§4),
// each { get; internal set; }, not [DataMember], assigned in the constructor from IdGeneration.Current.NewId('m').
// Accessor MethodGraphs (Variable.GetterMethod/SetterMethod) also get one; it is never written (keys use the variable id).
public partial class ClassGraph
{
    public IEnumerable<object> Members { get; }                // Variables, Methods, Constructors, EventGraphs, in that order
    public bool EnsureUniqueMemberIds();                       // gives a later duplicate (member order) a fresh unique id; true if anything changed
    public void AssignLegacyMemberIds();                       // SeededIdGenerator(StableIds.SeedFor(FullName)): variables, methods, constructors; deleted in T062a (research R21)
    [IgnoreDataMember] public bool IsDirty { get; private set; }
    public void MarkDirty();
    public void MarkClean();
}

public static class GraphKeys { public static string For(NodeGraph graph); public static NodeGraph? Resolve(ClassGraph cls, string key); }
```

| Rule | Contract |
|---|---|
| Node ids | `Node` constructors call `graph.AllocateNodeId()` **before** `Graph.Nodes.Add(this)`; the mapper overwrites `Id` from the document (or a freshly repaired id, document-format.md §2.6) and calls `graph.ReindexNode`; an id in the document that does not match `IdFormat.PatternFor('n')` is replaced by a fresh id first (`NPD009`, research R21). An undo that re-adds a removed node re-adds the same instance, so it keeps its id. A document with two nodes sharing an id does not fail the load: the later one (document order) is reassigned a fresh id and reported as a `DocumentIssue.DuplicateIdReassigned` warning. |
| Member ids | Assigned in the constructors (from `IdGeneration.Current`); the mapper overwrites them from the document (or a freshly repaired id for an invalid or duplicate one, same as node ids). `ProjectPersistence.SaveAsync` calls `EnsureUniqueMemberIds` before mapping (a collision from two independently-created generator instances is improbable, not impossible). |
| Graph keys | `For`: `class` for the class graph; `<memberId>` for a method, constructor or event graph; `<variableId>/type`, `/get`, `/set` for a variable's graphs; `InvalidOperationException` if the graph is not attached to a class. `Resolve` is the inverse; `null` for an unknown key. |
| Pin keys | `PinKeys.For` and `Find` implement document-format.md §1.4.2 from `Node.GetPinKeyName`; the translator does not use them. |
| Auto-placement | `PlaceUnpositioned` processes `unpositioned` in graph node order. Neighbour = the node on the other end of the first connected input pin (exec, then data, then type, each in collection order) that is already placed → candidate `(neighbour.X + ColumnSpacing, neighbour.Y)`; else the first connected output pin's placed neighbour → `(neighbour.X − ColumnSpacing, neighbour.Y)`; else the fallback column `(maxX + ColumnSpacing, minY + k × RowSpacing)`, where `maxX`/`minY` are computed once, before the call, over the nodes that already had positions (`(0, k × RowSpacing)` if there are none) and `k` = 0, 1, … counts the nodes placed in the fallback column. While a placed node lies within `CollisionWidth` × `CollisionHeight` of the candidate (`|dx| < 200 && |dy| < 100`), add `RowSpacing` to its Y. A node placed by this call counts as placed (as a neighbour and for collisions) for the nodes after it. Deterministic. |
| Positions | The model keeps `double PositionX/Y`; documents store integers (document-format.md §1.1). |
| Dirty state | `IsDirty` is false after load (`FromDocument`), true for a class created in memory (`Project.CreateNewClass`, profile class templates), set by the editor on every user edit of that class (editor-services.md §3), and cleared by `ProjectPersistence.SaveAsync` after writing. It is not observable in P1 (dirty-state UX is P3a). |

## 3. Method-local variables (US5)

```csharp
namespace NetPrints.Core;

public enum VariableScope { Member = 0, Local = 1 }

[DataContract]
public sealed partial class LocalVariable : ModelObject
{
    public LocalVariable(string name, TypeSpecifier type);     // ArgumentException: name not a valid C# identifier
    [ObservableProperty, DataMember] public partial string Name { get; set; }
    [ObservableProperty, DataMember] public partial TypeSpecifier Type { get; set; }
    public VariableSpecifier ToSpecifier();                    // Scope = Local, DeclaringType = null, visibilities Private, modifiers None
}

public abstract class ExecutionGraph : NodeGraph
{
    [DataMember] public ObservableRangeCollection<LocalVariable> LocalVariables { get; private set; }
    public bool IsLocalNameAvailable(string name, LocalVariable? except = null); // not a parameter name, not another local, not a C# keyword
}

public class VariableSpecifier   // existing type, one member added
{
    [DataMember] public VariableScope Scope { get; set; }      // new; Member by default
}
```

| Rule | Contract |
|---|---|
| Translation | Locals are declared first in the method body, in collection order: `<Type.FullCodeName> <Name> = default(<Type.FullCodeName>);` after the `// Variables` header line; their names are reserved before pin-variable naming (`TranslatorUtil.GetUniqueVariableName`). Getter → the name; setter → `<Name> = <value>;` (no `this.`/target). |
| Nodes | `VariableGetterNode`/`VariableSetterNode` with `Variable.Scope == Local` have no target pin (existing `IsLocalVariable` logic now keyed on `Scope`). |
| Rename/retype | Renaming/retyping a local updates the specifier of every getter/setter node in that graph (undoable as one command). |
| Remove | Removes its getter/setter nodes (undoable), like class variables. |

## 4. Event graphs (US4)

```csharp
namespace NetPrints.Core;

[DataContract]
public sealed class EventGraph : NodeGraph
{
    public EventGraph(string name);
    public string Id { get; internal set; }                    // member id (§2), graph key "<Id>"
    [DataMember] public string Name { get; set; }
    public IEnumerable<EventEntryNode> Entries { get; }        // Nodes.OfType<EventEntryNode>() in node order
}

public partial class ClassGraph
{
    [DataMember] public ObservableRangeCollection<EventGraph> EventGraphs { get; private set; }
}

namespace NetPrints.Graph;

[DataContract]
public sealed partial class EventEntryNode : Node
{
    public EventEntryNode(EventGraph graph, string eventName);            // custom event: one exec out, no args
    public EventEntryNode(EventGraph graph, MethodSpecifier overridden);  // override: Modifiers = Override, args from the base signature
    [ObservableProperty, DataMember] public partial string EventName { get; set; }
    [ObservableProperty, DataMember] public partial MemberVisibility Visibility { get; set; }
    [ObservableProperty, DataMember] public partial MethodModifiers Modifiers { get; set; }   // Async, Static, Override allowed
    [DataMember] public MethodSpecifier? OverriddenMethod { get; private set; }
    public NodeOutputExecPin InitialExecutionPin { get; }                 // OutputExecPins[0]
    public void AddArgument();                                             // same pin pattern as MethodEntryNode (type in, data out)
    public void RemoveArgument();
}
```

| Rule | Contract |
|---|---|
| Translation | `ClassTranslator` emits, after methods, for each event graph in order and each entry in node order (open item: this makes `nodes` order semantic for events, so two branches that append entries conflict at the `nodes` tail and the merged order decides method order; see research.md K13): `ExecutionGraphTranslator.TranslateEventEntry(graph, entry)` → a `void` (or `async System.Threading.Tasks.Task` if `Async`) method named `EventName`, visibility/modifiers from the entry, `override` + base signature when `OverriddenMethod` is set. Only nodes reachable from the entry (exec successors + their pure dependencies) are translated. |
| Errors | Data dependency on a node reachable only from another entry → `NPT001` (graph key, node id); duplicate `EventName` among entries/methods → `NPT002`. |
| Allowed nodes | Method-graph built-ins except `return`; extension kinds with `GraphKinds.Event`. |
| Method graphs | `Translate(MethodGraph)` output unchanged (it still declares variables for all nodes of the graph, as today). |

## 5. Project (backed by the `.csproj`)

```csharp
namespace NetPrints.Core;

public partial class Project : ModelObject
{
    public static Project FromSnapshot(ProjectSnapshot snapshot);      // Name, DefaultNamespace (RootNamespace), OutputBinaryType, TargetFramework, ProfileId, Path (= ProjectFilePath)
    [ObservableProperty] public partial ProjectSnapshot Snapshot { get; set; }   // replaced after LoadAsync/ApplyAsync; derived properties re-raised
    public string Path { get; }                                         // full .csproj path
    public string Name { get; }                                         // from snapshot (read-only; rename = rename file, not in P1)
    public string DefaultNamespace { get; }                             // RootNamespace
    public BinaryType OutputBinaryType { get; }                         // OutputType; change via IProjectSystem.ApplyAsync(SetOutputType)
    public string TargetFramework { get; }
    public string ProfileId { get; }
    public ObservableRangeCollection<ClassGraph> Classes { get; }
    public ObservableRangeCollection<CodeDiagnostic> LastDiagnostics { get; }   // replaces LastCompileErrors
    [ObservableProperty] public partial bool IsCompiling { get; set; }
    [ObservableProperty] public partial string CompilationMessage { get; set; } // "Ready" | "Compiling..." | "Build succeeded" | "Build failed with N error(s)"
    [ObservableProperty] public partial bool LastCompilationSucceeded { get; set; }
    public bool CanCompile { get; }                                     // !IsCompiling && Snapshot.ReferencesNetPrintsSdk
    public bool CanCompileAndRun { get; }                               // CanCompile && OutputBinaryType == Executable
    public string GetGraphFilePath(ClassGraph cls);                     // loaded path, or <project dir>/<FullName>.netpc.json for new classes
    public ClassGraph CreateNewClass(IProjectProfile profile);          // unique name MyClass, MyClass2… (PAR-12), namespace = DefaultNamespace; IsDirty = true
    // removed: CreateNew, LoadFromPath, Save, SaveClassInProjectDirectory, AddExistingClass, CompileProject, RunProject,
    //          GetRunCommand, References, ClassPaths, CompilationOutput, SaveVersion, LastCompiledAssemblyPath, DefaultReferences
}
```

`Project` no longer has `[DataContract]` members (T063). The legacy DataContract shapes that T038
re-declared in `src/NetPrints.Serialization/Legacy/` (`LegacyProject`, `LegacyCompilationReference`,
`LegacyAssemblyReference`, `LegacyFrameworkAssemblyReference`, `LegacySourceDirectoryReference`) served
only the one-time migration and are deleted in T062a; Core's original reference classes are deleted in
T063 (research R21).
The References dialog (PAR-16…20) works on `ProjectSnapshot.DeclaredReferences` + `ProjectEdit`s.

## 6. Old → new mapping (whole phase)

| Old (`master`) | New | Notes |
|---|---|---|
| `PropertyChanged.Fody` `[AddINotifyPropertyChangedInterface]` on `Node`, `NodePin`, `Variable`, `Project` | `ModelObject` + CTK `[ObservableProperty]` (`src/NetPrints.Core/Core/ModelObject.cs`) | `FodyWeavers.xml/.xsd` deleted; `Fody`, `PropertyChanged.Fody` removed from `Directory.Packages.props` |
| `[DoNotNotify]` in `src/NetPrints.Core/Graph/TypeNode.cs` (`ObservableValue<T>`) | removed (manual INPC class unchanged) | |
| `src/NetPrints.Core/Serialization/SerializationHelper.cs` | `src/NetPrints.Serialization/Json/JsonDocumentFormat.cs`; DataContract XML is not read | document-format.md §4; research R21 |
| `Project.LoadFromPath/Save/SaveClassInProjectDirectory/AddExistingClass` | `ProjectPersistence` (graphs) + `IProjectSystem` (`.csproj`) | async; editor, CLI, tests updated |
| `.netpp` project file (DataContract XML) | `.csproj` + `NetPrints.Sdk`; not converted (research R21) | project-system.md |
| `Project.References`, `CompilationReference` hierarchy | MSBuild items via `ProjectSnapshot.DeclaredReferences`/`ProjectEdit`; the classes are deleted (T063) | |
| `ProjectCompilationOutput` setting | removed | spec clarification |
| `Project.CompileProject()`, `RunProject()`, `CodeCompiler` (Roslyn emit), runtimeconfig writing | `IProjectSystem.BuildAsync` / `GetRunCommand` (`dotnet build` / `dotnet run`) | |
| `Project.LastCompileErrors` (`string`) | `Project.LastDiagnostics` (`CodeDiagnostic`) | error list rows |
| `src/NetPrints.Core/Core/ReferenceAssemblyResolver.cs` | deleted; references from MSBuild (`ProjectSnapshot.References`) | project-system.md §4 |
| `src/NetPrints.Core/Core/FrameworkAssemblyReference.cs` `ProgramFilesX86` path | deleted with the class (T063) | |
| `src/NetPrints.Reflection/DocumentationUtil.cs` path probing | `ResolvedAssembly.DocumentationPath` (sibling `.xml` of the MSBuild-resolved reference) | D5 |
| `ReflectionProvider(assemblyPaths, sourcePaths, sources)` | `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, …, IReadOnlySet<string> excludedAssemblyNames)` | |
| `ExecutionGraphTranslator` static `nodeTypeHandlers` table, public `Translate*Node` methods | `NodeTranslatorRegistry.BuiltIn` (internal built-in translators), `INodeTranslator` | |
| `new ClassTranslator()` / `new ExecutionGraphTranslator()` | `new ClassTranslator(TranslationEnvironment)` | callers pass `registry.Translation` or `TranslationEnvironment.BuiltIn` |
| (none) node ids, member ids | `Node.Id` (`n…`), member ids (`m…`), both Snowflake ids (`IdFormat`), `IdGeneration` (§2) | graph-format research; R20, R21 |
| `Node.PositionX/Y` default `(0,0)` for nodes without a stored position | `GraphAutoLayout.PlaceUnpositioned` on load | |
| `MethodGraph.OnDeserialized` relaxation | `GraphTypeInference.Relax(NodeGraph)` (`src/NetPrints.Core/Graph/GraphTypeInference.cs`) | |
| Editor `ClassInspectorView` TextBox (`GeneratedCode`) | `CodeView` + `CodeViewVM` | editor-services.md §3 |
| Editor error `ListBox` of strings | `ErrorListVM` rows | |
| `MemberVariableVM(Variable, ClassEditorVM owner)`, `NodeGraphVM(NodeGraph, ClassEditorVM owner)` | `(…, ClassEditorServices services)` | |
| `GraphEditorView.axaml.cs` pointer handlers for disconnect/reroute/connection completed | `NodeGraphVM` commands bound to Nodify command properties | |
| `EditorComposition(Func<EditorContext, EditorContext>? customize)` | `EditorComposition(EditorHostServices)`; test compositions in test projects | |
| `Desktop` `.LogToTrace()` | `AvaloniaLogSink` + console `ILoggerFactory` | |
| `samples/HelloWorld/*.netpp`, `*.netpc` | `HelloWorld.csproj`, `HelloWorld.Program.netpc.json`, `HelloWorld.Program.netpc.g.cs` (+ `samples/Directory.Build.props` for in-repo SDK import), migrated once (T054a); no legacy copies remain after T062a | |
| (none) | `src/NetPrints.Sdk`, `src/NetPrints.Generator`, `src/NetPrints.Workspace` | new projects |
| `Nullable=disable`, `TreatWarningsAsErrors=false` in Core/Reflection/Core.Tests | enabled everywhere | `Directory.Build.props` default applies |
