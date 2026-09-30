---
name: avalonia-behaviors
description: "How to wire user interaction in NetPrints Avalonia views without code-behind: keyboard shortcuts and hotkeys, focus and select-all, closing dialogs with a result, drag and drop, list reordering, and auto-scroll or following the newest line. Use when a change adds a shortcut or replaces a Click, KeyDown, SelectionChanged or drag handler or a Focus()/Close()/ScrollToEnd() call, even if behaviors aren't mentioned. Always load avalonia-xaml alongside it."
allowed-tools: Bash(dotnet run ${CLAUDE_SKILL_DIR}/scripts/behavior-props.cs *)
paths:
  - "**/*.axaml"
  - "**/*.axaml.cs"
  - "**/NetPrints.Editor/**/*.cs"
  - "**/NetPrints.Desktop/**/*.cs"
---

# Behaviors and shortcuts in NetPrints

**Invoke the `avalonia-xaml` skill too, before you edit anything, unless it is already loaded in this session.** It
holds the enforced rules (E1-E6, which `XamlHygieneTests` checks at build time) and the defaults that every XAML change
follows; this skill only adds D9 and D11. Rule IDs are shared across the `avalonia-*` skills.

The tiers (Enforced, Default, Consider) and E1-E6 are defined in `avalonia-xaml`. List any Default rule you deviate from in the PR.

## Default

**D9. Keyboard shortcuts are bindings to commands.** Put them in `<Window.KeyBindings>` or `<UserControl.KeyBindings>`, using a
`KeyBinding` with a `Gesture` and a `Command`. A `KeyBinding` fires only while focus is inside that element. Use `HotKey` on the
`Button` or `MenuItem` that already shows the action, and `InputGesture` to display it in menus. Write `Ctrl+` (see Gotchas for macOS). When the focused control swallows the key first (a
TextBox or Nodify), use a tunnel-routed behavior (`ExecuteCommandOnKeyDownBehavior EventRoutingStrategy="Tunnel"`)
before falling back to code-behind.

**D11. Use a prebuilt behavior before you write code-behind or a custom behavior.** NetPrints references
`Xaml.Behaviors.Interactions`, `.Interactions.Custom` and `.Interactions.DragAndDrop` 12 (exact version: `Directory.Packages.props`). Their types sit in the
default `https://github.com/avaloniaui` xmlns, so they need no prefix. Don't add the `Xaml.Behaviors.Avalonia` meta
package: it also pulls in Animations, Draggable, Events and Responsive, which NetPrints doesn't use (ADR-0007). Take the first option that fits:

1. **No behavior at all:** `Command` on the control, or a `KeyBinding` or `HotKey` (D9). An edit with side effects
   stays a `OneWay` binding plus a command (R2-10), never a two-way binding whose setter starts the work.
2. **A typed behavior for the event**, such as `ExecuteCommandOnTappedBehavior` or
   `ExecuteCommandOnKeyDownBehavior Key="Enter"`.
3. **A typed trigger with several actions**, such as `KeyTrigger` + `InvokeCommandAction` + `FocusControlAction`.
4. **`EventTriggerBehavior EventName="..."` + `InvokeCommandAction`**, only when no typed trigger covers the event. It
   finds the event through reflection, so it isn't trim-safe and a typo fails only at runtime.
5. **A custom `StyledElementBehavior<T>`** in `NetPrints.Editor/Behaviors/` with a headless test, only for reusable
   view mechanics that no prebuilt covers (see [custom-behaviors.md](references/custom-behaviors.md)). Check `src/NetPrints.Editor/Behaviors/` for one that already exists.
6. **Code-behind**, only for gesture math and interop (D1).

The typical conversion replaces a handler with a behavior, and the decision moves into a view model (VM) command:
```xml
<!-- Before: KeyDown="OnSearchKeyDown", and a handler that checks e.Key == Key.Enter and calls the VM. -->
<!-- After (src/NetPrints.Editor/Search/NodeSearchView.axaml): -->
<TextBox Text="{Binding SearchText}">
  <Interaction.Behaviors>
    <ExecuteCommandOnKeyDownBehavior Key="Enter" Command="{Binding SelectFirstCommand}" />
  </Interaction.Behaviors>
</TextBox>
```

**Find the behavior by job.** The Xaml.Behaviors catalog is split by job under `references/`. Most files start with
recipes (real repo XAML where one exists), followed by the catalog tables for that job. Open only the file the table
below names; [catalog-guide.md](references/catalog-guide.md) explains the stance tags and the packages. To see a
type's properties, including the ones it inherits, run
`dotnet run ${CLAUDE_SKILL_DIR}/scripts/behavior-props.cs -- <TypeName>` (outside Claude Code the path is
`.claude/skills/avalonia-behaviors/scripts/behavior-props.cs`); part of a name lists similar types. Needs the .NET 10 SDK
and a restored NuGet cache.

