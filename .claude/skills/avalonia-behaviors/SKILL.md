---
name: avalonia-behaviors
description: "How to wire interaction in NetPrints Avalonia 12 views without code-behind: Xaml.Behaviors 12.0.7 behaviors, triggers and actions (full catalog split by job, with recipes from the repo), keyboard shortcuts (KeyBinding, HotKey, tunnel-routed keys), focus and select-all, dialogs that close with or without a result, drag and drop and list reordering, and auto-scroll. Use it whenever you would add or replace an event handler (Click, Tapped, DoubleTapped, KeyDown, SelectionChanged, pointer or drag events) or a Focus()/SelectAll()/Close()/ScrollToEnd() call in *.axaml.cs, add a shortcut, or pick a behavior, even if the request doesn't mention behaviors."
---

# Behaviors and shortcuts in NetPrints

This skill holds rules D9 and D11 of the shared XAML rules. The enforced rules (E1-E6) and the other defaults live in
`avalonia-xaml`, which applies to the same change; load it too. Rule IDs are shared across the `avalonia-*` skills.

The rules come in three tiers:
- **Enforced**: a test checks it (`XamlHygieneTests`), so a violation fails the build. To make an exception, add an allowlist entry with a reason.
- **Default**: follow it unless you have a reason not to. If you deviate, give the reason in the PR description.
- **Consider**: tips that are worth knowing. Nobody has to follow them.

## Default

**D9. Keyboard shortcuts are bindings to commands.** Put them in `<Window.KeyBindings>` or `<UserControl.KeyBindings>`, using a
`KeyBinding` with a `Gesture` and a `Command`. A `KeyBinding` fires only while focus is inside that element. Use `HotKey` on the
`Button` or `MenuItem` that already shows the action, and `InputGesture` to display it in menus. Write `Ctrl+`: Avalonia maps
it to Cmd on macOS (per the docs; confirm on the Mac mini). When the focused control swallows the key first (a
TextBox or Nodify), use a tunnel-routed behavior (`ExecuteCommandOnKeyDownBehavior EventRoutingStrategy="Tunnel"`)
before falling back to code-behind.

**D11. Use a prebuilt behavior before you write code-behind or a custom behavior.** NetPrints references
`Xaml.Behaviors.Interactions`, `.Interactions.Custom` and `.Interactions.DragAndDrop` 12.0.7. Their types sit in the
default `https://github.com/avaloniaui` xmlns, so they need no prefix. Don't add the `Xaml.Behaviors.Avalonia` meta
package: its line stops at 11.3 (ADR-0007). Take the first option that fits:

1. **No behavior at all:** `Command` on the control, `KeyBinding` or `HotKey` (D9), or a two-way binding whose VM
   `On<Name>Changed` hook reacts.
2. **A typed behavior for the event**, such as `ExecuteCommandOnTappedBehavior` or
   `ExecuteCommandOnKeyDownBehavior Key="Enter"`.
3. **A typed trigger with several actions**, such as `KeyTrigger` + `InvokeCommandAction` + `FocusControlAction`.
4. **`EventTriggerBehavior EventName="..."` + `InvokeCommandAction`**, only when no typed trigger covers the event. It
   finds the event through reflection, so it isn't trim-safe and a typo fails only at runtime.
5. **A custom `StyledElementBehavior<T>`** in `NetPrints.Editor/Behaviors/` with a headless test, only for reusable
   view mechanics that no prebuilt covers. Today that is only `DialogCloseBehavior`.
6. **Code-behind**, only for gesture math and interop (D1).

The typical conversion replaces a handler with a behavior, and the decision moves into a VM command:
```xml
<!-- Before: KeyDown="OnSearchKeyDown", and a handler that checks e.Key == Key.Enter and calls the VM. -->
<!-- After (src/NetPrints.Editor/Search/NodeSearchView.axaml): -->
<TextBox Text="{Binding SearchText}">
  <Interaction.Behaviors>
    <ExecuteCommandOnKeyDownBehavior Key="Enter" Command="{Binding SelectFirstCommand}" />
  </Interaction.Behaviors>
</TextBox>
```

