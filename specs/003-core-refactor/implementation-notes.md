# Implementation notes (P1)

Decisions made while implementing `tasks.md` where a contract was ambiguous or the current
(pre-P1) code did not behave as a task or contract implied. Each entry names the task, the
issue, the choice made, and what (if anything) a later task should revisit.

## Sub-phase A

### T002 — `decimal` has no unconnected value on the unmodified model

`NodeInputDataPin.UsesUnconnectedValue` (unmodified `src/NetPrints.Core/Graph/NodeInputDataPin.cs`)
is `true` only when `TypeSpecifier.IsPrimitive` is `true`, and `TypeSpecifier.IsPrimitive`
(`src/NetPrints.Core/Core/TypeSpecifier.cs`) does not include `System.Decimal` in its list. Setting
`UnconnectedValue` on a `decimal` input data pin therefore throws `ArgumentException` on today's
code, even though document-format.md §1.6 requires `TypedValue` to support `System.Decimal`
(`TypedValueConverter`, invariant culture).

Choice: the `AllNodes` characterization fixture (`AllNodesFixtureFactory`) creates a `decimal`
`LiteralNode` (exercising the node kind) without an unconnected value, instead of failing to build
the fixture at all.

Revisit: T028 (`Mapping/TypedValueConverter.cs`) implements `decimal` support in the new document
format regardless (the contract is explicit and normative). Whether `IsPrimitive`/
`UsesUnconnectedValue` on the *model* pin should also be extended to include `decimal` — so the
editor can accept an unconnected decimal literal the same way it accepts `int`/`double` — is a
separate, small model change; nothing in P1 requires it (unconnected decimal values are not part of
the "identical C#" golden-output requirement, FR-008), so it is left as a follow-up rather than
made part of sub-phase A/B, which must not change model behavior.

### T002 — `GraphUtil.CreateNestedTypeNode` does not support closed generic arguments

