# Dialogs, windows and popups

Close a dialog with or without a result, and show popups, flyouts, tooltips and notifications.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Recipes: A dialog closes with no result; A dialog closes with a result
- Catalog: Window; Popup; ToolTips; SplitView; ContextDialogs; Notifications

## Recipes

### A dialog closes with no result

OK or Close buttons on dialogs that return nothing (`ErrorDialog`, `IssuesDialog`, `ReferencesDialog`):

```xml
<!-- src/NetPrints.Editor/Dialogs/ErrorDialog.axaml -->
<Button Content="OK" IsDefault="True"
        AutomationProperties.AutomationId="{x:Static ed:AutomationIds.ErrorOkButton}">
  <Interaction.Behaviors>
    <ButtonClickEventTriggerBehavior>
      <CloseWindowAction />
    </ButtonClickEventTriggerBehavior>
  </Interaction.Behaviors>
</Button>
```

### A dialog closes with a result

`Window.Close(object? dialogResult)` needs a value, and no prebuilt behavior can take one from a VM. This is the
one place D11 keeps a custom behavior. The VM derives from `DialogViewModel<TResult>` (`NetPrints.Editor.Dialogs`) and
calls `RequestClose(result)` from its accept and cancel commands:

```csharp
// src/NetPrints.Editor/Dialogs/SelectTypeDialogViewModel.cs
[RelayCommand]
private void Select() => RequestClose(ResolveSelection());
```

The window attaches `DialogCloseBehavior` once. It watches its `DataContext` for `IDialogCloseSource` and calls
`window.Close(source.Result)`:

```xml
<!-- src/NetPrints.Editor/Dialogs/SelectTypeDialog.axaml -->
<Window xmlns:edb="clr-namespace:NetPrints.Editor.Behaviors" x:DataType="eddialogs:SelectTypeDialogViewModel" ...>
  <Interaction.Behaviors>
    <edb:DialogCloseBehavior />
  </Interaction.Behaviors>
  ...
</Window>
```

The `Window` subclass still implements `IDialogResult<T>` by forwarding `Result` to the VM, so `EditorDialogs` and
tests that read `dialog.Result` work unchanged. `SelectMethodDialog` and `TrustDialog` follow the same pattern.
Tests: `tests/NetPrints.Editor.Tests/Dialogs/DialogViewModelTests.cs` (VM) and
`tests/NetPrints.Editor.UITests/Dialogs/DialogTests.cs` (headless wiring).

## Catalog

### Window · `Xaml.Behaviors.Interactions.Custom`

Use when: Close/show/center a window, drag-move a borderless window [CloseWindowAction for OK/Close buttons].

| Name | Kind | What it does |
|---|---|---|
| `CenterWindowBehavior` | Behavior | Centers the attached window on the current screen when it is attached to the visual tree. |
| `CloseWindowAction` | Action | Closes the associated or target window when executed. |
| `ShowWindowAction` | Action | Shows the specified window when executed. |
| `WindowAction` | Action | An action that performs common window operations. |
| `WindowDragMoveBehavior` | Behavior | A behavior that allows the user to drag the window by clicking and holding the associated control. |
| `WindowStateTrigger` | Trigger | Executes actions when the window state matches the specified value. |

### Popup · `Xaml.Behaviors.Interactions.Custom`

Use when: React to popup open/close [e.g. focus the search box when CanvasPopup opens].

| Name | Kind | What it does |
|---|---|---|
| `PopupClosedTrigger` | Trigger | Executes actions when the Popup.Closed event is raised. |
| `PopupOpenedTrigger` | Trigger | Executes actions when the Popup.Opened event is raised. |

### ToolTips · `Xaml.Behaviors.Interactions.Custom`

Use when: Programmatic tooltip show/hide [rare].

| Name | Kind | What it does |
|---|---|---|
| `HideToolTipAction` | Action | Hides the ToolTip of the associated or target control when executed. |
| `SetToolTipTipAction` | Action | Sets the ToolTip.TipProperty of the associated or target control when executed. |
| `ShowToolTipAction` | Action | Shows the ToolTip of the associated or target control when executed. |
| `ToolTipClosingTrigger` | Trigger | Trigger that listens for the ToolTip.ToolTipClosingEvent. |
| `ToolTipOpeningTrigger` | Trigger | Trigger that listens for the ToolTip.ToolTipOpeningEvent. |

### SplitView · `Xaml.Behaviors.Interactions.Custom`

Use when: SplitView pane glue [not used].

| Name | Kind | What it does |
|---|---|---|
| `SplitViewPaneClosedTrigger` | Trigger | Executes actions when the SplitView.PaneClosed event is raised. |
| `SplitViewPaneClosingTrigger` | Trigger | Executes actions when the SplitView.PaneClosing event is raised. |
| `SplitViewPaneOpenedTrigger` | Trigger | Executes actions when the SplitView.PaneOpened event is raised. |
| `SplitViewPaneOpeningTrigger` | Trigger | Executes actions when the SplitView.PaneOpening event is raised. |
| `SplitViewStateBehavior` | Behavior | Updates SplitView properties based on size conditions. |
| `SplitViewTogglePaneAction` | Action | Toggles the SplitView.IsPaneOpen state of a SplitView when executed. |

### ContextDialogs · `Xaml.Behaviors.Interactions.Custom`

Use when: Inline context dialogs [prefer CanvasPopup per ADR-0004].

| Name | Kind | What it does |
|---|---|---|
| `ContextDialogBehavior` | Behavior | Behavior that manages a context dialog implemented using a Popup. |
| `ContextDialogClosedTrigger` | Trigger | Trigger that listens for the Closed event of ContextDialogBehavior. |
| `ContextDialogOpenedTrigger` | Trigger | Trigger that listens for the Opened event of ContextDialogBehavior. |
| `HideContextDialogAction` | Action | Closes a ContextDialogBehavior when executed. |
| `ShowContextDialogAction` | Action | Opens a ContextDialogBehavior when executed. |

### Notifications · `Xaml.Behaviors.Interactions.Custom`

Use when: WindowNotificationManager toasts from XAML [possible for status toasts, VM decides the message].

| Name | Kind | What it does |
|---|---|---|
| `CloseNotificationAction` | Action | Action that closes the specified NotificationCard. |
| `NotificationManagerBehavior` | Behavior | Provides a INotificationManager for the associated Control. |
| `ShowErrorNotificationAction` | Action | Shows an error notification using an INotificationManager. |
| `ShowInformationNotificationAction` | Action | Shows an information notification using an INotificationManager. |
| `ShowNotificationAction` | Action | Shows a notification using an INotificationManager. |
| `ShowSuccessNotificationAction` | Action | Shows a success notification using an INotificationManager. |
| `ShowWarningNotificationAction` | Action | Shows a warning notification using an INotificationManager. |
