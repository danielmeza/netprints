# 0003: Analyzer promotion and string literal discipline

## Status

Accepted (2026-09-27). Revised the same day after an adversarial review of the first pass rejected it
(base commit `2cb7316`, P1 = `git merge-base HEAD origin/master`..`HEAD`): the blanket
`dotnet_analyzer_diagnostic.severity = suggestion` silently disabled the SDK's own CA rules too, the
`src/NetPrints.Editor/**.cs` carve-out and most of its 22 pragmas were unjustified, and the VSTHRD111/S109
escape hatches and the disposal-chain story were inaccurate. That revision fixed all of that.

Revised again the same day after a second, closing review: the `.editorconfig` "Pre-P1 suppressions"
section and the `S109` Editor-wide carve-out are gone. Every one of that ledger's rows was either fixed
for real (`Variable.cs`, `GraphUtil.cs`, `TernaryNode.cs`, `AutomationAgent.cs`'s `Wait(0)`, the
`ReferenceListVM`/`ClassEditorVM` ownership sites, `IconConverter.cs`) or moved to a member-level
`[SuppressMessage]` (the six sites the "Suppression ledger" below lists). `S109` is now `error` in all of
`src/`, including the Editor's 36 pre-existing hits (named constants and honestly-named divisors, no
value-named constants). This closes the contradiction the first revision left behind: its own
"Follow-up, not done here" list still called the Editor's `S109` carve-out future work while the severity
policy section above it already described the carve-out as current.

