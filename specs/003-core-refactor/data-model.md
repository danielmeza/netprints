# Data Model: Core Refactor and Extension Points (P1)

Paths use the post-reorganization layout (plan.md). Where to find the rest:

| Area | Contract |
|---|---|
| Project file (`.csproj`), `NetPrints.Sdk` targets, generator, `IProjectSystem`, legacy conversion | [contracts/project-system.md](./contracts/project-system.md) |
| Graph documents (JSON schema v1, DTO records, formats, stores, mapper, persistence, legacy graph rules) | [contracts/document-format.md](./contracts/document-format.md) |
| Extension points, manifest, loader, registry, host channel, settings, profiles, catalogs | [contracts/extension-points.md](./contracts/extension-points.md) |
| Diagnostics, source map, translator output, quick info | [contracts/compilation-and-diagnostics.md](./contracts/compilation-and-diagnostics.md) |
| Editor context, VMs, composition/DI, error model, logging ids, architecture gate | [contracts/editor-services.md](./contracts/editor-services.md) |

This file defines the **runtime model changes** in `src/NetPrints.Core` and the **old → new mapping**.

## 1. Model base and INPC (Fody removal, research R6)

`src/NetPrints.Core/Core/ModelObject.cs`:

```csharp
namespace NetPrints.Core;

[DataContract]
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
  the partial declaration (verified in `p1spike`). Computed properties use `[NotifyPropertyChangedFor]`
  on their sources or explicit `OnPropertyChanged(nameof(X))`.
- The table is indicative; the **golden notification map** recorded from the Fody build (T005,
  `tests/NetPrints.Core.Tests/Characterization/NotificationMap.golden.json`) is authoritative.
- `Node.OnInputTypeChanged` (protected virtual) is renamed `HandleInputTypeChanged` so no Fody-style
  `On<Name>Changed` convention method remains.
- DataContract deserialization runs no constructors or initializers: every **new** collection or
  defaulted member of a *graph-side* type is initialized in an `[OnDeserializing]` method (`LocalVariables`,
  `EventGraphs`; `Node.Id` stays null until `AssignLegacyNodeIds`, member ids until `AssignLegacyMemberIds`). The legacy `Project` DataContract is
  read only by `ProjectConverter` (project-system.md §5).

## 2. Identity, graph keys, pin keys, layout and dirty state

Graph-format research (`docs/research/2026-09-25-graph-format/` §5.2, owner-approved; research.md R17).
File-level rules are in document-format.md §1.4.1–§1.4.2.

`src/NetPrints.Core/Core/Ids.cs`:

```csharp
namespace NetPrints.Core;

public interface IIdGenerator
{
    string NewId(char prefix);                                 // prefix + 6 chars of "0123456789abcdefghjkmnpqrstvwxyz"
}

public sealed class RandomIdGenerator : IIdGenerator            // Random.Shared; thread-safe
{
    public static RandomIdGenerator Instance { get; }
}

public sealed class SeededIdGenerator : IIdGenerator            // new Random(seed); not thread-safe; tests and legacy import
{
    public SeededIdGenerator(int seed);
}

public static class IdGeneration
{
    public static IIdGenerator Current { get; }                // AsyncLocal override, else RandomIdGenerator.Instance
    public static IDisposable Use(IIdGenerator generator);     // scope for the current async flow; Dispose restores the previous one
}

public static class StableIds
{
    public static int SeedFor(string text);                    // FNV-1a 32-bit over UTF-8 (offset 2166136261, prime 16777619), cast to int
    public static bool IsValidDocumentId(string? id);          // non-empty, no '/', no whitespace
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
    public string AllocateNodeId();                            // IdGeneration.Current.NewId('n') until FindNode(id) == null; InvalidOperationException after 100 tries
    public Node? FindNode(string id);
    public void AssignLegacyNodeIds();                         // sets Id = "n<index>" for every node (legacy import only); InvalidOperationException if any Id is already set
    [IgnoreDataMember] public object? PreservedDocumentState { get; set; } // owned by NetPrints.Serialization (unknown nodes); opaque to Core
}

