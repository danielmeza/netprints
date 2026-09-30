# 0007: XAML practices for the Avalonia editor

## Status

Accepted (2026-09-28).

## Context

`src/NetPrints.Editor` had no written XAML conventions beyond ADR-0004's popup rule and whatever a
given PR happened to do. A research pass (`avalonia-xaml-research.md`, batch X1) surveyed every
`.axaml`/`.axaml.cs` file against the Avalonia 12 docs and the Xaml.Behaviors library and found a
recurring pattern worth codifying before P3a (the editor-shell rewrite) touches most of these files
again: several code-behind event handlers exist only to call one view-model command
(`OnMethodTapped` being the clearest case, added by batch D1 and migrated away by this ADR), several
views hardcode a color that should be a Light/Dark token, and a few automation ids and icon-only
buttons predate the `AutomationIds`/accessibility conventions used elsewhere.

The owner set three ground rules for any such policy:

1. Prefer XAML (bindings, VM commands, styles, behaviors) over code-behind. Code-behind stays for
   complex view-only logic — drag gestures, pointer math. Every UI action is one VM command, unit
   tested without the view.
2. The rules are tiered and not too strict. Converters are fine: a converter may map app or business
   state to visuals (color, brush, icon, visibility, style); computing or deciding the state
   (validation, rules, service or model calls, side effects) stays in the VM. The VM decides *what*
   the state is; a converter may decide *how it looks*.
3. Prefer a prebuilt behavior (Xaml.Behaviors) first, a custom behavior second, and code-behind only
   as a last resort.

## Decision