Follow-up batch landed the same day: the researched rule set (see "Follow-up, not done here" below)
promoted, and every violation it flagged fixed for real (see "Follow-up batch: the researched rule set
lands", below). `S1309` and `CA2007` — both in the original research draft's table — are skipped, not
landed: `S1309` (flags every `#pragma`/`[SuppressMessage]`, file-grained) is superseded by
`SourceHygieneTests.NoUnlistedSuppressions` (rule- and site-grained, the tighter gate this ADR already
requires); `CA2007` would duplicate `VSTHRD111`, which already enforces `ConfigureAwait` with this same
ADR's UI/non-UI layering table.

## Context

The owner found that agents keep introducing magic strings and duplicated literals with no discipline
enforced. Concrete evidence: `src/NetPrints.Extensibility/Loading/ExtensionDiagnosticCodes.cs` correctly
holds the `NPX` codes as documented `const string`s, but the translator's `NPT` codes were raw literals
scattered across `ExecutionGraphTranslator.cs`, `ClassTranslator.cs` and `UntranslatedNodeTranslator.cs` —
no shared constants class. Separately, the built-in node-kind strings (`"eventEntry"`, `"methodEntry"`, …)
were each duplicated across four sites with no shared source. No analyzer beyond the SDK defaults was
configured; `.editorconfig` documented that existing severities were "mostly silent/suggestion" so
`dotnet build` stayed warning-free by construction, not by policy.

## Decision

### Fix the two concrete duplications

- `src/NetPrints.Core/Translator/TranslationDiagnosticCodes.cs`: one `const string` per `NPT` code the
  translator or the editor throws/reports (`NPT001`, `NPT002`, `NPT005`, `NPT006`, `NPT007`, and
  `Unclassified = "NPT000"`, `MainEditorVM.CompileAsync`'s interim id for a `ClassTranslationFailure` not
  yet mapped to a coded `TranslationException`, T086). `NPT003` stays declared in
  `NetPrints.Generator.GraphCodeGenerator` (a different, already-named constant); `NPT004` is reserved for
  T085 (local variables), not yet thrown. Every raw literal, including `MainEditorVM.cs`'s, now references
  a constant.
- `src/NetPrints.Serialization/Documents/BuiltInNodeKinds.cs`: one documented `const string` per built-in
  node kind (`MethodEntry`, `EventEntry`, `Return`, … — the 24 kinds document-format.md §1.5 defines).
  `NodeDocuments.cs`'s `JsonDerivedType` discriminators, each built-in converter's `Kind` property,
  `NodeDocumentConverterRegistry.KnownBuiltInKinds` and `BuiltInNodeLibrary`'s `Describe` calls all
  reference it; `NodeDocumentConverterRegistry.EventEntryKind` (a `Documents` concept declared in
  `Mapping`, an inverted layering) is retired in its favor. Values are unchanged, so serialized documents
  and the committed JSON Schema (`schemas/netpc.v1.schema.json`) are byte-identical.
- `src/NetPrints.Core/Translator/CSharpKeywords.cs`: one shared `internal` class for the C# modifier
  keywords `ClassTranslator` and `ExecutionGraphTranslator` each declared their own copy of
  (`Partial`, `Static`, `Abstract`, `Sealed`, `Unsafe`, `ReadOnly`, `New`, `Override`, `Virtual`).
  `ExecutionGraphTranslator`'s placeholder-replacement constant is now named `JumpStackPlaceholder`, and
  `BuiltInNodeTranslators.cs`'s `"return;"` literal is `EarlyOutStatement`, named for why the translator
  writes it (an early-out from control-flow nodes that skip the rest of their body) rather than what it is.

### Enforcement: `SourceHygieneTests.NoRawDiagnosticCodeLiteralsOutsideTheirConstants`

Follows the existing `NoSourceFileMentionsProgramFilesX86` pattern (plain file scan over `git
ls-files`-style enumeration, xUnit v3). For every `src/**/*.cs` file (excluding `///` doc-comment prose,
which documents the code format, not a duplicated value): a line declaring a diagnostic code as a
`const string` is allowed only in the six designated files (`TranslationDiagnosticCodes.cs`,
`ExtensionDiagnosticCodes.cs`, `DocumentIssue.cs`, `GraphCodeGenerator.cs`, `ProjectSystemException.cs`,
`ProjectMessage.cs`) — a declaration anywhere else fails the test, not just a silently-ignored one; a bare
`"NPT001"`-shaped string literal anywhere that is not such a declaration line also fails it. The pattern
covers all four prefixes this codebase uses (`NPT`/`NPD`/`NPX`/`NPW`) and matches 3-or-more-digit codes
(`\d{3,}`, not `0\d\d`), so a future `NPT100`+ code is caught too; `NPW` is split across two declaring
types the same way `NPT`/`NPD`/`NPX` already were, since the test's declaring-files list was already a
list, not a single file. Every declared code is additionally asserted unique. There is no more file-wide
exemption (the previous version exempted every line of the four declaring files, including any unrelated
literal that happened to match): `MainEditorVM.cs`'s former exception is gone along with the raw literal it
excused. Verified to actually gate: a reintroduced raw literal, and a redeclaration outside the designated
files, each fail the test (reverted after verifying).

### Analyzer packages

- **SonarAnalyzer.CSharp** 10.34.0.3385 — chosen over StyleCop.Analyzers because StyleCop is mostly
  formatting/ordering, which `dotnet format --verify-no-changes` already enforces; Sonar's S-rules target
  the actual smells this ADR's evidence describes (duplicate literals, magic numbers, dead stores, unused
  locals).
- **IDisposableAnalyzers** 4.0.8 — this codebase has real manual-disposal surface (extension hosts,
  document stores, view models that own subscriptions).
- **Microsoft.VisualStudio.Threading.Analyzers** 18.7.23 (VSTHRD) — there is at least one deliberate
  `async void` event handler and extensive `Dispatcher.UIThread`/UI-thread-affine code; these rules catch
  async/UI-thread misuse.
- **`AnalysisLevel=latest`** (SDK's own analyzers) — at their own default severity (see "Severity policy"
  below; a global default of `suggestion` for *every* analyzer diagnostic, the first pass's mistake,
  silently disabled these too). **`AnalysisMode=Recommended` was evaluated and rejected**: unlike the three
  packages above, it bakes per-rule severities into its own shipped global analyzer config, which outranks
  any severity set in `.editorconfig` (confirmed empirically: a `suggestion`-severity default did not
  downgrade `CA1041`, `CA1305`, `CA1510`, `CA1859`, `CA1868`, `CA2201` and others enabled by that preset).
  Downgrading every individual rule it turns on would need dozens of per-rule entries with an unknown
  amount of pre-existing debt behind them — left as a follow-up.
- Not proposed here: StyleCop.Analyzers (redundant with `dotnet format`), Meziantou.Analyzer and Roslynator
  (large triage cost, high overlap — see the follow-up draft below for the fuller comparison).

