# Agent rules for NetPrints

These rules apply to every AI coding session in this repository (Claude Code, opencode, Copilot
and any other agent), including subagents. They sit on top of the project constitution
(`.specify/memory/constitution.md`) and the roadmap (`.specify/memory/roadmap.md`); where they
conflict, the constitution wins.

## Workflow per phase
- Spec Kit flow: `speckit-specify` → `speckit-clarify` → `speckit-plan` (with `research.md`) →
  `speckit-tasks` → `speckit-analyze` → `speckit-implement`. One phase = one spec, one branch
  (`NNN-short-name`), one PR.
- PRs target the fork `danielmeza/netprints` (`gh pr create --repo danielmeza/netprints --base master`).
  `gh` defaults to the upstream parent for forks, so always pass `--repo`. Never open anything
  against an upstream repository without the owner's explicit approval.
- Every PR is reviewed by a separate agent that did not implement it. The implementer fixes each
  finding, replies on each review thread with how it was resolved (commit sha) or why it is
  deferred, keeps CI green, and does not merge. The owner or the coordinator merges.
- Governance files (`.specify/memory/*`) are changed only by the coordinating session with the
  owner's approval. Other agents propose changes in their report instead of editing them.

## Model selection
Use the strongest model where judgment matters and a faster, cheaper model for well-specified execution.

| Work | Model |
|---|---|
| Spec, research, plan, tasks, analyze | Opus |
| Implementing `tasks.md` with concrete tasks and acceptance tests | Sonnet |
| Independent PR review | Opus |
| Hard debugging (crashes, native/managed interop/ABI, hangs, gdb) | Opus |
| Post-review fixes, docs, CI, mechanical changes | Sonnet |

If a Sonnet implementer is stuck on a hard problem, escalate that specific problem to an Opus agent
instead of retrying blindly.

## Working alongside other agents
- **Prefer sequential work.** Keep one phase and one PR moving at a time, and put follow-up work
  into the current PR rather than splitting it into extra branches or PRs. Use a `git worktree`
  only when parallel work is truly needed. This keeps usage within session limits.
- **One agent per working tree and branch.** Before starting, check for other agents already
  working here: `git status`, running processes (`pgrep -af 'opencode|claude|UnrealEditor|dotnet'`),
  and files that changed recently. For parallel work use a separate `git worktree` (and branch).
  Never switch branches, stash, reset or commit someone else's uncommitted changes.
- **Commit only your own files**, with an explicit pathspec (`git commit -- <paths>`), when the tree is shared.
- **Displays:** never use `DISPLAY=:1`; that is the owner's live desktop. Start your own Xvfb
  display (`Xvfb :NN`) with a number no other agent is using. Check `pgrep -af Xvfb`; `:97` has
  been used for Unreal and `:100` for NetPrints E2E. Never send global keystrokes or desktop
  shortcuts (Print, Super, …).
- **Unreal Editor:** at most one editor process per Unreal project. Close it gracefully before
  rebuilding: UBT otherwise writes `-0001` libraries that crash the next start. Run the editor
  headless (`-nullrhi` on your own Xvfb) unless the owner asks otherwise.
- **Clean up** the processes you started (editors, Xvfb, watchers) when you finish or stop.

## Code comments
- Match the surrounding code's comment density. No long explanatory blocks between lines of code;
  at most a short one-line comment where something is genuinely non-obvious.
- Put rationale, background and design discussion in commit messages, PR descriptions or
  `specs/` docs, not in the code. This matters most in repositories we contribute to upstream.
- Exception: XML documentation comments (`///` on types and members, or the equivalent doc
  comments in C++) may be as detailed and precise as needed. Don't shorten them for brevity.
- Every public type and member in `src/` has an XML doc comment (`<summary>`, plus `<param>`,
  `<typeparam>`, `<returns>`, `<exception>` where they apply). The build enforces it (CS1591 and the
  related diagnostics are errors in `.editorconfig`). A doc says what the member does and its contract
  (nulls, units, side effects, thrown exceptions), not a restatement of its name. Don't describe a
  member with a plain `//` comment; use `///`. Use `<inheritdoc/>` for overrides and interface
  implementations that add nothing.

## MVVM (CommunityToolkit.Mvvm)
- Use `[ObservableProperty]` (partial properties) for any property with its own backing value; don't write
  `SetProperty`/`OnPropertyChanged` setters by hand.