`GraphUtil.CreateNestedTypeNode` (unmodified `src/NetPrints.Core/Graph/GraphUtil.cs`) recurses into
every one of a `TypeSpecifier`'s `GenericArguments` and connects the resulting nested `TypeNode` to
`typeNode.InputTypePins[0]` (an off-by-index bug beyond the first argument) — but the parent
`TypeNode` (`src/NetPrints.Core/Graph/TypeNode.cs`) only creates an input type pin for generic
arguments that are `GenericType` (unbound), not for closed `TypeSpecifier` arguments. Building a
`Variable` (`src/NetPrints.Core/Core/Variable.cs`, which calls `CreateNestedTypeNode` for the
variable's type graph) with a *closed* generic type, e.g. `List<string>`, throws
`ArgumentOutOfRangeException` on today's code.

Choice: the `AllNodes` fixture's "generic type built in its type graph" requirement (T002) is
satisfied with an *open* generic type instead: the class declares a generic parameter `T`
(`ClassGraph.DeclaredGenericArguments`) and the `Items` variable's type is `List<T>`, bound to that
class parameter (a `GenericType`, not a closed `TypeSpecifier`). This is the same pattern already
used by `tests/NetPrints.Core.Tests/Translator/GenericsTests.cs`.

Revisit: this is a pre-existing bug in `CreateNestedTypeNode`, not something sub-phase A may fix
(characterization must run against unmodified behavior). It is not on the P1 critical path (no P1
task calls `CreateNestedTypeNode` with a closed generic argument), so no task currently needs to
fix it; noted here so a future phase does not rediscover it from scratch.

## Sub-phase B

### T008/T009/T010 — Fody wove every settable property reachable by inheritance, not just `Node`'s own

`PropertyChanged.Fody` weaves any type that implements `INotifyPropertyChanged`, which under the
unmodified code included every subclass of `Node`/`NodePin`/`Variable`/`Project` (the interface is
inherited), not just the properties declared directly on those four base types. Converting only
`Node`, `NodePin`, `Variable` and `Project` themselves to `[ObservableProperty]`/`ModelObject` and
then removing Fody silently stopped notifications on every **subclass-declared** settable property
that Fody used to weave implicitly — including `private set` ones, which `NotificationMapTests`
does not cover (T005 only exercises public setters) and so did not catch.

Found two ways:
- `MakeArrayNode.UsePredefinedSize` (hand-written setter, public): caught directly by
  `NotificationMapTests` — its golden entry went from `["UsePredefinedSize"]` to `[]`.
- `CallMethodNode.MethodSpecifier` (plain `{ get; private set; }`): not caught by
  `NotificationMapTests` (private setter), but broke a real behavior,
  `tests/NetPrints.Editor.Tests/Graph/Nodes/NodeVMTests.cs` `OverloadsAndUndoableChange`. `Node`'s
  constructor adds itself to `Graph.Nodes` *before* the derived class's own constructor body runs
  (e.g. `CallMethodNode`'s `MethodSpecifier = methodSpecifier;` executes after the `base(graph)`
  call returns), and the editor's `NodeGraphVM` reacts to that `Nodes` collection change
  synchronously, constructing a `NodeVM` and calling `NodeVM.UpdateOverloads()` while
  `MethodSpecifier` is still `null` (guarded there with a `{ MethodSpecifier: not null }` pattern,
  landing on the empty branch). The *original* code recovered because setting `MethodSpecifier`
  afterwards raised a Fody-woven `PropertyChanged` that `NodeVM`'s (by-then-subscribed)
  `OnNodePropertyChanged` handler used to recompute `Overloads`. With no notification, `Overloads`
  stayed empty forever for a freshly constructed node.

Fix: audited every `Node`/`NodePin` subclass in `src/NetPrints.Core/Graph/*.cs` for settable
properties (`grep` for `get;\s*(private\s+)?set;` across multiple lines, plus a scan for
hand-written `set` blocks) and converted every one that Fody would have woven to
`[ObservableProperty]` (keeping `private set` where it was already private):
`CallMethodNode.MethodSpecifier`, `MakeDelegateNode.MethodSpecifier`,
`ConstructorNode.ConstructorSpecifier`, `LiteralNode.LiteralType`, `TypeNode.Type`,
`VariableNode.Variable`. `NodeDataPin.PinType`, `NodePin.Node`, `Node.Graph` and the six pin-list
properties on `Node` are `private set` too, but are provably never reassigned after construction
anywhere in the codebase, so they were left as plain properties (no behavior to preserve).

Revisit: sub-phases C–L must re-run this same audit whenever a **new** `Node`/`NodePin` subclass is
added (event graphs in G, any extension node kinds in F) — the failure mode is silent (no compiler
warning, no exception unless something reads the property through a `PropertyChanged` subscription
like `NodeVM` does), so it is easy to reintroduce.

### T011 — Nullable reference type rollout: replacing `!`/`null!`/`default!` (owner, commit 36ccc3b)

First pass at T011 reached a 0-warning build by sprinkling the null-forgiving operator wherever the
compiler complained. The owner rejected that (rule quoted in full in `AGENTS.md` "Nullable reference
types") and asked for every `!`/`null!`/`default!` added on this branch to be replaced with real
nullable typing, a guard exception, a flow attribute, or (in tests) `Assert.NotNull`, keeping only
the ones where the invariant is real, local, and the compiler genuinely cannot see it — each with a
one-line comment.

Patterns applied across `src/NetPrints.Core` and `src/NetPrints.Editor`:
- **Nullable + caller handling**: `NodeGraph.Class`/`Project`, `Variable.GetterMethod`/
  `SetterMethod`, `MethodSpecifier.MethodParameter.ExplicitDefaultValue`.
- **Guard exception at the boundary**: `ExecutionGraph.EntryNode` (backing field nullable, getter
  throws `InvalidOperationException` if read before set — `required` was ruled out, `CS9032`:
  `required` needs a setter at least as visible as the containing type, and `EntryNode`'s setter is
  intentionally more restricted); `Project.GetProjectDirectory` (throws instead of
  `Path.GetDirectoryName(x)!`); `NodeGraphVM.Drop` (`Graph.Class ?? throw new
  InvalidOperationException(...)`).
- **Flow attributes over assertions**: `OperatorUtil.TryGetOperatorInfo` rewritten to the standard
  `[MaybeNullWhen(false)] out T` `TryGetValue` pattern. Confirmed by spike that this only narrows
  the compiler's flow analysis at call sites using `out var name`, not `out ExplicitType name` — the
  explicit type at the declaration site defeats the attribute (undocumented but reproducible with a
  hand-rolled wrapper and with `Dictionary<K,V>.TryGetValue` itself). The three call sites
  (`CallMethodNode.ToString`, `ExecutionGraphTranslator`, `SuggestionListVM`/`SuggestionItem`) were
  changed to `out var operatorInfo` accordingly.
- **Fallbacks for values that are conceptually always-present but typed nullable upstream**:
  `Project.SaveVersion` (`?? new Version(0, 0)`), `ConstructorGraph.ToString` (`Class?.Name ??
  "?"`).
- **Tests**: `Project.LoadFromPath` returns `Project?`; the five call sites
  (`tests/NetPrints.Editor.Tests/TestPaths.cs`, `tests/NetPrints.Editor.Tests/Main/
  MainEditorVMTests.cs` x3, `tests/NetPrints.Editor.UITests/Graph/EditFlowTests.cs`) use
  `Assert.NotNull(project)` before use instead of `!`, per the rule's explicit test-code preference
  (xUnit v3 annotates `Assert.NotNull` so the compiler narrows the flow afterward).
- Two `!` found during the second pass turned out to be entirely redundant, not just replaceable:
  `MethodGraph.GenericArgumentTypes` and `SuggestionListVM`'s `NodeOutputTypePin` branch both called
  `.InferredType!` on a `NodeOutputTypePin`, but `NodeOutputTypePin.InferredType` is already declared
  as a non-nullable override of the base `NodeTypePin.InferredType` (`src/NetPrints.Core/Graph/
  NodeOutputTypePin.cs`) — the `!` was dead code left over from before the override was narrowed;
  removed instead of commented.

`!` that remain (all in `#nullable enable` files, all locally justified, all with a comment at the
site — see `git diff origin/master -- src tests` for exact context):
- `CallMethodNode.ArgumentTypes`, `.Arguments`, `.ReturnTypes` (`src/NetPrints.Core/Graph/
  CallMethodNode.cs`): `PinType.Value!` — the pin's `PinType.Value` is set from
  `MethodSpecifier.Parameters`/`.ReturnTypes` when each pin is created in the constructor and never
  cleared afterward, so it is never null for this node's own pins. `ObservableValue<T>.Value` itself
  has to stay `[AllowNull, MaybeNull]` (it is legitimately null for *other* pins, e.g. an
  unconnected/uninferred type pin), so the compiler cannot see this node-local invariant.
- `ConstructorNode.ClassType`, `.ArgumentTypes` (`src/NetPrints.Core/Graph/ConstructorNode.cs`): same
  reasoning, set from `ConstructorSpecifier.DeclaringType`/`.Arguments`.
- `GraphUtil.AddRerouteNode` (`src/NetPrints.Core/Graph/GraphUtil.cs`): `pin.PinType.Value!`/
  `pin.IncomingPin.PinType.Value!` — both pins are already connected to each other (checked with a
  guard clause immediately above), so their inferred types are resolved by construction of the
  connection, not by anything the compiler can express.
- `MakeArrayTypeNode.ToString` (`src/NetPrints.Core/Graph/MakeArrayTypeNode.cs`): `arrayType.Value!`
  — always assigned from `GetArrayType()` (constructor and `HandleInputTypeChanged`), which never
  returns null; the field's declared type has to accommodate `ObservableValue<T>.Value`'s general
  nullability, not this type's specific usage.

All seven are the same shape: an `ObservableValue<T>.Value` (necessarily `[AllowNull, MaybeNull]`
because it *is* legitimately null in other contexts) being read at a call site where a local,
node-specific invariant — established in that node's own constructor and never violated afterward —
guarantees it is set. No flow attribute expresses "null only until this specific constructor runs on
this specific pin," so `!` with a comment is the sanctioned choice for these.

## Sub-phase C

### T024 — `CodeDiagnostic` added early, in `src/NetPrints.Core/Compilation/CodeDiagnostic.cs`

`DiagnosticExtensions.ToDiagnostic(this DocumentIssue)` (compilation-and-diagnostics.md §1) returns
`NetPrints.Compilation.CodeDiagnostic`, but that type's own contract entry is normatively part of
sub-phase I (T090, alongside `DiagnosticMapper`/`SourceFile`/`CodeDiagnosticSeverity`), which the
dependency chart runs long after sub-phase C. Rather than stub or duplicate the type, added just the
record (`CodeDiagnosticSeverity` enum + `CodeDiagnostic` record, exactly as compilation-and-diagnostics.md
§1 specifies, using `Microsoft.CodeAnalysis.Text.LinePositionSpan`, already available transitively
through Core's existing `Microsoft.CodeAnalysis.CSharp.Workspaces` reference) now, with no other
members of that section (`SourceFile`, `DiagnosticMapper`, `SourceMap`, `TranslationException`,
`ClassTranslator(TranslationEnvironment)` overload). `ToDiagnostic` maps `Severity`/`Code`/`Message`
one-to-one and `Document?.Path` to `SourcePath`; `ClassFullName`/`GraphKey`/`NodeId`/`Span` are left
null (a `DocumentIssue` does not carry them). T090's implementer should extend this file, not recreate
it, and should not be surprised to find `CodeDiagnostic` already present when starting sub-phase I.

### T024 — doc-comment `cref` direction

`DocumentId`'s XML doc referred to `IDocumentStore` (T040, not yet added) and `CodeDiagnostic`'s
referred to `NetPrints.Serialization.DiagnosticExtensions.ToDiagnostic` — Core cannot `cref` into
Serialization (wrong dependency direction: Serialization references Core, not the reverse) and a
`cref` to a type that does not exist yet fails the build (`CS1574`, XML docs are required and checked,
AGENTS.md). Both were written as plain `<c>` text instead. Revisit `DocumentId`'s once T040 lands
`IDocumentStore` (a real `cref` will then resolve); `CodeDiagnostic`'s stays `<c>` permanently (the
dependency direction never reverses).

### T025 — `VariableRef.Scope`'s enum has no home in `NetPrints.Core` yet

data-model.md §3 (sub-phase H, US5) adds a `VariableScope` enum to `NetPrints.Core` and a matching
`Scope` member to `VariableSpecifier`; document-format.md §1.6's `VariableRef` record already carries
a `Scope` field (`Member`\|`Local`, omit `Member`) because the *document* contract is written for the
whole phase, not just sub-phase C. Since sub-phase C must not implement locals (that is T084-T088) and
`VariableSpecifier` has no `Scope` of its own yet, added `NetPrints.Serialization.Documents.VariableScope`
(a document-only enum, `Member`/`Local`) instead of reaching into Core ahead of its own task. T029's
`NodeMappingContext.ToRef(VariableSpecifier)` always produces `Scope = VariableScope.Member` (omitted,
since every variable in sub-phase C is a class member); `FromRef` does not read it back onto the model
at all (nothing to set it on yet). T086 (sub-phase H, "`locals` (inline records) + `VariableRef.scope`
mapping") is where this enum's home and the mapping both need revisiting — most likely moving (or
duplicating, if `Serialization` should not depend on that shape of `Core`) this enum to `NetPrints.Core`
alongside the new `VariableSpecifier.Scope`, and updating `ToRef`/`FromRef` to round-trip a real local
scope instead of a constant.

### T025 — kind fields modeled without STJ enum types where the format uses lowercase literals

document-format.md §1.1 shows enum values (`MemberVisibility`, `MethodModifiers`, …) writing their C#
member name verbatim and PascalCased (`"Public"`, `"Static, Async"` — confirmed against the worked
`HelloWorld.Program.netpc.json` example in §1.7), which is `JsonStringEnumConverter`'s default behavior
(the camelCase naming policy applies to *property names*, not enum member names, unless a converter is
built with an explicit naming policy — nothing in §2.3's serializer options does that). `RerouteNodeDocument.PinKind`
is documented with lowercase literals instead (`"exec"|"data"|"type"`, matching the lowercase `kind`
segment of a pin reference, §1.4.2), so it is modeled as a plain `string`, not a C# enum — inventing an
enum here would either serialize PascalCase (wrong) or need a bespoke per-property naming override
(more machinery than a hand-checked string constant needs). `Mapping/BuiltIn/FlowConverters.cs` (T034)
must write/compare exactly `"exec"`/`"data"`/`"type"`, matching `PinKeys`' own kind strings.

### T017 — `TypeGraph` needs a non-serialized back-reference to its class

Extending T016's finding: `GraphKeys.For(NodeGraph)` needs to find a variable's type graph's owning
class to key it `<variableId>/type` (document-format.md §1.4.1), but nothing before this task ever
sets `NodeGraph.Class` (the inherited, `[DataMember]` property) on a `Variable.TypeGraph` — unlike
method/constructor graphs, which the editor and `AllNodesFixtureFactory` already set `.Class` on
today. Setting it (`TypeGraph = new TypeGraph { Class = cls }` in `Variable`'s constructor) was the
first attempt; it broke `AllNodesFixtureRegenerationTests.FactoryMatchesCheckedInFixture` the same
way T016's literal `[DataMember] Id` did, because `Class` is a real, already-serialized DataContract
member and every variable's type graph in the checked-in `AllNodes.netpp`/`.netpc` fixture had always
serialized it as absent.

Choice: a second, `[IgnoreDataMember]` property on `TypeGraph`, `OwningClass`, set by `Variable`'s
constructor and `[OnDeserialized]` hook, distinct from the inherited `Class`. `GraphKeys.For` uses
`OwningClass` for a `TypeGraph` and the inherited `Class` for everything else (which already carries
real values in existing code paths, so no fixture is affected). Verified: full `Core.Tests` suite
green both before and after (65/65, up from 59 after T016), including both fixture-regeneration
tests. General lesson carried into T021+: any new in-memory-only navigation this project needs on a
`[DataContract]` graph-side type must be a distinct, `[IgnoreDataMember]`-marked member — never reuse
or repurpose an existing `[DataMember]` property for it, even when the existing property's static
type already fits, because doing so silently changes what legacy XML serializes.

### T023 — Checkpoint B reached

All of sub-phase B (T007–T022) is done. Verified: `dotnet build NetPrints.slnx -c Release` 0
warnings/0 errors solution-wide (11 new projects since T021 included); full suite 310 total, 301
succeeded, 9 skipped, 0 failed (`dotnet test --solution NetPrints.slnx -c Release --no-build --
--ignore-exit-code 8`), including the full headless UI suite; `dotnet format NetPrints.slnx
--verify-no-changes` clean with no `--exclude-diagnostics` (T013 already dropped the RS1024
exclusion from CI; this run confirms `CONTRIBUTING.md`'s own command, corrected in this commit to
match, is clean too); the four sub-phase A characterization gates (`GoldenCSharpTests`,
`NotificationMapTests`, `AllNodesFixtureRegenerationTests`, `HelloWorldSampleTests`) all still pass;
no reference to Fody remains outside the legacy VSIX (`.gitattributes`' `FodyWeavers.xml/.xsd`
lines, kept for the out-of-scope VS extension per the constitution). `CONTRIBUTING.md`'s own
"before you open a PR" commands still had the RS1024 exclusion T013 removed from CI; corrected here
so contributors' local checks match CI (T013 only updated `ci.yml`, not this file).

### T022 — `UnhandledExceptionHandler`'s two `internal` reporting methods

ED-T12 ("logs 1001/1002 through a collecting logger and still shows the dialog") lives in
`tests/NetPrints.Editor.Tests` — the non-UI, non-headless test project (no Avalonia platform, no
running dispatcher loop). Triggering the real `Dispatcher.UIThread.UnhandledException` event needs a
running dispatcher; triggering a real `TaskScheduler.UnobservedTaskException` needs a GC-forced,
finalizer-driven wait, which is slow and not the kind of thing this project's fast unit tests do
elsewhere. Choice: split each event handler into a thin subscriber (marks the event handled/observed)
and an `internal` method with the actual logging + dialog + duplicate-suppression logic
(`ReportUnhandledUiException`, `ReportUnobservedTaskException`), reachable directly from
`NetPrints.Editor.Tests` via the existing `InternalsVisibleTo`. Production wiring (the constructor's
event subscriptions) is unchanged; the test calls exactly the code a real event would run.

### T022 — `EditorHostServices`/`EditorContext.LoggerFactory` call-site inventory

Three places construct `EditorContext` positionally and two construct `EditorComposition`; all five
needed updating for the new required `LoggerFactory` parameter (record) / `EditorHostServices` first
parameter (composition): `EditorComposition.cs` itself, `EditorApp.axaml.cs` (`HostServices` static
gate, `InvalidOperationException` if read unset — set by Desktop `Program.Main` before
`StartWithClassicDesktopLifetime`), `HeadlessApp.cs` (`new EditorHostServices(NullLoggerFactory.Instance)`
alongside the existing `customize` hook — P0's hook stays until T098 as the task says),
`tests/NetPrints.Editor.Tests/Hosting/Fakes.cs`'s `TestEditor` and one direct `EditorContext` call in
`tests/NetPrints.Editor.UITests/Dialogs/DialogTests.cs`, both `NullLoggerFactory.Instance`. Grepped
for `new EditorContext(`/`new EditorComposition(` (not just "EditorContext"/"EditorComposition"
mentions) to be sure nothing else constructs either positionally; `with` expressions elsewhere
(`HeadlessApp`'s customize hook) don't need touching, since record `with` works by property name.

`AvaloniaLogSink`/`CA1848`: forwarding an arbitrary, runtime-supplied Avalonia message template
through a single `[LoggerMessage]`-generated method isn't expressible as a compile-time template (the
message *is* one of the dynamic values here), so the generated method takes `LogLevel level` as an
ordinary parameter (omitting `Level` from the attribute enables this) and the two `ILogSink.Log`
overloads pre-format Avalonia's `propertyValues` into a plain string before logging, keeping the
`[LoggerMessage]` template itself static ("{Source}: {Message}") — satisfies CA1848 and CA2254
without losing the forwarded content. Verified end to end (not just unit tests): ran the built
`NetPrints.Desktop.dll` under a throwaway `Xvfb :171` — it started, `EditorApp.HostServices` was set
correctly (no `InvalidOperationException`), and Avalonia's own GLX-blacklist warning and layout/binding
messages appeared on the console with `Avalonia.<Area>` categories through the new sink, confirming the
whole logging pipeline (not just that it compiles).

### T019 — `GraphAutoLayout`: which connection counts for a multi-connection pin

data-model.md §2's neighbour rule ("the first connected input pin ... that is already placed") is
unambiguous for data/type pins (`IncomingPin`, singular) but `NodeInputExecPin.IncomingPins` and
`NodeOutputDataPin`/`NodeOutputTypePin.OutgoingPins` are collections (an exec pin can have several
incoming branches; a data/type output can fan out). Choice: for a pin with more than one connection,
check its own connections in their collection order and take the first that leads to an
already-placed node, rather than only ever looking at the first connection regardless of whether it
is placed. None of DF-T20's required scenarios exercise a multi-connection pin, so this is
unobserved by the current test suite; recorded so a later phase does not need to re-derive it, and so
a future test can pin it down explicitly if it turns out to matter (e.g. once the editor lets branch
merges happen before layout is settled).

**Regression found and fixed by the full test suite, not by the build**: the *first* attempt at
removing `MethodGraph!.` in `ReturnNode.cs`/`MethodEntryNode.cs` (see "Current Work" above) cached the
containing `MethodGraph` in a `private readonly MethodGraph methodGraph;` field set from the
constructor parameter. `DataContractSerializer` bypasses constructors entirely (`FormatterServices`-
style uninitialized allocation, `[DataMember]` properties set by reflection afterward), so that field
stayed `null` on every deserialized `ReturnNode`/`MethodEntryNode` — a `NullReferenceException` in
`ReturnNode.UpdateMainNodeInputTypes()` (called from `OnMethodDeserialized` while loading *any* saved
project, not a rare edge case) that a solution build cannot see (it's a runtime null, not a nullable-
type mismatch) and that the characterization/golden tests' *building* fixtures in-memory did not hit
either — only `HelloWorldSampleTests`, `GoldenCSharpTests` (which load `AllNodes`/`HelloWorld` from
disk) and every `Editor.Tests`/`UITests` test built on `TestPaths.LoadHelloWorldCopy()` caught it, by
actually failing. Fixed by computing `methodGraph` from `Graph` on every access instead
(`private MethodGraph methodGraph => (MethodGraph)Graph;`), the same pattern already used by the
unmodified `ConstructorEntryNode.ConstructorGraph` — `Node.Graph` itself *is* a `[DataMember]` and is
correctly restored by DataContract, so deriving from it stays correct on both the constructor and the
deserialization path, instead of only the constructor path.

Lesson for T012/T013/T014 and beyond: a `private readonly` field assigned only in a constructor is
exactly the kind of "always set, so I can drop the `!`" reasoning the owner's rule is about — correct
for plain C# construction, **wrong** for anything under a `[DataContract]`/`[OnDeserialized]` type in
this codebase, since those bypass constructors. Prefer computing from an already-`[DataMember]`
property (as done here), or initialize in `[OnDeserializing]`/`[OnDeserialized]`, never a field only
the constructor touches.

Verified after the fix: `dotnet build NetPrints.slnx -c Release` — 0 warnings, 0 errors, solution-
wide; full `dotnet test --solution NetPrints.slnx -c Release --no-build -- --ignore-exit-code 8` was
re-run afterward (see checkpoint report for the result). Characterization/golden tests
(`GoldenCSharpTests`, `NotificationMapTests`, `AllNodesFixtureRegenerationTests`) are otherwise
unchanged by this pass — none of the nullability changes altered any *intended* model behavior, only
how the compiler is told about it (and, in this one case, a real bug the compiler could not see at
all, caught only by running the tests that exercise deserialization from disk).

### T011 — `TreatWarningsAsErrors` scoping ahead of T014

T011's title is "Nullable + warnings-as-errors in `src/NetPrints.Core` `Core/` and `Graph/`", but
`Directory.Build.props`'s `TreatWarningsAsErrors` default stays `false` until T014 (which flips the
*root* default and deletes every per-project override, including this one). T011 and T012 both
touch `src/NetPrints.Core` — the same physical project, different folders — so a project-wide flip
in T011 would also gate T012's not-yet-nullable-annotated `Translator/`, `Compilation/`,
`Serialization/` folders.

Verified empirically (clean rebuild of just `NetPrints.Core.csproj` with the override added) that
this is a non-issue in practice: those folders currently produce zero warnings of any kind even with
`TreatWarningsAsErrors=true`, so turning it on for the whole project does not block T012. Choice:
added `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to `src/NetPrints.Core/
NetPrints.Core.csproj` now (with a comment) rather than waiting for T014, since T011's own folders
are fully done and this gives an immediate regression guard against reintroducing warnings there. If
T012 later needs to touch `Translator/`/`Compilation/`/`Serialization/` incrementally and hits a
warning it isn't ready to fix yet, this line can be removed again until T014 without affecting T011's
own scope — noted here so that isn't mistaken for a T011 regression.

### T012 — Nullable rollout for `Translator/`, `Compilation/`, `Serialization/`

Same rule as T011, applied to the remaining `src/NetPrints.Core` folders. Notable points:

- `CodeCompileResults.PathToAssembly` and `Project.LastCompiledAssemblyPath` made nullable
  (`string?`): the legacy XML sample files already round-trip `<LastCompiledAssemblyPath
  i:nil="true"/>`, confirming this was already a real optional value, not an oversight.
- `ExecutionGraphTranslator`'s `graph`/`random` fields are set once, as the first statement of
  `Translate()`, and read by every other method on the class — but never in a constructor
  (the translator instance is created once and `Translate()` is called on it repeatedly, once per
  method/constructor, by `ClassTranslator`). Same guard-exception pattern as
  `ExecutionGraph.EntryNode` (T011): a nullable backing field, a non-nullable property that throws
  `InvalidOperationException` if read before `Translate()` ran.
- `nodeTypeHandlers`' dispatch table cast each node with `node as SomeNodeType` before calling the
  type-specific `TranslateXNode(SomeNodeType node)` overload; since the dictionary is keyed by
  `node.GetType()` (`TranslateNode`), the cast is always exact. Changed to a hard cast
  (`(SomeNodeType)node`) instead of `as` + `!`: same runtime behavior when the invariant holds, but
  throws a clear `InvalidCastException` instead of an `!`-asserted null if it is ever violated.
  `CallMethodNode.HandlesExceptions` gained `[MemberNotNullWhen(true, nameof(CatchPin))]` instead of
  a `!` at its one call site (`WriteGotoOutputPinIfNecessary(node.CatchPin, ...)` under
  `if (node.HandlesExceptions)`), since the property's own getter (`!IsPure && CatchPin?.OutgoingPin
  != null`) already encodes exactly that postcondition.
- `GetPinIncomingValue` (an input pin's C# expression) genuinely returns null for one case: an
  unconnected pin using `UsesExplicitDefaultValue` (omit the call argument, let the C# default
  parameter value apply — a `CallMethodNode`/`ConstructorNode`-only concept). Its return type is now
  `string?`, and callers either already branch on `is null` (call-argument lists, which is why this
  was correct all along) or are for a pin flavor that never sets that flag (a condition, a value
  being assigned, an array size, ...) — those keep a commented `!`.
- `TranslatorUtil.ObjectToLiteral`'s `obj` parameter is `object?` (it already handled `obj is null`
  explicitly, returning the `"null"` literal, so this was a pre-existing gap between the annotation
  and the real behavior, not a new one; found because `NodePinVM.cs`'s call site passes
  `MethodParameter.ExplicitDefaultValue`, nullable since T011).
- The recurring `pin.PinType.Value!.FullCodeName` (7 sites in this file, plus the pre-existing 7 from
  T011's `CallMethodNode`/`ConstructorNode`/`GraphUtil`/`MakeArrayTypeNode`) is the same
  translator-only invariant: the translator runs solely on graphs that already went through
  `GraphTypeInference.Relax` (via `[OnDeserialized]`, T020 extracts this call explicitly), so every
  pin's inferred/assigned type is resolved by the time any `Translate*` method runs. No flow
  attribute expresses "resolved because a whole separate pass ran earlier," so this remains the one
  recurring, commented `!` pattern for the entire translator.
- Two `NodeGraph.Class` dereferences (`VariableSetterNode`/`VariableGetterNode` translating a static
  member with no explicit target type) now guard with `?? throw new InvalidOperationException(...)`
  instead of a bare `!`, since an untethered graph reaching code generation is a genuine bug worth a
  clear message, not just a locally-obvious invariant.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors (Reflection's 4 pre-existing
RS1024 and Cli's 2 pre-existing CS8600/CS8602 are T013/T014 scope, untouched); full
`dotnet test --solution` 268 total, 0 failed, 259 succeeded, 9 skipped — identical counts to the
T011 checkpoint, including `GoldenCSharpTests` (the translator's actual output is unchanged).

### T009/T010 — new AGENTS.md "MVVM (CommunityToolkit.Mvvm)" rules (owner, commit a863e7e)

While migrating, the owner added an explicit MVVM section to `AGENTS.md`: `[ObservableProperty]`
partial properties for anything with its own backing value; setter side effects (including raising
a model's own custom delegate event, e.g. `IncomingPinChanged`) go in the generated
`partial void On<Name>Changed(oldValue, newValue)` hook, not a hand-written setter; dependent
properties use `[NotifyPropertyChangedFor(nameof(Other))]`; `OnPropertyChanged(nameof(X))` by hand
only when the change does not go through an observable setter (e.g. `Node.IsPure`, which has no
backing field — it is computed from pin counts and changed via `SetPurity`). Applied throughout;
see the pin files and `Variable.cs`/`Project.cs` for examples of each pattern.

One consequence: `[NotifyPropertyChangedFor]` raises the source property's own change *before* its
dependents (verified against the generated code, CommunityToolkit.Mvvm 8.4.2), the reverse of
Fody's dependents-before-self order recorded in the sub-phase A golden
(`NotificationMap.golden.json`). `NotificationMapTests` was changed to compare each property's
raised names as a sorted set instead of an exact sequence (T010's "T004 and T005 pass unchanged" is
about the same notifications firing, not Fody's specific order); the golden file's per-property
*sets* are otherwise unchanged from the sub-phase A baseline.

### T013 — RS1024 site count, and which site actually changes behaviour

The task text's "10 `RS1024` sites" does not match current code: a clean `dotnet build` of
`src/NetPrints.Reflection` (Nullable still `disable` at the start of this task) reports exactly
**4** RS1024 warnings, all in `ReflectionProvider.cs`: the `HashSet<IMethodSymbol>` in
`GetAllMembers` (line 42 pre-edit), `IsSubclassOf`'s base-type walk (`candidateBaseType == cls`),
and two argument/return-type filters in `GetMethods` (`t == searchType`, `m.ReturnType ==
searchType`). No other file in the project compares a Roslyn symbol type with `==`/`.Equals`/an
unguarded collection (`ReflectionConverter.cs`, `DocumentationUtil.cs`, `IReflectionProvider.cs`'s
query classes compare NetPrints' own `TypeSpecifier`, not Roslyn symbols). Recorded here so a later
phase does not go looking for six more sites that do not exist; the count in the task was likely
written against an earlier draft of the file.

Applying `SymbolEqualityComparer.Default` to all four sites mechanically reproduces exactly the PR
#5 regression the task warns about: `SuggestionListVMTests.CategoriesPerPinKind` fails (a
`stringOutCategories` assertion gains an unexpected `"This Methods"` entry). Bisected by reverting
one site at a time and re-running the single test (`dotnet <Editor.Tests.dll> -method
"*CategoriesPerPinKind*"`, ~5s): reverting only `IsSubclassOf`'s `candidateBaseType == cls` makes
the test pass again with the other three sites still on `SymbolEqualityComparer.Default`; the other
three are safe with the comparer (full suite green, 268/259/9/0 unchanged from the T012 checkpoint).

Root cause understood well enough to be confident this is a real behaviour change, not a red
herring: `GetTypeFromSpecifier` (used to resolve `query.Type` into the `baseType` that `IsSubclassOf`
is ultimately called on/with) resolves a metadata name by scanning *every* referenced assembly and
keeping the first match (`GetValidTypes(string)`), independently for the search type and for the
type whose base chain is walked. For a common type like `object`, when more than one referenced
assembly can produce a same-named symbol (ref-pack facades and the runtime assembly both exposing
`System.Object`, for instance), that scan and Roslyn's own `.BaseType`/`.Parameters[].Type`
resolution can land on different-but-structurally-equal `INamedTypeSymbol` instances.
`SymbolEqualityComparer.Default` treats those as equal (correctly, in the abstract); plain `==`
(reference equality, since `ITypeSymbol` has no operator overload) does not. That difference changes
which methods `IsSubclassOf`-gated queries return — here, a same-class instance method landing in
the wrong "This Methods" vs "Static Methods" bucket in the search list.

Choice: keep identity comparison at this one site, explicit rather than the bare `==` RS1024 flags
(`ReferenceEqualityComparer.Instance.Equals(candidateBaseType, cls)`, one-line comment pointing back
to this entry), instead of "fixing" the reference-equality gap as a side effect of a warnings task.
Fixing `GetValidTypes` to return one canonical symbol per name (which would make
`SymbolEqualityComparer` and identity agree everywhere, and arguably belongs with T058's
`ReflectionProvider` constructor rework) is left as a follow-up, not part of P1's critical path —
noted here so it is not rediscovered from scratch. The other three sites use
`SymbolEqualityComparer.Default` with no behaviour change (verified by the full suite).

Nullable rollout for the rest of `src/NetPrints.Reflection` (bundled into T013 by its "Same for..."
phrasing) followed the same patterns as T011/T012, plus two new ones this project needed:
- `IEqualityComparer<T>.Equals(T?, T?)`'s nullable-parameter annotation applies to
  `ReflectionProviderMethodQuery`/`ReflectionProviderVariableQuery` (`IReflectionProvider.cs`): both
  query classes' `Type`/`VisibleFrom`/`ReturnType`/`ArgumentType`/`VariableType` fields are
  documented as "or null for no filter" but were typed non-nullable pre-P1 (Nullable was off); made
  `TypeSpecifier?`, `Equals(T?, T?)` and `override Equals(object?)` follow, with an explicit
  both-null-or-neither guard added (the fields being null-safe was implicit before, now explicit).
- `MemoizedReflectionProvider`'s eleven `Func<...>` fields are all assigned unconditionally in
  `Reset()`, called from the constructor, so they are never actually null after construction — but a
  field assigned only by a method the constructor calls (not the constructor body itself) is exactly
  what CS8618 flags. `[MemberNotNull(nameof(field), ...)]` on `Reset()` (a method attribute; the
  attribute is not valid on a constructor per its `AttributeUsage`, so the ctor keeps no attribute and
  relies on the compiler seeing the `Reset()` call inside it) resolves this without a nullable field
  type and without touching the ten call sites that assume the fields are always set.
- `Memoization.Memoize<A, R>` builds a `ConcurrentDictionary<A, R>`, which requires `A : notnull`;
  an unconstrained `A` doesn't satisfy that once nullable is on. Added `where A : notnull` to the
  single-argument overload only (the two-argument overload's `Tuple<A, B>` key is a reference type
  and already satisfies `notnull` regardless of `A`/`B`, so it needs no constraint of its own).
- `DefaultOperatorSpecifiers.all`: a lazily-built `static List<MethodSpecifier>` field, filled by a
  private `AddOperator` helper that closed over the field directly. Nullable-typed the field and
  changed `AddOperator` to take the target list as a parameter instead, building into a local
  `built` list and assigning `all = built` only once construction is complete — avoids both a null
  check inside `AddOperator` on every call and any window where the field is non-null but partially
  built.
- `DocumentationUtil`'s four caches use `null` itself as a cached value (see `cachedDocuments[key] =
  null;`, "remember that there is no documentation"), so every cache dictionary's value type and
  every method that reads/writes them is nullable (`string?`, `XmlDocument?`), not just the ones that
  looked null-returning from the method signature alone. `XmlNode.SelectNodes` and
  `XmlNodeList.Item` are also nullable per current `System.Xml` annotations; added the corresponding
  null checks (`nodes != null && nodes.Count > 0`, `nodes.Item(0)?.InnerText`) rather than asserting.
- `ISymbolExtensions.IsSubclassOf` gained a real, previously-latent null path: its `cls` parameter
  was dereferenced (`cls.TypeKind`) with no null guard, while `symbol` was already guarded
  (`symbol != null && ...`). Since `GetMethods`' return-type filter calls
  `m.ReturnType.IsSubclassOf(searchType)` where `searchType` comes from `GetTypeFromSpecifier` (now
  visibly nullable), an unresolvable return-type query would have been a live
  `NullReferenceException` risk (nothing currently reaches it, or the pre-nullable code would already
  be crashing). Changed the signature to `(this ITypeSymbol? symbol, ITypeSymbol? cls)` and return
  `false` when either is null, matching the behaviour the `symbol`-null path already had — a real
  null-handling fix the rollout surfaced, not a new behaviour on any currently-tested path.
- One `!`-shaped case decided the other way: `typeof(Array).FullName` in
  `ReflectionConverter.TypeSpecifierFromSymbol` (`Type.FullName` is `string?` in general, but never
  null for a concrete, non-generic, non-array-element runtime type like `Array` itself) uses `!` with
  a one-line comment — the one `!` this task adds to `src/NetPrints.Reflection`.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors (Cli's 2 pre-existing
CS8600/CS8602 are T014 scope, untouched); `dotnet format NetPrints.slnx --verify-no-changes`
(no `--exclude-diagnostics`) passes; full suite 268 total, 0 failed, 259 succeeded, 9 skipped
(unchanged from T012). One file (`IReflectionProvider.cs`) was accidentally rewritten with LF line
endings mid-task despite being `-text`/CRLF in `.gitattributes`; caught by `git ls-files --eol`
before committing and restored to CRLF.

### T014 — root `TreatWarningsAsErrors` flip, `Core.Tests` and `Cli` nullable rollout

Flipped `Directory.Build.props`'s `TreatWarningsAsErrors` default to `true` and dropped its
"legacy projects" comment; removed the now-redundant per-project override from `src/NetPrints.Core`,
`src/NetPrints.Reflection` (added ahead of T014 by T011/T013), `src/NetPrints.Editor`,
`src/NetPrints.Desktop` and every `tests/*` project that had one (`NetPrints.Editor.Tests`,
`NetPrints.Testing.Ui`, `NetPrints.Editor.UITests`, `NetPrints.Desktop.E2ETests`). `Core` and
`Reflection` also dropped their `<Nullable>disable</Nullable>` override: every `.cs` file in `Core`
already carries a per-file `#nullable enable` pragma from T007–T012 (verified: none of its non-`obj/`
sources lack it), so the project default becoming `enable` changes nothing; `Reflection` had no
pragmas at all (T013 fixed the whole project), so removing the override is likewise a no-op once its
own line goes too. `tests/NetPrints.Core.Tests` had no per-file pragmas anywhere (18/18 files), so
its project-level `<Nullable>disable</Nullable>` was removed outright rather than migrated to
pragmas, matching how `Editor.Tests`/`Testing.Ui`/etc. already work (no override, project-wide
nullable, no pragmas).

This surfaced 34 new errors, all in `tests/NetPrints.Core.Tests` (Cli's own 2 pre-existing warnings
became errors as expected but needed no further fix beyond what's below):
- `src/NetPrints.Cli/Program.cs`: `Project.LoadFromPath` returns `Project?` (T011); the CLI's
  `Compile` path had no null handling at all (an unloadable project would previously have thrown a
  raw `NullReferenceException` after printing "Compiling ..."). Added an explicit null check that
  prints a message and returns the same failure code (`0`) the "compilation failed" branch already
  uses — the existing `options.ProjectPath!` two lines above predates this branch (present in
  `origin/master`) and was left alone. This file is fully replaced by T062's CLI rewrite, so the fix
  is intentionally minimal rather than a redesign.
- Test-only "load a project, assume it's there" pattern (`GoldenCSharpTests`,
  `HelloWorldSampleTests` ×2, `AllNodesFixtureRegenerationTests`' sibling pattern via
  `Directory.GetFiles`): `Assert.NotNull(project)` before use, per the established test-code
  preference over `!`. `Process.Start(psi)` (also `Process?`) in `HelloWorldSampleTests` needed the
  same treatment split out of the `using` declaration (`Process? startedProcess = Process.Start(psi);
  Assert.NotNull(startedProcess); using Process process = startedProcess;`).
- `Directory.GetFiles(...).Select(Path.GetFileName)` (`HelloWorldSampleTests`,
  `AllNodesFixtureRegenerationTests`): `Path.GetFileName` is `string?` in general, but never null for
  a real path returned by `Directory.GetFiles`. Rather than asserting that per element, added
  `.OfType<string>()` after the `Select` (same idiom as T013's `ReflectionProvider.GetValidTypes`):
  narrows to `IEnumerable<string>` and defensively drops any (never-expected) null instead of
  asserting it away, so the subsequent explicit-type `foreach (string file in ...)` has nothing left
  to warn about.
- `ClassTranslatorTests`: `VariableNode.TargetPin` is genuinely nullable (null for a static
  variable's getter/setter, per T011's own modeling) but the test's `getLengthNode` is a known
  instance-target node; narrowed through a local (`NodeInputDataPin? targetPin = ...;
  Assert.NotNull(targetPin);`) rather than asserting the property access directly, since a property
  access isn't as reliably narrowed by `Assert.NotNull` as a local.
- `ClassTranslatorTests`/`MethodTranslatorTests`: fields set only by a private `CreateXyz()` helper
  the constructor calls (not by the constructor body itself) are exactly the CS8618 shape
  `MemberNotNull` exists for (same pattern as this task's `MemoizedReflectionProvider` fix, T013):
  `[MemberNotNull(nameof(field))]` on each helper method.
- `GenericsTests`: `NodeTypePin.InferredType` is nullable when the pin is disconnected; the test
  connects it first, so `Assert.NotNull` on a local before reading `.Value`.
- `NotificationMapTests` (the largest single file, exercising reflection over arbitrary
  `INotifyPropertyChanged` types): this test's whole job is comparing "before" and "after" property
  values for types it does not know ahead of time, so `object` was never really non-nullable here —
  `TryMakeDifferentValue`'s `current`/`candidate` and the `AddOne`/enum-branch locals are now
  `object?` throughout (a property's reflected value, and the value being written back via
  `PropertyInfo.SetValue`, are both legitimately null for reference-typed properties; `candidate` was
  already explicitly set to `null` on two paths before this task, just not typed to say so). Also:
  - `PropertyChangedEventHandler`'s actual delegate signature is `(object? sender,
    PropertyChangedEventArgs e)`; the test's local `Handler` had a non-nullable `sender` (CS8622).
  - `e.PropertyName` is `string?` per `PropertyChangedEventArgs`'s general contract, but every INPC
    implementation in this codebase raises it with a real name (`nameof(X)`), never null or "all
    properties changed" (`null` conventionally means the latter, which nothing here does) — a guard
    exception (`?? throw new InvalidOperationException(...)`) instead of `!`, so a future violation
    fails loudly in this characterization test rather than silently sorting an empty string.
  - `IReadOnlyDictionary<Type, object>.TryGetValue`'s `[MaybeNullWhen(false)] out TValue` annotation
    applies even though this dictionary's `TValue` (`object`) is itself non-nullable; `out object?`
    at both call sites (the outer `instancesByType` lookup, and `pool` inside
    `TryMakeDifferentValue`), with an `Assert.NotNull`/`!= null` check consistent with how the method
    already used the result.
  - `Type.FullName` is `string?` in general (null for generic parameters, or types with incomplete
    reflection metadata) but never null for the closed, non-abstract, non-generic-definition public
    types this test enumerates; `type.FullName!` with a one-line comment (same shape as T013's
    `typeof(Array).FullName!`).
  - Two sites needed `!` specifically because reflection's `PropertyInfo.GetValue` result is
    statically `object?` but is provably non-null for a `bool`- or numeric-typed property
    (`!(bool)current!`, `AddOne(propertyType, current!)`): unboxing a possibly-null `object` to a
    value type is its own diagnostic (CS8605), separate from reference-nullability CS86xx codes, and
    fires even though the unbox only executes when `propertyType` guarantees a non-null boxed value.
  - `RegisterGraph`'s `NodeGraph` parameter is called with `Variable.GetterMethod`/`SetterMethod`
    (nullable since T011); made the local function's parameter `NodeGraph?` — the existing
    `if (graph == null) return;` guard already handled it correctly, it just wasn't typed to say so.

`!` added by this task (all in test code, all reflection-over-unknown-types invariants a
per-property-type check establishes but the compiler cannot correlate to the `object` it reflected):
`NotificationMapTests.cs` — `(bool)current!`, `AddOne(propertyType, current!)`, `type.FullName!`.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors across every project;
`dotnet format NetPrints.slnx --verify-no-changes` passes; full suite unchanged, 268 total,
259 succeeded, 9 skipped, 0 failed. This is the project-wide 0-warnings gate T023 (Checkpoint B)
needs, but T015–T022 (ids, node/member identity, pin keys, auto-placement, `GraphTypeInference`
extraction, the new project shells, logging foundation) are still open, so Checkpoint B itself is
not reached yet; T023 stays unchecked until they are.

### T016 — `Node.Id` must be `[IgnoreDataMember]`, not `[DataMember]`

data-model.md §2's code block shows `[DataMember] public string Id { get; internal set; }`. Taken
literally, this breaks two sub-phase A characterization gates the moment it's added:
`AllNodesFixtureRegenerationTests.FactoryMatchesCheckedInFixture` and
`HelloWorldSampleTests.FactoryMatchesCheckedInSample` both fail immediately, because both fixtures
are still produced by `AllNodesFixtureFactory`/`SampleProjectFactory` calling `Project.Save()` (the
legacy `DataContractSerializer` path, unchanged until T063) — with `Id` as a real `[DataMember]`, the
factory's freshly-built graphs (whose nodes now have real random ids from T016) serialize those ids
into the `.netpp`/`.netpc` XML, which no longer matches the checked-in copies from T003.

Regenerating the two fixtures (`NETPRINTS_REGENERATE_SAMPLES=1`) is not a safe fix here, unlike a
`NETPRINTS_UPDATE_SNAPSHOTS=1` golden refresh: these are *the* legacy fixtures document-format.md and
T038 describe throughout ("both fixtures' classes import without issues" via
`LegacyXmlDocumentFormat`, which document-format.md §2.2 says "calls `NodeGraph.AssignLegacyNodeIds()`
on every graph" **unconditionally**). `AssignLegacyNodeIds` throws `InvalidOperationException` if any
node already has an id (data-model.md §2, and T016's own contract). A regenerated fixture with real
ids baked in would make every future import of these exact files throw during T038 — the opposite of
"legacy files never have ids of their own", which is the whole reason `AssignLegacyNodeIds` exists.

Choice: `[IgnoreDataMember]` instead of `[DataMember]` on `Node.Id`. `Id` is never read from or
written to the legacy DataContract XML in either direction; the legacy path is unaffected (still
null until `AssignLegacyNodeIds` runs), and the two sub-phase A fixtures need no changes. This also
matches the constitution's non-negotiable "keep old tests green" and T023's own "A-gates unchanged"
requirement, which are unambiguous where a single illustrative code snippet is not. Verified: full
`NetPrints.Core.Tests` suite green (59/59, up from 46 before T015's `IdGenerationTests` and T016's
`NodeIdTests`) with `[IgnoreDataMember]`; failed with exactly the two fixture mismatches above under
`[DataMember]`. The same reasoning will apply to member ids (T017) and any other new random-id field
added to a `[DataContract]` graph-side type for the rest of P1 — recorded here so it isn't
rediscovered per field.

### T027 — layout position inlining needs a 4-state machine, not 3

First attempt at `CanonicalJsonWriter`'s layout-position rule (§2.3.1 rule 3, "the inner maps of the
root `layout` object") tracked only a 3-value `LayoutContext` (`None`/`LayoutMap`/`GraphMap`) and
marked a node inline as soon as its OWN context was `GraphMap` — which made the *per-graph position
map itself* (`"class": { "n0": [...] }`) inline as `"class": { "n0": [112, 112] }`, contradicting the
worked example in document-format.md §1.7, where that map is block (`"class": {\n  "n0": [112, 112]\n}`)
and only the leaf `[x, y]` array is inline. Fixed by adding a fourth state (`PositionValue`) so
inline-ness is checked one level later than the state that names it: `None → LayoutRoot` (the `layout`
property's own value, block) `→ GraphMap` (a per-graph map, still block) `→ PositionValue` (the `[x, y]`
array, inline). Caught by `HandcraftedDocumentMatchesExpectedCanonicalForm`, written to mirror the
exact §1.7 `HelloWorld.Program.netpc.json` example's line breaks (not just an ad hoc small case),
which is what surfaced this: the smaller per-rule tests (`ElementsOfNamedArraysAreInline`, etc.) would
not have caught it since none of them exercise a two-level nested map.

### T027 — the "inline array" rules only ever apply to elements, never to the array node itself

Re-reading §1.7's own example settled an ambiguity in §2.3.1's wording: a property named `connections`
(etc.) makes each *element* of that array inline (one connection per line), but the array itself is
still written as a normal block array (`"connections": [\n  { ... },\n  { ... }\n]`), never collapsed
to one line even when it has a single element (`"pins": [\n  { ... }\n]` in the worked example, not
`"pins": [ { ... } ]`). The first draft of the DF-T04 handcrafted-document test got this wrong (assumed
single-element `pins`/`method` collapsed to one line) and was corrected to match the block form once
the mismatch surfaced against the real writer output; `CanonicalJsonWriter` itself needed no change for
this one (the bug was in the test's expectation, not the code) — noted here in case a future reader
compares this test against §1.7 and is tempted to "fix" the writer instead of the (now correct) test.

### T028/T029 — enum-typed pin unconnected values are pre-stringified by the model, unlike everything else `TypedValueConverter` handles

`NodeMappingContext.ToValue(object? value, string where)`'s contract signature has no declared-type
parameter, which works for every supported type except one: `NodeInputDataPin.UnconnectedValue`'s
setter (`OnUnconnectedValueChanging`, unmodified pre-P1 code) requires the **stored value itself** to
already be a `string` for an enum-typed pin (`newValue.GetType() != typeof(string)` throws) — so
`pin.UnconnectedValue` for e.g. a `System.DayOfWeek` pin is the *string* `"Monday"`, never a boxed
`DayOfWeek.Monday`. Calling `TypedValueConverter.ToTypedValue("Monday", where)` on that value would
(correctly, per its own contract) produce `TypedValue("System.String", "Monday")` — the wrong `Type`,
since the pin's declared type is the enum, not `string`.

`TypedValueConverter`/`NodeMappingContext.ToValue`/`FromValue` are otherwise used for **genuine**
runtime-typed values that reveal their own type via `value.GetType()` — a real boxed enum from
`MethodParameter.ExplicitDefaultValue` (a reflected method's default parameter value can legitimately
be an enum instance), for instance — where `value.GetType()` is exactly right. So `TypedValueConverter`
keeps the literal 2-argument contract signature and handles `Enum` instances directly (`value switch { ...,
Enum v => v.ToString(), ... }`) for that genuine case. The one pin-unconnected-value quirk is handled
where the pin's declared type is already in scope: T035's `DocumentMapper` (the uniform, per-node "build
`pins: PinStateDocument[]`" step) checks `pin.PinType.Value is TypeSpecifier { IsEnum: true } t` and
`pin.UnconnectedValue is string s` first, building `new TypedValue(t.Name, s)` directly for that one
case, and calling `context.ToValue`/`TypedValueConverter.ToTypedValue` for every other pin. Symmetric on
read: `FromValue`/`FromTypedValue` return a *string* regardless of whether `Type` says `"System.String"`
or an enum name, which is exactly what an enum pin's `UnconnectedValue` setter requires — so no special
casing is needed on the read side, only on write.

### T030-T034 — a converter's `Id`/`Name`/`Pins` are placeholders; the "fixed node" pattern; enums vs. genericArgumentCount

Grouped into one commit: `NodeDocumentConverterRegistry.BuiltIn` (T030) is only meaningful once its 23
converters exist (T031-T034), and the "reuse the graph's constructor-created node" pattern below needed
one small addition to `NodeMappingContext` (`ClaimMainReturnNode`, T029's file) shared by only one of
them — splitting these five tasks into five commits would have left several individually non-buildable.

**Every `INodeDocumentConverter.ToDocument` passes `node.Id, null, null` for the base `Id`/`Name`/`Pins`
positional parameters.** The values are placeholders: document-format.md §2.6's `FromDocument`/`ToDocument`
description assigns `Id` (from the model or the document), `Name` (omitted when it equals `DefaultName`)
and `Pins` (renames/unconnected values, via `PinKeys`) **uniformly across every node kind** — that is
`DocumentMapper`'s job (T035), not each converter's; duplicating pin/name logic in 23 places would be both
wasteful and an easy place to drift. `DocumentMapper` is expected to build the final document with a
record `with` expression (`converter.ToDocument(node, context) with { Id = ..., Name = ..., Pins = ... }`),
which works across the base/derived boundary since C# records expose inherited positional members as
ordinary settable-via-`with` properties. Symmetrically, `CreateNode`'s contract (`INodeDocumentConverter.cs`'s
own XML doc) says the id/name/pins the *document* names are applied by the caller after `CreateNode`
returns — converters only need to give the node the right kind-specific pins.

**The five "fixed" node kinds (`methodEntry`, `constructorEntry`, `return`, `classReturn`, `typeReturn`)
never call `new` for the node their graph's own constructor already created.** `MethodGraph`'s constructor
unconditionally builds its `MethodEntryNode` + main `ReturnNode`; `ConstructorGraph`'s builds its
`ConstructorEntryNode`; `ClassGraph`'s and `TypeGraph`'s build their one `ClassReturnNode`/`TypeReturnNode`
— none of these constructors can be changed (sub-phase B model, used throughout the pre-P1 codebase too;
changing them risks the "keep old tests green" gate) and none expose a way to skip that step. So
`CreateNode` for these four singleton kinds fetches the graph's already-existing node
(`((MethodGraph)graph).EntryNode`, `((ClassGraph)graph).ReturnNode`, …) and reconfigures it in place
(`AddArgument()`/`AddInterfacePin()`/…) rather than constructing a new instance — by the time any node
document is processed, the graph (built via its normal constructor before the mapper starts placing
nodes) already has it. `return` is the one non-singleton case (a method can have extra early-return
nodes beyond the main one): `NodeMappingContext.ClaimMainReturnNode(MethodGraph)` (internal, added to
`NodeMappingContext.cs`) hands back the graph's pre-existing `MainReturnNode` the first time it's asked
for a given graph and `null` afterward, so the converter reconfigures the main node once and constructs
a fresh `ReturnNode` (whose constructor already replicates the main node's pins) for every one after
that. `ConstructorEntryNode` has no argument mechanism yet (a pre-P1 `// TODO`, T103a); its converter
throws `DocumentFormatException` if a document ever claims a nonzero `argumentCount`, since nothing can
honor it.

**Known format gap, not fixed here:** `CanSetPure`-capable node kinds (`CallMethodNode`, `ConstructorNode`,
`ExplicitCastNode`, `TernaryNode`, `AwaitNode`) have no document field recording whether they were toggled
pure — document-format.md §1.5's tables for these kinds list no such field, and their model constructors
always build the impure (with exec pins) pin shape. A pure instance of one of these, if ever saved, loads
back impure. Not exercised by AllNodes or any DF-Txx test (none toggles purity), so left as a discovered
gap for the contract owner rather than an invented field.

**`callMethod`'s `genericArgumentCount` is redundant with `method.genericArgs.Count`, kept for schema
completeness only.** `CallMethodNode`'s constructor derives every pin — target, catch/exception, generic
input type pins, arguments (with explicit defaults), returns — from the `MethodSpecifier` alone, so
`CreateNode` is just `new CallMethodNode(graph, context.FromRef(doc.Method))`; the separate
`GenericArgumentCount` field written by `ToDocument` (`node.InputTypePins.Count`) is not read back.

**Registry validation for "a kind must contain `/` (extension) or be a known built-in name"** needed a
concrete list to check the "known built-in" half against, since `INodeDocumentConverter` carries no
separate "this is built-in" flag — added a private `KnownBuiltInKinds` `HashSet<string>` (the same 23
strings `NodeDocumentConverterRegistry.BuiltIn` registers) to `NodeDocumentConverterRegistry`'s
constructor. `eventEntry` (sub-phase G, T080) must be added to both `KnownBuiltInKinds` and `BuiltIn`
together, or a hand-built eventEntry converter would fail registry construction.

### T036 — `DocumentMigrator.Supported` is computed from the actual migration chain, not hardcoded to `CurrentSchemaVersion`

DF-T10 requires "a synthetic test migration v1→v2 registered in a test migrator is applied" without
implying the real, production `CurrentSchemaVersion` const changes. So `Supported` is computed in the
constructor: for each `DocumentKind` with any registered migrations, walk the chain from version 1
(`1, 2, 3, …` while a migration exists for that `(kind, version)`) and take `chain length + 1`; the
overall `Supported` is the max of that across every kind and `CurrentSchemaVersion` itself (so with zero
migrations — P1's real, shipped configuration — `Supported == CurrentSchemaVersion == 1`, unaffected by
this generality). The constructor also verifies each kind's migrations form a contiguous chain starting
at version 1 (no gaps), matching the "gaps" half of its own contract line, which a hand-built
`GapMigration` (`FromVersion = 2` with nothing registered for `FromVersion = 1`) exercises.

Not implemented yet: the log call for `DocumentMigrated` (code 3001, document-format.md §2.5) — the
`DocumentMigrator` contract's constructor takes only `IReadOnlyList<IDocumentMigration>`, no `ILogger`,
and T103 ("Serialization log call sites (3001-3006)") is the task that wires logging through the
serialization layer generally. `Upgrade` does the migration but does not yet log it; T103's implementer
should add the log call there (and to every other 3001-3006 site) rather than assume it already exists.

### T026 — a source-generated `JsonTypeInfo` reused through a different `JsonSerializerOptions` does not keep the context's naming policy or default-ignore condition

The design sketch below (written before T026 was implemented) assumed `[JsonSourceGenerationOptions(PropertyNamingPolicy
= CamelCase, DefaultIgnoreCondition = WhenWritingDefault, ...)]` on `NetPrintsJsonContext` bakes those
two settings into the generated `JsonTypeInfo`/`JsonPropertyInfo` objects themselves, so `NetPrintsJsonOptions`
would only need to set `TypeInfoResolver` (plus the reader/writer flags document-format.md §2.3 lists)
and never repeat them. Verified empirically wrong: a debug harness comparing
`JsonSerializer.Serialize(doc, NetPrintsJsonContext.Default.Options)` (camelCase, defaults omitted) against
`JsonSerializer.Serialize(doc, new JsonSerializerOptions { TypeInfoResolver = NetPrintsJsonContext.Default })`
(a *different*, freshly-constructed options instance — exactly `NetPrintsJsonOptions`'s situation, since it must
combine the context with extension resolvers) showed the second form serializes with **PascalCase property names
and every default value written** (`{"Nodes":[...],"Id":"n0","Name":null,"Pins":null,"InterfaceCount":0}` instead
of `{"nodes":[{"$kind":"classReturn","id":"n0"}]}`). Per-property state (converters, e.g. the enum-as-string
converter and `NodeListConverter` on `GraphDocument.Nodes`; `[JsonPropertyOrder]`; the node DTOs' own
`[JsonIgnore(Condition = Never)]` overrides) *is* preserved across options instances, but the two options-level
blanket settings are not. Fix: `NetPrintsJsonOptions.SerializerOptions` explicitly repeats
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase` and `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault`
alongside the reader/writer flags the contract already lists; `UseStringEnumConverter` needed no such repeat
(confirmed with the same harness — enum properties still serialized as strings without it), matching that
converters are attached per `JsonPropertyInfo` rather than looked up from the options at resolution time.
Recorded here so a future schema/options change does not "clean up" these two lines as redundant with
`NetPrintsJsonContext`'s attribute — they are not, for any consumer other than the context's own `.Options`.

### T026 — `NodeListConverter.Read`'s array loop needs an explicit `reader.Read()` after `JsonElement.ParseValue`

First draft called `reader.Read()` once before the loop (to enter the array) and then, inside the loop,
only `JsonElement.ParseValue(ref reader)` per element, assuming `ParseValue` leaves the reader positioned
on the *next* token the way `Utf8JsonReader.Read()` normally advances. It does not: `ParseValue` (like
`TrySkip`) leaves the reader positioned on the parsed value's own *last* token (`EndObject`/`EndArray`, or
the scalar token itself), so the un-advanced loop condition re-entered the loop and tried to parse a new
value starting at `}`, throwing `'}' is an invalid start of a value.` for any node past the first (and, for
a single-node array, the reader never reached `EndArray` at all — deserialization silently produced a
`null` `Nodes` list instead of throwing, since the exception surfaced one level up as `ReadAndCacheConstructorArgument`
swallowing the per-property read failure into a missing constructor argument, not a visible error).
Fixed by calling `reader.Read()` again after `nodes.Add(...)`, before the loop condition is re-checked, so
the reader advances to the next element's start token (or `EndArray`) — the same pattern as the two-clause
`while (reader.Read() && ...)` form shown in Microsoft's own custom-list-converter samples, split into a
loop-body statement here since the array's own `StartArray` token is already consumed by an explicit
`reader.Read()` before the loop (matching this file's `derivedTypes` lookup happening once, outside the
loop, rather than being folded into the condition).

### T035 — `Mapping/DocumentMapper.cs` and `IDocumentMapper.cs`; `NodeDocumentConverterRegistry.FindByDocumentType`

Implemented per the design sketched by the previous agent (member id checks, `MapGraph`/`BuildPinStates`/
`BuildConnections`/`BuildLayout` on the write side; header → member-id validation → per-member-graph
construction → connections → positions/auto-placement → relax on the read side), with `Mapping/IDocumentMapper.cs`
added alongside it (document-format.md §2.6 names it as this section's third file; `LegacyXmlDocumentFormat`,
`ProjectPersistence` and `GraphCodeGenerator` all take it as a constructor parameter, so it has to exist as
its own public interface, not just `DocumentMapper`'s implicit contract) and `NodeDocumentConverterRegistry.FindByDocumentType(Type)`
added (third dictionary, same duplicate-check pattern as `FindByKind`/`FindByNodeType`) — both exactly as the
previous agent's notes anticipated. Two real deviations from that plan, found by running `DocumentMapperTests`
against `AllNodesFixtureFactory` and a set of hand-built documents:

**Pin states must be applied after connections (and a `GraphTypeInference.Relax` pass), not interleaved with
node creation.** The original plan applied `nodeDoc.Pins` (renames, `UnconnectedValue`) to each node immediately
after creating it, before any connections existed. This throws `ArgumentException` from
`NodeInputDataPin.OnUnconnectedValueChanging` for any pin whose *eligibility* for an unconnected value depends
on a type-pin connection rather than on its node's constructor — `TernaryNode.TrueObjectPin`/`FalseObjectPin`
are `System.Object` (not primitive, so `UsesUnconnectedValue` is `false`) until `TypePin`'s incoming connection
resolves a concrete type via `TernaryNode.HandleInputTypeChanged`, which only happens once `GraphUtil.ConnectTypePins`
has run. `MapGraphFromDocument` now: creates every node (collecting `(Node, NodeDocument)` pairs, no pin
application yet) → applies connections → `GraphTypeInference.Relax(graph)` (settles any type-pin chain the
direct connections did not immediately resolve) → *then* applies pin states → positions/auto-placement →
a second `GraphTypeInference.Relax(graph)` (matches the task line's "at the end of each graph" literally; a
no-op in every case exercised so far, since neither a rename nor an `UnconnectedValue` change feeds back into
type inference, but cheap to keep for whatever a future node kind does). Verified with
`AllNodesRoundTripsThroughToDocumentAndFromDocument`/`VariableTypeGraphSurvivesRoundTripWithSettledTypes`, which
both failed with exactly this exception (on `TrueObjectPin`) before the reorder and pass after it.

**Discovered, pre-existing model bug: `LiteralNode.UpdatePinTypes()` disconnects the node's value pins on
*every* `GraphTypeInference.Relax` pass, even with no generic arguments involved.** Its `if (constructedType
!= InputValuePin.PinType.Value)` (`Graph/LiteralNode.cs`) compares two `BaseType`-typed operands; `BaseType`
itself declares no `==`/`!=` (only `TypeSpecifier` does, for `(TypeSpecifier, TypeSpecifier)` and two
`(TypeSpecifier, BaseType)` overloads — none of which C#'s overload resolution picks when *both* operands are
statically `BaseType`), so the comparison falls back to reference equality; `GenericsHelper.ConstructWithTypePins`
returns a new `TypeSpecifier` instance on every call regardless of whether anything actually changed, so the
condition is `true` unconditionally and `GraphUtil.DisconnectInputDataPin`/`DisconnectOutputDataPin` run every
time, silently dropping any connection into or out of a `LiteralNode`'s value pins. `AllNodesFixtureFactory`
already discovered this independently while writing the sub-phase A characterization fixture (its own comment,
`tests/NetPrints.Core.Tests/Characterization/AllNodesFixtureFactory.cs` lines ~205-210, deliberately gives the
`ifElse` condition an *unconnected* value instead of a connected literal, citing "see implementation-notes.md" —
but no entry existed yet; this is that entry) — `LegacyXmlDocumentFormat` (T038) and `JsonDocumentFormat`
(T037) both call `GraphTypeInference.Relax` too, so **any real class whose C# used a literal passed directly
into a call or return (`Foo(5)`, `return 5;` — extremely common) will lose that connection the moment it is
loaded**, on both paths, not just through `DocumentMapper`. `DocumentMapperTests.CallMethodParameterInsertionKeepsConnectionsByName`
(DF-T19) uses a caller method's own entry-node argument pins instead of literal nodes as its connection source
to avoid the bug (entry-node argument pins reassign `PinType.Value` unconditionally too, but never call
`Disconnect*Pin`, so they do not lose their connections). Not fixed here: it is a `NetPrints.Core` model change
(`LiteralNode.cs`), out of scope for the two serialization tasks assigned this session, and any fix must keep
the sub-phase A/B golden-output tests green. **Flagging for whoever picks up T037/T042 next: this will very
likely surface in the `HelloWorld`/converted-sample golden round trips (DF-T02/DF-T03) the moment a sample
connects a literal to something instead of only using unconnected values** — worth a fix (likely: compare
`BaseType.Name`/structural equality instead of `!=`, or skip the disconnect when the constructed type has not
actually changed) before or alongside T042, not discovered mid-golden-test.

**Fixed** (start of the T037-T040 batch, own commit before T037): both comparisons in
`LiteralNode.UpdatePinTypes()` now use `constructedType.Equals(...)` (negated) instead of `!=`, so they dispatch
to `TypeSpecifier.Equals(object?)` — the concrete type's own value equality (name, generic arguments, `IsEnum`)
— regardless of the operands' `BaseType`-typed declaration. No new operator was added to `BaseType`: the existing
`TypeSpecifier`/`GenericType` equality already covers every value that reaches this method (`LiteralType` is
always a `TypeSpecifier`, never a bare `GenericType`, so `ConstructWithTypePins` always returns a `TypeSpecifier`
at runtime here). Regression test: `NetPrints.Tests.Graph.LiteralNodeTests.ConnectedLiteralKeepsItsConnectionAfterRelax`
connects a literal's value pin to a `CallMethodNode` argument pin and asserts the connection survives
`GraphTypeInference.Relax`. Fixing this changed two checked-in fixtures, both expected and regenerated:
`AllNodesFixtureRegenerationTests`' checked-in `AllNodes.Everything.netpc` (the object-identity/`z:Ref` shape of
the legacy XML changes — `LiteralType`, `InputValuePin.PinType.Value` and `ValuePin.PinType.Value` are now one
shared reference instead of two — even though every literal in the fixture was already non-generic and
value-unchanged; regenerated with `NETPRINTS_REGENERATE_SAMPLES=1`, no logical difference). The
`AllNodesFixtureFactory` `ifElse` workaround (unconnected condition) and the `DocumentMapperTests.
CallMethodParameterInsertionKeepsConnectionsByName` (DF-T19) workaround (entry-node argument pins instead of
literal nodes as connection sources) are both removed now that a connected literal survives `Relax`; all four
sub-phase A characterization gates (`GoldenCSharpTests`, `NotificationMapTests`,
`AllNodesFixtureRegenerationTests`, `HelloWorldSampleTests`) still pass after regenerating
`AllNodes.Everything.netpc`, `AllNodes.Everything.cs` and `PinKeys.golden.txt` (`NETPRINTS_REGENERATE_SAMPLES=1`
+ `NETPRINTS_UPDATE_SNAPSHOTS=1`): the `ifElse` condition's generated C# changes from inlining `true` directly
to materializing it through a local variable (`System.Boolean varValue = true; if (varValue)`), which is the
translator's normal, correct handling of a connected literal, not a regression.

Also implemented, per the plan and without incident: the `Variable.TypeGraph` special case (`new Variable(cls,
name, TypeSpecifier.FromType<object>(), null, null, modifiers)` for a real `TypeGraph`+`TypeReturnNode` to
populate, then — because unlike every other graph kind's constructor, `Variable`'s *also* builds a placeholder
`TypeNode` tree via `GraphUtil.CreateNestedTypeNode` for that placeholder type — stripping every node but the
`TypeReturnNode` and resetting `ReturnNode.TypePin.IncomingPin = null` before mapping the document's own
type-graph nodes into the now-empty-except-for-its-fixed-node graph); a variable (and its getter/setter method
graphs) must be added to `cls.Variables`/`variable.GetterMethod`/`SetterMethod` *before* their own graphs are
mapped, since `GraphKeys.For` resolves a variable's type/accessor graph keys by scanning `cls.Variables` for a
`ReferenceEquals` match, which only succeeds once that back-reference is wired; `FindByDocumentType(Type)` on
the registry, mirroring `FindByKind`/`FindByNodeType` exactly.

Test coverage (`tests/NetPrints.Core.Tests/Serialization/DocumentMapperTests.cs`): DF-T06 (full
`AllNodesFixtureFactory` round trip compared via `JsonNode.DeepEquals` of two `ToDocument` calls either side of
a `FromDocument`), DF-T08 (a hand-built unknown node plus a connection into it, preserved through `FromDocument`
→ `ToDocument`), DF-T19 (parameter insertion keeps the connection on the unmoved parameter names), DF-T20 (an
unpositioned node is auto-placed, positioned ones keep their exact coordinates, two loads of the same document
place it identically), DF-T22 (missing/duplicate member id → `DocumentFormatException`; layout keys are member
ids, unaffected by reordering `cls.Methods`), DF-T25 (default-named vs. renamed node), DF-T27 JSON-path half
(`Variable.TypeGraph` non-null and the variable's resolved type unchanged after a round trip). DF-T02/T03/T05
(golden-byte round trips against committed samples) and DF-T27's legacy-import half are T042/T020, not this task.

### Design notes for the next agent: T037-T043 remaining

**T037-T043**: not designed in detail yet. `JsonDocumentFormat` (T037) is the read/write pipeline around
`DocumentMapper`+`DocumentMigrator`+`NetPrintsJsonOptions`+`CanonicalJsonWriter`, per document-format.md
§2.2 — should be mechanical once T026/T035 exist. `LegacyXmlDocumentFormat`/the `Legacy*` DataContract
copies (T038) need `AssignLegacyNodeIds`/`AssignLegacyMemberIds` (both already exist, sub-phase B) plus
copies of the legacy `Project`/reference classes with matching `[DataContract(Name=…, Namespace=…)]` —
read the *actual* legacy classes in `src/NetPrints.Core/Core/{Project,AssemblyReference,FrameworkAssemblyReference,
SourceDirectoryReference,CompilationReference}.cs` (not yet read this session) before writing the copies,
since the contract/namespace attributes must match byte-for-byte for `DataContractSerializer` to accept
old files. `DocumentFormatRegistry` (T039) and the stores (T040) are small and match their contract code
blocks closely. Schema generation (T041) needs `JsonSchemaExporter` (new in STJ) — read up on
`JsonSchemaExporterOptions.TransformSchemaNode` before starting; the contract flags this as genuinely
uncertain ("Open item" row, §6) and asks for findings to go in research.md R17 K14, not just this file.

Known limitation carried into T035/T042: `TypedValueConverter.FromTypedValue`'s enum branch resolves
the type name with `Type.GetType(typeName, throwOnError: false)`, which only succeeds for BCL enums
(`System.DayOfWeek`, `System.IO.FileAccess`, …) and enums declared in the calling assembly — an enum
from a *referenced* project or NuGet package (a real `ParameterRef.Default` for such a method) will not
resolve and raises `DocumentFormatException`. Full type resolution needs a reflection provider aware of
the project's referenced assemblies, which `NetPrints.Serialization` does not have; nothing in the P1
DF-Txx test set exercises this path (AllNodes' only enum literal is `System.DayOfWeek`, going through
the pin special case above, not a method default value), so it is left as a follow-up rather than
solved here.

### T037 — `IDocumentFormat.cs`, `Json/JsonDocumentFormat.cs`

`IDocumentFormat` (document-format.md §2.2) was not created by an earlier task, so it is added here as
its first consumer: a plain interface at the `NetPrints.Serialization` namespace root, matching the
contract's member list exactly (`Id`, `ClassExtensions`, `CanWrite`, `ReadClassAsync`, `WriteClassAsync`).
`JsonDocumentFormat` implements it mechanically, as anticipated: read is `JsonNode.Parse` (the
synchronous stream overload the contract names, not `ParseAsync`) → require an object root → validate
and strip a `$schema` property if present → `DocumentMigrator.Upgrade` → `JsonSerializer.Deserialize<ClassDocument>`;
write is `JsonSerializer.SerializeToNode` into a fresh `JsonObject` with `$schema` first (each property
node is `Remove`d from the serialized object before being assigned into the new one — a `JsonNode` can
only have one parent, so it must be detached before being re-parented) → `CanonicalJsonWriter.Write`.

One design point not spelled out in the contract: syntax errors (caught around `JsonNode.Parse`) carry
`DocumentFormatException.Line`/`BytePosition` (`JsonException.LineNumber` is 0-based; `Line` is declared
1-based, so `+ 1`); errors found after parsing (caught around `JsonSerializer.Deserialize`) do not set
those — `JsonException.Message` already has the JSON path appended by `System.Text.Json`'s own
`ReThrowWithPath` machinery by the time it reaches this catch, so passing `ex.Message` through
verbatim satisfies "carry the JSON path in the message" without reconstructing it.

Test coverage (`tests/NetPrints.Core.Tests/Serialization/JsonDocumentFormatTests.cs`): DF-T09 (a syntax
error gets `Line`/`BytePosition`; a structurally invalid but syntactically valid document, built by
string-corrupting a canonical write, gets neither but does get the JSON path in the message), DF-T21
(comments, trailing commas, a `$kind` after other properties, no `$schema`, reordered top-level
properties and arbitrary whitespace load to the same model as the canonical form via
`JsonNode.DeepEquals`, and writing the tolerantly-read result reproduces the canonical bytes exactly; a
non-string `$schema` throws). The "load → edit → save" half of DF-T21 (an edit through `DocumentMapper`
between load and save) is exercised at the `DocumentMapper` level already (T035) and again end-to-end at
T042; this task's tests stay at the format layer (no model edit, just tolerant-read-then-write).

### T038 — `Legacy/LegacyProject.cs`, `Legacy/LegacyXmlDocumentFormat.cs`

Read the real legacy classes first, as the design notes flagged: `Project`, `CompilationReference`,
`AssemblyReference`, `FrameworkAssemblyReference`, `SourceDirectoryReference` in `src/NetPrints.Core/Core/`.
None declares an explicit `[DataContract(Name=…, Namespace=…)]`, so each relies on the CLR default (Name
= the type name, Namespace = `http://schemas.datacontract.org/2004/07/<CLR namespace>` =
`.../NetPrints.Core`), confirmed against the checked-in `AllNodes.netpp` fixture's raw XML (root element
`<Project xmlns="http://schemas.datacontract.org/2004/07/NetPrints.Core">`, `<CompilationReference
i:type="FrameworkAssemblyReference">`). The legacy copies (`Legacy/LegacyProject.cs`) declare those same
Name/Namespace explicitly on plain DTO classes (`List<T>` instead of `ObservableRangeCollection<T>`,
plain auto-properties instead of `ModelObject`/CTK) — DataContractSerializer only cares about the
contract shape, not the concrete collection or property-change-notification type. One non-obvious detail:
`FrameworkAssemblyReference`'s `[DataMember]` is on its *private field* `frameworkRelativePath`, not a
property, so the legacy copy's `FrameworkRelativePath` property needs `[DataMember(Name =
"frameworkRelativePath")]` to match the original element name (lowercase) instead of defaulting to the
property's own name.

`LegacyXmlDocumentFormat` (`Legacy/`) is mechanical per document-format.md §2.2/§3, using
`DocumentMapper.EnumerateGraphs` (made `internal` — it was `private`; shared rather than duplicated,
since both this and `DocumentMapper.BuildLayout` need "every graph of a class" and it is 15 lines):
`AssignLegacyMemberIds()` on the class, `AssignLegacyNodeIds()` then `GraphTypeInference.Relax` on every
graph, then `mapper.ToDocument`.

**Found while running this against the real `AllNodes.Everything.netpc` fixture (not exercised by any
sub-phase A/B/C test until now, since T035's tests only ever built `Variable`s through its constructor,
never through `DataContractSerializer`): `Variable.OnDeserialized`'s `if (TypeGraph is null)` guard never
runs its `OwningClass = Class` assignment for a legacy class, because a legacy `Variable` always
deserializes a real, non-null `TypeGraph` (a `[DataMember]` with actual content) — only a
never-before-serialized `Variable` would hit the null branch.** `TypeGraph.OwningClass` (`[IgnoreDataMember]`,
T017) therefore stayed at its default (`null`) for every legacy-imported variable, and
`GraphKeys.For(variable.TypeGraph)` (called from `DocumentMapper.BuildLayout`, itself called from
`ToDocument`) threw `InvalidOperationException` the moment the fixture had even one variable. Fixed at
the root, in `Variable.OnDeserialized` (`src/NetPrints.Core/Core/Variable.cs`): `TypeGraph ??= new
TypeGraph();` then `TypeGraph.OwningClass = Class;` unconditionally, instead of only inside the null
branch — `Class` (a real `[DataMember]`) is already populated by the time this hook runs, matching the
assumption the original fallback line already made. Verified: full `Core.Tests` suite green before and
after (215 → 221, the six new legacy tests), including both fixture-regeneration characterization tests;
whole-solution `dotnet build -c Release` still 0 warnings/0 errors.

Test coverage: `LegacyProjectTests` (both fixtures' `.netpp` deserialize; `AllNodes.netpp` exercises all
three reference kinds — framework, plain assembly, source directory — and both fixture-specific field
values) in `tests/NetPrints.Core.Tests/Serialization/LegacyProjectTests.cs`; `LegacyXmlDocumentFormatTests`
(DF-T18 legacy part: both fixtures' `.netpc` import through `ReadClassAsync` and then round-trip through
`DocumentMapper.FromDocument` with zero issues; importing the same fixture twice gives
`JsonNode.DeepEquals`-identical documents) in `.../LegacyXmlDocumentFormatTests.cs`.

### T039 — `DocumentFormatRegistry.cs`

Mechanical, per document-format.md §2.2/§2.6. One judgment call the contract's own "Find" row does not
spell out: `Find(DocumentId, DocumentKind)` takes a `kind`, but `IDocumentFormat` has no property saying
which `DocumentKind` it handles, and `ProjectDocument`/the `.netpp.json` file are already "(removed)"
(§1.3) — every registered format in P1 only ever claims `.netpc`/`.netpc.json`-style *class* extensions.
Read `Find` as: only `DocumentKind.Class` is resolved through this registry at all (returns `null`
immediately for `DocumentKind.Project`); project files are `IProjectSystem`'s job (project-system.md),
not this registry's, so there is nothing for a `Project`-kind lookup to ever match. `kind` stays a real
parameter (not removed) because the contract's `IDocumentFormat.ReadClassAsync`/`WriteClassAsync` and
`ProjectPersistence.LoadAsync` (§2.8) both frame this as "class" documents specifically, and a future
document kind could add its own extension list without changing this signature.

The "exactly one `json` format" ctor check and the "duplicate `Id`" check both run in P1, but never
together on the same failure: two formats sharing `Id == "json"` already throws on the duplicate-id
check (which runs first, in the same loop as the duplicate-extension check), so the "more than one
json-id format" half of the later `jsonFormats.Length != 1` check is presently unreachable — only its
"zero json formats" half is. Both halves are still worth keeping: they document the invariant
literally, and "duplicate id" stops being the only way to end up with two `json`s if `Id` is ever
compared case-insensitively or similar in a future change.

Test coverage (`tests/NetPrints.Core.Tests/Serialization/DocumentFormatRegistryTests.cs`, DF-T16): a
duplicate `Id` throws, a duplicate extension throws, a missing `json` format throws, `Default` returns
the registered `json` format, `Find` prefers `.netpc.json` over `.netpc` for a name ending in both, and
`Find` returns `null` for an unmatched extension or a `Project`-kind lookup.

### T040 — `Stores/IDocumentStore.cs`, `InMemoryDocumentStore.cs`, `FileSystemDocumentStore.cs`

Added `System.Reactive` and `Microsoft.Extensions.Logging.Abstractions` package references to
`NetPrints.Serialization.csproj` (both already centrally versioned in `Directory.Packages.props`, unused
here until now) and `Microsoft.Reactive.Testing` to `NetPrints.Core.Tests.csproj` (for `TestScheduler`,
DF-T13). `InMemoryDocumentStore` is mechanical: a `ConcurrentDictionary<DocumentId, byte[]>`, a per-id
`SemaphoreSlim` for `WriteAsync`, and a `Subject<DocumentChange>` `Set` pushes into directly (synchronous,
per the contract); `WriteAsync` never raises `Changes` (this is the "own write" side of DF-T13 for this
store — nothing suppresses it because nothing needs to: it just never emits).

`FileSystemDocumentStore.Changes` is a `FileSystemWatcher` (recursive, `LastWrite`/`FileName` only — no
`DirectoryName`, so bare directory create/delete/rename is not itself a "document change") whose raw
events are debounced per id: each new raw event for an id cancels that id's pending
`IScheduler.Schedule(TimeSpan, Action)` timer and starts a fresh one at `ThrottleWindow` (200 ms), so a
burst of events on the same id coalesces into one emission using the last event's kind — this is a plain
`ConcurrentDictionary<DocumentId, IDisposable>` of pending timers, not an Rx `GroupBy`/`Throttle` pipeline
(simpler to reason about and to test). Own-write suppression: `WriteAsync` records
`ownWriteAt[id] = scheduler.Now` right before the atomic `File.Move` (which is what actually fires the
raw event — a *rename*, from the temp path to the target path, not a `Changed`/`Created` event, since
that is how `File.Move` surfaces through `FileSystemWatcher`); the very next raw event for that id, if
it arrives within `ThrottleWindow`, is recognized as self-caused and dropped immediately (before it ever
reaches the debounce timer), consuming the marker so a genuinely later external change still surfaces
normally. Temp files (`*.tmp-<guid>`) are filtered out of every raw event and out of `ListAsync` by name.
`ToDocumentId`/`GetFullPath` round-trip through `Path.GetRelativePath`, normalizing `/`; a path outside
the root (`GetRelativePath` returning `..`-prefixed or rooted) throws `ArgumentException`.

Event 3005 (`ExternalChange`, editor-services.md §6) is logged here even though it is in the "3001-3006"
range T036's note flagged as T103's job (`Log.cs` added under `Stores/`) — unlike `DocumentMigrator.Upgrade`
(no `ILogger` reachable from its signature at all), `FileSystemDocumentStore`'s constructor *does* take an
`ILogger<FileSystemDocumentStore>`, and 3005 is precisely the `Changes` mechanism this task builds; taking
a logger parameter and never calling it would be strange. The other five (3001-3004, 3006, on
`DocumentMigrator`/`DocumentMapper`/`ProjectPersistence`/`ProjectConverter`) are still T103's: none of
those types have a logger to call from yet.

**Found by running the new `Changes` test five times in a row (it passed once, then crashed the test
host with `ObjectDisposedException` on two of the next four runs — not a test flake, a real bug):**
`Dispose()` unsubscribed and disposed the `FileSystemWatcher`, disposed every pending debounce timer,
then completed and disposed the `changes` `Subject` — but a raw file system event already queued on a
background thread (received before `EnableRaisingEvents = false` took effect, or a rename event for a
write that was already in flight) can still reach `TryHandleRawChange` afterward, scheduling a *new*
timer that later calls `changes.OnNext` on an already-disposed `Subject`. Fixed with a `lock (disposeLock)`
around both the "check `disposed`, then `OnNext`" step (inside the debounce callback) and the
disposed-flag-plus-`OnCompleted`/`Dispose` step, so the two can never interleave; applied the same fix to
`InMemoryDocumentStore.Set`/`Dispose` for the same reason (the contract's own "all members may be called
concurrently" applies to `Set` and `Dispose` too, even though `InMemoryDocumentStore` has no background
thread of its own to race with — a caller-supplied one can still call `Set` concurrently with `Dispose`).
Verified: the `Changes` test run 5 times in a row, then the whole `Core.Tests` suite run twice in a row,
both clean.

Test coverage: `Stores/DocumentStoreTestBase.cs` (abstract, DF-T14: write/read round trip, a missing
document, overwrite, directory auto-creation, `ListAsync` ordinal-sorted and prefix-filtered), inherited
by `InMemoryDocumentStoreTests` (+ DF-T13's memory half: `Set` raises `Created` then `Changed`
synchronously, `WriteAsync` raises nothing, `Dispose` completes the observable) and
`FileSystemDocumentStoreTests` (+ DF-T12: an exception or an `OperationCanceledException` from `write`
leaves the original file untouched and no temp file behind; + DF-T13's file half, the trickiest test in
this batch: a real `FileSystemWatcher` event arrives at an unpredictable *real* time, but the debounce
that follows it runs on a `TestScheduler` — so the test repeatedly interleaves a short real
`Task.Delay` with a `TestScheduler.AdvanceBy` in a loop, which needs no hook into the store's internals:
whenever the real event does arrive and gets scheduled, the next few loop iterations' cumulative virtual
advances eventually pass its due time and fire it).

### T040a — `RandomIdGenerator`/`SeededIdGenerator` keep their public API; `SnowflakeIdGenerator` is the new engine underneath

Owner decision 2026-09-25 (research.md R20, data-model.md §2): the id *value* becomes a monotonic 63-bit
Snowflake-style long (41-bit ms since `2026-01-01T00:00:00Z` | 16-bit session | 6-bit sequence), the text
form 13 Crockford32 digits instead of 6. Rather than replace `RandomIdGenerator`/`SeededIdGenerator` (used
at dozens of call sites across `Core`, `Serialization` and both test assemblies), both keep their exact
name, constructor signature and (for `RandomIdGenerator`) `.Instance` shape, now implemented as a thin
wrapper over a `SnowflakeIdGenerator`: `RandomIdGenerator` = `TimeProvider.System` + a random session
chosen once at first use (mirrors the old `Random.Shared` "thread-safe, one shared instance" contract);
`SeededIdGenerator(seed)` = a fixed clock pinned at `SnowflakeIdGenerator.Epoch` (so it never advances on
its own) + a session derived from the seed (`(seed ^ (seed >> 16)) & 0xFFFF`). A fixed, non-advancing
clock still produces a distinct id per call: the monotonic "sequence increments within the same
millisecond" path (meant for the overflow/backwards-clock case) is what runs on *every* call here, since
the clock never reports a later millisecond on its own — this is exactly what determinism needs (two
`SeededIdGenerator(42)` instances, each starting fresh at sequence 0, produce the identical sequence of
values call-for-call) and was not a special case to build.

`IdFormat` (new, public) is the single source of the shape: `Alphabet`, `ValueDigits` (13),
`Pattern` (`^[nm][0-9a-hjkmnp-tv-z]{13}$`), `Format`/`TryParse`. `TryParse` is case-insensitive and also
accepts Crockford's own `i`/`l` → 1, `o` → 0 transcription aliases in the value digits (trivial: one
extra branch each in the digit-decode `switch`) — the prefix character itself must still be exactly `n`
or `m`, case-insensitive, no alias. `StableIds.IsValidDocumentId` (non-empty, no `/` or whitespace) is
deliberately kept looser than `IdFormat.Pattern` and is not changed: it is what "accepted on read" checks
(document-format.md §1.4.1), and a legacy `n0`/`n1` id must keep passing it forever.

`StableIds.AllocateUnique(prefix, existingIds)` is `ClassGraph`'s private `AllocateUniqueMemberId` moved
up and generalized (same 100-attempt bounded retry against a caller-supplied set), so T040c's node-id
duplicate repair does not duplicate it. This is a **different** operation from ordinary allocation
(`NodeGraph.AllocateNodeId`, a member constructor's `IdGeneration.Current.NewId('m')`): ordinary
allocation trusts the generator and never retries (T040b); `AllocateUnique` exists specifically for
repairing a concrete, already-loaded document against ids the generator could not have known about.

No `!` added. Verified: `dotnet build src/NetPrints.Core -c Release` 0 warnings; new
`tests/NetPrints.Core.Tests/Core/SnowflakeIdGeneratorTests.cs` (bit layout via `IdFormat.Pattern`,
sequence increment and overflow, a backwards clock via a small `TimeProvider` test double — `FakeTimeProvider.SetUtcNow`
refuses to go backwards, so the "clock goes backwards" case needs its own settable-either-way double,
not the package's fake — format/parse round trip, alias parsing, malformed input); `IdGenerationTests`/
`NodeIdTests` regexes updated to the 13-digit pattern.

### T040b — `NodeGraph`'s id index can't be built eagerly, and can't be kept current by `CollectionChanged` alone

`FindNode` needed an id → `Node` `Dictionary` to become O(1) (data-model.md §2). Two problems, both from
the same root cause (document-format.md §3.1: `DataContractSerializer` never runs field initializers,
constructors, or property setters it wasn't told to call):

1. A field initializer (`private Dictionary<...> nodeIndex = new();`) would stay unset on a legacy-import
   `NodeGraph`, since DataContract-constructed instances skip it exactly like they skip `Node.Id`'s
   `[IgnoreDataMember]` narrow null window (T016). Fixed the same way data-model.md §2 suggests: lazily.
   `nodeIndex` starts `null` regardless of how the graph was constructed; the first `FindNode` call scans
   `Nodes` once (by then, whatever process built the graph — normal construction or `AssignLegacyNodeIds`
   — has already finished, so every node's `Id` is already final) and subscribes to `Nodes.CollectionChanged`
   for everything after that.
2. `CollectionChanged` alone is not enough: `AssignLegacyNodeIds` and `DocumentMapper`'s "constructor gives
   a placeholder id, then overwrite it from the document" both change `Node.Id` on a node **already**
   sitting in `Nodes` — no collection event fires for a property change. Rather than turn `Node.Id` into a
   full property with a callback (touching a widely-used auto-property's shape for a change only three
   call sites need), the three call sites that reassign an existing node's id call a new
   `internal NodeGraph.ReindexNode(Node, string? previousId)` themselves, right after the assignment. It
   is a deliberate no-op while `nodeIndex` is still `null` (nothing to keep in sync yet — the eventual
   first scan already reflects the final id), so call sites never need to guard "has anything looked this
   graph up yet."

`AllocateNodeId` lost its retry loop and `MaxAllocateAttempts`: it is now exactly
`IdGeneration.Current.NewId('n')` (research.md R20 — the generator's own monotonic uniqueness makes the
search pointless). `NodeIdTests.AllocateNodeIdRetriesAnAlreadyUsedId` (asserted the search) was replaced
by `AllocateNodeIdDoesNotRetryOrSearch` (asserts the opposite: a generator stub that returns an
already-used id is trusted as-is).

No `!` added. Verified: `dotnet test tests/NetPrints.Core.Tests` green throughout (existing `FindNode`/
`RemovingAndReAddingANodeKeepsItsId`/legacy-import tests unchanged and still passing against the new
index).

### T040c — Duplicate node/member ids repair instead of failing the load; the repair cannot always rewire a duplicate's own connections

Owner decision 2026-09-25 (research.md R20): `DocumentMapper.FromDocument` no longer throws
`DocumentFormatException` for a duplicate node id (within a graph) or member id (within a class) — it
reassigns the later occurrence (document order) a fresh id via `StableIds.AllocateUnique` and reports
`DocumentIssue.DuplicateIdReassigned` (new code `NPD007`) at `Warning`. `ValidateMemberIds` (renamed
`ValidateMemberIdShapes`) keeps only the shape checks (empty, `/`, whitespace, `== "class"`); the
duplicate check moved into the per-member creation loop (`ResolveDuplicateId`, shared by
`MapVariableFromDocument`/`MapMethodFromDocument`/`MapConstructorFromDocument` via a `seenMemberIds` set
threaded through all three) because the fix only needs uniqueness against ids *already assigned so far*,
not the whole document up front — check-then-fix-then-continue in one pass, rather than a separate
validation pass followed by a second pass that would need to re-derive which ids were the duplicates.
The node-id equivalent lives in `MapGraphFromDocument`'s node-creation loop, calling `graph.ReindexNode`
after the (possibly reassigned) id is set — see T040b.

**What "consistent" means here, precisely, and its one real limit.** For member ids: nothing in a
document refers to a member id by text except the member's own `id` field — `GraphKeys.For` always
derives a graph key from the *live* `Variable`/`MethodGraph`/`ConstructorGraph.Id` at the moment it is
called (already true before this task), so reassigning a duplicate's id and only then building its
graph(s) is automatically consistent everywhere; no separate rewrite step exists or is needed. For node
ids, layout is a map (`nodeId -> [x, y]`) — a source document literally cannot have two *different*
entries with the same key, so a reassigned duplicate simply has no matching layout entry and is
auto-placed like any other unpositioned node (already-existing behavior, no new code). Connections are
the one place a real limit remains: `connections` is an array, so a document *can* contain two edges
that both cite the same (duplicated) id text, one meant for each occurrence — and there is no reliable
way to tell them apart from the text alone once the source id has been duplicated. The repair resolves
`FindNode(thatId)` to whichever occurrence still holds the text after reassignment (deterministically
the first one seen, since only later duplicates are ever reassigned); an edge that was meant for the
reassigned occurrence attaches to the first one instead of failing to resolve. This is an accepted,
deliberate trade-off (documented in data-model.md §2/research.md R20, not silently swallowed): the
owner's ask was "the document still loads" (merge safety) for what is already a rare, out-of-band
condition (a real collision from two live Snowflake generators, or a hand-edited/copy-pasted file), not
"perfectly reconstruct an inherently ambiguous merge's intent" — `NPD007` surfaces the situation so a
person can look at the diff.

Fixture regeneration: `tests/NetPrints.Core.Tests/Fixtures/Golden/PinKeys.golden.txt` (`AllNodesFixtureFactory`
via `SeededIdGenerator(42)`, `NETPRINTS_UPDATE_SNAPSHOTS=1`) — diffed with every id and id-shaped graph
key replaced by a placeholder before comparing old vs. new; the placeholder-normalized multisets are
identical line-for-line (same pin references, same counts, under the same graphs), confirming only ids
changed. No other checked-in fixture contains a node or member id: `Node.Id`/`MethodGraph.Id`/
`ConstructorGraph.Id`/`Variable.Id` are excluded from (or never annotated as) `[DataMember]`s (T016), so
neither legacy `.netpp`/`.netpc` fixture nor the two `Fixtures/Golden/*.cs` generated-C# goldens ever
contained one.

No `!` added. Verified: `dotnet test tests/NetPrints.Core.Tests -c Release` green (260 tests, up from
244 before this task group: +14 `SnowflakeIdGeneratorTests`, +2 `IdGenerationTests`/`NodeIdTests`, +1 net
in `DocumentMapperTests` — one throw-test rewritten to an issue-reporting test, one new node-duplicate
test added).

### T041 — `Json/NetPrintsJsonSchema.cs`; the exporter's `required` bug (K14)

`NetPrintsJsonSchema.GenerateV1()` calls `JsonSchemaExporter.GetJsonSchemaAsNode` on
`NetPrintsJsonContext.Default.Options` (built-in kinds only, per document-format.md §6) over
`ClassDocument`, then does four things `TransformSchemaNode` cannot express as a pure per-node callback
plus a small amount of root-level `JsonObject` surgery in `GenerateV1` itself: inserts `$schema`/`$id`/
`title` ahead of the exporter's own `type`/`properties`/`required`, and adds a `$schema` property (a
plain string) to the root's `properties`, since `$schema` is a writer-added header (document-format.md
§2.2), never a member of `ClassDocument`, so the exporter never sees it. `TransformSchemaNode` itself
does three things: pins `schemaVersion` to `"const": 1`; adds `"minItems": 2, "maxItems": 2` to every
`int[]` schema (the layout leaf type, matched by `context.TypeInfo.Type`, not by property name — there is
only one `int[]`-typed member in the whole graph); and appends the extension-kind `anyOf` branch (the
exact literal document-format.md §6 gives) once, when `context.TypeInfo.Type == typeof(NodeDocument)`.

**Full write-up of the K14 investigation (exporter's polymorphism output, and the `required` bug it
uncovered) is in research.md §6 (R17), not repeated here** — the short version: polymorphism needed no
fix (the exporter already unrolls all 24 `[JsonDerivedType]`s into their own `anyOf` branches, ignoring
`GraphDocument.Nodes`'s custom `NodeListConverter` entirely and describing `NodeDocument`'s own declared
contract instead); `required` needed a real fix, since the exporter marks a member required from "no C#
default value on the record parameter," independent of nullability or of
`[JsonIgnore(Condition=Never)]`, which silently over-included dozens of fields the model actually omits
on write. `NetPrintsJsonSchema.FixRequired` recomputes it per object schema: exhaustively from
`[JsonIgnore(Never)]` for a type that uses that convention anywhere (every `ClassDocument`/member/
`NodeDocument`-common type), or by subtracting nullable members (checked with `NullabilityInfoContext`
against the real `PropertyInfo`, since a repeated shape like `TypeRef` is written once and `$ref`'d
everywhere else, with no `type` keyword on the `$ref` node to sniff nullability from) from the exporter's
own list otherwise (the §1.6 reference/value DTOs, none of which use `[Never]` at all).

One `!` fixed before commit, not added: a first draft read `schema["properties"]!` in `GenerateV1`
(`JsonObject`'s indexer returns `JsonNode?`); replaced with `schema["properties"] as JsonObject ?? throw
new InvalidOperationException(...)`, per AGENTS.md (throw a clear exception at the boundary instead of
asserting with `!`). `SchemaTests.cs` avoids `!` the same way `CanonicalJsonTests.Parse` does
(`Assert.NotNull` then use the narrowed value), factored into small `Child`/`Array`/`Value<T>` navigation
helpers so the DF-T24 structural assertions (root `$id`, one `anyOf` branch per built-in kind plus the
extension branch, `schemaVersion` `const`, layout array bounds, `MethodDocument`'s `required`) stay
readable.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings; `dotnet test tests/NetPrints.Core.Tests -c
Release` green, 266 tests (+6 `SchemaTests`); `dotnet format NetPrints.slnx --verify-no-changes` clean;
`schemas/netpc.v1.schema.json` committed (`NETPRINTS_UPDATE_SNAPSHOTS=1`), no BOM, single trailing `\n`,
2-space indent; DF-T24's `GeneratedSchemaMatchesCommittedFile` fails the build the moment the two drift.

### T042 — `GoldenCSharpTests` switched to the new importer; `RoundTripTests.cs`, `MergeTests.cs`

`GoldenCSharpTests` (DF-T01, sub-phase A's own gate) no longer loads a fixture with `Project.LoadFromPath`
and the pre-P1 `ClassTranslator` pipeline directly: it now reads the fixture's `.netpc` through
`LegacyXmlDocumentFormat.ReadClassAsync` and `DocumentMapper.FromDocument`, then translates the resulting
`ClassGraph` — the same golden files (`Fixtures/Golden/*.cs`), now proving the *new* importer reproduces
them, not the old one. This is what the task explicitly asked for ("switch `GoldenCSharpTests` to the new
importer (DF-T01)"), and it does not weaken sub-phase A's own regression gate: `Project.LoadFromPath` and
the original `DataContractSerializer` path stay exercised elsewhere (`LegacyProjectTests`,
`AllNodesFixtureRegenerationTests`, `HelloWorldSampleTests`, all unchanged) — Core's original
`Project`/`ClassGraph` DataContract types are untouched until T063, per T038's note. Both fixtures have
exactly one class each (`AllNodesFixtureFactory`/`SampleProjectFactory` each add a single `ClassGraph`), so
the theory needs no loop over `project.Classes`, just the one `.netpc` named per fixture.

`RoundTripTests.cs`: DF-T02 (`LegacyThroughJsonProducesGoldenCSharp`, reusing
`GoldenCSharpTests.LegacyFixtures()` as `[MemberData]`) converts each legacy fixture to canonical JSON
first (`LegacyXmlDocumentFormat` → `JsonDocumentFormat.WriteClassAsync`), then loads *that* and translates
— proving the JSON path alone reproduces the same golden C#, not just the legacy path. DF-T03
(`JsonRoundTripIsByteIdentical`) runs load → `MarkDirty()` → `ToDocument` → write on three sources — both
legacy fixtures (converted to JSON in memory the same way) and `samples/HelloWorld/HelloWorld.Program.netpc`
(also converted in memory: the sample isn't `.netpc.json` yet — that conversion is T057) — and asserts the
rewritten bytes equal the ones just read, and that they re-parse. DF-T05
(`MovingOneNodeChangesExactlyOneLayoutLine`) moves HelloWorld's one `CallMethodNode`, saves twice, and
diffs the two byte arrays line by line: exactly one line differs, it is inside `layout`, and both versions
of that line match the `"<id>": [x, y]` inline-position shape (document-format.md §2.3.1 rule 3).

`MergeTests.cs` (DF-T23) builds the base/branch-A/branch-B `ClassGraph`s directly through the model API
(not by loading a fixture), overwriting the four base nodes' and the class's own `ClassReturnNode`'s ids
via `Node.Id`'s `internal set` (`NetPrints.Core.Tests` has `InternalsVisibleTo`) plus `NodeGraph.ReindexNode`,
instead of a queue-stub `IIdGenerator`: the ids the ambient generator would allocate during construction
are placeholders immediately overwritten anyway (`DocumentMapper.FromDocument`'s own pattern, T035), so
there's no need to reverse-engineer the exact number and order of `IdGeneration.Current.NewId` calls a
real build makes. **Two findings from actually running `git merge-file`, not assumed from the contract's
prose:**

1. **The class graph's own node needs a fixed id too.** All three builds (`base`, `branchA`, `branchB`)
   call `BuildBase()` independently; each construction allocates a fresh, real (`Snowflake`/random) id for
   `ClassGraph.ReturnNode` since only the *method's* four nodes were being overridden at first. That
   produced a spurious, unrelated conflict on the class graph's one node (and its `layout["class"]` entry)
   in every run, before the intended one — fixed by overriding `cls.ReturnNode`'s id too, identically
   across all three builds, the same way as the method's nodes.
2. **`git merge-file` does not treat "two branches insert N new lines at the same point" as one
   opaque conflict.** It further diffs the two inserted regions against *each other* (zealous/refine-style
   conflict minimization, not textbook diff3), so if both branches' additions are line-for-line similar —
   e.g. both call `Console.WriteLine(string)` with the same literal value, differing only in `id` — git
   reports several small pinpoint conflicts (one per differing line) instead of the single hunk
   document-format.md §7/DF-T23 describes, and can even use an unrelated shared line (like a matching
   method `name` inside two otherwise-different `method` objects) as a false anchor that splits one
   conflict into two. `TwoBranchesAddingUnrelatedNodesMergeWithOneConflictAtTheNodesTail` makes branch A's
   addition call `Console.WriteLine(string)` on a string literal and branch B's call `Console.Beep(int)`
   on an int literal — sharing no text at all — which reproduces the exactly-one-conflict-at-the-tail
   shape reliably. `TwoBranchesInsertingIntoTheSameGapStillConflict` (document-format.md §7's "documents
   the limit too") keeps both branches' additions identical in shape and colliding ids, and only asserts
   the loose, always-true-for-a-real-conflict shape (exit code > 0, at least one `<<<<<<<`), since exactly
   how many hunks a same-gap collision produces is exactly the kind of git-internal detail finding 1 shows
   isn't worth pinning down precisely.

Resolving "by keeping both sides" is done by a JSON-structural merge of the two *pre-conflict* documents
(`MergeBothSides`: union nodes by id, connections by `from`/`to`, layout entries by node id), not by
patching git's raw conflict-marker text: the observed conflict (finding 2) splits mid-object, so the
"missing `,`" the contract mentions is really a missing **duplicate of the shared closing lines** each
side's fragment is missing (`],\n"modifiers":...}\n}`) — reconstructing that correctly from plain text
would need brace-depth tracking, not line splicing, for no real benefit over reusing the two documents
already sitting in memory. The test still asserts everything the raw git output must show first (exit
code, hunk count and location, both branches' ids present, connections/layout outside the conflict)
before falling back to the structural merge to prove the *result* loads with the right node and
connection counts.

No `!` added: `SchemaTests.cs`'s `Child`/`Array`/`Value<T>` pattern is repeated here too
(`RequireObject`/`RequireArray`/`RequireString` in `MergeTests.cs`), and `RoundTripTests.cs` never
navigates a `JsonNode` tree by hand at all (it only re-parses to check the written bytes are valid JSON,
via `Assert.NotNull(JsonNode.Parse(...))`).

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings; `dotnet test tests/NetPrints.Core.Tests -c
Release` green, 274 tests (266 → 274: +6 `RoundTripTests`, +2 `MergeTests`; `GoldenCSharpTests`' own count
unchanged at 2, now against the new importer); `dotnet format NetPrints.slnx --verify-no-changes` clean.
`MergeTests` needs `git` on `PATH` (present here and in CI) and skips with a reason otherwise
(`Assert.Skip`, matching `Desktop.E2ETests`' and `GridRenderTests`' existing pattern).

### T043 — Checkpoint C reached

All of sub-phase C (T024–T042) is done. Verified: `dotnet build NetPrints.slnx -c Release` 0
warnings/0 errors solution-wide; `dotnet format NetPrints.slnx --verify-no-changes` clean; full suite
green under a session-owned `Xvfb :171` (`DISPLAY=:171 dotnet test --solution NetPrints.slnx -c Release
-- --ignore-exit-code 8`, no other agent's display touched), 505 total, 496 succeeded, 9 skipped
(Desktop E2E and headless-driver capability skips, both expected and unchanged), 0 failed; Xvfb killed
immediately after the run. The four sub-phase A characterization gates
(`GoldenCSharpTests`, `NotificationMapTests`, `AllNodesFixtureRegenerationTests`,
`HelloWorldSampleTests`) and sub-phase B's own gate (no Fody, 0 warnings) all still pass —
`GoldenCSharpTests` itself changed under T042 (it now goes through the new importer instead of
`Project.LoadFromPath`), but the golden files it checks against, and the "translated C# never changes"
guarantee they encode, did not.

## Phase 4: User Story 1b — build pipeline (P1) (sub-phase D)

### T044 — `GraphCodeGenerator`, `GenerateRequestFile`, `Program.cs` (`generate`); two contract types don't exist yet

project-system.md §3's `GraphCodeGenerator` constructor, `RenderFile` signature and `Program`'s
`generate`/`convert` split describe the state after **all** of P1, not after T044. Two of its cited
types are built by tasks that come later than this one, in the phase order tasks.md itself lays out
(D → E → F → … → I):

- **`ExtensionRegistry`** (the constructor's first parameter) is added in T068 (sub-phase F); the whole
  `src/NetPrints.Extensibility` project is still empty of code. Building even a minimal stand-in now
  would mean re-doing it under the real contract (`IClassEmitter`, `ITypeCatalog`, `IProjectProfile`,
  `TranslationEnvironment`, `NodeDocumentConverterRegistry`, none of which exist either) three sub-phases
  early, for no test this task needs (PS-T14, the only test that exercises extensions in the generator,
  is explicitly T074's). **Deviation**: `GraphCodeGenerator`'s constructor takes only
  `(DocumentFormatRegistry formats, IDocumentMapper mapper)` today. `GenerateRequest.Extensions` (the
  request's `extension=` lines) is still parsed and carried on the record — the request file format
  itself does not need to change later — but nothing reads it yet. T065's task line already says
  "update call sites (generator, editor)" for exactly this reason (`ClassTranslator(TranslationEnvironment)`
  replacing today's parameterless `ClassTranslator()`); **T068 should add the `ExtensionRegistry`
  parameter to `GraphCodeGenerator`'s constructor at the same time**, wiring it through to whatever T065
  ends up needing for extension node translation.
- **`TranslatedClass`** (`RenderFile`'s parameter type, with a `SourceMap`) is added in T089
  (`ClassTranslator.Translate`, sub-phase I) — today `ClassTranslator.TranslateClass(ClassGraph)` returns
  a plain `string`. **Deviation**: `RenderFile(string translatedCode, string graphFileName)` takes the
  translated code as a `string`. T089 changes this to `RenderFile(TranslatedClass translated, string
  graphFileName)` (reading `translated.Code`) as part of adding the source map, alongside whatever else
  in `GraphCodeGenerator` needs a class's `SourceMap` at that point (compilation-and-diagnostics.md §1's
  `classesByGeneratedPath`).

Both deviations are pure narrowings of the eventual contract (same behavior for the part that already
exists), not new design, so neither should need rework beyond adding the missing parameter/type once its
own task lands — flagged here instead of guessed at now so T065/T068/T089 don't have to rediscover it.

`DocumentId` doesn't fit either, for a different reason: it models a path relative to an `IDocumentStore`
root (document-format.md §2.1: no `..` segments, never rooted), but a `GraphJob`'s `Input`/`Output` are
full paths from MSBuild (`%(FullPath)`), and PS-T03 requires a graph *outside* the project folder to
work — making it relative to the project directory would produce a rejected `..` segment for exactly
that case. `GenerateOneAsync` builds the `DocumentId` passed to `ReadClassAsync`/`FromDocument` from the
bare file name only (`Path.GetFileName(job.Input)`, always relative and never containing `..`); it is
used only for format resolution and exception/issue attribution (both documented as such), never as a
lookup key across jobs, so same-named files in different directories within one request don't collide.
All diagnostics and canonical error lines use the job's real, full `Input`/`Output` paths instead of the
`DocumentId`.

A generator-only document-read failure needed a `DocumentIssue`-style code that isn't one of the seven
already named in document-format.md's table: `NPD001`–`NPD007` are all issues attached to an otherwise
*successful* load (`DocumentMapper.FromDocument`'s `issues` collection never contains one — every one of
today's fatal cases throws `DocumentFormatException`/`DocumentVersionException` instead). Minted
`DocumentIssue.DocumentUnreadable = "NPD008"` (`Error` severity, documented in both `DocumentIssue.cs`
and document-format.md's table) for exactly this case: `GraphCodeGenerator` catches
`DocumentFormatException` from `ReadClassAsync`/`FromDocument`, converts it to a `CodeDiagnostic` with
this code (line/column from the exception's `Line`/`BytePosition` when present, matching
document-format.md §2.8's "a malformed graph → issue (Error, with line/position)" for `ProjectPersistence`),
and keeps going — the previous `.g.cs`, if any, is left on disk untouched, per project-system.md §3's
"a graph with errors keeps its previous `.g.cs`". T056 (`ProjectPersistence.LoadAsync`, DF-T11) should
reuse the same constant rather than minting another one for the same situation.

`Program.Main`'s canonical-line formatter (`FormatCanonical`) implements the full rule from
project-system.md §3 (span → `path(line,col)`; no span but a `GraphKey`/`NodeId` → `path: … (graph
<key>, node <id>)`; neither → bare `path: …`) even though only the bare-path and span branches are
reachable today (no diagnostic carries a `GraphKey`/`NodeId` until `TranslationException` exists,
T065) — so T065 only needs to make `ExecutionGraphTranslator`/emitters populate those fields, not touch
`Program.cs` again.

`Program.cs` implements only the `generate` command; `convert` is added in T054 together with
`ProjectConverter` (tasks.md already assigns them to the same task). An unrecognized first argument (or
missing request path) exits `2` with a usage line on stderr, matching the "bad arguments" exit code.

Write path: a small self-contained atomic-write helper (temp file next to the target, `File.Move(...,
overwrite: true)`, matching `FileSystemDocumentStore.WriteAsync`'s idiom) rather than reusing
`IDocumentStore`: the generator writes arbitrary absolute paths outside any store root, needs no
watcher/lock/debounce, and runs once per `dotnet exec` process.

Tests (PS-T06 only; PS-T01–T04 are T047's job against the real MSBuild targets, not built yet):
`GraphCodeGeneratorTests.cs`. One test converts the HelloWorld legacy fixture to canonical JSON (same
`ConvertLegacyToJsonAsync` pattern as `RoundTripTests`) into a temp directory, runs `GenerateAsync` twice
and checks the header, absence of `\r`, a single trailing `\n`, and byte-identical output and `Written
== false` on the second run; then separately loads the same graph and translates it directly
(`ClassTranslator().TranslateClass`) to check the file equals `RenderFile` of that translation, literally
covering "content equals `RenderFile` of the editor translation". A second, pure-function test pins
`RenderFile`'s header/newline normalization without any I/O. Verified: `dotnet build NetPrints.slnx -c
Release` 0 warnings/0 errors; `dotnet test tests/NetPrints.Core.Tests -c Release -- --ignore-exit-code 8`
green, 276 tests (274 → 276, both new); `dotnet format NetPrints.slnx --verify-no-changes` clean. Added
`ProjectReference` to `NetPrints.Generator` from `NetPrints.Core.Tests.csproj` (its first: no test project
referenced the generator before this task). No `!`/`null!`/`default!` added.

### T045 — `NetPrints.Sdk.props`/`.targets` (copied verbatim); packing the generator's publish output dynamically

`build/NetPrints.Sdk.props` and `build/NetPrints.Sdk.targets` are copied from project-system.md §2
verbatim (the targets file is explicitly marked normative there); the props file's code sample doesn't
show `_NetPrintsExeExtension`, but the prose right after the targets block does ("set in the props"), so
it's added there as a third property, condition matching the prose exactly
(`$([MSBuild]::IsOSPlatform('Windows'))`).

`NetPrints.Sdk.csproj`'s pack layout needed `tools/net10.0/**` = a framework-dependent `dotnet publish`
of `NetPrints.Generator`, which does not exist at project-evaluation time (nothing to glob yet) and must
not run on every `dotnet build` of the solution (T045's own checkpoint requires the solution build to
stay fast and warning-free, and this project has no source of its own to justify a publish on every
build). Used NuGet's documented extension point for exactly this — `TargetsForTfmSpecificContentInPackage`
naming a target that emits `TfmSpecificPackageFile` items with `PackagePath` metadata — so the publish
(and the glob over its output) only runs when `dotnet pack` actually asks for the package's per-TFM
content, never on a plain build. `_NetPrintsPublishGenerator` invokes `<MSBuild Targets="Publish">`
against `NetPrints.Generator.csproj` (no `RuntimeIdentifier`, `SelfContained=false`, `UseAppHost=false`:
framework-dependent, matching `dotnet exec` in the targets) into `obj/generator-publish/` (removed first,
so a stale file from a previous TFM/config never survives into the package); `_NetPrintsAddGeneratorToPackage`
then globs that directory with `%(RecursiveDir)` in `PackagePath` so the generator's satellite resource
assemblies (`cs/`, `de/`, `ja/`, … — pulled in transitively by `Microsoft.CodeAnalysis.CSharp.Workspaces`)
land under their own `tools/net10.0/<culture>/` subfolders instead of colliding at the top level. A
`ProjectReference` to `NetPrints.Generator.csproj` with `ReferenceOutputAssembly="false"` establishes the
build-order edge (and gives IDEs a real link) without adding a compile-time dependency this project (no
source of its own) has no use for.

**Deviation caught by manually running the pack, not by an automated test** (PS-T05, the pack test, is
T048's): the first attempt wrote both `_NetPrintsGeneratorPublishDir` and `PackagePath`'s literal prefix
with backslashes (`obj\generator-publish\`, `tools\net10.0\...`) to match a Windows-flavored MSBuild
style; on Linux this produced a doubled separator in every packed path (`tools/net10.0//NetPrints.Generator.dll`,
`tools/net10.0//cs/….dll`) because the glob's base directory already ended in a literal backslash MSBuild
does not treat as a path separator on this OS, so `%(RecursiveDir)` came back prefixed with an extra
separator on top of it. Fixed by using forward slashes throughout (matching this repo's own props/targets
style, e.g. `../tools/net10.0/...` in `NetPrints.Sdk.props`), verified with a scratch
`dotnet pack src/NetPrints.Sdk -o /tmp/... /p:Version=0.0.1-packtest` and `unzip -l` on the result:
`build/NetPrints.Sdk.props`, `build/NetPrints.Sdk.targets`, `tools/net10.0/NetPrints.Generator.dll` and
its dependencies, `tools/net10.0/cs/…resources.dll` etc., no `lib/` folder (`IncludeBuildOutput=false`),
no `<dependencies>` in the nuspec (`SuppressDependenciesWhenPacking=true`), `<developmentDependency>true</developmentDependency>`
present. Scratch pack output and `src/NetPrints.Sdk/obj/generator-publish/` are not tracked (`obj/` is
already in `.gitignore`).

**Correction (found in T047):** `dotnet pack` copies `NetPrints.Sdk.props` into the package without
ever importing/evaluating it, so its one piece of real MSBuild logic (`_NetPrintsExeExtension`'s
condition) went untested here despite the "verified" pack dry run above. T047's first real project
import of this file failed evaluation outright (`MSB4092`) — see its notes below.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors (unaffected: the new pack target
does not run on `Build`); `dotnet test tests/NetPrints.Core.Tests -c Release -- --ignore-exit-code 8`
green, 276 (unchanged, T045 adds no new test — PS-T01–T04 are T047's, against these same files through
the in-repo dev mode T046 sets up); `dotnet format NetPrints.slnx --verify-no-changes` clean. No
`!`/`null!`/`default!` added.

### T046 — `samples/Directory.*`; `LocalSdkLayout`; the "reference with `ReferenceOutputAssembly=false`" step is already covered

`samples/Directory.Build.props`/`.targets`/`Directory.Packages.props` copied from project-system.md
§2.1 verbatim. `samples/Directory.Packages.props`'s existence is itself what isolates samples from the
repository's own central package management: NuGet's CPM discovery walks up from a project directory
and stops at the *first* `Directory.Packages.props` it finds, so `samples/HelloWorld/*.csproj` (added in
T057) picks up this one, not the repository root's, without needing any `Condition`.

`LocalSdkLayout.Write(string directory)` (`tests/NetPrints.Core.Tests/Projects/LocalSdkLayout.cs`) writes
the same three files with absolute paths: `NetPrintsGeneratorPath` and the two `<Import Project="…">`
targets resolve `SampleProjectFactory.FindRepositoryRoot()` once and bake in full paths, since a temp
directory used by a test (unlike a real sample under `samples/`) has no fixed relationship to the
repository the relative `../src/...` forms depend on. `NetPrintsGeneratorPath`'s configuration segment
is *detected*, not hard-coded to `"Release"`: it reads the running test binaries' own
`bin/<configuration>/net10.0/` output path (`AppContext.BaseDirectory`) so the generator path this
writes always matches whichever configuration actually built it (this repo's checkpoints run
`-c Release`, but a contributor running plain `dotnet test` locally gets `Debug` and it still resolves
correctly).

The task line's last item, "test projects reference `src/NetPrints.Generator` with
`ReferenceOutputAssembly=false` so it is built first," needs no new reference:
`NetPrints.Core.Tests.csproj` already references `NetPrints.Generator.csproj` (T044, default
`ReferenceOutputAssembly=true`) because `GraphCodeGeneratorTests.cs` calls `GraphCodeGenerator`'s C# API
directly — a stronger reason than build ordering, and one `ReferenceOutputAssembly=false` would break
(the compile-time reference `GraphCodeGeneratorTests.cs` needs would be gone). A `ProjectReference`
always builds its target first regardless of `ReferenceOutputAssembly` (that flag only controls whether
the *output assembly* becomes a compile-time reference, not build order), and MSBuild does not allow a
second `<ProjectReference>` item at the same project path with different metadata, so the "built first"
property this line asks for is already satisfied by the one reference that exists; adding another would
either be rejected or redundant. Noted here so T047/T048 (which rely on `NetPrints.Generator.dll`
existing under `bin/<Configuration>/net10.0/` before their MSBuild-driven tests run) don't go looking for
a reference that was never meant to be added twice.

No dedicated test for this task (project-system.md's test table has no id for it); `LocalSdkLayout` is
exercised for the first time, against a real `dotnet build`, by T047's `SdkTargetsTests.cs`. Verified:
`dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors; `dotnet test tests/NetPrints.Core.Tests -c
Release -- --ignore-exit-code 8` green, 276 (unchanged); `dotnet format NetPrints.slnx --verify-no-changes`
clean. No `!`/`null!`/`default!` added.

### T047 — `SdkTargetsTests.cs` (PS-T01…T04); a real bug in `NetPrints.Sdk.props`'s condition syntax; manual spikes before every assertion

**Bug found and fixed**: the very first real `dotnet build` through `LocalSdkLayout` (i.e. the first time
anything actually *imports* `NetPrints.Sdk.props`, since T045's own verification only packed the file,
never evaluated it) failed immediately with `MSB4092: An unexpected token "Windows" was found …` on
`_NetPrintsExeExtension`'s condition. T045's `'$([MSBuild]::IsOSPlatform('Windows'))' == 'true'` —
nesting a single-quoted string literal inside an already single-quoted condition operand — is *not*
valid MSBuild condition syntax on this MSBuild version, despite looking like a pattern real-world SDK
targets use; MSBuild's condition grammar does not support that nesting. Fixed with the `%27`
URL-style escape for the inner quotes instead: `'$([MSBuild]::IsOSPlatform(%27Windows%27))' == 'true'`
— verified directly (not just through the test suite) with a hand-built temp project before writing any
of this task's tests, both for the fixed condition alone and then for the whole build pipeline end to
end.

**Approach**: rather than writing assertions against a guess of MSBuild's exact log wording, every one of
PS-T01…T04's behaviors was reproduced first by hand — a scratch temp directory, `LocalSdkLayout`-equivalent
files written directly, two minimal graph documents (an empty class each: `ClassGraph`'s `SuperType`
always falls back to `System.Object` when unset, so even a class with no members translates to valid C#,
`public class X : System.Object { }` — no method/nodes needed for any of these tests, only whether the
*pipeline* runs, not what it emits, which T044's `GraphCodeGeneratorTests.cs` already covers) — and
`dotnet build`/`dotnet msbuild -getItem:Compile` run directly in the shell to see the actual log text and
JSON shape before encoding assertions against them:
- PS-T01: first build creates both `.g.cs` files; second build's log contains exactly
  `Skipping target "NetPrintsGenerate" because all output files are up-to-date…`; setting one graph's
  `LastWriteTimeUtc` 5 seconds into the future (instead of sleeping — AGENTS.md's UI-test "no sleeps"
  rule generalizes to any timestamp-dependent assertion) and rebuilding logs
  `Building target "NetPrintsGenerate" partially, because some output files are out of date…` and updates
  only that graph's `.g.cs` (`File.GetLastWriteTimeUtc` before/after, asserting one changed and the other
  is `Equal`, not just "not the same instant" — this is MSBuild's own target Inputs/Outputs batching,
  confirmed by hand first, not something the `.targets` file's `Exec`/`WriteLinesToFile` do themselves).
- PS-T02: `dotnet msbuild <proj> -getItem:Compile -nologo` prints `{"Items":{"Compile":[{…every default
  metadata key, "DependentUpon": "<graph file name>", …}]}}` directly (no extra flag needed to see
  `DependentUpon`); asserted exactly one `Compile` item whose `Identity` ends with the generated file
  name, and that its `DependentUpon` is the graph's own file name; separately asserted the build log
  contains neither `CS2002` nor `CS0101`.
- PS-T03: `EnableDefaultNetPrintsGraphItems=false` plus one explicit `<NetPrintsGraph Include="../Shared/Outside.netpc.json" />`
  builds cleanly and writes `Outside.netpc.g.cs` next to the graph file in `Shared/` (`%(RootDir)%(Directory)%(Filename).g.cs`
  is computed from the *item's own* path metadata, so an outside-the-project graph's generated file lands
  outside the project folder too — confirmed by hand, then asserted with `File.Exists`).
- PS-T04: corrupting a graph's JSON and rebuilding fails (`Assert.NotEqual(0, exitCode)`), the log
  contains `<full graph path>(` (the line/column form) and matches `error NPD\d{3}`, and the previous
  `.g.cs`'s content (read before corrupting the graph) is byte-for-byte unchanged afterward — the
  generator's own "keep the previous output on error" rule (T044), exercised here through a real failed
  MSBuild build instead of a unit test.

Every build/msbuild invocation passes `-tl:off` (matching `IProjectSystem.BuildAsync`'s own contract,
project-system.md §4) so assertions target the classic, line-oriented console logger's exact wording
regardless of whether the process happens to run attached to a terminal (the new Terminal Logger's output
does not contain these strings at all). Process output is read by awaiting both `StandardOutput` and
`StandardError` readers concurrently with `WaitForExitAsync` (`Task.WhenAll`), matching `MergeTests`'
existing external-process pattern in this repo but avoiding its two-step (`ReadToEndAsync` then
`WaitForExitAsync`) form, which only reads one stream and would deadlock here once MSBuild's `-v:n` output
exceeds the pipe buffer.

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors; `dotnet test tests/NetPrints.Core.Tests
-c Release -- --ignore-exit-code 8` green, 280 (276 → 280, all four new); `dotnet format NetPrints.slnx
--verify-no-changes` clean. No `!`/`null!`/`default!` added.

### T048 — `SdkPackageTests.cs` (PS-T05); MinVer is not wired up yet, so `-p:Version=` (not `MinVerVersionOverride`)

release-and-docs.md §1 documents local packs as `-p:MinVerVersionOverride=<version>` ("not `-p:Version`:
MinVer would still set `PackageVersion`") — but that only applies once `MinVer` is actually a
`GlobalPackageReference`, which is added in sub-phase L (T109+), not yet. Right now `NetPrints.Sdk.csproj`
has no versioning package at all, so a plain `-p:Version={TestVersion}` on the `dotnet pack` invocation is
both correct and the only option today; a comment in the test says so, so nobody "fixes" it to
`MinVerVersionOverride` before T109 lands (at which point it would silently stop working, since MinVer
would override `-p:Version`).

Package mode is deliberately the mirror image of T047's in-repo dev mode: no `LocalSdkLayout`, no
`NetPrintsUseLocalSdk`; the temp project's only connection to the SDK is an ordinary
`<PackageReference Include="NetPrints.Sdk" Version="…" PrivateAssets="all" />`, exactly the shape a real
consumer would write (project-system.md §1). Isolation has two independent layers, both required: the temp
`nuget.config`'s `<clear/>` removes every inherited package source (this machine's, and any user config)
so restore cannot resolve `NetPrints.Sdk` from anywhere but the just-packed local feed; `NUGET_PACKAGES` is
overridden to a fresh temp directory (via `ProcessStartInfo.Environment`, which starts as a copy of the
current process's environment and so overrides cleanly regardless of what the outer test process's own
environment happens to have) so a stale extracted copy of the exact same test version from a previous
run — or from a developer's own manual `dotnet pack` spike while working on T045 — can never be what the
build actually uses. Framework/reference-pack resolution (`Microsoft.NETCore.App.Ref`) is unaffected by
either: it comes from the SDK's own installed packs, never from a configured NuGet source (confirmed in
T047's spikes, where a project with zero `PackageReference`s still restored offline).

`Assert.True(exitCode == 0, output)` (rather than `Assert.Equal`) on both the pack and the build step: a
failure here is a real, exercise-critical MSBuild/NuGet failure a future reader needs the actual log for,
not just "0 != 1".

Verified: `dotnet build NetPrints.slnx -c Release` 0 warnings/0 errors; `dotnet test tests/NetPrints.Core.Tests
-c Release -- --ignore-exit-code 8` green, 281 (280 → 281); `dotnet format NetPrints.slnx --verify-no-changes`
clean. No `!`/`null!`/`default!` added.