> **Amended 2026-09-29** (PR #10): the single `avalonia-xaml` skill was split into the three skills
> below, with rule IDs unchanged, and the package-choice reasoning was corrected: the
> `Xaml.Behaviors.Avalonia` meta package does have 12.x releases; it is avoided for the unused
> packages it pulls in, not for a missing 12.x line.

- **Three `avalonia-*` skills** (`.claude/skills/`) are the single place these rules live. They share one
  rule numbering, so an ID cited in code, a test or a PR means the same thing in any of them, and each
  skill's description triggers it only for the kind of change it covers:
  - `avalonia-xaml`: E1-E6 and D1-D4, D6, D10, D12-D16, loaded for every change to `.axaml`,
    `.axaml.cs` or a VM command bound from XAML;
  - `avalonia-behaviors`: D9 and D11, loaded when a change adds or replaces an event handler, a
    shortcut, focus, drag and drop or a dialog result;
  - `avalonia-styling`: D5, D7 and D8, loaded when a change adds a converter, a color, a style, a
    ControlTheme or a theme resource.
  They state the three owner rules above as tiers:
  - **Enforced** (E1-E6 below): a test fails the build. An exception needs an allowlist entry with a
    reason.
  - **Default** (D1-D16): follow it unless there's a reason not to, stated in the PR.
  - **Consider**: tips, not rules.
  The full generated catalog of the 383 Xaml.Behaviors 12.0.7 types across the 10 packages lives in
  `avalonia-behaviors/references/`, split by job (commands and keys, triggers and actions, dialogs,
  focus, lists, drag and drop, …) with worked recipes in each file, so a model loads only the file its
  change needs; D11 keeps the order of preference, a "for X, prefer Y" table that routes to those
  files, and the rule for when a prebuilt behavior beats a custom one or code-behind (rule 3).
- **Package choice.** NetPrints references `Xaml.Behaviors.Interactions`,
  `Xaml.Behaviors.Interactions.Custom` and `Xaml.Behaviors.Interactions.DragAndDrop`, all at 12.0.7
  (the latest 12.x release; floor is Avalonia >= 12.0.5, repo runs 12.1.3). Not the
  `Xaml.Behaviors.Avalonia` meta package: its 12.x releases (12.0.7 included) also pull in
  `Xaml.Behaviors.Animations`, `.Interactions.Draggable`, `.Interactions.Events` and
  `.Interactions.Responsive`, which NetPrints does not use. `Xaml.Behaviors.Interactivity` (the base
  behavior types) comes in transitively as a shared dependency of the three referenced packages, so it is
  not referenced directly; `EventTriggerBehavior` and `InvokeCommandAction` themselves live in
  `Xaml.Behaviors.Interactions`. All three packages' types resolve in the default `https://github.com/avaloniaui`
  xmlns, so no prefix is needed in a view.
- **Enforced checks (`XamlHygieneTests`, next to `SourceHygieneTests`, same project and pattern).**
  Each rule parses every `src/**/*.axaml` file with `XDocument.Load(path, LoadOptions.SetLineInfo)` and
  reports `file:line`:
  - E1: no `x:CompileBindings="False"` or `{ReflectionBinding}` (compiled bindings only).
  - E2: no hex or named color literal on a brush/color property outside a `ThemeDictionaries` block
    (`Transparent` is exempt, since it only makes an element hit-testable).
  - E3: no `Click`/`Tapped`/`DoubleTapped`/`Key*`/similar handler wired to a bare method name in XAML;
    pointer and `DragDrop.*` handlers are exempt (gestures are view mechanics, rule 1's carve-out).
  - E4: every `AutomationProperties.AutomationId` is `{x:Static AutomationIds.*}`, never a literal.
  - E5: an icon-only button (`Button`-family, no `Content`, every child an icon element) has
    `AutomationProperties.Name` or `LabeledBy`.
  - E6: no `{StaticResource key}` where `key` is declared under a `ThemeDictionaries` (it must be
    `DynamicResource`, or the lookup throws or freezes one theme variant).
  - **Allowlist policy:** E1, E2, E3 and E5 each carry an explicit `Dictionary<"file:line", reason>`
    seeded with every violation found when the rule shipped (2, 7, 15 and 12 entries respectively,
    after this batch's own migration removed two E3 entries — see Consequences). Each test also fails
    if an allowlisted key is no longer produced by the scan, so an allowlist can only shrink: fixing a
    violation and forgetting to remove its entry breaks the build, the same direction of failure
    `NullForgivingAllowlist` and `SuppressionAllowlist` use in `SourceHygieneTests`. E4 and E6 ship at
    zero violations, so they get no allowlist at all — they are pure regression guards.
- **First migration.** `ClassEditorWindow`'s method/constructor row `Tapped="OnMethodTapped"`
  (batch D1) is replaced with `EventTriggerBehavior EventName="Tapped"` + `InvokeCommandAction
  Command="...OpenMethodCommand" CommandParameter="{Binding}"` in XAML, and the handler is deleted.
  This is the "no prebuilt fits, no reusable custom behavior is worth writing for one call site"
  branch of rule 3: `Xaml.Behaviors.Interactions.Custom`'s `ExecuteCommandOnTappedBehavior` is the
  more direct fit and is left for the next batch that touches this file, once the row markup is
  otherwise stable. `EventTriggerBehavior` is reflection-based and not trim-safe, which is exactly why
  `avalonia-behaviors` lists it as a fallback rather than a first choice.
- **Dialog-close pattern (batch X2b).** A dialog that closes with no result (`ErrorDialog`,
  `IssuesDialog`, `ReferencesDialog`) fits the prebuilt `ButtonClickEventTriggerBehavior` +
  `CloseWindowAction` pair directly. A dialog that closes *with* a result (`SelectMethodDialog`,
  `SelectTypeDialog`, `TrustDialog`) needs one more piece, since no prebuilt behavior can hand a VM
  value back to `Window.Close(object?)`: each dialog gets a small VM deriving from
  `DialogVM<TResult>` (`NetPrints.Editor.Dialogs`), whose accept/cancel commands call
  `RequestClose(result)`, which sets `Result` and raises `IDialogCloseSource.CloseRequested`. One
  reusable custom behavior, `DialogCloseBehavior` (`NetPrints.Editor.Behaviors`, D11 "no prebuilt
  fits, custom second"), sits on the dialog `Window`, watches its own (auto-synced) `DataContext` for
  `IDialogCloseSource` and calls `window.Close(source.Result)` once `CloseRequested` fires. The
  `Window` subclass keeps its existing `IDialogResult<T>`/`Result` surface (`EditorDialogs.ShowAsync`'s
  no-owner fallback and existing tests read `dialog.Result` directly) by forwarding to the VM's
  `Result`; `SelectTypeDialog.ResolveSelection()` similarly forwards to the VM, which now owns that
  logic (D16).

## Consequences

- New XAML work has one set of skills to load instead of re-deriving conventions per PR, and a build-time
  gate that only tightens (an allowlist can shrink but the check for a *new* violation is always live).
- The remaining catalogued violations (2 for E1, 7 for E2 after this batch fixed nothing there, 13 for
  E3 after removing the two `OnMethodTapped` sites, 12 for E5) were batch X2's burn-down list; the
  roadmap (P3a) recorded this so it was not lost between batches. Batch X2a emptied E1, E2 and E5;
  batch X2b emptied E3 (15 seeded entries at that point, all fixed — none needed the "genuinely must
  stay in code-behind" carve-out rule 1 allows for gestures like Nodify panning or Ctrl+Space, since
  pointer/`DragDrop.*` handlers were never in E3's scan to begin with). Every `XamlHygieneTests`
  allowlist is now empty.
- Referencing three Xaml.Behaviors packages instead of the meta package means a future package that
  NetPrints starts needing (say, `Xaml.Behaviors.Interactions.Responsive` for adaptive classes) must be
  added explicitly; this is intentional (rule 2's "not too strict" cuts against unused dependencies,
  not against explicit ones).
- `EventTriggerBehavior` is reflection-based; if trimming the editor ever becomes a goal, every use
  found by grepping for it is a candidate to replace with a typed trigger or behavior first.