- Setter side effects go in the generated `partial void On<Name>Changed(oldValue, newValue)` (or
  `On<Name>Changing`) hook, not in a hand-written setter.
- Dependent properties use `[NotifyPropertyChangedFor(nameof(Other))]`; commands that depend on a property use
  `[NotifyCanExecuteChangedFor]`.
- Call `OnPropertyChanged(nameof(X))` manually only when the change doesn't go through an observable setter (a
  model event, a collection change, a value computed from another object). Always `nameof`, never a string.
- No Fody or other IL weaving (`[AlsoNotifyFor]`, `[DependsOn]`, …).
- View model types are named `<Name>ViewModel`, never `<Name>VM`, enforced by `SourceHygieneTests.NoTypeNameEndsInVM`.

## Nullable reference types
The goal is to model and handle null correctly, not to silence warnings. A green build with `!` sprinkled
around only hides future `NullReferenceException`s.
- Don't use the null-forgiving operator (`!`, `null!`, `default!`) to make a warning go away. Fix the cause:
  - a value that can be null gets a nullable type, and callers handle it;
  - a value that can't be null gets a non-nullable type and is guaranteed by construction (constructor,
    `required`, an initializer);
  - an invalid state throws a clear exception at the boundary (`ArgumentNullException.ThrowIfNull`,
    `InvalidOperationException` with a message), instead of failing later with a `NullReferenceException`.
- Use the flow attributes (`[NotNullWhen]`, `[MemberNotNull]`, `[NotNullIfNotNull]`, `[MaybeNullWhen]`) so the
  compiler can see invariants, instead of asserting them with `!`.
- Common cases:
  - `Path.GetDirectoryName(x)!`: handle the null (root or relative path) or validate the path first.
  - `FirstOrDefault()!`: use `First()`, or return a nullable type and handle it.
  - `= null!` on a property: make it nullable, `required`, or set it in the constructor.
  - `Version!` / `Location!`: use a fallback (`?? new Version(0, 0)`).
