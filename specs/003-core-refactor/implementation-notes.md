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

### Design notes for the next agent: T026, T035-T043 remaining

**T026** (`Json/NetPrintsJsonContext.cs`, `NetPrintsJsonOptions.cs`, `NodeListConverter.cs`) was
deliberately deferred past T027-T034 because `NetPrintsJsonOptions`'s contract constructor takes a
`NodeDocumentConverterRegistry`, which did not exist until T030. It now does (committed). Before
starting T026:
- `NetPrintsJsonContext`: exactly the code block in document-format.md §2.3 (`[JsonSourceGenerationOptions(...,
  UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]`,
  `[JsonSerializable(typeof(Documents.ClassDocument))]`, `internal sealed partial class : JsonSerializerContext`).
- `NodeListConverter`: a `JsonConverter<IReadOnlyList<NodeDocument>>`. On write: for each element, if it
  is `UnknownNodeDocument`, write `$kind`/`id` first then `Raw`'s other properties via
  `JsonElement.WriteTo` in source order (no direct field on `UnknownNodeDocument` needs re-deriving this
  — its own `Raw` already holds everything); otherwise `JsonSerializer.Serialize(writer, node, typeof(NodeDocument), options)`
  (declared type `NodeDocument`, not the runtime type, so STJ's polymorphism/discriminator machinery
  fires). On read: buffer each array element as a `JsonElement` (`JsonElement.ParseValue(ref reader)`),
  find `$kind`/`id` by scanning ALL properties (not assuming order — `AllowOutOfOrderMetadataProperties`
  only affects normal polymorphic dispatch, not this hand-written converter); missing either →
  `DocumentFormatException`. Look up whether `$kind` matches a derived type registered on `NodeDocument`
  via `options.GetTypeInfo(typeof(NodeDocument)).PolymorphismOptions!.DerivedTypes` (covers built-ins,
  compile-time `[JsonDerivedType]`, AND any extension kinds added at runtime by
  `NetPrintsJsonOptions`'s resolver modifier) — if found, `element.Deserialize(derivedType, options)`;
  if not, `new UnknownNodeDocument(id, kind, element)` (+ track that an `NPD001` issue is warranted —
  the converter itself has no issue-collection channel, so it likely needs to either throw a lightweight
  marker the caller catches, or (simpler) `DocumentMapper`/`JsonDocumentFormat` re-scans the deserialized
  `Nodes` list afterward for `UnknownNodeDocument` instances and raises `NPD001` for each — prefer that;
  it keeps `NodeListConverter` a pure format-level concern with no issue-reporting responsibility).
- `NetPrintsJsonOptions(NodeDocumentConverterRegistry nodes)`: resolver =
  `JsonTypeInfoResolver.Combine(NetPrintsJsonContext.Default, ...nodes.ExtensionResolvers)` then
  `.WithAddedModifier(typeInfo => { if (typeInfo.Type == typeof(NodeDocument)) foreach (extension
  converter in nodes.Converters where Kind contains '/') typeInfo.PolymorphismOptions!.DerivedTypes.Add(new
  JsonDerivedType(converter.DocumentType, converter.Kind)); })`. `SerializerOptions` built from that
  resolver plus `AllowOutOfOrderMetadataProperties = true`, `ReadCommentHandling = Skip`,
  `AllowTrailingCommas = true`, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`,
  `WriteIndented = false`, then `.MakeReadOnly()`. Do NOT also set `PropertyNamingPolicy`/enum
  converters/`DefaultIgnoreCondition` at the options level — those come from the source-gen context
  already baked into its `JsonTypeInfo`s; re-setting them at the options level only affects
  reflection-based (non-source-gen) type resolution, which this resolver chain never falls back to.
  `NetPrintsSchema.V1Url` (the const string) belongs in this file too, per the contract's code block.
  `NetPrintsJsonOptions.DocumentOptions` (static `JsonDocumentOptions`): `CommentHandling = Skip`,
  `AllowTrailingCommas = true`.
- Once T026 lands, go back and add `[property: JsonConverter(typeof(NodeListConverter))]` to
  `GraphDocument.Nodes` in `Documents/GraphDocuments.cs` (left off deliberately in T025 to keep that
  commit buildable before `NodeListConverter` existed — see the T025 note above).

**T035 (`Mapping/DocumentMapper.cs`)** — design worked out but not yet written; a full design is below
so it does not need re-deriving:
- Constructor: `DocumentMapper(NodeDocumentConverterRegistry nodes)`, store `nodes` in a field.
- `ToDocument(ClassGraph cls)`: `var context = new NodeMappingContext(cls);` then:
  - Duplicate member id check up front: iterate `cls.Members`, collect ids into a `HashSet<string>`
    (ordinal), `InvalidOperationException` on a repeat (callers run `EnsureUniqueMemberIds()` first per
    §2.8, so this is a "should never happen" guard, not graceful handling).
  - `MapGraph(NodeGraph graph, NodeMappingContext context) -> GraphDocument`: for each `Node` in
    `graph.Nodes`, `INodeDocumentConverter converter = nodes.FindByNodeType(node.GetType()) ??
    throw new InvalidOperationException($"No document converter registered for node type '{node.GetType()}'.");`,
    `NodeDocument raw = converter.ToDocument(node, context);`, then
    `raw with { Id = node.Id, Name = node.Name == node.DefaultName ? null : node.Name, Pins = BuildPinStates(node, context) }`
    (record `with` works across the base/derived boundary for `Id`/`Name`/`Pins` — verified in a spike
    while writing the BuiltIn converters, which all pass placeholder `node.Id, null, null` for exactly
    this reason). Append `graph.PreservedDocumentState`'s unknown nodes (if any — see below) after the
    known ones. Connections: `BuildConnections(graph)` (enumerate every node's *output* pins only —
    `OutputExecPins`/`OutputDataPins`/`OutputTypePins` — building `"<nodeId>/<PinKeys.For(pin)>"` →
    `"<targetNodeId>/<PinKeys.For(targetPin)>"`, plus any preserved connections referencing an unknown
    node id, then ordinal-sort by `From` then `To`). Locals: `null` (sub-phase H). Return
    `new GraphDocument(nodeDocs, connections.Count > 0 ? connections : null, null)`.
  - `BuildPinStates(Node node, NodeMappingContext context) -> List<PinStateDocument>?`: for every pin of
    every direction/kind (`InputDataPins`, `OutputDataPins`, `InputExecPins`, `OutputExecPins`,
    `InputTypePins`, `OutputTypePins`), compute `keyName = node.GetPinKeyName(pin)`; `name = pin.Name ==
    keyName ? null : pin.Name`; for `NodeInputDataPin` only, `value`: if `pin.UnconnectedValue is not
    null`, and `pin.PinType.Value is TypeSpecifier { IsEnum: true } enumType` (the model quirk from the
    T028/T029 note — `UnconnectedValue` is already a `string` for an enum pin), `new
    TypedValue(enumType.Name, (string)pin.UnconnectedValue)`; else
    `context.ToValue(pin.UnconnectedValue, $"{node.Id}/{PinKeys.For(pin)}")`. Only add a
    `PinStateDocument` when `name is not null || value is not null` (skip pins with nothing to record);
    return `null` if nothing was added for the whole node (never an empty list — matches "omit if
    empty" and avoids ever needing `JsonIgnore` gymnastics on `NodeDocument.Pins`).
  - `MapMethod`/`MapConstructor`/`MapVariable`: thin wrappers building `MethodDocument`/`ConstructorDocument`/
    `VariableDocument` from the model member plus `MapGraph` of its own graph(s) (variable → `TypeGraph`
    + optional `GetterMethod`/`SetterMethod` as `AccessorDocument`s).
  - `BuildLayout(ClassGraph cls) -> SortedDictionary<string, SortedDictionary<string, int[]>>?`: for
    every graph of the class (`cls` itself, each variable's `TypeGraph`/`GetterMethod`/`SetterMethod`,
    each method, each constructor), `GraphKeys.For(graph)` → inner `SortedDictionary<string, int[]>`
    (ordinal) of `node.Id` → `[Round(PositionX), Round(PositionY)]`, `Round = (int)Math.Round(x,
    MidpointRounding.AwayFromZero)`; omit an inner map only if a graph genuinely has zero nodes
    (shouldn't happen given model invariants, but cheap to guard); omit the whole `Layout` only if
    there is not one graph with a node (i.e., never in practice, but keep the `null`-when-empty shape
    for symmetry with every other optional field).
  - Assemble `ClassDocument` with `SchemaVersion: NetPrints.Serialization.Migrations.DocumentMigrator.CurrentSchemaVersion`
    (now available; **do not** hardcode `1` a second time), `Namespace: string.IsNullOrEmpty(cls.Namespace)
    ? null : cls.Namespace`, `GenericArguments` from `cls.DeclaredGenericArguments`, `EventGraphs: null`
    (sub-phase G — `ClassGraph.EventGraphs` does not exist until T078; T080 must come back and populate
    this instead of leaving it `null`), and the rest as above.
- `FromDocument(ClassDocument document, Project project, ICollection<DocumentIssue> issues, DocumentId id)`:
  order per the contract table — header (`new ClassGraph { Namespace = ..., Name = ..., Visibility =
  ..., Modifiers = ..., DeclaredGenericArguments = ... }`, `Project = project` and set on every graph
  created below) → validate every member id up front (missing/contains `/`or whitespace (`!StableIds.IsValidDocumentId`)/equal to `"class"`/duplicate → `DocumentFormatException`) → build each
  member's graph. Building a method/constructor/variable graph: construct via the model's own
  constructor (`new MethodGraph(document.Name)`, `new ConstructorGraph()`, `new Variable(cls, name,
  <placeholder type — see below>, null, null, modifiers)`) — remember the constructor already
  auto-creates that graph's one fixed node (T031's whole "reuse, don't recreate" design exists
  precisely so `FromDocument` can rely on this) — then set `.Id = document.Id` (an `internal set`,
  accessible via `InternalsVisibleTo`), then process `graph.Nodes` from the `GraphDocument`: for each
  `NodeDocument`, `INodeDocumentConverter? converter = nodes.FindByKind(nodeDoc is UnknownNodeDocument u
  ? u.Kind : GetKindOf(nodeDoc))` — actually simplest: iterate `document.Graph.Nodes`; if the element is
  `UnknownNodeDocument`, collect it into a `preservedNodes` list and raise `issues.Add(new
  DocumentIssue(Warning, NPD001, ..., id))`, do NOT call any converter; otherwise `nodes.FindByNodeType`
  won't work here (we only have the *document* type, not a node type) — need
  `NodeDocumentConverterRegistry` to also support **kind → converter** lookup by the `$kind` the
  `NodeListConverter` already resolved into a *concrete document type* (there is no `$kind` string left
  on a strongly-typed `MethodEntryNodeDocument` instance at this point!). Resolve this by adding (or
  reusing) `FindByDocumentType(Type)` type lookup — `NodeDocumentConverterRegistry` currently only
  exposes `FindByKind(string)`/`FindByNodeType(Type)`; add a third dictionary
  keyed by `converter.DocumentType` (mirrors the two existing ones exactly, same duplicate-check
  pattern) so `FromDocument` can do `nodes.FindByDocumentType(nodeDoc.GetType())`. Node with unresolved
  document type → this should not happen for a known `NodeDocument` subtype (every built-in is
  registered), so treat as an `InvalidOperationException` (a real gap in the registry, not a document
  problem) — only genuinely-unknown `$kind` values become `UnknownNodeDocument` upstream in
  `NodeListConverter`/`JsonDocumentFormat`, never reach here as a "known type but no converter" case.
  `converter.CreateNode(nodeDoc, graph, context)` → returned node already has the graph's default id (or,
  for the four "fixed" kinds, its pre-existing id) — set `.Id = nodeDoc.Id` (internal set) — duplicate
  node id within the graph → `DocumentFormatException`; `.Name = nodeDoc.Name ?? node.DefaultName`; apply
  `nodeDoc.Pins` (for each, `PinKeys.Find(node, pinRef)` — unknown → `issues.Add(NPD003)`, drop;
  found → set `.Name` if present, set `.UnconnectedValue` if `Value` present (mirror the enum special
  case in `BuildPinStates`, in reverse: if `pin.PinType.Value is TypeSpecifier { IsEnum: true }`, assign
  the raw `Value.Value` string directly; else `context.FromValue(value)`)). After all nodes exist,
  process `document.Graph.Connections`: for each, split `"<nodeId>/<pinRef>"` on the *first* `/`
  (`DocumentId`-style split is wrong here — use `endpoint.IndexOf('/')` directly, node ids never
  contain `/` per `StableIds.IsValidDocumentId`), `graph.FindNode(nodeId)` (missing → `NPD002`, drop),
  `PinKeys.Find(node, pinRef)` (missing → `NPD002`, drop), then dispatch to the right
  `GraphUtil.Connect*Pins` overload by the resolved pins' concrete pin types (a `switch` on
  `(fromPin, toPin)` pattern matching `NodeOutputExecPin`/`NodeInputExecPin`,
  `NodeOutputDataPin`/`NodeInputDataPin`, `NodeOutputTypePin`/`NodeInputTypePin` — anything else is
  "incompatible pins", `NPD002`, drop; wrap `GraphUtil.Connect*Pins`'s own possible exceptions, if any,
  as the same drop+issue rather than letting them propagate, since a hand-edited file could reference
  two pins of the wrong direction pairing that still resolve individually). Connections whose `From` or
  `To` node id resolves to nothing AND that id matches a *preserved* unknown node's id: keep as a
  preserved connection (store alongside the preserved nodes) rather than an `NPD002` drop — this is the
  "connections to it preserved" requirement (DF-T08) — check preserved-node ids first, before treating a
  missing node as an error. Positions: for each node with a `Layout[graphKey][nodeId]` entry, set
  `PositionX/Y` directly from the two ints; collect nodes with NO entry and call
  `GraphAutoLayout.PlaceUnpositioned(graph, unpositioned)` once per graph, at the end (after connections
  are wired — auto-placement's neighbour rule needs connections already in place); layout entries for an
  unknown graph key or node id → `issues.Add(NPD004)` (info), ignore. Finally
  `GraphTypeInference.Relax(graph)` per graph (document-format.md §3.1). Set `NodeGraph.Class`/`Project`
  back-references throughout. At the very end, `cls.MarkClean()`-equivalent: the contract says "the
  returned class has `IsDirty == false`" — since `MarkDirty`/`IsDirty` default to `false` already on a
  freshly-constructed `ClassGraph` and nothing here calls `MarkDirty()`, this should already hold
  without extra code; add an explicit assertion/test for it rather than trusting that by omission.
  `Variable`'s type: its constructor requires a `TypeSpecifier` up front to build the *initial* type
  graph, which `FromDocument` immediately discards/rebuilds from `VariableDocument.TypeGraph` — likely
  cleanest to pass a cheap placeholder (`TypeSpecifier.FromType<object>()`) then immediately overwrite
  `variable.TypeGraph` with the mapped one (`MapGraphFromDocument` populating a *fresh* `TypeGraph`,
  setting `OwningClass = cls`, then replacing `variable.TypeGraph` wholesale) rather than trying to graft
  document nodes onto the constructor's auto-built placeholder type graph (that placeholder's own
  `TypeReturnNode` would otherwise conflict with the document's own, similarly to the "fixed node" story
  above, so a variable's type graph is one place where the "reuse the constructor's node" trick does
  apply too, exactly like `ClassGraph`/`TypeGraph`/`MethodGraph`/`ConstructorGraph` — do NOT construct a
  second, throwaway `TypeGraph`; build the DOCUMENT's nodes into the constructor-provided
  `variable.TypeGraph` the same way every other graph kind is handled, changing only the constructor
  call to something minimal like `new Variable(cls, name, TypeSpecifier.FromType<object>(), null, null,
  modifiers)` purely to get a real `TypeGraph`+`TypeReturnNode` to populate, then overwrite `Name`/
  `Visibility`/`Modifiers`/`Id` from the document as normal).
- Round trip (DF-T03): `ToDocument(FromDocument(d))` must equal `d`'s bytes for a canonical `d` with a
  layout entry for every node — this mostly falls out of the above if `BuildPinStates`/`BuildConnections`/
  `BuildLayout` are exact inverses of the `FromDocument` pin/connection/position application, which is
  why the design above tries hard to keep each pair (`ToValue`/`FromValue`, pin key encode/decode,
  connection encode/decode) symmetric.
- Needed registry addition before starting: `NodeDocumentConverterRegistry.FindByDocumentType(Type)`
  (mirrors `FindByKind`/`FindByNodeType`, same duplicate-check in the constructor extended to a third
  dictionary). Do this as part of T035's own commit (it is only needed by `FromDocument`).

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
