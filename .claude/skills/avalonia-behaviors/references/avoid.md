# Behaviors NetPrints avoids

Behaviors that bypass the VM or its services. Read this before using anything that touches the clipboard, files, pickers, the network, dialogs or VM state from XAML.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Recipes: What not to do
- Catalog: Clipboard; FileSystem; Network; StorageProvider; StorageProvider/Button; StorageProvider/MenuItem; Collections; Debugging; Dialog; FileUpload; Logic; System; ViewModel; ReactiveUI; Scripting

## Recipes

### What not to do

These behaviors exist but bypass the VM and its services, so a unit test can't reach the decision:

```xml
<!-- Don't: the copy happens in XAML, untested and outside IClipboardService -->
<ButtonClickEventTriggerBehavior>
  <SetClipboardTextAction Text="{Binding GeneratedCode}" />
</ButtonClickEventTriggerBehavior>

<!-- Do: a VM command that calls the service -->
<Button Content="Copy" Command="{Binding CopyCodeCommand}" />
```

The same applies to the file-system, storage-picker, HTTP, `SetViewModelProperty`, `ToggleViewModelBoolean`,
`ConditionalAction`/`SwitchCaseAction`, `Collections`, `Scripting` and dialog behaviors: use `IClipboardService`,
`IFilePickerService` and `IWindowService` from a command instead.

## Catalog

### Clipboard · `Xaml.Behaviors.Interactions`

Use when: Pure-view copy/paste with no VM involvement [avoid: NetPrints routes clipboard through IClipboardService so commands stay testable].

| Name | Kind | What it does |
|---|---|---|
| `ClearClipboardAction` | Action | An action that will clear the clipboard. |
| `GetClipboardDataAction` | Action | An action that will get the data from the clipboard. |
| `GetClipboardFormatsAction` | Action | An action that will get the clipboard formats. |
| `GetClipboardTextAction` | Action | An action that will get the text from the clipboard. |
| `SetClipboardDataObjectAction` | Action | An action that will set the data object to the clipboard. |
| `SetClipboardTextAction` | Action | An action that will set the text to the clipboard. |

### FileSystem · `Xaml.Behaviors.Interactions`

Use when: Direct file-system side effects from XAML [avoid: side effects belong in VM/services].

| Name | Kind | What it does |
|---|---|---|
| `CreateDirectoryAction` | Action | An action that creates a directory at the specified path. |
| `DeleteDirectoryAction` | Action | An action that deletes a directory at the specified path. |
| `DeleteFileAction` | Action | An action that deletes a file at the specified path. |
| `FileSystemWatcherTrigger` | Trigger | A trigger that listens to file system events. |
| `WriteTextToFileAction` | Action | An action that writes text to a file. |

### Network · `Xaml.Behaviors.Interactions`

Use when: HTTP calls / network state from XAML [avoid].

| Name | Kind | What it does |
|---|---|---|
| `HttpRequestAction` | Action | An action that performs an HTTP request. |
| `NetworkInformationTrigger` | Trigger | A trigger that listens to network availability changes. |

### StorageProvider · `Xaml.Behaviors.Interactions`

Use when: Opening pickers straight from XAML and pushing the result to a command [avoid in NetPrints: IFilePickerService keeps commands testable].

| Name | Kind | What it does |
|---|---|---|
| `OpenFilePickerAction` | Action | An action that will open a file picker dialog. |
| `OpenFolderPickerAction` | Action | An action that will open a folder picker dialog. |
| `SaveFilePickerAction` | Action | An action that will open a file picker dialog. |

### StorageProvider/Button · `Xaml.Behaviors.Interactions`

Use when: Same, attached to a Button [avoid, see above].

| Name | Kind | What it does |
|---|---|---|
| `ButtonOpenFilePickerBehavior` | Behavior | Open file picker behavior for Button. |
| `ButtonOpenFolderPickerBehavior` | Behavior | Open folder picker behavior for Button. |
| `ButtonSaveFilePickerBehavior` | Behavior | Save file picker behavior for Button. |

### StorageProvider/MenuItem · `Xaml.Behaviors.Interactions`

Use when: Same, attached to a MenuItem [avoid, see above].

| Name | Kind | What it does |
|---|---|---|
| `MenuItemOpenFilePickerBehavior` | Behavior | Open file picker behavior for MenuItem. |
| `MenuItemOpenFolderPickerBehavior` | Behavior | Open folder picker behavior for MenuItem. |
| `MenuItemSaveFilePickerBehavior` | Behavior | Save file picker behavior for MenuItem. |

### Collections · `Xaml.Behaviors.Interactions.Custom`

Use when: Mutating collections from XAML [avoid: VM owns collections].