- `!` is acceptable only where the compiler cannot express a real, local invariant (for example right after a
  check it doesn't track). Then add a short comment saying why, or prefer a `Debug.Assert`/guard. In tests,
  prefer `Assert.NotNull(x)` (xUnit annotates it) to `x!`.
- The model no longer uses DataContract, so members are initialized in constructors or `DocumentMapper`.
- Reviewers list every `!` added in a PR and check each one.

## C# rules
Fix the cause, not the diagnostic. `[ID]` is the analyzer that fails the build (`TreatWarningsAsErrors`
is on); `[review]` means only the reviewer catches it (not promoted to `error` yet, or no analyzer can express it).

### Async
- Async all the way: a method that awaits returns `Task`/`ValueTask`, and so do its callers up to the
  root (`static async Task<int> Main`, `[RelayCommand] async Task`, xUnit `async Task`). [VSTHRD103]
- Never block on async: no `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, with or without a `Task.Run`
  wrapper. Change the caller to async instead. [VSTHRD002, RS0030]
- No `async void`, and no bare `_ = SomethingAsync(...)` discard. A view invokes a `[RelayCommand]
  async Task` through its generated command's `Execute` (never `ExecuteAsync(...).Forget(...)`):
  CommunityToolkit.Mvvm's default `AsyncRelayCommandOptions` await and rethrow a fault on the calling
  (UI) context, reaching the error dialog the same way a synchronous command's exception would. Every
  other fire-and-forget task discard goes through `NetPrints.Editor.Hosting.TaskExtensions.Forget`
  (13 sites in `src/NetPrints.Editor` as of this batch): `Forget(logger)` for a task whose own method
  already reports its failure through the error dialog (a log-only 1030 is then just a safety net), and
  `Forget(EditorContext, title)` for one with no catch of its own, which additionally shows the error
  dialog on the UI thread. [VSTHRD100, VSTHRD101, review]
- Observe every task: await it or return it. Await a `ValueTask` exactly once. [VSTHRD110, CA2012]
- Async methods take `CancellationToken cancellationToken` as the last parameter and forward it to every
  call that accepts one. [CA2016, CA1068]
- `ConfigureAwait(false)` on every await in non-UI libraries (the paths scoped in `.editorconfig`); never
  in Editor, Desktop or tests, which need their context. [VSTHRD111]
- Async methods end in `Async`; a `Task`-returning method never returns `null`. [VSTHRD200, VSTHRD114]
- `new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)`. No async work in
  constructors: use a `static async Task<T> CreateAsync`. `Task.Run` is for CPU-bound work or a
  long-running background loop a constructor starts and forgets (`AutomationAgent`'s accept loop);
  not a substitute for `await`ing I/O you could await directly. [review]
- Marshal to the UI with `await Dispatcher.UIThread.InvokeAsync(...)`, never the blocking `Invoke`. [RS0030]

### Disposal
- The creator owns a disposable: `using` (`await using` for async disposables) for locals, owned fields
  disposed by the owner, injected ones never disposed. [IDISP001, IDISP002, IDISP004, IDISP007, IDISP017]
- A type that owns an `IAsyncDisposable` implements `IAsyncDisposable` itself, all the way up the
  ownership chain to a root that awaits it (async `Main`, Desktop's `ShutdownCoordinator`, test
  `DisposeAsync`). [IDISP006, review]
- Never bridge async disposal into a sync `Dispose()` (`DisposeAsync().AsTask().GetAwaiter().GetResult()`,
  `Task.Run` wrappers). Make the owner `IAsyncDisposable`. Also implement `IDisposable` only if a real
  synchronous cleanup exists. [VSTHRD002, RS0030]
- Disposable classes are `sealed`; otherwise `protected virtual Dispose(bool)` / `DisposeAsyncCore()`.
  Dispose the old value before reassigning a disposable field; never use a disposed instance; `Dispose` is
  idempotent and does not throw. [IDISP003, IDISP016, IDISP025, IDISP026, S3877, S3881]

### Constants and literals
See "Batch rules for implementer agents" below (the "identifier used in more than one place" rule) for
the naming-and-placement convention. [S1192, S109, SourceHygieneTests, CA1861]

### Nullability
See "Nullable reference types" above (no `!`, `null!`, `default!`) and "Batch rules for implementer
agents" below. Guard entry points with `ArgumentNullException.ThrowIfNull`,
`ArgumentException.ThrowIfNullOrEmpty`, `ArgumentOutOfRangeException.ThrowIf*`,
`ObjectDisposedException.ThrowIf` instead of re-checking what the annotations already guarantee.
[SourceHygieneTests, CA1510, CA1511, CA1512, CA1513]

### Exceptions
- Throw the most specific existing type; never `Exception`, `ApplicationException`, `SystemException` or
  `NullReferenceException`. Argument exceptions get `nameof(param)`. [CA2201, CA2208]
- Rethrow with `throw;` or wrap the original as `InnerException` [CA2200]. No empty catch. Catch
  `Exception` only at a boundary (entry point, command handler, extension load/register/dispose,
  `Forget`'s fault path), and log it with the exception object. Log or rethrow, never both; an expected
  problem in the user's project (bad graph, manifest, reference) is a diagnostic with a stable code, not
  an exception. [S2486, S108, S2139, review]
- Never throw from `Dispose`, finalizers, `Equals`, `GetHashCode`, `ToString`, static constructors or
  exception filters. [CA1065]

### Strings, collections, LINQ
- Make culture and comparison explicit: `StringComparison.Ordinal` for ids, paths and codes, and
  `CultureInfo.InvariantCulture` for anything written to generated code, JSON or files. Generated output
  must be byte-identical on every locale. [CA1305, CA1307, CA1309, CA1310, CA1311]
- APIs return `IReadOnlyList<T>`/`IReadOnlyDictionary<TKey,TValue>`/`ImmutableArray<T>`. Collection
  properties are get-only. [CA1002, CA2227]
- Enumerate an `IEnumerable<T>` once; materialize it if you need it again. Prefer `Count`/`Length`/`Any()`
  to `Count()`. [CA1851, CA1827, CA1829, CA1860]

### Types and state
- Classes are `sealed` unless designed for inheritance. Fields assigned only in the constructor are
  `readonly`. No visible mutable static fields. Value-like data (options, results, messages, DTOs) are
  `record`s with `init`/`required`; observable state follows the MVVM rules. [CA1852, S2933, CA2211]
- Inject `TimeProvider`; never use `DateTime.Now`/`UtcNow` or `Thread.Sleep` in `src/`. [RS0030]
- Canvas overlays use `CanvasPopup` (ADR-0004); never a raw `Popup`.
- JSON goes through source-generated `JsonSerializerContext`s only: call the `JsonTypeInfo` overloads of `JsonSerializer`, never the reflection ones (`JsonSerializerOptions` or no options) in `src/`. [RS0030]

### Logging and naming
- Log through `[LoggerMessage]` partials in the namespace's `Log` class (precedent:
  `NetPrints.Serialization/Log.cs`, `NetPrints.Editor/Hosting/Log.cs`) with constant PascalCase templates,
  and pass the exception as the exception argument. Libraries never write to `Console`; `Cli`, `Desktop`,
  `Editor` and `Generator` are entry points, not libraries, and are exempt. [CA1848, CA2254, CA2017,
  CA1727, RS0030]
- Follow `.editorconfig` and the file's existing naming. Constants are PascalCase; generic parameters are
  `T…`. [CA1715]

### Analyzer suppressions (ADR-0003)
- Fix the diagnostic. Never suppress one in code you wrote or touched, and never on new code.
- The only suppression mechanism is a member-level `[SuppressMessage("<Category>", "<ID>",
  Justification = "ADR-0003: <one-line reason>")]` on the smallest containing member, for pre-P1 code the
  analyzer's ownership/intent-tracking genuinely cannot see the truth of, listed in ADR-0003's suppression
  ledger. Never `#pragma warning disable`. `.editorconfig` has no per-file severity override: never add one.
  [SourceHygieneTests: `NoUnlistedSuppressions`]