### Severity policy: `error` for a curated set, `suggestion` for the rest — never one global default

**`error`, scoped to `[src/**.cs]` (tests keep every rule at `suggestion`, below):**

- SonarAnalyzer.CSharp: `S1192` (duplicate string literals), `S109` (magic numbers, no carve-out — see
  "S109 (magic numbers): no carve-out" below), `S1854` (dead stores), `S1481` (unused locals).
- IDisposableAnalyzers: every `IDISP001`–`IDISP026` rule, not only the ones observed to fire at the time
  of writing.
- Microsoft.VisualStudio.Threading.Analyzers: every `VSTHRD` rule except `VSTHRD111`, which is laid out
  separately (see "VSTHRD111 layering" below), including `VSTHRD101`/`VSTHRD110`, which an earlier pass of
  this ADR left ungated.

These are per-rule `dotnet_diagnostic.<id>.severity` entries, not a category-wide bulk setting: no single
category name was confirmed to cover exactly — and only — a package's rules (see the next paragraph).

**`suggestion`, named explicitly, everywhere (`[*.cs]`, so it also covers `tests/`), for every rule these
three packages ship that is not in the curated `error` set above.** There is no blanket
`dotnet_analyzer_diagnostic.severity = suggestion` default (the first pass's central mistake): that key
applies to *every* analyzer diagnostic, including the SDK's own CA rules, silently un-doing
`AnalysisLevel=latest`'s coverage and any CA rule a project's `TreatWarningsAsErrors` (set once, repo-wide,
in the root `Directory.Build.props`) would otherwise fail the build on — verified with a temporary `throw
e;` probe in `NetPrints.Core`: with the blanket in place, `CA2200` (rethrow loses stack info) built clean;
without it, the same probe failed the build with `CA2200`, as it should. A category-based substitute was
tried and rejected: `dotnet_analyzer_diagnostic.category-<Category>.severity` does not accept a category
name containing a space (verified empirically), which rules out Sonar's own categories (`"Major Code
Smell"`, …); IDisposableAnalyzers' single category (`"IDisposableAnalyzers.Correctness"`) and VSTHRD's
(`"Usage"`, `"Style"`) would work syntactically, but `"Usage"`/`"Style"` are shared with plain CA rules
this ADR does not want to silence. Every non-curated rule these three packages ship — 319 Sonar diagnostic
keys (314 numbered `S`-rules plus 9 `S9999-*` cross-file metrics/telemetry keys the analyzer also reports
on; 323 total minus the 4 curated), all 26 IDISP IDs, all 24 VSTHRD IDs — is therefore named individually
at `suggestion` in `.editorconfig`, so nothing in their default sets fails the build by surprise, and every
CA diagnostic from `AnalysisLevel=latest` (or any other analyzer this repo references) builds at its own
default severity.

**An `S`-rule this list does not (yet) name** — one a future SonarAnalyzer.CSharp upgrade adds — keeps
whatever severity the package ships it at, not `suggestion` and not `error`: this repo's `.editorconfig`
only overrides IDs it lists. Verified with a probe: `S1858` ("`ToString()` calls should not be redundant",
not in this list) against `s.ToString()` where `s` is already a `string` produced no diagnostic at all,
at any severity — not a build warning/error, and not even an `info`-level hit from `dotnet format
analyzers --severity info --verify-no-changes` — confirming the bare NuGet package ships most Sonar
"Code Smell" rules below `info` by default, so an unnamed rule stays silent rather than starting to fail
`TreatWarningsAsErrors`'s build by surprise. This is a narrower guarantee than "every non-curated rule is
`suggestion`" reads as: it holds only for the 319 IDs actually named here. A future analyzer upgrade that
ships a *new* rule at `warning` by default would fail the build the moment it starts existing, until this
list is extended to name it — a known gap, not one a fixed per-ID list can close in advance.

### VSTHRD111 (ConfigureAwait) layering

`error` by default in `src/**.cs` (a library with no UI synchronization context to resume on should always
specify it), `none` for code that resumes on the UI thread deliberately (owner-approved 2026-09-27):

| Path | Severity | Why |
|---|---|---|
| `src/NetPrints.Editor/**VM.cs` | `none` | View models: bound to views, continuations run on the UI thread. |
| `src/NetPrints.Editor/**.axaml.cs` | `none` | Views (code-behind). |
| `src/NetPrints.Editor/ModelSync/*.cs` | `none` | `ObservableViewModelCollection`, shown directly in views. |
| `src/NetPrints.Editor/Hosting/EditorComposition.cs` | `none` | Composition root: `StartAsync` drives dialogs and view-model calls in sequence on the UI thread. |
| `src/NetPrints.Editor/Hosting/Avalonia/**.cs` | `none` | Adapters over UI-thread-affine Avalonia APIs (dialogs, clipboard, storage pickers, the dispatcher itself): the continuation after each `await` touches those APIs again. |
| `src/NetPrints.Editor/Graph/GraphDragDrop.cs` | `none` | Drag-and-drop gesture handling: `Avalonia.Input.DragDrop.DoDragDropAsync`'s continuation is UI work. |
| `src/NetPrints.Desktop/**.cs` | `none` | Desktop's `Program`/`EditorApp` bootstrap, same reason. |
| Everything else in `src/NetPrints.Editor/` (`Hosting`'s services, `Hosting/Automation/`, `UndoRedo`, …) | `error` (inherited) | Not UI-bound: `Hosting/Automation/AutomationAgent.cs`'s named-pipe protocol loop, for example, got `ConfigureAwait(false)` at every `await`. A non-UI class that grows belongs in a library project; CPU-heavy work started from the Editor goes through `Task.Run`, with the caller awaiting it back on the UI thread. |

### S109 (magic numbers): no carve-out

`error` in all of `src/**.cs`, including `src/NetPrints.Editor/**.cs` — the Editor's own pre-existing hits
are fixed, not carved out. Every P1-touched `S109` hit was fixed for real: `Ids.cs`'s Crockford base32
encode/decode now derives its digit values from the single `Alphabet` string instead of a 22-line
hand-written switch (`DecodeDigit`), plus two named bit-width constants (`AlphabetBits`, `AlphabetMask`);
`BuiltInNodeTranslators.cs` and `CanonicalJsonWriter.cs`/`NetPrintsJsonSchema.cs` in `NetPrints.Serialization`
each got one purpose-named constant. `NetPrints.Core/Core/Variable.cs`, `NetPrints.Core/Graph/GraphUtil.cs`
and `NetPrints.Core/Graph/TernaryNode.cs` (arbitrary canvas coordinates and a pin index) are fixed with
named constants too: `GraphUtil.NewMethodEntryPositionX/Y` and `NewMethodReturnOffsetX` are shared with the
two identical call sites in `NetPrints.Editor/UndoRedo/ModelOperations.cs` that duplicated the same three
literals, instead of each declaring its own.

The Editor's 36 pre-existing hits, across `GraphConverters.cs`, `GraphEditorView.axaml.cs`,
`GridRenderer.cs`, `GridStyle.cs`, `Pins/NodePinVM.cs`, `Hosting/Automation/AutomationContracts.cs`,
`Hosting/Automation/AutomationTree.cs`, `UndoRedo/ModelOperations.cs` and `ClassEditor/ClassEditorVM.cs`,
are named constants (colors, opacities, thicknesses, grid-cell offsets) or small helper methods (a
`Midpoint`/`CenterDivisor`-style honest name for a `/ 2`, never a constant named after its value like
`Two`). None of these needed AXAML resources: they are computed in code (converters, view models, Skia
render paths), not literal values in markup.

### The fully async disposal chain

`IExtensionHost` is `IAsyncDisposable`, with `ValueTask<ExtensionRegistry> LoadForProjectAsync` awaiting
the previous registry's `DisposeAsync()`; `ExtensionRegistry.DisposeAsync()` genuinely awaits its owned
`IAsyncDisposable`; there is no synchronous `Dispose()` bridge anywhere in this chain. Every caller awaits
it: `MainEditorVM`'s project-switch path, `EditorHostServices.DisposeAsync()` (which now disposes the
extension host and host channel through a `Func<ValueTask>` its caller builds at the same call site that
created them — see "Real fixes" below), Desktop's `Program.Main` (still synchronous — `[STAThread]` is not
honored on an async `Main`, and Avalonia's own message loop already blocks synchronously — but its
`EditorApp.OnFrameworkInitializationCompleted` wires `IClassicDesktopStyleApplicationLifetime.ShutdownRequested`
to cancel the first shutdown request, run the async cleanup, then call `Shutdown()` once it finishes,
verified by `ShutdownTests.ClosingTheMainWindowDisposesHostServicesExactlyOnceThenExits`), and the Cli's
`static async Task<int> Main`. `EditorComposition` and `MainEditorVM` are themselves `IDisposable` now
(a `PersistenceBinding` subscription and a `HostChannelBridge` subscription respectively), disposed on the
same shutdown path. There is no more "blocked, needs a follow-up batch" story: every consumer this ADR's
predecessor named as blocking the propagation is fixed.

