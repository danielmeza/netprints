# Data Model: Core Refactor and Extension Points (P1)

Paths use the post-reorganization layout (plan.md). Where to find the rest:

| Area | Contract |
|---|---|
| Documents (JSON schema v1, DTO records, formats, stores, mapper, persistence, legacy rules) | [contracts/document-format.md](./contracts/document-format.md) |
| Extension points, manifest, loader, registry, host channel, settings, profiles, catalogs | [contracts/extension-points.md](./contracts/extension-points.md) |
| Reference packs, compiler, diagnostics, source map, quick info | [contracts/references-and-compilation.md](./contracts/references-and-compilation.md) |
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
| `NetPrints.Core.Project` | `ModelObject` | `Name`, `DefaultNamespace`, `Path`, `CompilationOutput`, `OutputBinaryType`, `IsCompiling`, `CompilationMessage`, `LastCompilationSucceeded`, `LastDiagnostics`, `LastCompiledAssemblyPath`, `CanCompile`, `CanCompileAndRun`, `TargetFramework`, `ProfileId` |
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
  defaulted member is initialized in an `[OnDeserializing]` method (`LocalVariables`, `EventGraphs`,
  `TargetFramework`, `FrameworkReferences`, `ProfileId`, `ExtensionIds`, `ExtensionSettings`).

## 2. Identity and graph keys

```csharp
namespace NetPrints.Graph;
public abstract partial class Node : ModelObject
{
    [DataMember] public string Id { get; internal set; }      // "n<k>"; unique in its graph; never null after construction or import
}

namespace NetPrints.Core;
public abstract class NodeGraph
{
    public string AllocateNodeId();                            // "n" + (max k over ids matching ^n\d+$ + 1), "n0" for an empty graph
    public Node? FindNode(string id);
    public void AssignLegacyNodeIds();                         // sets Id = "n<index>" for every node (legacy import only); InvalidOperationException if any Id is already set
    [IgnoreDataMember] public object? PreservedDocumentState { get; set; } // owned by NetPrints.Serialization (unknown nodes); opaque to Core
}

public static class GraphKeys { public static string For(NodeGraph graph); public static NodeGraph? Resolve(ClassGraph cls, string key); }
```

Invariants: `Node` constructors call `graph.AllocateNodeId()` **before** `Graph.Nodes.Add(this)`; the
mapper overwrites `Id` from the document; ids are never reused within a save.

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
| Translation | `ClassTranslator` emits, after methods, for each event graph in order and each entry in node order: `ExecutionGraphTranslator.TranslateEventEntry(graph, entry)` → a `void` (or `async System.Threading.Tasks.Task` if `Async`) method named `EventName`, visibility/modifiers from the entry, `override` + base signature when `OverriddenMethod` is set. Only nodes reachable from the entry (exec successors + their pure dependencies) are translated. |
| Errors | Data dependency on a node reachable only from another entry → `NPT001` (graph key, node id); duplicate `EventName` among entries/methods → `NPT002`. |
| Allowed nodes | Method-graph built-ins except `return`; extension kinds with `GraphKinds.Event`. |
| Method graphs | `Translate(MethodGraph)` output unchanged (it still declares variables for all nodes of the graph, as today). |

## 5. Project and references

```csharp
namespace NetPrints.Core;

public partial class Project : ModelObject
{
    [ObservableProperty, DataMember] public partial string TargetFramework { get; set; }                 // "net10.0"
    [DataMember] public ObservableRangeCollection<string> FrameworkReferences { get; private set; }       // ["Microsoft.NETCore.App"]
    [ObservableProperty, DataMember] public partial string ProfileId { get; set; }                        // "netprints.default"
    [DataMember] public ObservableRangeCollection<string> ExtensionIds { get; private set; }
    public SortedDictionary<string, JsonElement> ExtensionSettings { get; }                               // StringComparer.Ordinal
    public ObservableRangeCollection<CodeDiagnostic> LastDiagnostics { get; }                             // replaces LastCompileErrors
    public static Project CreateNew(string name, string defaultNamespace, IProjectProfile profile,
        ProjectCompilationOutput compilationOutput = ProjectCompilationOutput.All);
    public string GetClassStoragePath(ClassGraph cls);                                                    // "<FullName>.netpc.json"
    // removed: LoadFromPath, Save, SaveClassInProjectDirectory, AddExistingClass, CompileProject, DefaultReferences
}
```

