# Triggers and actions

Chain several actions on one event or key, react to lifecycle events, or handle an event no typed behavior covers.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Recipes: One key, two actions; No typed behavior fits the event
- Catalog: InputElement/Triggers; InputElement/Actions; Actions; Core (Interactions); Core (Custom); Gestures; Show; Control; Events

## Recipes

### One key, two actions

Down in the search box highlights the first result (a VM decision) and moves focus to the list (view mechanics).
No single behavior does both, so a trigger runs two actions in order.

```xml
<!-- src/NetPrints.Editor/Search/NodeSearchView.axaml -->
<KeyTrigger Key="Down">
  <InvokeCommandAction Command="{Binding HighlightFirstCommand}" />
  <FocusControlAction TargetControl="ResultList" />
</KeyTrigger>
```

`TargetControl` takes the `x:Name` of the control to focus, which is one of the reasons D15 allows a name.

### No typed behavior fits the event

`ComboBox.SelectionChanged` and `ToggleSwitch.IsCheckedChanged` have no `ExecuteCommandOn…` behavior. Use the
generic trigger, which finds the event by name through reflection (not trim-safe, so it is the last prebuilt
option):

```xml
<!-- a view that binds a chooser to a read-only property -->
<ComboBox x:Name="BinaryTypeChooser" SelectedItem="{Binding OutputBinaryType, Mode=OneWay}">
  <Interaction.Behaviors>
    <EventTriggerBehavior EventName="SelectionChanged">
      <InvokeCommandAction Command="{Binding SetOutputTypeCommand}"
                           CommandParameter="{Binding #BinaryTypeChooser.SelectedItem}" />
    </EventTriggerBehavior>
  </Interaction.Behaviors>
</ComboBox>
```

Keep the binding `OneWay`, as here, and let the command apply the change (R2-10). A two-way binding whose setter
starts an edit bypasses the command, its test and undo, and two quick changes can race.

## Catalog

### InputElement/Triggers · `Xaml.Behaviors.Interactions.Custom`

Use when: Typed input-event triggers (Click, KeyDown, KeyGesture, pointer, focus) that run any action list [use when you need more than one action or a non-command action].

| Name | Kind | What it does |
|---|---|---|
| `ClickEventTrigger` | Trigger | Trigger that emulates Button click semantics on any Control. |
| `DoubleTappedTrigger` | Trigger | Trigger that runs its actions on the DoubleTapped event. |
| `GotFocusTrigger` | Trigger | Trigger that runs its actions on the GotFocus event. |
| `HoldingTrigger` | Trigger | Trigger that runs its actions on the Holding event. |
| `KeyDownTrigger` | Trigger | Trigger that runs its actions on the KeyDown event. |
| `KeyGestureTrigger` | Trigger | Trigger that listens for a key gesture. |
| `KeyTrigger` | Trigger | A trigger that listens for key events and executes its actions when the specified key or gesture is detected. |
| `KeyUpTrigger` | Trigger | Trigger that runs its actions on the KeyUp event. |
| `LostFocusTrigger` | Trigger | Trigger that runs its actions on the LostFocus event. |
| `PointerCaptureLostTrigger` | Trigger | Trigger that runs its actions on the PointerCaptureLost event. |
| `PointerEnteredTrigger` | Trigger | Trigger that runs its actions on the PointerEntered event. |
| `PointerExitedTrigger` | Trigger | Trigger that runs its actions on the PointerExited event. |
| `PointerMovedTrigger` | Trigger | Trigger that runs its actions on the PointerMoved event. |
| `PointerPressedTrigger` | Trigger | Trigger that runs its actions on the PointerPressed event. |
| `PointerReleasedTrigger` | Trigger | Trigger that runs its actions on the PointerReleased event. |
| `PointerWheelChangedTrigger` | Trigger | Trigger that runs its actions on the PointerWheelChanged event. |
| `TappedTrigger` | Trigger | Trigger that runs its actions on the Tapped event. |
| `TextInputMethodClientRequestedTrigger` | Trigger | Trigger that runs its actions on the TextInputMethodClientRequested event. |
| `TextInputTrigger` | Trigger | Trigger that runs its actions on the TextInput event. |

### InputElement/Actions · `Xaml.Behaviors.Interactions.Custom`

Use when: Capture/release the pointer from a trigger.

| Name | Kind | What it does |
|---|---|---|
| `CapturePointerAction` | Action | Captures the pointer. |
| `ReleasePointerCaptureAction` | Action | Releases the pointer capture. |

### Actions · `Xaml.Behaviors.Interactions.Custom`

