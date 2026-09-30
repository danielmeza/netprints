# Xaml.Behaviors 12.0.7 catalog: index

The catalog covers all 383 behaviors, triggers and actions in the 10 `Xaml.Behaviors.*` packages, split by job so
that one file holds everything a given change needs. Each file starts with worked recipes (real XAML from
`src/NetPrints.Editor` where one exists) and then lists the catalog groups for that job. Read the one file that
matches the job; the others aren't needed.

| The job | File |
|---|---|
| Run a VM command on tap, double-tap or a key; replace a `Tapped`/`DoubleTapped`/`KeyDown` handler | `commands-and-keys.md` |
| Several actions for one event or key; lifecycle (`Loaded`, `DataContextChanged`); an event with no typed behavior | `triggers-and-actions.md` |
| Close a dialog (with or without a result); popups, flyouts, tooltips, notifications | `dialogs-windows-popups.md` |
| Focus on open or show; select all; auto-complete | `focus-and-text.md` |
| Lists, trees, tabs; follow a growing list; scroll into view | `lists-and-scrolling.md` |
| Drag item VMs onto targets; reorder lists | `drag-and-drop.md` |
| Animations, transitions, theme variants, cursors, responsive classes | `visuals-and-layout.md` |
| Automation names and announcements; validation visuals | `accessibility-and-validation.md` |
| No prebuilt fits: write your own behavior | `custom-behaviors.md` |
| Clipboard, files, pickers, network, dialogs or VM state from XAML | `avoid.md` (read it before using any of these) |

To find a type when you don't know the job, search the folder:
`grep -rn -i 'doubletap' .claude/skills/avalonia-xaml/references/behaviors/`.

## Reading a catalog entry

Each group heading gives the group (the source folder it lives in) and its package. The `Use when` line that
follows ends with the NetPrints stance in brackets: `[avoid ...]` means the job belongs in a command or service;
`[not used]`, `[not needed]`, `[no]` or `[never]` means NetPrints has no case for it; anything else is a fit. Each table
row names the type, its kind (Behavior, Trigger or Action) and what it does.

The catalog doesn't list properties. To check a type's property names before you use it, grep the package's XML
docs, for example:

```bash
grep -o 'P:[A-Za-z.]*FocusControlAction\.[A-Za-z]*' \
  ~/.nuget/packages/xaml.behaviors.interactions.custom/12.0.7/lib/net10.0/*.xml
```

A property can live on a base class, so grep the base name too when a property seems missing.
`ExecuteCommandOnKeyDownBehavior`, for example, inherits `Key` and `Gesture` from `ExecuteCommandOnKeyBehaviorBase`,
`EventRoutingStrategy` and `MarkAsHandled` from `ExecuteCommandRoutedEventBehaviorBase`, and `Command`,
`CommandParameter` and `FocusControl` from `ExecuteCommandBehaviorBase`. Build after a change: compiled XAML rejects
an unknown property.

## Packages

| Package | In NetPrints |
|---|---|
| `Xaml.Behaviors.Interactions` | referenced |
| `Xaml.Behaviors.Interactions.Custom` | referenced |
| `Xaml.Behaviors.Interactions.DragAndDrop` | referenced |
| `Xaml.Behaviors.Interactivity` | transitive (the base types of the three above) |
| `Xaml.Behaviors.Animations`, `.Interactions.Draggable`, `.Interactions.Events`, `.Interactions.ReactiveUI`, `.Interactions.Responsive`, `.Interactions.Scripting` | not referenced |

Versions are pinned in `Directory.Packages.props`. Don't add the `Xaml.Behaviors.Avalonia` meta package: its
nuget.org line stops at 11.3, and the per-package 12.x line is the one that targets Avalonia 12 (ADR-0007).
To use a type from an unreferenced package, add a `PackageVersion` there and a `PackageReference` in
`src/NetPrints.Editor/NetPrints.Editor.csproj`, and say why in the PR.

`Xaml.Behaviors.Interactions.Events` duplicates the `InputElement/Triggers` family with typed, trim-safe types
(`XEventTrigger` versus `XTrigger` style). If it is ever referenced, pick one family per file and stay
consistent within it.

## Source

Generated from the `wieslawsoltes/Xaml.Behaviors` source at `45387e3` (release 12.0.7), grouped by source folder,
then split by job. When the packages are upgraded, regenerate the tables for the new version and keep the
recipes.