**Two edge cases the previous revision left open:** a `Dispose()`/`DisposeAsync()` that throws must not
leave the process running with no window, and a second close request while cleanup is still in flight must
not dispose twice. `OnShutdownRequested` cancels every request until cleanup has finished (`cleanedUp`), and
coalesces concurrent requests onto one in-flight `Task` (`cleanup ??= CleanUpAndShutdownAsync()`), so a
second click never starts a second disposal; `CleanUpAndShutdownAsync`'s `finally` sets `cleanedUp = true`
and calls `desktop.Shutdown()` unconditionally, so a throwing dispose (logged via
`Hosting.Log.ShutdownCleanupFailed`) still exits instead of hanging with no window
(`ShutdownTests.RequestingShutdownTwiceStillDisposesHostServicesExactlyOnce`). Desktop's `Program.Main`
also wraps `StartWithClassicDesktopLifetime` in a `try`/`finally` that fires-and-forgets
`extensions.DisposeAsync()`/`channel.Channel.DisposeAsync()` as a safety net for a startup failure before
`OnFrameworkInitializationCompleted` ever wires the shutdown path — both are idempotent, so the redundant
call on the normal exit path is a no-op, and it never blocks (`Main` cannot `await` without reintroducing
`VSTHRD002`, so this is deliberately best-effort, not a guaranteed drain).