// Member ids ("m" + 6 chars): MethodGraph.Id, ConstructorGraph.Id, Variable.Id, EventGraph.Id (§4),
// each { get; internal set; }, not [DataMember], assigned in the constructor from IdGeneration.Current.NewId('m').
// Accessor MethodGraphs (Variable.GetterMethod/SetterMethod) also get one; it is never written (keys use the variable id).
public partial class ClassGraph
{
    public IEnumerable<object> Members { get; }                // Variables, Methods, Constructors, EventGraphs, in that order
    public bool EnsureUniqueMemberIds();                       // gives a later duplicate (member order) a fresh unique id; true if anything changed
    public void AssignLegacyMemberIds();                       // SeededIdGenerator(StableIds.SeedFor(FullName)): variables, methods, constructors; unique; InvalidOperationException if any member id is already set
    [IgnoreDataMember] public bool IsDirty { get; private set; }
    public void MarkDirty();
    public void MarkClean();
}

public static class GraphKeys { public static string For(NodeGraph graph); public static NodeGraph? Resolve(ClassGraph cls, string key); }
```

| Rule | Contract |
|---|---|
| Node ids | `Node` constructors call `graph.AllocateNodeId()` **before** `Graph.Nodes.Add(this)`; the mapper overwrites `Id` from the document; legacy import calls `AssignLegacyNodeIds`. An undo that re-adds a removed node re-adds the same instance, so it keeps its id. |
| Member ids | Assigned in the constructors (random, from `IdGeneration.Current`); the mapper overwrites them from the document; legacy import calls `AssignLegacyMemberIds` (DataContract skips constructors, so the ids are null until then). `ProjectPersistence.SaveAsync` calls `EnsureUniqueMemberIds` before mapping (a collision of two random ids is improbable, not impossible). |
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
    [DataMember] public VariableScope Scope { get; set; }      // new; Member by default (legacy files)
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

`Project` no longer has `[DataContract]` members used for writing. The legacy DataContract shapes are
re-declared in `src/NetPrints.Serialization/Legacy/` as `LegacyProject` (`[DataContract(Name = "Project",
Namespace = "http://schemas.datacontract.org/2004/07/NetPrints.Core")]`) and `LegacyCompilationReference`,
`LegacyAssemblyReference`, `LegacyFrameworkAssemblyReference`, `LegacySourceDirectoryReference` (each with
the original contract `Name`/`Namespace` and `[KnownType]`s), read only by `ProjectConverter`; Core's
original reference classes are deleted.
The References dialog (PAR-16…20) works on `ProjectSnapshot.DeclaredReferences` + `ProjectEdit`s.

## 6. Old → new mapping (whole phase)

| Old (`master`) | New | Notes |
|---|---|---|
| `PropertyChanged.Fody` `[AddINotifyPropertyChangedInterface]` on `Node`, `NodePin`, `Variable`, `Project` | `ModelObject` + CTK `[ObservableProperty]` (`src/NetPrints.Core/Core/ModelObject.cs`) | `FodyWeavers.xml/.xsd` deleted; `Fody`, `PropertyChanged.Fody` removed from `Directory.Packages.props` |
| `[DoNotNotify]` in `src/NetPrints.Core/Graph/TypeNode.cs` (`ObservableValue<T>`) | removed (manual INPC class unchanged) | |
| `src/NetPrints.Core/Serialization/SerializationHelper.cs` | `src/NetPrints.Serialization/Legacy/LegacyXmlDocumentFormat.cs`, `…/Json/JsonDocumentFormat.cs` | document-format.md §4 |
| `Project.LoadFromPath/Save/SaveClassInProjectDirectory/AddExistingClass` | `ProjectPersistence` (graphs) + `IProjectSystem` (`.csproj`) | async; editor, CLI, tests updated |
| `.netpp` project file (DataContract XML) | `.csproj` + `NetPrints.Sdk`; legacy → `ProjectConverter` | project-system.md |
| `Project.References`, `CompilationReference` hierarchy | MSBuild items via `ProjectSnapshot.DeclaredReferences`/`ProjectEdit`; legacy classes moved to `Serialization/Legacy/` | |
| `ProjectCompilationOutput` setting | removed | spec clarification |
| `Project.CompileProject()`, `RunProject()`, `CodeCompiler` (Roslyn emit), runtimeconfig writing | `IProjectSystem.BuildAsync` / `GetRunCommand` (`dotnet build` / `dotnet run`) | |
| `Project.LastCompileErrors` (`string`) | `Project.LastDiagnostics` (`CodeDiagnostic`) | error list rows |
| `src/NetPrints.Core/Core/ReferenceAssemblyResolver.cs` | deleted; references from MSBuild (`ProjectSnapshot.References`) | project-system.md §4 |
| `src/NetPrints.Core/Core/FrameworkAssemblyReference.cs` `ProgramFilesX86` path | legacy marker only | |
| `src/NetPrints.Reflection/DocumentationUtil.cs` path probing | `ResolvedAssembly.DocumentationPath` (sibling `.xml` of the MSBuild-resolved reference) | D5 |
| `ReflectionProvider(assemblyPaths, sourcePaths, sources)` | `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, …, IReadOnlySet<string> excludedAssemblyNames)` | |
| `ExecutionGraphTranslator` static `nodeTypeHandlers` table, public `Translate*Node` methods | `NodeTranslatorRegistry.BuiltIn` (internal built-in translators), `INodeTranslator` | |
| `new ClassTranslator()` / `new ExecutionGraphTranslator()` | `new ClassTranslator(TranslationEnvironment)` | callers pass `registry.Translation` or `TranslationEnvironment.BuiltIn` |
| (none) node ids, member ids | `Node.Id` (random `n…`, legacy `n<index>`), member ids (`m…`), `IdGeneration` (§2) | graph-format research |
| `Node.PositionX/Y` default `(0,0)` for nodes without a stored position | `GraphAutoLayout.PlaceUnpositioned` on load | |
| `MethodGraph.OnDeserialized` relaxation | `GraphTypeInference.Relax(NodeGraph)` (`src/NetPrints.Core/Graph/GraphTypeInference.cs`) | |
| Editor `ClassInspectorView` TextBox (`GeneratedCode`) | `CodeView` + `CodeViewVM` | editor-services.md §3 |
| Editor error `ListBox` of strings | `ErrorListVM` rows | |
| `MemberVariableVM(Variable, ClassEditorVM owner)`, `NodeGraphVM(NodeGraph, ClassEditorVM owner)` | `(…, ClassEditorServices services)` | |
| `GraphEditorView.axaml.cs` pointer handlers for disconnect/reroute/connection completed | `NodeGraphVM` commands bound to Nodify command properties | |
| `EditorComposition(Func<EditorContext, EditorContext>? customize)` | `EditorComposition(EditorHostServices)`; test compositions in test projects | |
| `Desktop` `.LogToTrace()` | `AvaloniaLogSink` + console `ILoggerFactory` | |
| `samples/HelloWorld/*.netpp`, `*.netpc` | `HelloWorld.csproj`, `HelloWorld.Program.netpc.json`, `HelloWorld.Program.netpc.g.cs` (+ `samples/Directory.Build.props` for in-repo SDK import); legacy copies in `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/` | |
| (none) | `src/NetPrints.Sdk`, `src/NetPrints.Generator`, `src/NetPrints.Workspace` | new projects |
| `Nullable=disable`, `TreatWarningsAsErrors=false` in Core/Reflection/Core.Tests | enabled everywhere | `Directory.Build.props` default applies |