Use when: View-only reactions: focus a control, add/remove a style class, show/hide popups/flyouts/controls, change a property. [Good fit for focus and visual-state glue].

| Name | Kind | What it does |
|---|---|---|
| `AddClassAction` | Action | Adds a specified AddClassAction.ClassName to the StyledElement.Classes collection when invoked. |
| `CallMethodAsyncAction` | Action | An action that calls an asynchronous method on a specified object when invoked. |
| `ChangeAvaloniaPropertyAction` | Action | An action that will change a specified Avalonia property to a specified value when invoked. |
| `DelayedShowControlAction` | Action | Shows the associated or target control after a specified delay when executed. |
| `FocusControlAction` | Action | Focuses the associated or target control when executed. |
| `FocusNextElementAction` | Action | An action that moves focus to the next element in the tab order. |
| `HideControlAction` | Action | Hides the associated or target control when executed. |
| `HideFlyoutAction` | Action | Hides a FlyoutBase when executed. |
| `HidePopupAction` | Action | Hides a Popup when executed. |
| `LaunchUriAction` | Action | An action that launches a URI using the system's default handler. |
| `PopupAction` | Action | An action that displays a Popup for the associated control when executed. |
| `RemoveClassAction` | Action | Removes a specified RemoveClassAction.ClassName from StyledElement.Classes collection when invoked. |
| `RemoveElementAction` | Action | Removes the associated or target element from its parent when executed. |
| `ScreenshotAction` | Action | An action that captures a screenshot of a control and saves it to a file. |
| `ScrollToControlAction` | Action | An action that brings a target control into view (scrolls to it). |
| `SetEnabledAction` | Action | Sets the InputElement.IsEnabled property of a control when executed. |
| `SetThemeVariantAction` | Action | Sets the ThemeVariantScope.RequestedThemeVariant on the target control when executed. |
| `ShowContextMenuAction` | Action | Shows the ContextMenu of the associated or target control when executed. |
| `ShowControlAction` | Action | Shows the associated or target control when executed. |
| `ShowFlyoutAction` | Action | Shows a FlyoutBase when executed. |
| `ShowPopupAction` | Action | Shows a Popup when executed. |
| `ToggleClassAction` | Action | Toggles a specified ToggleClassAction.ClassName in the StyledElement.Classes collection when invoked. |

### Core · `Xaml.Behaviors.Interactions`