### Real fixes vs. suppression — member-level `[SuppressMessage]` only, never `.editorconfig`, never `#pragma`

A violation the curated rules catch is fixed for real. The only suppression mechanism this ADR allows, for
the narrow set of sites where the analyzer's ownership/intent-tracking genuinely cannot see the truth (not
merely "this batch didn't get to it" and not "confirmed false positive" on its own), is a member-level
`[SuppressMessage("<Category>", "<ID>", Justification = "ADR-0003: <one-line reason>")]` on the smallest
containing member — never a per-file `.editorconfig` severity override, and never a `#pragma warning
disable` in the `.cs` file: `git grep -n "pragma warning" -- 'src/*.cs'` stays empty, by policy, and
`.editorconfig` has no per-file suppression section at all. `git grep -n "SuppressMessage" -- 'src/*.cs'`
lists exactly the six sites the ledger below names, each with an `"ADR-0003: …"` justification — nothing
else, and never on new code. Most of the previous revision's pre-P1 `.editorconfig` overrides turned out to
have real fixes once looked at again: an index loop, `Interlocked.Exchange`, an explicit `using` around an
enumerator, an `await standardOutputTask`, `JsonNode.ParseAsync`, and — where the analyzer's
ownership-tracking cannot see a deferred, callback-based disposal at all, such as `EditorHostServices`
disposing a constructor-injected `IExtensionHost`/`IHostChannel` — moving the actual `Dispose`/
`DisposeAsync` call to a `Func<ValueTask>` built at the object's true construction site, which
IDisposableAnalyzers does recognize as ownership. This batch closed the rest: `Variable.cs`, `GraphUtil.cs`
and `TernaryNode.cs`'s `S109` hits got named constants; `AutomationAgent.cs`'s `SemaphoreSlim.Wait(0)`
became `await connectionSlots.WaitAsync(0).ConfigureAwait(false)` (no more `VSTHRD103` site to suppress);
`MainEditorVM.ShowReferencesAsync` now owns `using var references = new ReferenceListVM(…)` itself instead
of relying on `EditorDialogs.ShowReferencesAsync`'s `finally` (dropped); and `IWindowService.OpenClassEditor`
takes the `ClassGraph`/`EditorContext` and constructs the `ClassEditorVM` itself, so the `new` and its
eventual `Dispose()` (on the window's `Closed` handler) are both visible to IDisposableAnalyzers instead of
crossing a `MainEditorVM` → `WindowService` boundary as an opaque parameter; `IconConverter.cs`'s
lock-and-`Dictionary` cache is a `ConcurrentDictionary.GetOrAdd`.

**Suppression ledger** — every current `[SuppressMessage]` in `src/`, all pre-existing (not sites this
batch's own changes introduced) and each ADR-0003-approved because the analyzer cannot see the real
invariant:

| File : member | Rule | Reason |
|---|---|---|
| `NetPrints.Editor/ClassEditor/ClassEditorVM.cs` : `OpenGraph` | IDISP003 | `OnOpenedGraphChanged` (the CommunityToolkit.Mvvm-generated property hook) disposes the old value; the analyzer cannot see a generated setter's side effect. |
| `NetPrints.Editor/ClassEditor/ClassEditorVM.cs` : `DropDetachedState` | IDISP003 | Same: assigning `OpenedGraph = null` re-triggers the generated hook. |
| `NetPrints.Editor/ClassEditor/ClassEditorVM.cs` : `Dispose` | IDISP003 | Same, at the `OpenedGraph = null` in teardown. |
| `NetPrints.Editor/Graph/GraphDragDrop.cs` : `DragSourceHelper.Moved` | VSTHRD100 | `async void Moved` is a deliberate event handler: exceptions reach `Dispatcher.UIThread.UnhandledException`, tested by `UnhandledExceptionTests.AsyncVoidHandlerExceptionIsReported`. |
| `NetPrints.Editor/Graph/GridBackground.cs` : `Render` | IDISP004 | Avalonia's renderer disposes a queued `ICustomDrawOperation` after executing it (a documented Avalonia ownership-transfer contract the analyzer does not know). |
| `NetPrints.Editor/Hosting/Automation/AutomationAgent.cs` : `ServeAsync` | IDISP007 | Takes ownership of its connection stream (`await using var _ = stream;`), a deliberate transfer, not an accidental double-owner. |

### Multi-line comments

Every comment this batch and its predecessor added is one line, matching the surrounding code's density
(AGENTS.md "Code comments"); rationale that does not fit a line lives here or in
`specs/003-core-refactor/implementation-notes.md` instead.

### Follow-up batch: the researched rule set lands

The rule set the "Follow-up, not done here" section below described as "research only" now lands, at the
severities and scoping a dedicated research pass (`csharp-agent-rules-draft.md` §2a) worked out. Two rules
from that table are skipped rather than landed:

- **S1309** (flags every `#pragma warning disable`/`[SuppressMessage]`, per file): superseded by
  `SourceHygieneTests.NoUnlistedSuppressions` (landed alongside `NoNullForgivingOperator` in the batch
  right before this one), which is strictly tighter — per rule ID and per site, not per file — and is
  this ADR's actual enforced ledger gate already.
- **CA2007** (ConfigureAwait, scoped to libraries): `VSTHRD111` above already enforces `ConfigureAwait`
  with this exact ADR's UI/non-UI layering table; turning CA2007 on too would double-report every site
  VSTHRD111 already covers for no additional coverage.

**`error`, added to `[src/**.cs]`:** `CA2016` (forward `CancellationToken`), `CA2012` (`ValueTask` misuse),
`CA2201` (reserved/too-general exception types), `CA2208` (argument exception constructor args),
`CA2200` (rethrow to preserve stack), `S2486`/`S108` (swallowed exceptions, empty blocks), `CA2211`
(visible mutable static fields), `CA2254`/`CA2017` (log template hygiene).

**`warning` (gates the build too, since `TreatWarningsAsErrors` is on, but may in principle carry a
member-level `[SuppressMessage]` in a future batch, unlike the `error` rules above), added to
`[src/**.cs]`:** `CA1068` (`CancellationToken` last), `CA1861` (constant array arguments),
`CA1510`–`CA1513` (throw helpers), `S2139` (log-and-rethrow), `CA1065` (throwing from unexpected
members), `CA1307`/`CA1309` (explicit `StringComparison`), `CA1851`/`CA1827`/`CA1829`/`CA1860`
(`IEnumerable` re-enumeration, `Count()`/`Any()`/`Length`), `CA1852`/`S2933` (seal internal types,
`readonly` fields), `CA1848`/`CA1727` (`LoggerMessage`, PascalCase placeholders).

**`CA1305`/`CA1310`/`CA1311`** (locale-dependent formatting/comparison): `error` in
`NetPrints.Generator`, `NetPrints.Serialization` and `NetPrints.Core` — the projects whose output
(generated code, JSON, golden fixtures) must be byte-identical on any locale — `warning` elsewhere.

**`CA1002`/`CA2227`** (expose read-only collection types): `error` in the newer projects
(`NetPrints.Extensibility`, `NetPrints.Serialization`, `NetPrints.Workspace`, `NetPrints.Generator`,
`NetPrints.Sdk`); `suggestion` in the legacy `NetPrints.Core`/`NetPrints.Reflection` model, which still
has public mutable-collection APIs a future batch refactors; not set for the remaining projects (no
collection-shaped public surface these rules would flag there).

**No escape hatch needed.** Every rule above landed at the severity the table set, with every violation
fixed for real — no rule came close to the ~40-hit threshold that would have left it out of
`.editorconfig`. Every other rule in the table (`CA2016`, `CA1068`, `CA2012`, `CA1861`, `CA1511`–`CA1513`,
`CA2200`, `S108`, `S2139`, `CA1309`, `CA1827`, `CA1829`, `CA1860`, `CA2211`, `CA1848`, `CA2254`, `CA2017`,
`CA1727`, `CA1002`, `CA2227`) had zero pre-existing hits once promoted; the rest had a small, concentrated
count, entirely in code-generation/translation paths where the fix is unambiguous:

| Rule | Hits fixed | Where |
|---|---|---|
| CA1307 | ~45 | Template placeholder substitution (`ClassTranslator.cs`'s `.Replace` chains), identifier sanitization, `IndexOf`/`Contains` on kind strings and ids (`NetPrints.Core`, `NetPrints.Serialization`, `NetPrints.Extensibility`, `NetPrints.Generator`, `NetPrints.Editor`) — all fixed with `StringComparison.Ordinal`. |
| CA1305 | 20 | `StringBuilder.AppendLine`/`Append` of interpolated generated-code text in `ExecutionGraphTranslator.cs` — fixed with `CultureInfo.InvariantCulture`. |
| CA1851 | 4 | `extraModifiers`/`argumentNames`/`returnNames` parameters enumerated more than once in `ExecutionGraphTranslator.cs` and `BuiltInNodeTranslators.cs` — fixed by materializing each to a `List<T>` once. |
| CA2201 | 4 | Internal invariant violations thrown as bare `Exception` in `ExecutionGraphTranslator.cs`/`BuiltInNodeTranslators.cs` (now `InvalidOperationException`) and a malformed type-name lookup in `ReflectionProvider.cs` (now `FormatException`). |
| CA2208 | 3 | `throw new ArgumentException(nameof(type))` passed the parameter name as the *message* in `GenericType.cs`, `TypeSpecifier.cs` and `ReflectionConverter.cs` — a real bug CA2208 caught, fixed with a real message plus `nameof(type)` as the second argument. |
| CA1310 | 4 | `StartsWith`/`EndsWith` on identifier prefixes/suffixes (`ExecutionGraphTranslator.cs`, `ReflectionProvider.cs`) — fixed with `StringComparison.Ordinal`. |
| CA1510 | 3 | Manual null checks in `ObservableRangeCollection.cs` — fixed with `ArgumentNullException.ThrowIfNull`. |
| CA1852 | 2 | `NetPrints.Cli/Program.cs`'s `Program` and `CompileOptions` — sealed. |
| CA1065 | 1 | `TypeSpecifier.Equals` threw `ArgumentException` on a same-name/different-`IsEnum` collision; `Equals` must never throw, so it now returns `false` (an honest "not equal", not a defect: two types with the same name but different enum-ness were never actually the same type). |
| CA1311 | 1 | `TranslatorUtil.GetTemporaryVariableName`'s `.ToUpper()` on a random identifier character — fixed with `.ToUpperInvariant()`. |
| S2933 | 2 | Constructor-only-assigned fields in `MakeArrayTypeNode.cs`/`NodeOutputTypePin.cs` — made `readonly` (mechanical, `dotnet format analyzers`). |

No generated-code or JSON output changed byte-for-byte: every `CultureInfo`/`StringComparison` fix makes
already-invariant-culture behavior explicit rather than changing it (the build and dev machines here run
under the invariant/en-US locale already), confirmed by the golden fixtures and JSON schema being
unchanged (`git status samples/ tests/NetPrints.Core.Tests/Fixtures` clean) and the full suite passing.

## Consequences

- `dotnet build -c Release` is 0 warnings/errors; the curated rules above genuinely fail the build on a new
  violation anywhere in `src/**.cs`, including `NetPrints.Editor` — no carve-out anywhere, for any curated
  rule, `S109` included. `dotnet format --verify-no-changes` is clean.
- The suppression ledger above is the complete, reviewable record of every `[SuppressMessage]` in `src/`:
  `git grep -n "SuppressMessage" -- 'src/*.cs'` lists exactly those six sites, each with an `"ADR-0003: …"`
  justification, and `git grep -n "pragma warning" -- 'src/*.cs'` stays empty (there is no other suppression
  mechanism in `.cs` source, and none in `.editorconfig`, by policy); `SourceHygieneTests` is the enforced
  gate for the diagnostic/kind-code duplication this ADR was written for.
- **Pre-merge analyzer check**: the curated rules are `error`-severity, so `dotnet build -c Release` failing
  is already the enforced gate for them — no separate step needed. The `suggestion`-severity remainder
  (every non-curated Sonar/IDISP/VSTHRD rule, anything else `AnalysisLevel=latest` can produce) does not
  fail a build, so before a PR touching `src/**.cs` is approved, run `dotnet format analyzers --severity
  info --verify-no-changes` scoped to `git diff --name-only origin/master...HEAD`, and fix or explicitly
  suppress (member-level `[SuppressMessage]`, with a one-line reason, recorded here) every hit — not
  deferred silently.
- **Follow-up, not done here** (each a separate, dedicated batch):
  - `AnalysisMode=Recommended`, once its much larger set of newly-enabled rules has its own
    baseline/suppression pass.
  - ~~A much larger analyzer rule set was independently researched for a future batch~~ — **done** (see
    "Follow-up batch: the researched rule set lands" above): Async (VSTHRD100/101/110/114/200,
    CA2016/2012/1068), Disposal (IDISP002/006/009/016/025/026, S3881/S3877 — landed at `suggestion` by
    the earlier revision above), Constants (S109 at `error` everywhere in `src/`), Nullability
    (CA1510–1513), Exceptions (CA2200/2201/2208, S2486/S108/S2139, CA1065), Strings/collections
    (CA1305/1307/1309/1310/1311, CA1002/2227, CA1827/1829/1851/1860), Types (CA1852, S2933, CA2211),
    Logging (CA1848/2254/2017/1727) all landed at the severities/scoping above, every violation fixed for
    real, no escape hatch triggered. `S1309` and `CA2007` from that research draft are skipped by design
    (see above), not deferred. The remaining two items in that draft — the
    `Microsoft.CodeAnalysis.BannedApiAnalyzers` package and the two `SourceHygieneTests` facts
    (`NoNullForgivingOperator`, `NoUnlistedSuppressions`) — landed in the batch before this one (`RS0030`,
    and the hygiene tests respectively). Nothing remains from the original research draft's table.

## References

- `docs/adr/0001-repo-layout.md`, `docs/adr/0002-designer-comments-scheduled-p3b.md` — format precedent.
- `specs/003-core-refactor/contracts/compilation-and-diagnostics.md` §2 — `NPT`/`NPD`/`NPX` code format.
- `specs/003-core-refactor/contracts/document-format.md` §1.5 — the built-in node kinds `BuiltInNodeKinds`
  now names.
- The fuller rule-set research (see "Follow-up" above) drew on the dotnet/runtime, aspnetcore, roslyn,
  efcore, maui and Avalonia agent-instruction files, the IDisposableAnalyzers and sonar-dotnet rule
  catalogs, and the ConfigureAwait/BannedApiAnalyzers/DisposeAsync docs; a follow-up batch lands the
  researched C# rule set.
