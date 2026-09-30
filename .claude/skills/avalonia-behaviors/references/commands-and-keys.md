# Commands on tap, double-tap and keys

Run a VM command when an element is tapped, double-tapped or gets a key. Start here to replace a Tapped, DoubleTapped or KeyDown handler.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Recipes: Enter in a text box runs a command; Tap an item to run the list's command with that item; The focused control swallows the key
- Catalog: ExecuteCommand; Button; Input

## Recipes

### Enter in a text box runs a command

Replaces a `KeyDown` handler in code-behind that checks `e.Key == Key.Enter` and calls the view model. The
decision (which result to open) stays in the VM command, where a unit test can reach it.

```xml
<!-- src/NetPrints.Editor/Search/NodeSearchView.axaml -->
<TextBox Text="{Binding SearchText}">
  <Interaction.Behaviors>
    <ExecuteCommandOnKeyDownBehavior Key="Enter" Command="{Binding SelectFirstCommand}" />
  </Interaction.Behaviors>
</TextBox>
```

On a list, pass the selection: `CommandParameter="{Binding SelectedItem}"` (same file, the `ResultList` `ListBox`).

### Tap an item to run the list's command with that item

Inside an item template the `DataContext` is the item, so reach up to the list's VM with a typed cast (D3, D4)
and pass the item as the parameter.

```xml
<!-- src/NetPrints.Editor/Search/NodeSearchView.axaml -->
<DataTemplate x:DataType="edsearch:SuggestionItem">
  <Panel Background="Transparent">
    <Interaction.Behaviors>
      <ExecuteCommandOnTappedBehavior
        Command="{Binding $parent[ListBox].((edsearch:SuggestionListVM)DataContext).SelectCommand}"
        CommandParameter="{Binding}" />
    </Interaction.Behaviors>
    ...
  </Panel>
</DataTemplate>
```

When the item VM can expose the command itself (D4 option 1), bind `{Binding SelectCommand}` and skip the reach-up,
as `MemberVariableView.axaml` does. Double-tap works the same with `ExecuteCommandOnDoubleTappedBehavior`.
`Background="Transparent"` makes the whole row hit-testable, not only the text in it (E2 allows it for that reason).

### The focused control swallows the key

A key can be raised on a descendant (the focused `ListBoxItem` of a list), or a `TextBox` or the Nodify editor can
handle it before a bubbling handler sees it. Listen on the tunnel pass instead of falling back to code-behind (D9):

```xml
<!-- src/NetPrints.Editor/ClassEditor/ClassEditorWindow.axaml (the error list) -->
<ListBox x:Name="ErrorList" ItemsSource="{Binding ErrorList.Rows}">
  <Interaction.Behaviors>
    <KeyTrigger Key="Enter" EventRoutingStrategy="Tunnel">
      <InvokeCommandAction Command="{Binding ErrorList.NavigateCommand}"
                           CommandParameter="{Binding #ErrorList.SelectedItem}" />
    </KeyTrigger>
  </Interaction.Behaviors>
</ListBox>
```

`ExecuteCommandOnKeyDownBehavior` takes the same `EventRoutingStrategy="Tunnel"` (and `MarkAsHandled`) when one
command is enough. For a shortcut that should work anywhere in a window, a `KeyBinding` in `<Window.KeyBindings>` is
simpler (D9).

## Catalog

### ExecuteCommand · `Xaml.Behaviors.Interactions.Custom`

Use when: Run an ICommand on an input event (tap, double-tap, key, focus, pointer, gestures) in one element [first choice to replace Tapped/DoubleTapped/KeyDown handlers].

