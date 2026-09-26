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