| For | Prefer | Recipes and catalog |
|---|---|---|
| Tap / double-tap / right-tap runs a command | `ExecuteCommandOnTappedBehavior`, `…OnDoubleTappedBehavior`, `…OnRightTappedBehavior` | [commands-and-keys.md](references/commands-and-keys.md) |
| A key in one control (Enter in a box) | `ExecuteCommandOnKeyDownBehavior Key="Enter"` (or `Gesture`); `EventRoutingStrategy="Tunnel"` when the control swallows the key | [commands-and-keys.md](references/commands-and-keys.md) |
| Commit a text box on Enter | `LoseFocusOnEnterBehavior` | [commands-and-keys.md](references/commands-and-keys.md) |
| Several actions for one event or key | `KeyTrigger`, `KeyDownTrigger`, `DoubleTappedTrigger`, `ClickEventTrigger`… + `InvokeCommandAction`, `FocusControlAction`, `ChangePropertyAction` | [triggers-and-actions.md](references/triggers-and-actions.md) |
| Lifecycle (Loaded, DataContext changed, theme changed) | `LoadedTrigger`, `DataContextChangedTrigger`, `ActualThemeVariantChangedTrigger` | [triggers-and-actions.md](references/triggers-and-actions.md) |
| One event, one command, no typed behavior fits | `EventTriggerBehavior EventName="..."` + `InvokeCommandAction` (option 4) | [triggers-and-actions.md](references/triggers-and-actions.md) |
| OK or Close closes the dialog with no result | `ButtonClickEventTriggerBehavior` + `CloseWindowAction` | [dialogs-windows-popups.md](references/dialogs-windows-popups.md) |
| Accept/cancel closes the dialog *with* a result | a VM deriving from `DialogVM<TResult>` + the custom `edb:DialogCloseBehavior` on the `Window` | [dialogs-windows-popups.md](references/dialogs-windows-popups.md) |
| Popups and flyouts | `PopupOpenedTrigger`, `HideFlyoutAction`, `ButtonHideFlyoutOnClickBehavior`; canvas popups stay `CanvasPopup` (ADR-0004) | [dialogs-windows-popups.md](references/dialogs-windows-popups.md) |
| Focus on open, show or click; select all | `FocusOnAttachedToVisualTreeBehavior`, `FocusOnVisibleBehavior`, `FocusSelectedItemBehavior`, `TextBoxSelectAllOnGotFocusBehavior` | [focus-and-text.md](references/focus-and-text.md) |
| Follow a growing log or list | `AutoScrollToBottomBehavior` | [lists-and-scrolling.md](references/lists-and-scrolling.md) |
| Automation names and announcements, validation visuals | `ScreenReaderAnnounceAction` and the validation types | [accessibility-and-validation.md](references/accessibility-and-validation.md) |
| Animations, transitions, theme-variant triggers, responsive classes (rarely needed) | see the file | [visuals-and-layout.md](references/visuals-and-layout.md) |
| Drag an item VM from a list onto a target | `ContextDragBehavior Context="{Binding}"` + `ContextDropBehavior Handler=...` (`DropHandlerBase`) | [drag-and-drop.md](references/drag-and-drop.md) |
| Reorder a list by drag | the same `ContextDragBehavior` + `ContextDropBehavior` pair, whose handler calls one undoable VM `Move…` command; not the Draggable package's `ListReorderDragBehavior`/`ItemDragBehavior`, which never call a command, so the model and undo miss the move | [drag-and-drop.md](references/drag-and-drop.md) |

Don't use the behaviors that bypass the VM or its services: clipboard, file system, storage pickers, HTTP,
`SetViewModelProperty`, `ToggleViewModelBoolean`, `ConditionalAction`/`SwitchCaseAction`, `Collections`, `Scripting`
and the dialog behaviors. Their decisions and side effects belong in commands and services (`IClipboardService`,
`IFilePickerService`, `IWindowService`); [avoid.md](references/avoid.md) lists them all.

## Gotchas

- Properties often live on a base class. `ExecuteCommandOnKeyDownBehavior` gets `Key`/`Gesture`,
  `EventRoutingStrategy`/`MarkAsHandled` and `Command`/`CommandParameter` from three different bases; run the script
  before deciding a property doesn't exist.
- `EventTriggerBehavior EventName="..."` finds the event by reflection, so a typo fails only at runtime.
- `AutoScrollToBottomBehavior` does nothing on a `TextBox`. Attach it to a `ScrollViewer` or an `ItemsControl`.

## Known debt

- `Ctrl+` in a gesture is documented to map to Cmd on macOS, but it has not been verified on a Mac.

## Before you finish

1. `dotnet build -v q -tl:off --nologo`. Compiled XAML rejects an unknown behavior property here.
2. A VM unit test covers each command the behavior calls; a headless test (`[AvaloniaFact]`) covers the wiring when
   it is more than one line (a trigger with several actions, a custom behavior, a tunnel-routed key).
3. Build the test project and run `tests/NetPrints.Core.Tests/bin/Debug/net10.0/NetPrints.Core.Tests -class '*XamlHygieneTests'`: E3 fails on a command-shaped handler left in XAML. Fix and re-run until green.

## Reference files and scripts

- `references/<job>.md`: recipes and catalog tables for one job. The D11 table names the file; open only that one.
- [references/catalog-guide.md](references/catalog-guide.md): how to read a catalog entry and its stance tag, the
  packages, and where the catalog came from.
- `scripts/behavior-props.cs`: prints a type's properties with their declaring base. Run it; don't read it; it covers Xaml.Behaviors types, while the `avalonia-docs` MCP tools cover Avalonia core types.