- Never change analyzer packages, severities or `.editorconfig` to get a green build; propose it in your
  report.

## Commits and tests
- End commit messages with the attribution line(s) your session is configured with; PR bodies
  end with the "Generated with Claude Code" footer when produced by Claude Code.
- Reproduce a bug with a test that fails before fixing it. Tests use xUnit v3 and always pass
  `TestContext.Current.CancellationToken`. UI tests go through page objects and `AutomationIds`,
  with no sleeps.
- CI (`CI` workflow) runs on Linux only and must be green before merge.

## Batch rules for implementer agents
A batch prompt names the task range and pastes the task text; everything below applies to every batch.
- Read only the spec files and sections the prompt names, plus the code you modify. Never read whole specs.
- Build quietly: `dotnet build -v q -tl:off --nologo`. Tests: build first, then
  `dotnet test --no-build --no-progress --no-ansi` (global.json selects Microsoft.Testing.Platform; the output
  lists only failed/skipped tests plus the summary). Run one class with
  `tests/<Project>/bin/Debug/net10.0/<Project> -class '*Name'`; `--treenode-filter` is not accepted.
- Iterate with filtered tests. Run the whole suite once at the end of the batch:
  `dotnet test --solution NetPrints.slnx -c Release --no-build --no-progress --no-ansi -- --ignore-exit-code 8`,
  then `dotnet build -c Release` (0 warnings) and `dotnet format NetPrints.slnx --verify-no-changes`.