| Name | Kind | What it does |
|---|---|---|
| `ExecuteCommandOnActivatedBehavior` | Behavior | Executes Command (with CommandParameter) when the Activated event fires. |
| `ExecuteCommandOnDoubleTappedBehavior` | Behavior | Executes Command (with CommandParameter) when the DoubleTapped event fires. |
| `ExecuteCommandOnGotFocusBehavior` | Behavior | Executes Command (with CommandParameter) when the GotFocus event fires. |
| `ExecuteCommandOnHoldingBehavior` | Behavior | Executes Command (with CommandParameter) when the Holding event fires. |
| `ExecuteCommandOnKeyDownBehavior` | Behavior | Executes Command (with CommandParameter) when the KeyDown event fires. |
| `ExecuteCommandOnKeyUpBehavior` | Behavior | Executes Command (with CommandParameter) when the KeyUp event fires. |
| `ExecuteCommandOnLostFocusBehavior` | Behavior | Executes Command (with CommandParameter) when the LostFocus event fires. |
| `ExecuteCommandOnPinchBehavior` | Behavior | Executes Command (with CommandParameter) when the Pinch event fires. |
| `ExecuteCommandOnPinchEndedBehavior` | Behavior | Executes Command (with CommandParameter) when the PinchEnded event fires. |
| `ExecuteCommandOnPointerCaptureLostBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerCaptureLost event fires. |
| `ExecuteCommandOnPointerEnteredBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerEntered event fires. |
| `ExecuteCommandOnPointerExitedBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerExited event fires. |
| `ExecuteCommandOnPointerMovedBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerMoved event fires. |
| `ExecuteCommandOnPointerPressedBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerPressed event fires. |
| `ExecuteCommandOnPointerReleasedBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerReleased event fires. |
| `ExecuteCommandOnPointerTouchPadGestureMagnifyBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerTouchPadGestureMagnify event fires. |
| `ExecuteCommandOnPointerTouchPadGestureRotateBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerTouchPadGestureRotate event fires. |
| `ExecuteCommandOnPointerTouchPadGestureSwipeBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerTouchPadGestureSwipe event fires. |
| `ExecuteCommandOnPointerWheelChangedBehavior` | Behavior | Executes Command (with CommandParameter) when the PointerWheelChanged event fires. |
| `ExecuteCommandOnPullGestureBehavior` | Behavior | Executes Command (with CommandParameter) when the PullGesture event fires. |
| `ExecuteCommandOnPullGestureEndedBehavior` | Behavior | Executes Command (with CommandParameter) when the PullGestureEnded event fires. |
| `ExecuteCommandOnRightTappedBehavior` | Behavior | Executes Command (with CommandParameter) when the RightTapped event fires. |
| `ExecuteCommandOnScrollGestureBehavior` | Behavior | Executes Command (with CommandParameter) when the ScrollGesture event fires. |
| `ExecuteCommandOnScrollGestureEndedBehavior` | Behavior | Executes Command (with CommandParameter) when the ScrollGestureEnded event fires. |
| `ExecuteCommandOnScrollGestureInertiaStartingBehavior` | Behavior | Executes Command (with CommandParameter) when the ScrollGestureInertiaStarting event fires. |
| `ExecuteCommandOnTappedBehavior` | Behavior | Executes Command (with CommandParameter) when the Tapped event fires. |
| `ExecuteCommandOnTextInputBehavior` | Behavior | Executes Command (with CommandParameter) when the TextInput event fires. |
| `ExecuteCommandOnTextInputMethodClientRequestedBehavior` | Behavior | Executes Command (with CommandParameter) when the TextInputMethodClientRequested event fires. |

### Button · `Xaml.Behaviors.Interactions.Custom`

Use when: Button-specific glue: click trigger, command on key, close flyout/popup on click [ButtonClickEventTriggerBehavior + CloseWindowAction for OK buttons].

| Name | Kind | What it does |
|---|---|---|
| `ButtonClickEventTriggerBehavior` | Trigger | A behavior that listens for a Button.ClickEvent event on its source and executes its actions when that event is fired. |
| `ButtonExecuteCommandOnKeyDownBehavior` | Behavior | Executes a command when a key is pressed while the button has focus. |
| `ButtonHideFlyoutBehavior` | Behavior | Behavior that hides a button's flyout when IsFlyoutOpen becomes false. |
| `ButtonHideFlyoutOnClickBehavior` | Behavior | Hides the flyout when the button is clicked. |
| `ButtonHidePopupOnClickBehavior` | Behavior | Hides the popup when the button is clicked. |
| `InvokeButtonClickAction` | Action | Invokes the target button when executed. |

### Input · `Xaml.Behaviors.Interactions.Custom`

Use when: Window-wide hotkey, inactivity, lose focus on Enter, select all on focus [prefer KeyBinding for hotkeys; LoseFocusOnEnter for inspector text boxes].

| Name | Kind | What it does |
|---|---|---|
| `GlobalHotkeyBehavior` | Trigger | A behavior that registers a global hotkey (window-wide) to execute actions. |
| `InactivityTrigger` | Trigger | A trigger that fires when the user has been inactive (no mouse/keyboard input) for a specified duration. |
| `LoseFocusOnEnterBehavior` | Behavior | A behavior that loses focus when the Enter key is pressed. |
| `SelectAllOnFocusBehavior` | Behavior | A behavior that automatically selects all text in a TextBox when it receives focus. |
