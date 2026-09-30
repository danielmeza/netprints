# Writing a custom behavior

The base types to derive from when no prebuilt behavior fits.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Recipes

### Writing a custom behavior

Only when no prebuilt one fits and the logic is reusable view mechanics. Derive from `StyledElementBehavior<T>`,
put it in `src/NetPrints.Editor/Behaviors/`, unhook in `OnDetaching`, react to `OnDataContextChangedEvent` if it
reads the `DataContext`, and give it a headless test. `DialogCloseBehavior.cs` is the reference implementation:
about 60 lines, `sealed`, with XML docs on the type.

## Catalog

### Interactivity · `Xaml.Behaviors.Interactivity`

Use when: Framework base types.

| Name | Kind | What it does |
|---|---|---|
| `DisposableAction` | Action | Represents an System.IDisposable that executes a specified action when disposed. |

**Abstract bases (for writing a custom behavior only):** `Action`, `ActualThemeVariantChangedBehavior<T>`, `AttachedToLogicalTreeBehavior<T>`, `AttachedToLogicalTreeTriggerBase<T>`, `AttachedToVisualTreeBehavior<T>`, `AttachedToVisualTreeTriggerBase<T>`, `Behavior`, `Behavior<T>`, `ContextDragBehaviorBase`, `ContextDropBehaviorBase`, `DataContextChangedBehavior<T>`, `DisposingBehavior<T>`, `DisposingTrigger<T>`, `DoubleTappedEventBehavior`, `DragAndDropEventsBehavior`, `DropBehaviorBase`, `EventTriggerBase`, `ExecuteCommandBehaviorBase`, `ExecuteCommandOnKeyBehaviorBase`, `ExecuteCommandRoutedEventBehaviorBase`, `FocusBehaviorBase`, `GotFocusEventBehavior`, `InitializedBehavior<T>`, `InteractiveBehaviorBase`, `InteractiveTriggerBase`, `InvokeCommandActionBase`, `InvokeCommandBehaviorBase`, `ItemsControlContainerEventsBehavior`, `KeyDownEventBehavior`, `KeyUpEventBehavior`, `LoadedBehavior<T>`, `LostFocusEventBehavior`, `OpenFilePickerBehaviorBase`, `OpenFolderPickerBehaviorBase`, `PickerActionBase`, `PickerBehaviorBase`, `PointerCaptureLostEventBehavior`, `PointerEnteredEventBehavior`, `PointerEventsBehavior`, `PointerExitedEventBehavior`, `PointerMovedEventBehavior`, `PointerPressedEventBehavior`, `PointerReleasedEventBehavior`, `PointerWheelChangedEventBehavior`, `ResourcesChangedBehavior<T>`, `RightTappedEventBehavior`, `RoutedEventTrigger`, `RoutedEventTriggerBase`, `RoutedEventTriggerBase<T>`, `SaveFilePickerBehaviorBase`, `ScrollGestureEndedEventBehavior`, `ScrollGestureEventBehavior`, `SelectingItemsControlEventsBehavior`, `ShowBehaviorBase`, `StyledElementAction`, `StyledElementBehavior`, `StyledElementBehavior<T>`, `StyledElementTrigger`, `StyledElementTrigger<T>`, `TappedEventBehavior`, `TextInputEventBehavior`, `TextInputMethodClientRequestedEventBehavior`, `Trigger`, `Trigger<T>`, `TypedDragBehaviorBase`, `UploadFileBehaviorBase`