Use when: General glue: run a command/method or set a property when an event fires or data changes; debounce/delay/group actions. [InvokeCommandAction is the default action; prefer typed triggers over EventTriggerBehavior's string EventName].

| Name | Kind | What it does |
|---|---|---|
| `AsyncActionGroup` | Action | An action that executes its child actions asynchronously, either in sequence or in parallel. |
| `CallMethodAction` | Action | An action that calls a method on a specified object when invoked. |
| `ChangePropertyAction` | Action | An action that will change a specified property to a specified value when invoked. |
| `DataTrigger` | Trigger | A behavior that performs actions when the bound data meets a specified condition. |
| `DataTriggerBehavior` | Trigger | A behavior that performs actions when the bound data meets a specified condition. |
| `DebounceAction` | Action | An action that will execute its child actions after a specified delay. If invoked again before the delay elapses, the timer is reset. |
| `DelayAction` | Action | An action that waits for a specified duration. |
| `EventTrigger` | Trigger | A behavior that listens for a specified event on its source and executes its actions when that event is fired. |
| `EventTriggerBehavior` | Trigger | A behavior that listens for a specified event on its source and executes its actions when that event is fired. |
| `InvokeCommandAction` | Action | Executes a specified System.Windows.Input.ICommand when invoked. |
| `MultiDataTrigger` | Trigger | A behavior that performs actions when all bound data conditions are satisfied. |
| `MultiDataTriggerBehavior` | Trigger | A behavior that performs actions when all bound data conditions are satisfied. |
| `ObservableStreamBehavior` | Behavior | A behavior that subscribes to an IObservable{T} and executes actions on OnNext, OnError, and OnCompleted. |
| `TaskCompletedTrigger` | Trigger | A trigger that invokes its actions when the supplied task completes. |
| `ThrottleAction` | Action | An action that will execute its child actions immediately, but ignores subsequent invocations until a specified interval has passed. |
| `TimerTrigger` | Trigger | A trigger that invokes its actions after a specified interval. |

### Core · `Xaml.Behaviors.Interactions.Custom`

Use when: Lifecycle triggers (Loaded, Unloaded, DataContextChanged, attached to tree, theme changed) and base classes [use a trigger instead of DataContextChanged/Loaded handlers].

| Name | Kind | What it does |
|---|---|---|
| `ActualThemeVariantChangedTrigger` | Trigger | Trigger that runs its actions on the ActualThemeVariantChanged event. |
| `AsyncLoadBehavior` | Behavior | Behavior that calls an asynchronous method when the associated control is loaded. |
| `AttachedToLogicalTreeTrigger` | Trigger | Trigger that runs its actions on the AttachedToLogicalTree event. |
| `AttachedToVisualTreeTrigger` | Trigger | A trigger that executes when the associated object is attached to the visual tree. |
| `BindingBehavior` | Behavior | Applies a binding to a target property when the control is attached to the visual tree. |
| `BindingTriggerBehavior` | Trigger | A behavior that performs actions when the bound data meets a specified condition. |
| `DataContextChangedTrigger` | Trigger | Trigger that runs its actions on the DataContextChanged event. |
| `DelayedLoadTrigger` | Trigger | Invokes its actions after the associated control is attached to the visual tree and a delay elapses. |
| `DetachedFromLogicalTreeTrigger` | Trigger | Trigger that runs its actions on the DetachedFromLogicalTree event. |
| `DetachedFromVisualTreeTrigger` | Trigger | Trigger that runs its actions on the DetachedFromVisualTree event. |
| `IfElseTrigger` | Trigger | A behavior that executes different collections of actions depending on the specified condition. |
| `InitializedTrigger` | Trigger | Trigger that runs its actions on the Initialized event. |
| `LaunchUriOrFileAction` | Action | An action that will launch a process to open a file or URI. For files, this action will launch the default program for the given file extension. A URI will open in a web browser. |
| `LoadedTrigger` | Trigger | Trigger that runs its actions on the Loaded event. |
| `ObservableTriggerBehavior<T>` | Trigger | A trigger that subscribes to an IObservable{T} and executes its actions whenever a new value is produced. The emitted value is exposed through the Value property and passed to the actions as a parameter. |
| `PropertyChangedTrigger` | Trigger | Represents a trigger that performs actions when the bound data have changed. |
| `ResourcesChangedTrigger` | Trigger | Trigger that runs its actions on the ResourcesChanged event. |
| `RoutedEventTriggerBehavior` | Trigger | A behavior that listens for a RoutedEvent event on its source and executes its actions when that event is fired. |
| `SizeChangedTrigger` | Trigger | A behavior that triggers its actions whenever the associated control size changes. |
| `UnloadedTrigger` | Trigger | Trigger that runs its actions on the Unloaded event. |
| `ValueChangedTriggerBehavior` | Trigger | A behavior that performs actions when the bound data produces new value. |

### Gestures · `Xaml.Behaviors.Interactions.Custom`

Use when: Triggers for gesture routed events (tap, hold, pinch, pull, scroll, touchpad) to chain arbitrary actions.

| Name | Kind | What it does |
|---|---|---|
| `DoubleTappedGestureTrigger` | Trigger | Trigger that runs its actions on the DoubleTapped event. |
| `HoldingGestureTrigger` | Trigger | Trigger that runs its actions on the Holding event. |
| `PinchEndedGestureTrigger` | Trigger | Trigger that runs its actions on the PinchEnded event. |
| `PinchGestureTrigger` | Trigger | Trigger that runs its actions on the Pinch event. |
| `PointerTouchPadGestureMagnifyGestureTrigger` | Trigger | Trigger that runs its actions on the PointerTouchPadGestureMagnify event. |
| `PointerTouchPadGestureRotateGestureTrigger` | Trigger | Trigger that runs its actions on the PointerTouchPadGestureRotate event. |
| `PointerTouchPadGestureSwipeGestureTrigger` | Trigger | Trigger that runs its actions on the PointerTouchPadGestureSwipe event. |
| `PullGestureEndedGestureTrigger` | Trigger | Trigger that runs its actions on the PullGestureEnded event. |
| `PullGestureGestureTrigger` | Trigger | Trigger that runs its actions on the PullGesture event. |
| `RightTappedGestureTrigger` | Trigger | Trigger that runs its actions on the RightTapped event. |
| `ScrollGestureEndedGestureTrigger` | Trigger | Trigger that runs its actions on the ScrollGestureEnded event. |
| `ScrollGestureGestureTrigger` | Trigger | Trigger that runs its actions on the ScrollGesture event. |
| `ScrollGestureInertiaStartingGestureTrigger` | Trigger | Trigger that runs its actions on the ScrollGestureInertiaStarting event. |
| `TappedGestureTrigger` | Trigger | Trigger that runs its actions on the Tapped event. |

### Show · `Xaml.Behaviors.Interactions.Custom`

Use when: Show a hidden control on tap/double-tap/key.

| Name | Kind | What it does |
|---|---|---|
| `ShowOnDoubleTappedBehavior` | Behavior | A behavior that allows to show control on double tapped event. |
| `ShowOnKeyDownBehavior` | Behavior | A behavior that allows to show control on key down event. |
| `ShowOnTappedBehavior` | Behavior | A behavior that allows to show control on tapped event. |

### Control · `Xaml.Behaviors.Interactions.Custom`

Use when: Generic control helpers: bind pointer-over, observe bounds, inline edit, hide on key/lost focus, drag a control [InlineEditBehavior / HideOnKeyPressedBehavior worth a look].

| Name | Kind | What it does |
|---|---|---|
| `BindPointerOverBehavior` | Behavior | Binds the InputElement.IsPointerOverProperty to the IsPointerOver property. |
| `BindTagToVisualRootDataContextBehavior` | Behavior | Binds AssociatedObject object Tag property to root visual DataContext. |
| `BoundsObserverBehavior` | Behavior | Observes the bounds of an associated Control and updates its Width and Height properties. |
| `DelayedLoadBehavior` | Behavior | Delays the visibility of the associated control when it is attached to the visual tree. |
| `DragControlBehavior` | Behavior | A behavior that allows controls to be moved around the canvas using RenderTransform of IBehavior.AssociatedObject. |
| `HideAttachedFlyoutBehavior` | Behavior | Hides the flyout attached to the control when IsFlyoutOpen is false. |
| `HideOnKeyPressedBehavior` | Behavior | A behavior that allows to hide control on key down event. |
| `HideOnLostFocusBehavior` | Behavior | A behavior that allows to hide control on lost focus event. |
| `InlineEditBehavior` | Behavior | Behavior that toggles visibility of display and edit controls to enable inline editing. |
| `ShowPointerPositionBehavior` | Behavior | A behavior that displays cursor position on InputElement.PointerMoved event for the StyledElementBehavior{T}.AssociatedObject using TextBlock.Text property. |

### Events · `Xaml.Behaviors.Interactions.Events` (package not referenced)

Use when: Typed, trim-safe triggers/behaviors per routed event (source-generated style) [same role as InputElement/Triggers; pick one family and stay consistent].

| Name | Kind | What it does |
|---|---|---|
| `DoubleTappedEventTrigger` | Trigger | Trigger that listens for InputElement.DoubleTappedEvent. |
| `DragEnterEventTrigger` | Trigger | Trigger that listens for the DragDrop.DragEnterEvent. |
| `DragLeaveEventTrigger` | Trigger | Trigger that listens for the DragDrop.DragLeaveEvent. |
| `DragOverEventTrigger` | Trigger | Trigger that listens for the DragDrop.DragOverEvent. |
| `DropEventTrigger` | Trigger | Trigger that listens for the DragDrop.DropEvent. |
| `GotFocusEventTrigger` | Trigger | Trigger that listens for the InputElement.GotFocusEvent. |
| `KeyDownEventTrigger` | Trigger | Trigger that listens for the InputElement.KeyDownEvent. |
| `KeyUpEventTrigger` | Trigger | Trigger that listens for the InputElement.KeyUpEvent. |
| `LostFocusEventTrigger` | Trigger | Trigger that listens for the InputElement.LostFocusEvent. |
| `PointerCaptureLostEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerCaptureLostEvent. |
| `PointerEnteredEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerEnteredEvent. |
| `PointerEventsTrigger` | Trigger | Trigger that listens for multiple pointer events. |
| `PointerExitedEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerExitedEvent. |
| `PointerMovedEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerMovedEvent. |
| `PointerPressedEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerPressedEvent. |
| `PointerReleasedEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerReleasedEvent. |
| `PointerWheelChangedEventTrigger` | Trigger | Trigger that listens for the InputElement.PointerWheelChangedEvent. |
| `RightTappedEventTrigger` | Trigger | Trigger that listens for the InputElement.RightTappedEvent. |
| `ScrollGestureEndedEventTrigger` | Trigger | Trigger that listens for the InputElement.ScrollGestureEndedEvent. |
| `ScrollGestureEventTrigger` | Trigger | Trigger that listens for the InputElement.ScrollGestureEvent. |
| `TappedEventTrigger` | Trigger | Trigger that listens for the InputElement.TappedEvent. |
| `TextInputEventTrigger` | Trigger | Trigger that listens for the InputElement.TextInputEvent. |
| `TextInputMethodClientRequestedEventTrigger` | Trigger | Trigger that listens for the InputElement.TextInputMethodClientRequestedEvent. |
