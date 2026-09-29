# Xaml.Behaviors catalog (12.0.7)

Generated from the `wieslawsoltes/Xaml.Behaviors` source at `45387e3`. Lists every concrete behavior, trigger and
action across the 10 `Xaml.Behaviors.*` packages, grouped by the folder it lives in in the source tree. The
`use when` line for each group gives the NetPrints stance in brackets; see SKILL.md D11 for the short version and
the rule for when a prebuilt behavior beats code-behind or a custom behavior. `Xaml.Behaviors.Interactions.Events`
duplicates the `InputElement/Triggers` family with typed, trim-safe types (`XEventTrigger` versus `XTrigger`
style); pick one family per file and stay consistent within it.

NetPrints references `Xaml.Behaviors.Interactions`, `Xaml.Behaviors.Interactions.Custom` and
`Xaml.Behaviors.Interactions.DragAndDrop` (see Directory.Packages.props). The other packages below
(`Animations`, `Draggable`, `Events`, `ReactiveUI`, `Responsive`, `Scripting`, `Interactivity`) are listed for
completeness; `Interactivity` is a transitive dependency of the three referenced packages and `Draggable`/
`Animations` are worth knowing about even though nothing references them yet.

#### Xaml.Behaviors.Animations


**Composition**: use when: Composition-layer selection animation on an items control [not needed].

| Name | Kind | What it does |
|---|---|---|
| `SelectingItemsControlBehavior` | Behavior | Enables the standard selection indicator animation on selecting items controls. |

#### Xaml.Behaviors.Interactions


**Clipboard**: use when: Pure-view copy/paste with no VM involvement [avoid: NetPrints routes clipboard through IClipboardService so commands stay testable].

| Name | Kind | What it does |
|---|---|---|
| `ClearClipboardAction` | Action | An action that will clear the clipboard. |
| `GetClipboardDataAction` | Action | An action that will get the data from the clipboard. |
| `GetClipboardFormatsAction` | Action | An action that will get the clipboard formats. |
| `GetClipboardTextAction` | Action | An action that will get the text from the clipboard. |
| `SetClipboardDataObjectAction` | Action | An action that will set the data object to the clipboard. |
| `SetClipboardTextAction` | Action | An action that will set the text to the clipboard. |

