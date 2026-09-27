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
- Run the whole suite in the foreground (Bash `timeout` 600000) with the output redirected to a log inside
  the session's scratch/temp dir, then read only its tail. If a command is started in the background you are
  re-invoked when it exits: never poll for it. If you ever need a wait loop, bound it (max ~60 iterations) and
  never `pgrep -f` a string that appears in your own command line; never wait on a file you did not create.
- Golden fixtures in `tests/NetPrints.Core.Tests/Fixtures/Golden/` stay byte-identical unless the task
  says otherwise; a deliberate change is reported with the reason.
- No `!`, `null!` or `default!`. XML docs on every public API (CS1591 is an error). No long code
  comments: rationale goes in the commit message and `specs/003-core-refactor/implementation-notes.md`.
- An identifier used in more than one place (a diagnostic code, a node/document "kind" string, a display
  name reused elsewhere) gets a named `const`/`static readonly` constant with an XML doc, declared once,
  referenced everywhere — never repeat the literal (precedent: `ExtensionDiagnosticCodes`,
  `TranslationDiagnosticCodes`, `BuiltInNodeKinds`, ADR-0003). SonarAnalyzer.CSharp, IDisposableAnalyzers and
  Microsoft.VisualStudio.Threading.Analyzers run at `error` severity in `src/**.cs` for a curated rule set —
  `S1192`/`S109`/`S1854`/`S1481`, every `IDISP001`–`IDISP026` rule, and every `VSTHRD` rule except
  `VSTHRD111` — regardless of whether every one of them fires today, and at `suggestion` (a hint, won't fail
  the build) for every other rule these packages ship, everywhere (including `tests/`); a Sonar rule this
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
