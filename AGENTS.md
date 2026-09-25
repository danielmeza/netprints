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
- Serializer-populated members (legacy DataContract) are nullable, or initialized in `[OnDeserializing]`.
- Reviewers list every `!` added in a PR and check each one.

## Commits and tests
- End commit messages with the attribution line(s) your session is configured with; PR bodies
  end with the "Generated with Claude Code" footer when produced by Claude Code.
- Reproduce a bug with a test that fails before fixing it. Tests use xUnit v3 and always pass
  `TestContext.Current.CancellationToken`. UI tests go through page objects and `AutomationIds`,
  with no sleeps.
- CI (`CI` workflow) runs on Linux only and must be green before merge.