**Core**: use when: General glue: run a command/method or set a property when an event fires or data changes; debounce/delay/group actions. [InvokeCommandAction is the default action; prefer typed triggers over EventTriggerBehavior's string EventName].

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

**FileSystem**: use when: Direct file-system side effects from XAML [avoid: side effects belong in VM/services].

| Name | Kind | What it does |
|---|---|---|
| `CreateDirectoryAction` | Action | An action that creates a directory at the specified path. |
| `DeleteDirectoryAction` | Action | An action that deletes a directory at the specified path. |
| `DeleteFileAction` | Action | An action that deletes a file at the specified path. |
| `FileSystemWatcherTrigger` | Trigger | A trigger that listens to file system events. |
| `WriteTextToFileAction` | Action | An action that writes text to a file. |

**Network**: use when: HTTP calls / network state from XAML [avoid].

| Name | Kind | What it does |
|---|---|---|
| `HttpRequestAction` | Action | An action that performs an HTTP request. |
| `NetworkInformationTrigger` | Trigger | A trigger that listens to network availability changes. |

**StorageProvider**: use when: Opening pickers straight from XAML and pushing the result to a command [avoid in NetPrints: IFilePickerService keeps commands testable].

| Name | Kind | What it does |
|---|---|---|
| `OpenFilePickerAction` | Action | An action that will open a file picker dialog. |
| `OpenFolderPickerAction` | Action | An action that will open a folder picker dialog. |
| `SaveFilePickerAction` | Action | An action that will open a file picker dialog. |

**StorageProvider/Button**: use when: Same, attached to a Button [avoid, see above].

| Name | Kind | What it does |
|---|---|---|
| `ButtonOpenFilePickerBehavior` | Behavior | Open file picker behavior for Button. |
| `ButtonOpenFolderPickerBehavior` | Behavior | Open folder picker behavior for Button. |
| `ButtonSaveFilePickerBehavior` | Behavior | Save file picker behavior for Button. |

**StorageProvider/MenuItem**: use when: Same, attached to a MenuItem [avoid, see above].

| Name | Kind | What it does |
|---|---|---|
| `MenuItemOpenFilePickerBehavior` | Behavior | Open file picker behavior for MenuItem. |
| `MenuItemOpenFolderPickerBehavior` | Behavior | Open folder picker behavior for MenuItem. |
| `MenuItemSaveFilePickerBehavior` | Behavior | Save file picker behavior for MenuItem. |

#### Xaml.Behaviors.Interactions.Custom


**Actions**: use when: View-only reactions: focus a control, add/remove a style class, show/hide popups/flyouts/controls, change a property. [Good fit for focus and visual-state glue].

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

**Animations**: use when: Start/await animations from triggers [use for view polish only].

| Name | Kind | What it does |
|---|---|---|
| `AnimateOnAttachedBehavior` | Behavior | Runs an animation when the associated control is attached to the visual tree. |
| `AnimationCompletedTrigger` | Trigger | Runs a specified Animation.Animation and executes actions when it completes. |
| `BeginAnimationAction` | Action | Starts an Animation.Animation on a specified control when executed. |
| `FadeInBehavior` | Behavior | Plays a simple fade in animation when the associated control is attached. |
| `PlayAnimationBehavior` | Behavior | Plays a specified Animation when the associated element is attached to the visual tree. |
| `RunAnimationTrigger` | Trigger | Runs an animation and invokes actions when the associated control is attached to the visual tree. |
| `StartAnimationAction` | Action | Starts an Animation.Animation on the associated control. |
| `StartBuiltAnimationAction` | Action | Starts an animation built in code on the associated control. |

**AutoCompleteBox**: use when: AutoCompleteBox focus/drop-down/selection glue [SelectTypeDialog candidate].

| Name | Kind | What it does |
|---|---|---|
| `AutoCompleteBoxOpenDropDownOnFocusBehavior` | Behavior | Opens the drop-down of an AutoCompleteBox when it receives focus. |
| `AutoCompleteBoxSelectionChangedTrigger` | Trigger | Triggers actions when the AutoCompleteBox selection changes. |
| `ClearAutoCompleteBoxSelectionAction` | Action | Clears the selection and text of an AutoCompleteBox. |
| `FocusAutoCompleteBoxTextBoxBehavior` | Behavior | FocusAutoCompleteBoxTextBoxBehavior (no XML summary; see source). |

**Automation/Actions**: use when: Screen-reader announcements or runtime AutomationId [ScreenReaderAnnounceAction for status changes].

| Name | Kind | What it does |
|---|---|---|
| `ScreenReaderAnnounceAction` | Action | An action that requests a screen reader announcement. |
| `SetAutomationIdAction` | Action | Sets AutomationProperties.AutomationIdProperty on the target control when executed. |

**Automation/Behaviors**: use when: Bind AutomationProperties.Name from another source [rarely needed; set the attached property].

| Name | Kind | What it does |
|---|---|---|
| `AutomationNameBehavior` | Behavior | Sets AutomationProperties.NameProperty on the associated control when attached. |

**Automation/Triggers**: use when: React when an automation name changes [rare].

| Name | Kind | What it does |
|---|---|---|
| `AutomationNameChangedTrigger` | Trigger | Executes actions when AutomationProperties.NameProperty of the associated control changes. |

**Behaviors**: use when: Auto-scroll log/output lists to newest item [program output / error list candidates].

| Name | Kind | What it does |
|---|---|---|
| `AutoScrollToBottomBehavior` | Behavior | A behavior that automatically scrolls to the bottom of a ScrollViewer or ItemsControl when new items are added. |

**Button**: use when: Button-specific glue: click trigger, command on key, close flyout/popup on click [ButtonClickEventTriggerBehavior + CloseWindowAction for OK buttons].

| Name | Kind | What it does |
|---|---|---|
| `ButtonClickEventTriggerBehavior` | Trigger | A behavior that listens for a Button.ClickEvent event on its source and executes its actions when that event is fired. |
| `ButtonExecuteCommandOnKeyDownBehavior` | Behavior | Executes a command when a key is pressed while the button has focus. |
| `ButtonHideFlyoutBehavior` | Behavior | Behavior that hides a button's flyout when IsFlyoutOpen becomes false. |
| `ButtonHideFlyoutOnClickBehavior` | Behavior | Hides the flyout when the button is clicked. |
| `ButtonHidePopupOnClickBehavior` | Behavior | Hides the popup when the button is clicked. |
| `InvokeButtonClickAction` | Action | Invokes the target button when executed. |

**Carousel**: use when: Carousel navigation [not used].

| Name | Kind | What it does |
|---|---|---|
| `CarouselKeyNavigationBehavior` | Behavior | Enables keyboard navigation for a Carousel using arrow keys. |
| `CarouselNextAction` | Action | Advances the target Carousel to the next page. |
| `CarouselPreviousAction` | Action | Moves the target Carousel to the previous page. |
| `CarouselSelectionChangedTrigger` | Trigger | Executes actions when the associated Carousel changes selection. |

**Collections**: use when: Mutating collections from XAML [avoid: VM owns collections].

| Name | Kind | What it does |
|---|---|---|
| `AddRangeAction` | Action | Adds a range of items to a target IList when invoked. |
| `ClearCollectionAction` | Action | Clears all items from a target IList when invoked. |
| `CollectionChangedBehavior` | Behavior | Executes different sets of actions when the observed collection changes. |
| `CollectionChangedTrigger` | Trigger | Executes associated actions whenever the bound collection raises a INotifyCollectionChanged.CollectionChanged event. |
| `RemoveRangeAction` | Action | Removes a range of items from a target IList when invoked. |

**Composition**: use when: Decorative composition effects [not needed].

| Name | Kind | What it does |
|---|---|---|
| `OrbitEffectBehavior` | Behavior | A behavior that allows rotating the attached control in 3D space using pointer manipulation. |
| `ParallaxBehavior` | Behavior | A behavior that moves the associated element at a different speed than the scrolling container, creating a parallax effect. |
| `TiltEffectBehavior` | Behavior | A behavior that applies a 3D tilt rotation to the element based on the pointer position. |

**ContextDialogs**: use when: Inline context dialogs [prefer CanvasPopup per ADR-0004].

| Name | Kind | What it does |
|---|---|---|
| `ContextDialogBehavior` | Behavior | Behavior that manages a context dialog implemented using a Popup. |
| `ContextDialogClosedTrigger` | Trigger | Trigger that listens for the Closed event of ContextDialogBehavior. |
| `ContextDialogOpenedTrigger` | Trigger | Trigger that listens for the Opened event of ContextDialogBehavior. |
| `HideContextDialogAction` | Action | Closes a ContextDialogBehavior when executed. |
| `ShowContextDialogAction` | Action | Opens a ContextDialogBehavior when executed. |

**Control**: use when: Generic control helpers: bind pointer-over, observe bounds, inline edit, hide on key/lost focus, drag a control [InlineEditBehavior / HideOnKeyPressedBehavior worth a look].

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

**Core**: use when: Lifecycle triggers (Loaded, Unloaded, DataContextChanged, attached to tree, theme changed) and base classes [use a trigger instead of DataContextChanged/Loaded handlers].

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

**Cursor**: use when: Set cursors on hover or from a provider [view-only cursor changes].

| Name | Kind | What it does |
|---|---|---|
| `PointerOverCursorBehavior` | Behavior | Changes the cursor when the pointer is over the associated control. |
| `SetCursorAction` | Action | Sets the cursor on a target control. |
| `SetCursorBehavior` | Behavior | Sets the cursor for the associated control when attached. |
| `SetCursorFromProviderAction` | Action | Sets the cursor on a target control using an ICursorProvider. |
| `SetCursorFromProviderBehavior` | Behavior | Sets the cursor provided by an ICursorProvider when attached. |

**Debugging**: use when: Break/log from XAML while debugging [never commit].

| Name | Kind | What it does |
|---|---|---|
| `BreakAction` | Action | An action that triggers a debugger break. |
| `LogAction` | Action | An action that logs a message to the debug output. |
| `VisualDebugBehavior` | Trigger | A behavior that visualizes events on the attached control for debugging purposes. |

**Dialog**: use when: Show a dialog window from XAML [avoid: WindowService opens dialogs].

| Name | Kind | What it does |
|---|---|---|
| `DialogClosedTrigger` | Trigger | Executes actions when the dialog window is closed. |
| `DialogOpenedTrigger` | Trigger | Executes actions when the dialog window is opened. |
| `ShowDialogAction` | Action | Shows a Window as a dialog. |

**ExecuteCommand**: use when: Run an ICommand on an input event (tap, double-tap, key, focus, pointer, gestures) in one element [first choice to replace Tapped/DoubleTapped/KeyDown handlers].

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

**FileUpload**: use when: Browser-style upload flows [not used].

| Name | Kind | What it does |
|---|---|---|
| `ButtonUploadFileBehavior` | Behavior | Upload file behavior for Button. |
| `UploadCompletedTrigger` | Trigger | Executes actions when the bound value becomes true. |
| `UploadFileAction` | Action | Asynchronously uploads a file to a specified URL and invokes a command when completed. |

**Focus**: use when: Focus on attach/visible/pointer, focus the selected item, trap focus [replaces Focus() calls in code-behind].

| Name | Kind | What it does |
|---|---|---|
| `AutoFocusBehavior` | Behavior | This behavior automatically sets the focus on the associated Control when it is loaded. |
| `FocusBehavior` | Behavior | Keeps a control focused while the IsFocused property is set. |
| `FocusControlBehavior` | Behavior | Sets focus on the associated control when FocusFlag is true. |
| `FocusOnAttachedBehavior` | Behavior | Focuses the associated control when it is attached to the visual tree. |
| `FocusOnAttachedToVisualTreeBehavior` | Behavior | Focuses the IBehavior.AssociatedObject when attached to visual tree. |
| `FocusOnPointerMovedBehavior` | Behavior | Focuses the IBehavior.AssociatedObject on InputElement.PointerMoved event. |
| `FocusOnPointerPressedBehavior` | Behavior | Focuses the StyledElementBehavior{T}.AssociatedObject on InputElement.PointerPressed event. |
| `FocusOnVisibleBehavior` | Behavior | Focuses the associated control when it becomes visible. |
| `FocusSelectedItemBehavior` | Behavior | Focuses the container of the currently selected item when attached. |
| `FocusTrapBehavior` | Behavior | A behavior that traps focus within the attached element. |

**Gestures**: use when: Triggers for gesture routed events (tap, hold, pinch, pull, scroll, touchpad) to chain arbitrary actions.

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

**Icon**: use when: PathIcon data swapping [not used; Material.Icons instead].

| Name | Kind | What it does |
|---|---|---|
| `PathIconDataBehavior` | Behavior | Sets the PathIcon.Data when the associated icon is attached to the visual tree. |
| `PathIconDataChangedTrigger` | Trigger | Invokes actions whenever the PathIcon.Data property changes. |
| `SetPathIconDataAction` | Action | Changes the PathIcon.Data of a target icon when executed. |

**Input**: use when: Window-wide hotkey, inactivity, lose focus on Enter, select all on focus [prefer KeyBinding for hotkeys; LoseFocusOnEnter for inspector text boxes].

| Name | Kind | What it does |
|---|---|---|
| `GlobalHotkeyBehavior` | Trigger | A behavior that registers a global hotkey (window-wide) to execute actions. |
| `InactivityTrigger` | Trigger | A trigger that fires when the user has been inactive (no mouse/keyboard input) for a specified duration. |
| `LoseFocusOnEnterBehavior` | Behavior | A behavior that loses focus when the Enter key is pressed. |
| `SelectAllOnFocusBehavior` | Behavior | A behavior that automatically selects all text in a TextBox when it receives focus. |

**InputElement/Actions**: use when: Capture/release the pointer from a trigger.

| Name | Kind | What it does |
|---|---|---|
| `CapturePointerAction` | Action | Captures the pointer. |
| `ReleasePointerCaptureAction` | Action | Releases the pointer capture. |

**InputElement/Triggers**: use when: Typed input-event triggers (Click, KeyDown, KeyGesture, pointer, focus) that run any action list [use when you need more than one action or a non-command action].

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

**ItemsControl**: use when: Container lifecycle triggers, nudge-on-drop, add/insert/clear items [container triggers only; VM owns items].

| Name | Kind | What it does |
|---|---|---|
| `AddItemToItemsControlAction` | Action | Allows a user to add the item to ItemsControl. |
| `ClearItemsControlAction` | Action | Allows a user to clear the items from a ItemsControl. |
| `InsertItemToItemsControlAction` | Action | Allows a user to insert the item to a ItemsControl. |
| `ItemNudgeDropBehavior` | Behavior | ItemNudgeDropBehavior (no XML summary; see source). |
| `ItemsControlContainerClearingTrigger` | Trigger | A behavior that listens for a ItemsControl.ContainerClearing event on its source and executes its actions when that event is fired. |
| `ItemsControlContainerIndexChangedTrigger` | Trigger | A behavior that listens for a ItemsControl.ContainerIndexChanged event on its source and executes its actions when that event is fired. |
| `ItemsControlContainerPreparedTrigger` | Trigger | A behavior that listens for a ItemsControl.ContainerPrepared event on its source and executes its actions when that event is fired. |
| `ItemsControlPreparingContainerTrigger` | Trigger | A behavior that listens for a ItemsControl.PreparingContainer event on its source and executes its actions when that event is fired. |
| `MoveItemInItemsControlAction` | Action | Moves an item within an ItemsControl from FromIndex to ToIndex. |
| `RemoveItemAtAction` | Action | Removes an item at a specified index from an ItemsControl. |
| `RemoveItemInItemsControlAction` | Action | Allows a user to remove the item from a ItemsControl. |
| `ScrollToItemBehavior` | Behavior | Scrolls the associated ItemsControl to a specific item. |
| `ScrollToItemIndexBehavior` | Behavior | Scrolls the associated ItemsControl to a given item index. |

**Layout**: use when: Fluid move animations / reparenting [not needed].

| Name | Kind | What it does |
|---|---|---|
| `FluidMoveBehavior` | Behavior | Behavior that animates position changes of a control or its children. |
| `MoveElementToPanelAction` | Action | Moves the associated or target element to a specified Panel. |

**ListBox**: use when: Select/unselect all, remove item [VM should own removal].

| Name | Kind | What it does |
|---|---|---|
| `ListBoxSelectAllBehavior` | Behavior | ListBoxSelectAllBehavior (no XML summary; see source). |
| `ListBoxUnselectAllBehavior` | Behavior | ListBoxUnselectAllBehavior (no XML summary; see source). |
| `RemoveItemInListBoxAction` | Action | Allows a user to remove the item from a ListBox ItemTemplate. |

**ListBoxItem**: use when: Hover-to-select lists (menus, suggestion lists).

| Name | Kind | What it does |
|---|---|---|
| `SelectListBoxItemOnPointerMovedBehavior` | Behavior | Sets ListBoxItem.IsSelected property to true of the associated ListBoxItem control on InputElement.PointerMoved event. |

**Logic**: use when: Conditional/switch action routing in XAML [avoid: decisions belong in the VM].

| Name | Kind | What it does |
|---|---|---|
| `ConditionalAction` | Action | An action that executes different collections of actions depending on the specified condition. |
| `ConditionalBehavior` | Behavior | A behavior that executes different collections of actions depending on the specified condition. |
| `SwitchCaseAction` | Action | An action that executes a specific set of actions based on a value match. |
| `SwitchCaseBehavior` | Behavior | A behavior that executes a specific set of actions based on a value match. |

**Multimedia**: use when: Console beep [no].

| Name | Kind | What it does |
|---|---|---|
| `ConsoleBeepAction` | Action | An action that plays a system beep. |

**Notifications**: use when: WindowNotificationManager toasts from XAML [possible for status toasts, VM decides the message].

| Name | Kind | What it does |
|---|---|---|
| `CloseNotificationAction` | Action | Action that closes the specified NotificationCard. |
| `NotificationManagerBehavior` | Behavior | Provides a INotificationManager for the associated Control. |
| `ShowErrorNotificationAction` | Action | Shows an error notification using an INotificationManager. |
| `ShowInformationNotificationAction` | Action | Shows an information notification using an INotificationManager. |
| `ShowNotificationAction` | Action | Shows a notification using an INotificationManager. |
| `ShowSuccessNotificationAction` | Action | Shows a success notification using an INotificationManager. |
| `ShowWarningNotificationAction` | Action | Shows a warning notification using an INotificationManager. |

**Popup**: use when: React to popup open/close [e.g. focus the search box when CanvasPopup opens].

| Name | Kind | What it does |
|---|---|---|
| `PopupClosedTrigger` | Trigger | Executes actions when the Popup.Closed event is raised. |
| `PopupOpenedTrigger` | Trigger | Executes actions when the Popup.Opened event is raised. |

**RenderTarget**: use when: Render a control to a bitmap [screenshots/exports only].

| Name | Kind | What it does |
|---|---|---|
| `RenderRenderTargetBitmapAction` | Action | Action that invokes IRenderTargetBitmapRenderHost.Render on the specified target. |
| `RenderTargetBitmapBehavior` | Behavior | Behavior that draws into a RenderTargetBitmap and assigns it to the associated Image. |
| `RenderTargetBitmapTrigger` | Trigger | Trigger that calls IRenderTargetBitmapRenderHost.Render periodically. |
| `StaticRenderTargetBitmapBehavior` | Behavior | Behavior that draws once into a RenderTargetBitmap and assigns it to the associated Image. Rendering can be triggered by calling IRenderTargetBitmapRenderHost.Render. |

**Screen**: use when: Screen/monitor info [not needed].

| Name | Kind | What it does |
|---|---|---|
| `ActiveScreenBehavior` | Behavior | A behavior that exposes the screen containing the associated TopLevel. |
| `RequestScreenDetailsAction` | Action | An action that requests extended screen information from Screens. |
| `ScreensChangedTrigger` | Trigger | A trigger that executes its actions when the screen configuration changes. |

**ScrollViewer**: use when: Scroll offset binding, scroll-to-offset, viewport info [ScrollIntoView replacements].

| Name | Kind | What it does |
|---|---|---|
| `HorizontalScrollViewerBehavior` | Behavior | Enables horizontal scrolling of a ScrollViewer using the mouse wheel. |
| `ScrollChangedTrigger` | Trigger | Executes actions when the ScrollViewer.ScrollChanged event occurs. |
| `ScrollToOffsetAction` | Action | Scrolls a ScrollViewer to the specified offsets when executed. |
| `ScrollViewerOffsetBehavior` | Behavior | Sets the ScrollViewer.Offset of the associated ScrollViewer. |
| `ViewportBehavior` | Behavior | Listens for the associated element entering or exiting the parent ScrollViewer viewport. |

**SelectingItemsControl**: use when: Selection events and type-to-search on lists.

| Name | Kind | What it does |
|---|---|---|
| `SelectingItemsControlSearchBehavior` | Behavior | Filters SelectingItemsControl items based on the text of a search box. |

**Show**: use when: Show a hidden control on tap/double-tap/key.

| Name | Kind | What it does |
|---|---|---|
| `ShowOnDoubleTappedBehavior` | Behavior | A behavior that allows to show control on double tapped event. |
| `ShowOnKeyDownBehavior` | Behavior | A behavior that allows to show control on key down event. |
| `ShowOnTappedBehavior` | Behavior | A behavior that allows to show control on tapped event. |

**SplitView**: use when: SplitView pane glue [not used].

| Name | Kind | What it does |
|---|---|---|
| `SplitViewPaneClosedTrigger` | Trigger | Executes actions when the SplitView.PaneClosed event is raised. |
| `SplitViewPaneClosingTrigger` | Trigger | Executes actions when the SplitView.PaneClosing event is raised. |
| `SplitViewPaneOpenedTrigger` | Trigger | Executes actions when the SplitView.PaneOpened event is raised. |
| `SplitViewPaneOpeningTrigger` | Trigger | Executes actions when the SplitView.PaneOpening event is raised. |
| `SplitViewStateBehavior` | Behavior | Updates SplitView properties based on size conditions. |
| `SplitViewTogglePaneAction` | Action | Toggles the SplitView.IsPaneOpen state of a SplitView when executed. |

**System**: use when: Clipboard monitor / network status [avoid].

| Name | Kind | What it does |
|---|---|---|
| `ClipboardMonitorBehavior` | Behavior | A behavior that monitors the system clipboard for specific data formats. |
| `NetworkStatusTrigger` | Trigger | A trigger that fires when the network status changes. |

**TabControl**: use when: Keyboard tab navigation and tab selection triggers [bottom tabs in ClassEditorWindow].

| Name | Kind | What it does |
|---|---|---|
| `TabControlKeyNavigationBehavior` | Behavior | Enables keyboard navigation for a TabControl using arrow keys. |
| `TabControlNextAction` | Action | Advances the target TabControl to the next tab. |
| `TabControlPreviousAction` | Action | Moves the target TabControl to the previous tab. |
| `TabControlSelectionChangedTrigger` | Trigger | Executes actions when the associated TabControl changes selection. |

**TextBox**: use when: Select-all on focus / auto-select [inspector name boxes].

| Name | Kind | What it does |
|---|---|---|
| `AutoSelectBehavior` | Behavior | Automatically selects the entire content of the associated TextBox when it is loaded. |
| `TextBoxSelectAllOnGotFocusBehavior` | Behavior | A behavior that allows to select all TextBox text on got focus event. |
| `TextBoxSelectAllTextBehavior` | Behavior | TextBoxSelectAllTextBehavior (no XML summary; see source). |

**ThemeVariant**: use when: Set or react to theme variant per subtree [prefer ThemeVariantScope + DynamicResource].

| Name | Kind | What it does |
|---|---|---|
| `ThemeVariantBehavior` | Behavior | Sets the ThemeVariantScope.RequestedThemeVariant on the associated control. |
| `ThemeVariantTrigger` | Trigger | Executes actions when the associated control's StyledElement.ActualThemeVariant matches the specified ThemeVariant. |

**ToolTips**: use when: Programmatic tooltip show/hide [rare].

| Name | Kind | What it does |
|---|---|---|
| `HideToolTipAction` | Action | Hides the ToolTip of the associated or target control when executed. |
| `SetToolTipTipAction` | Action | Sets the ToolTip.TipProperty of the associated or target control when executed. |
| `ShowToolTipAction` | Action | Shows the ToolTip of the associated or target control when executed. |
| `ToolTipClosingTrigger` | Trigger | Trigger that listens for the ToolTip.ToolTipClosingEvent. |
| `ToolTipOpeningTrigger` | Trigger | Trigger that listens for the ToolTip.ToolTipOpeningEvent. |

**Transitions**: use when: Manage Transitions from triggers [view polish].

| Name | Kind | What it does |
|---|---|---|
| `AddTransitionAction` | Action | Adds a TransitionBase to the Avalonia.Animation.Transitions collection on the target element. |
| `ClearTransitionsAction` | Action | Clears the Avalonia.Animation.Transitions collection. |
| `RemoveTransitionAction` | Action | Removes a TransitionBase from the Avalonia.Animation.Transitions collection on the target element. |
| `TransitionsBehavior` | Behavior | Sets the Avalonia.Animation.Transitions collection on the associated control when attached. |
| `TransitionsChangedTrigger` | Trigger | Executes actions whenever the Avalonia.Animation.Transitions collection changes. |

**TreeView**: use when: Filter a TreeView from a text box [future class/type browsers].

| Name | Kind | What it does |
|---|---|---|
| `ApplyTreeViewFilterAction` | Action | Filters a TreeView using the provided query string. |
| `TreeViewFilterBehavior` | Behavior | Filters TreeView items based on the text of a search box. |
| `TreeViewFilterTextChangedTrigger` | Trigger | Executes actions when the search box text changes. |

**TreeViewItem**: use when: Expand/collapse on double-tap.

| Name | Kind | What it does |
|---|---|---|
| `ToggleIsExpandedOnDoubleTappedBehavior` | Behavior | Toggles TreeViewItem.IsExpanded property of the associated TreeViewItem control on InputElement.DoubleTapped event. |

**Validation**: use when: Control-level validation visuals [avoid for rules: validation logic stays in the VM; INotifyDataErrorInfo].

| Name | Kind | What it does |
|---|---|---|
| `ComboBoxValidationBehavior` | Behavior | Validation behavior for ComboBox selected item. |
| `DatePickerValidationBehavior` | Behavior | Validation behavior for DatePicker selected date. |
| `NumericUpDownValidationBehavior` | Behavior | Validation behavior for NumericUpDown value. |
| `PropertyValidationBehavior<TControl, TValue>` | Behavior | Base behavior that validates a property value using a set of rules. |
| `SliderValidationBehavior` | Behavior | Validation behavior for range based controls like Slider value. |
| `TextBoxValidationBehavior` | Behavior | Validation behavior for TextBox text. |

**ViewModel**: use when: Set/toggle VM properties from XAML [avoid: bypasses commands, untestable].

| Name | Kind | What it does |
|---|---|---|
| `IncrementViewModelPropertyAction` | Action | Increments a numeric view model property when invoked. |
| `SetViewModelPropertyAction` | Action | Sets a view model property to a specified value when invoked. |
| `SetViewModelPropertyOnLoadBehavior` | Behavior | Sets a view model property when the associated control is loaded. |
| `ToggleViewModelBooleanAction` | Action | Toggles a boolean view model property when invoked. |
| `ViewModelPropertyChangedTrigger` | Trigger | Triggers when the specified view model property changes. |

**Window**: use when: Close/show/center a window, drag-move a borderless window [CloseWindowAction for OK/Close buttons].

| Name | Kind | What it does |
|---|---|---|
| `CenterWindowBehavior` | Behavior | Centers the attached window on the current screen when it is attached to the visual tree. |
| `CloseWindowAction` | Action | Closes the associated or target window when executed. |
| `ShowWindowAction` | Action | Shows the specified window when executed. |
| `WindowAction` | Action | An action that performs common window operations. |
| `WindowDragMoveBehavior` | Behavior | A behavior that allows the user to drag the window by clicking and holding the associated control. |
| `WindowStateTrigger` | Trigger | Executes actions when the window state matches the specified value. |

**WriteableBitmap**: use when: Render into WriteableBitmap on a timer [not needed].

| Name | Kind | What it does |
|---|---|---|
| `WriteableBitmapBehavior` | Behavior | Creates a WriteableBitmap and optionally renders it once using a renderer. |
| `WriteableBitmapRenderAction` | Action | Invokes an IWriteableBitmapRenderer to render into a bitmap. |
| `WriteableBitmapRenderBehavior` | Behavior | Creates a WriteableBitmap and updates it using a renderer on a timer. |
| `WriteableBitmapTimerTrigger` | Trigger | A trigger that fires its actions on a timer and passes a WriteableBitmap as parameter. |
| `WriteableBitmapTrigger` | Trigger | A trigger that executes its actions when Trigger is called, passing a WriteableBitmap as parameter. |

#### Xaml.Behaviors.Interactions.DragAndDrop


**(root)**: use when: Drag sources and drop targets with context objects, handlers or commands; file/text drops [ContextDragBehavior + ContextDropBehavior/IDropHandler for list-to-graph drags].

| Name | Kind | What it does |
|---|---|---|
| `AddPreviewFilesAction` | Action | Adds file paths from a drag event to an ItemsControl. |
| `ContentControlFilesDropBehavior` | Behavior | Behavior that handles file drop operations for ContentControl. |
| `ContextDragBehavior` | Behavior | Behavior that starts a drag operation using the associated context data. |
| `ContextDragWithDirectionBehavior` | Behavior | Behavior that starts drag and drop with information about drag direction. |
| `ContextDropBehavior` | Behavior | Behavior that enables dropping context data onto the associated control using predefined IDropHandler. |
| `DragDropCommandsBehavior` | Behavior | Behavior that exposes commands for drag-and-drop events. |
| `FilesDropBehavior` | Behavior | Behavior that handles file drop operations. |
| `FilesPreviewBehavior` | Behavior | Behavior that collects file paths while dragging over the associated control. |
| `PanelDragBehavior` | Behavior | Behavior that starts a drag operation for a control so it can be moved between panels. The control itself is used as the drag context. |
| `PanelDropBehavior` | Behavior | Behavior that allows dropping controls dragged with PanelDragBehavior. |
| `TextDropBehavior` | Behavior | Behavior that handles text drop operations. |
| `TypedDragBehavior` | Behavior | Behavior that initiates a drag operation for a specific data type. |

**ManagedDragDrop**: use when: In-process drag/drop without the platform DataTransfer (managed preview) [alternative for list-to-graph].

| Name | Kind | What it does |
|---|---|---|
| `ManagedContextDragBehavior` | Behavior | Behavior that initiates an in-process managed drag with an optional preview window. This avoids OS drag-drop and integrates with ManagedDragDropService. |
| `ManagedContextDropBehavior` | Behavior | Drop target behavior that integrates with ManagedDragDropService. It mirrors the semantics of ContextDropBehavior but works entirely in-process. |

#### Xaml.Behaviors.Interactions.Draggable


**(root)**: use when: Move elements or reorder list items by dragging, auto-scroll while dragging [list reordering of methods/variables if ever needed].

| Name | Kind | What it does |
|---|---|---|
| `AutoScrollDuringDragBehavior` | Behavior | Automatically scrolls the associated ScrollViewer when the pointer is dragged near its edges. |
| `CanvasDragBehavior` | Behavior | Enables dragging of child controls within a Canvas. |
| `GridDragBehavior` | Behavior | Allows dragging of grid child controls with optional layout copying. |
| `ItemDragBehavior` | Behavior | Allows dragging items within an ItemsControl. |
| `ListReorderDragBehavior` | Behavior | Allows reordering of items inside an ItemsControl while displaying a placeholder at the insertion point. |
| `MouseDragElementBehavior` | Behavior | Enables dragging of a control with the mouse using a TranslateTransform. |
| `MultiMouseDragElementBehavior` | Behavior | Enables dragging of multiple controls with the mouse using TranslateTransform. |

#### Xaml.Behaviors.Interactions.Events


**(root)**: use when: Typed, trim-safe triggers/behaviors per routed event (source-generated style) [same role as InputElement/Triggers; pick one family and stay consistent].

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

#### Xaml.Behaviors.Interactions.ReactiveUI


**(root)**: use when: ReactiveUI routing [not applicable: NetPrints uses CommunityToolkit.Mvvm].

| Name | Kind | What it does |
|---|---|---|
| `ClearNavigationStackAction` | Action | An action that resets the navigation stack. |
| `InteractionTriggerBehavior<TInput, TOutput>` | Trigger | A behavior that registers a handler for a Interaction{TInput,TOutput} and executes its actions when the interaction is triggered. |
| `NavigateAction` | Action | An action that navigates to a specified IRoutableViewModel. |
| `NavigateAndReset` | Action | An action that navigates to a specified IRoutableViewModel and resets the navigation stack. |
| `NavigateBackAction` | Action | An action that navigates back in the RoutingState stack. |
| `NavigateToAction<TViewModel>` | Action | An action that resolves and navigates to a view model of type . |
| `NavigateToAndResetAction<TViewModel>` | Action | An action that resolves and navigates to and clears the navigation stack. |

#### Xaml.Behaviors.Interactions.Responsive


**(root)**: use when: Adaptive classes by width/aspect ratio.

| Name | Kind | What it does |
|---|---|---|
| `AdaptiveBehavior` | Behavior | Observes StyledElementBehavior{T}.AssociatedObject control or SourceControl control Visual.Bounds property changes and if triggered sets or removes style classes when conditions from AdaptiveClassSetter are met. |
| `AspectRatioBehavior` | Behavior | Observes bounds changes of a control (or a specified source) and conditionally adds or removes classes based on AspectRatioClassSetter rules. |

#### Xaml.Behaviors.Interactions.Scripting


**(root)**: use when: Run C# scripts from XAML [never].

| Name | Kind | What it does |
|---|---|---|
| `ExecuteScriptAction` | Action | Executes a C# script using Roslyn scripting API. |

#### Xaml.Behaviors.Interactivity


**(root)**: use when: Framework base types.

| Name | Kind | What it does |
|---|---|---|
| `DisposableAction` | Action | Represents an System.IDisposable that executes a specified action when disposed. |

**Abstract bases (for writing a custom behavior only):** `Action`, `ActualThemeVariantChangedBehavior<T>`, `AttachedToLogicalTreeBehavior<T>`, `AttachedToLogicalTreeTriggerBase<T>`, `AttachedToVisualTreeBehavior<T>`, `AttachedToVisualTreeTriggerBase<T>`, `Behavior`, `Behavior<T>`, `ContextDragBehaviorBase`, `ContextDropBehaviorBase`, `DataContextChangedBehavior<T>`, `DisposingBehavior<T>`, `DisposingTrigger<T>`, `DoubleTappedEventBehavior`, `DragAndDropEventsBehavior`, `DropBehaviorBase`, `EventTriggerBase`, `ExecuteCommandBehaviorBase`, `ExecuteCommandOnKeyBehaviorBase`, `ExecuteCommandRoutedEventBehaviorBase`, `FocusBehaviorBase`, `GotFocusEventBehavior`, `InitializedBehavior<T>`, `InteractiveBehaviorBase`, `InteractiveTriggerBase`, `InvokeCommandActionBase`, `InvokeCommandBehaviorBase`, `ItemsControlContainerEventsBehavior`, `KeyDownEventBehavior`, `KeyUpEventBehavior`, `LoadedBehavior<T>`, `LostFocusEventBehavior`, `OpenFilePickerBehaviorBase`, `OpenFolderPickerBehaviorBase`, `PickerActionBase`, `PickerBehaviorBase`, `PointerCaptureLostEventBehavior`, `PointerEnteredEventBehavior`, `PointerEventsBehavior`, `PointerExitedEventBehavior`, `PointerMovedEventBehavior`, `PointerPressedEventBehavior`, `PointerReleasedEventBehavior`, `PointerWheelChangedEventBehavior`, `ResourcesChangedBehavior<T>`, `RightTappedEventBehavior`, `RoutedEventTrigger`, `RoutedEventTriggerBase`, `RoutedEventTriggerBase<T>`, `SaveFilePickerBehaviorBase`, `ScrollGestureEndedEventBehavior`, `ScrollGestureEventBehavior`, `SelectingItemsControlEventsBehavior`, `ShowBehaviorBase`, `StyledElementAction`, `StyledElementBehavior`, `StyledElementBehavior<T>`, `StyledElementTrigger`, `StyledElementTrigger<T>`, `TappedEventBehavior`, `TextInputEventBehavior`, `TextInputMethodClientRequestedEventBehavior`, `Trigger`, `Trigger<T>`, `TypedDragBehaviorBase`, `UploadFileBehaviorBase`