- **Full suite includes the Desktop E2E tests, as a second, dedicated run — mirror
  `.github/workflows/ci.yml`'s two-job split, don't fold E2E into the solution-wide command.** The
  solution-wide command above (no `NETPRINTS_E2E`) is CI's main `Test` job: `--ignore-exit-code 8`
  tolerates the Desktop E2E project's "zero tests ran" exit code (its tests self-skip without
  `NETPRINTS_E2E=1`), and `NetPrints.Editor.UITests`' `HeadlessSmokeTests.MinimizeAndRestoreClassWindow`/
  `PanCursor`/`DragFromLists` always skip too (the headless driver has no window manager, real cursor or
  OS drag-drop — see `UiCapabilities`/`SmokeScenarios.Require`) and are tolerated the same way, by not
  passing `--fail-skips`. Then run CI's `e2e` job: `NETPRINTS_E2E=1 dotnet test --project
  tests/NetPrints.Desktop.E2ETests -c Release --no-build --no-progress --no-ansi -- --fail-skips on` (no
  `--ignore-exit-code 8`: with `NETPRINTS_E2E=1` and the X11 driver's full capability set, zero skips is
  the passing state, so `--fail-skips` turns any skip in that project — expected or not — into a failure,
  same as CI). It starts its own Xvfb (see the "Displays" note above); check `pgrep -af Xvfb` first and
  set `NETPRINTS_E2E_DISPLAY_START` if `:100`+ is taken. Report both runs' totals.
- Run the whole suite in the foreground (Bash `timeout` 600000) with the output redirected to a log inside
  the session's scratch/temp dir, then read only its tail. If a command is started in the background you are
  re-invoked when it exits: never poll for it. If you ever need a wait loop, bound it (max ~60 iterations) and
  never `pgrep -f` a string that appears in your own command line; never wait on a file you did not create.
- Golden fixtures in `tests/NetPrints.Core.Tests/Fixtures/Golden/` stay byte-identical unless the task
  says otherwise; a deliberate change is reported with the reason.
- **Never leave changes under `samples/`** unless the task says so. Editor tests load copies of the
  samples into a temp directory, but a manual editor run saves into the real one. Run
  `git status samples/` before committing. A stray saved variable under `samples/` broke 11 tests on
  2026-09-27.
- No `!`, `null!` or `default!`. XML docs on every public API (CS1591 is an error). No long code
  comments: rationale goes in the commit message and `specs/003-core-refactor/implementation-notes.md`.
- An identifier used in more than one place (a diagnostic code, a node/document "kind" string, a display
  name reused elsewhere) gets a named `const`/`static readonly` constant with an XML doc, declared once,
  referenced everywhere — never repeat the literal (precedent: `ExtensionDiagnosticCodes`,
  `TranslationDiagnosticCodes`, `BuiltInNodeKinds`, ADR-0003). SonarAnalyzer.CSharp, IDisposableAnalyzers and
  Microsoft.VisualStudio.Threading.Analyzers run at `error` severity in `src/**.cs` for a curated rule set —
  `S1192`/`S109`/`S1854`/`S1481`/`S2486`/`S108` (`error`) and `S2139`/`S2933` (`warning`), every
  `IDISP001`–`IDISP026` rule, and every `VSTHRD` rule except `VSTHRD111` — regardless of whether every one
  of them fires today, and at `suggestion` (a hint, won't fail the build) for every other rule these
  packages ship, everywhere (including `tests/`); a Sonar rule this
  `.editorconfig` does not name at all (a future package upgrade's new rule) keeps whatever severity the
  package ships it at instead — see ADR-0003 for the probe that verified this. `VSTHRD111` (ConfigureAwait)
  is `error` by default too, but `none` for code that deliberately resumes on the UI thread (view models,
  views, and a short list of other paths — see ADR-0003's layering table). `S109` has no carve-out: it is
  `error` in all of `src/**.cs`, including `NetPrints.Editor`. `dotnet format analyzers --severity info
  --verify-no-changes` scoped to a batch's own changed files (`git diff --name-only origin/master...HEAD`)
  surfaces the `suggestion`-level hits the build doesn't. A violation the curated rules catch gets fixed for
  real, not suppressed — the one exception is a site where the analyzer's ownership/intent-tracking
  genuinely cannot see the truth, suppressed with a member-level
  `[SuppressMessage("<Category>", "<ID>", Justification = "ADR-0003: <one-line reason>")]` on the smallest
  containing member and listed in ADR-0003's suppression ledger — never a `.editorconfig` per-file severity
  override, and never a `#pragma` in the `.cs` file: `git grep -n "pragma warning" -- 'src/*.cs'` stays
  empty, and `git grep -n "SuppressMessage" -- 'src/*.cs'` lists only ledger entries. A "confirmed false
  positive" in code the batch touches is not by itself a reason to suppress: find the real fix (a different
  API, an ownership-transfer pattern the analyzer can see, …) instead
  (`SourceHygieneTests.NoRawDiagnosticCodeLiteralsOutsideTheirConstants` is the actual enforced gate for
  diagnostic/kind-code literals specifically).
- Never open images or windows on the owner's desktop: no `eog`/`xdg-open`/viewers, never `DISPLAY=:1`. To inspect a
  snapshot, use the Read tool on the PNG.
- Never run the whole `Editor.UITests` project under Xvfb blindly (OOM). Headless UITests need no Xvfb; if a
  display is needed, check `pgrep -af Xvfb` first, use your own display number, never `DISPLAY=:1`, and kill it.
- Commit per task with a pathspec of your own files only, tick the task in `tasks.md`, note decisions in
  implementation-notes.md as "Decision: ...", push the branch. Do not stop to ask: decide, record, continue.
- No publishing (tags, nuget, wiki), no posts to other repos, orbion and UnrealSharp are read-only.
- The report is short: what was added, goldens diff, suite totals, decisions, deviations, push confirmation.