## 6. Old → new mapping (whole phase)

| Old (`master`, pre-reorg path) | New (post-reorg path) | Notes |
|---|---|---|
| `PropertyChanged.Fody` `[AddINotifyPropertyChangedInterface]` on `Node`, `NodePin`, `Variable`, `Project` | `ModelObject` + CTK `[ObservableProperty]` (`src/NetPrints.Core/Core/ModelObject.cs`) | `FodyWeavers.xml/.xsd` deleted; `Fody`, `PropertyChanged.Fody` removed from `Directory.Packages.props` |
| `[DoNotNotify]` in `Graph/TypeNode.cs` (`ObservableValue<T>`) | removed (manual INPC class unchanged) | |
| `NetPrints/Serialization/SerializationHelper.cs` | `src/NetPrints.Serialization/Legacy/LegacyXmlDocumentFormat.cs`, `…/Json/JsonDocumentFormat.cs` | document-format.md §4 |
| `Project.LoadFromPath/Save/SaveClassInProjectDirectory/AddExistingClass` | `ProjectPersistence` + `ProjectFiles` | async; editor, CLI, tests updated |
| `Project.CompileProject()` | `ProjectCompiler.CompileAsync` (`src/NetPrints.Core/Compilation/ProjectCompiler.cs`) | |
| `Project.LastCompileErrors` (`string`) | `Project.LastDiagnostics` (`CodeDiagnostic`) | error list rows |
| `Core/ReferenceAssemblyResolver.cs` | `src/NetPrints.Core/References/ReferencePackResolver.cs`, `DotNetHost.cs` | references-and-compilation.md §2.3 |
| `Core/FrameworkAssemblyReference.cs` `ProgramFilesX86` path | legacy marker only | |
| `NetPrints.Reflection/DocumentationUtil.cs` path probing | `ResolvedAssembly.DocumentationPath` | D5 |
| `ReflectionProvider(assemblyPaths, sourcePaths, sources)` | `ReflectionProvider(IReadOnlyList<ResolvedAssembly>, …, IReadOnlySet<string> excludedAssemblyNames)` | |
| `ExecutionGraphTranslator` static `nodeTypeHandlers` table, public `Translate*Node` methods | `NodeTranslatorRegistry.BuiltIn` (internal built-in translators), `INodeTranslator` | |
| `new ClassTranslator()` / `new ExecutionGraphTranslator()` | `new ClassTranslator(TranslationEnvironment)` | callers pass `registry.Translation` or `TranslationEnvironment.BuiltIn` |
| `MethodGraph.OnDeserialized` relaxation | `GraphTypeInference.Relax(NodeGraph)` (`src/NetPrints.Core/Graph/GraphTypeInference.cs`) | |
| Editor `ClassInspectorView` TextBox (`GeneratedCode`) | `CodeView` + `CodeViewVM` | editor-services.md §3 |
| Editor error `ListBox` of strings | `ErrorListVM` rows | |
| `MemberVariableVM(Variable, ClassEditorVM owner)`, `NodeGraphVM(NodeGraph, ClassEditorVM owner)` | `(…, ClassEditorServices services)` | |
| `GraphEditorView.axaml.cs` pointer handlers for disconnect/reroute/connection completed | `NodeGraphVM` commands bound to Nodify command properties | |
| `EditorComposition(Func<EditorContext, EditorContext>? customize)` | `EditorComposition(EditorHostServices)`; test compositions in test projects | |
| `Desktop` `.LogToTrace()` | `AvaloniaLogSink` + console `ILoggerFactory` | |
| `samples/HelloWorld/*.netpp`, `*.netpc` | `*.netpp.json`, `*.netpc.json`; legacy copies in `tests/NetPrints.Core.Tests/Fixtures/Legacy/HelloWorld/` | |
| `Nullable=disable`, `TreatWarningsAsErrors=false` in Core/Reflection/Core.Tests | enabled everywhere | `Directory.Build.props` default applies |