**Find the behavior by job.** The catalog of all 383 types is split by job under `references/`. Each file
starts with worked recipes taken from real repo XAML, followed by the catalog tables for that job. Open only the
file for your job; `references/README.md` is the index and explains how to check property names.

| For | Prefer | Recipes and catalog |
|---|---|---|
| Tap / double-tap / right-tap runs a command | `ExecuteCommandOnTappedBehavior`, `…OnDoubleTappedBehavior`, `…OnRightTappedBehavior` | `commands-and-keys.md` |
| A key in one control (Enter in a box) | `ExecuteCommandOnKeyDownBehavior Key="Enter"` (or `Gesture`); `EventRoutingStrategy="Tunnel"` when the control swallows the key | `commands-and-keys.md` |
| Commit a text box on Enter | `LoseFocusOnEnterBehavior` | `commands-and-keys.md` |
| Several actions for one event or key | `KeyTrigger`, `KeyDownTrigger`, `DoubleTappedTrigger`, `ClickEventTrigger`… + `InvokeCommandAction`, `FocusControlAction`, `ChangePropertyAction` | `triggers-and-actions.md` |
| Lifecycle (Loaded, DataContext changed, theme changed) | `LoadedTrigger`, `DataContextChangedTrigger`, `ActualThemeVariantChangedTrigger` | `triggers-and-actions.md` |
| One event, one command, no typed behavior fits | `EventTriggerBehavior EventName="..."` + `InvokeCommandAction` (option 4) | `triggers-and-actions.md` |
| OK or Close closes the dialog with no result | `ButtonClickEventTriggerBehavior` + `CloseWindowAction` | `dialogs-windows-popups.md` |
| Accept/cancel closes the dialog *with* a result | a VM deriving from `DialogVM<TResult>` + the custom `edb:DialogCloseBehavior` on the `Window` | `dialogs-windows-popups.md` |
| Popups and flyouts | `PopupOpenedTrigger`, `HideFlyoutAction`, `ButtonHideFlyoutOnClickBehavior`; canvas popups stay `CanvasPopup` (ADR-0004) | `dialogs-windows-popups.md` |
| Focus on open, show or click; select all | `FocusOnAttachedToVisualTreeBehavior`, `FocusOnVisibleBehavior`, `FocusSelectedItemBehavior`, `TextBoxSelectAllOnGotFocusBehavior` | `focus-and-text.md` |
| Follow a growing log or list | `AutoScrollToBottomBehavior` | `lists-and-scrolling.md` |
| Drag an item VM from a list onto a target | `ContextDragBehavior Context="{Binding}"` + `ContextDropBehavior Handler=...` (`DropHandlerBase`) | `drag-and-drop.md` |
| Reorder a list by drag | the same `ContextDragBehavior` + `ContextDropBehavior` pair, whose handler calls one undoable VM `Move…` command; not the Draggable package's `ListReorderDragBehavior`/`ItemDragBehavior`, which never call a command, so the model and undo miss the move | `drag-and-drop.md` |

Don't use the behaviors that bypass the VM or its services: clipboard, file system, storage pickers, HTTP,
`SetViewModelProperty`, `ToggleViewModelBoolean`, `ConditionalAction`/`SwitchCaseAction`, `Collections`, `Scripting`
and the dialog behaviors. Their decisions and side effects belong in commands and services (`IClipboardService`,
`IFilePickerService`, `IWindowService`); `avoid.md` lists them all.

## Before you finish

1. `dotnet build -v q -tl:off --nologo`. Compiled XAML rejects an unknown behavior property here.
2. A VM unit test covers each command the behavior calls; a headless test (`[AvaloniaFact]`) covers the wiring when
   it is more than one line (a trigger with several actions, a custom behavior, a tunnel-routed key).
3. Run `XamlHygieneTests`: E3 fails the build on a command-shaped handler left in XAML.

## Reference files

Load only the file for the job; the D11 table routes to it.
- `references/README.md`: the index of the Xaml.Behaviors catalog, the package list, and how to check a behavior's
  property names in the package XML docs.
- `references/<job>.md`: recipes (real XAML from `src/NetPrints.Editor` where one exists) and the catalog tables for
  one job.