| Name | Kind | What it does |
|---|---|---|
| `AddRangeAction` | Action | Adds a range of items to a target IList when invoked. |
| `ClearCollectionAction` | Action | Clears all items from a target IList when invoked. |
| `CollectionChangedBehavior` | Behavior | Executes different sets of actions when the observed collection changes. |
| `CollectionChangedTrigger` | Trigger | Executes associated actions whenever the bound collection raises a INotifyCollectionChanged.CollectionChanged event. |
| `RemoveRangeAction` | Action | Removes a range of items from a target IList when invoked. |

### Debugging · `Xaml.Behaviors.Interactions.Custom`

Use when: Break/log from XAML while debugging [never commit].

| Name | Kind | What it does |
|---|---|---|
| `BreakAction` | Action | An action that triggers a debugger break. |
| `LogAction` | Action | An action that logs a message to the debug output. |
| `VisualDebugBehavior` | Trigger | A behavior that visualizes events on the attached control for debugging purposes. |

### Dialog · `Xaml.Behaviors.Interactions.Custom`

Use when: Show a dialog window from XAML [avoid: WindowService opens dialogs].

| Name | Kind | What it does |
|---|---|---|
| `DialogClosedTrigger` | Trigger | Executes actions when the dialog window is closed. |
| `DialogOpenedTrigger` | Trigger | Executes actions when the dialog window is opened. |
| `ShowDialogAction` | Action | Shows a Window as a dialog. |

### FileUpload · `Xaml.Behaviors.Interactions.Custom`

Use when: Browser-style upload flows [not used].

| Name | Kind | What it does |
|---|---|---|
| `ButtonUploadFileBehavior` | Behavior | Upload file behavior for Button. |
| `UploadCompletedTrigger` | Trigger | Executes actions when the bound value becomes true. |
| `UploadFileAction` | Action | Asynchronously uploads a file to a specified URL and invokes a command when completed. |

### Logic · `Xaml.Behaviors.Interactions.Custom`

Use when: Conditional/switch action routing in XAML [avoid: decisions belong in the VM].

| Name | Kind | What it does |
|---|---|---|
| `ConditionalAction` | Action | An action that executes different collections of actions depending on the specified condition. |
| `ConditionalBehavior` | Behavior | A behavior that executes different collections of actions depending on the specified condition. |
| `SwitchCaseAction` | Action | An action that executes a specific set of actions based on a value match. |
| `SwitchCaseBehavior` | Behavior | A behavior that executes a specific set of actions based on a value match. |

### System · `Xaml.Behaviors.Interactions.Custom`

Use when: Clipboard monitor / network status [avoid].

| Name | Kind | What it does |
|---|---|---|
| `ClipboardMonitorBehavior` | Behavior | A behavior that monitors the system clipboard for specific data formats. |
| `NetworkStatusTrigger` | Trigger | A trigger that fires when the network status changes. |

### ViewModel · `Xaml.Behaviors.Interactions.Custom`

Use when: Set/toggle VM properties from XAML [avoid: bypasses commands, untestable].

| Name | Kind | What it does |
|---|---|---|
| `IncrementViewModelPropertyAction` | Action | Increments a numeric view model property when invoked. |
| `SetViewModelPropertyAction` | Action | Sets a view model property to a specified value when invoked. |
| `SetViewModelPropertyOnLoadBehavior` | Behavior | Sets a view model property when the associated control is loaded. |
| `ToggleViewModelBooleanAction` | Action | Toggles a boolean view model property when invoked. |
| `ViewModelPropertyChangedTrigger` | Trigger | Triggers when the specified view model property changes. |

### ReactiveUI · `Xaml.Behaviors.Interactions.ReactiveUI` (package not referenced)

Use when: ReactiveUI routing [not applicable: NetPrints uses CommunityToolkit.Mvvm].

| Name | Kind | What it does |
|---|---|---|
| `ClearNavigationStackAction` | Action | An action that resets the navigation stack. |
| `InteractionTriggerBehavior<TInput, TOutput>` | Trigger | A behavior that registers a handler for a Interaction{TInput,TOutput} and executes its actions when the interaction is triggered. |
| `NavigateAction` | Action | An action that navigates to a specified IRoutableViewModel. |
| `NavigateAndReset` | Action | An action that navigates to a specified IRoutableViewModel and resets the navigation stack. |
| `NavigateBackAction` | Action | An action that navigates back in the RoutingState stack. |
| `NavigateToAction<TViewModel>` | Action | An action that resolves and navigates to a view model of type . |
| `NavigateToAndResetAction<TViewModel>` | Action | An action that resolves and navigates to and clears the navigation stack. |

### Scripting · `Xaml.Behaviors.Interactions.Scripting` (package not referenced)

Use when: Run C# scripts from XAML [never].

| Name | Kind | What it does |
|---|---|---|
| `ExecuteScriptAction` | Action | Executes a C# script using Roslyn scripting API. |
