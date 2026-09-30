# Drag and drop

Drag item VMs onto drop targets, reorder lists, and move elements by dragging.

Part of the Xaml.Behaviors 12.0.7 catalog; `README.md` in this folder is the index. Prebuilt types need no xmlns prefix.

## In NetPrints today

`MemberVariableView.axaml` starts a variable drag from pointer handlers in code-behind, which D1 allows as gesture
mechanics. For a new drag of an item VM from a list onto a target, prefer `ContextDragBehavior Context="{Binding}"`
on the source and `ContextDropBehavior` with a `DropHandlerBase` subclass on the target; the handler calls one VM
command. Reordering (`ListReorderDragBehavior`, `ItemDragBehavior`) needs the `Xaml.Behaviors.Interactions.Draggable`
package, which isn't referenced: add it as `README.md` describes and say why in the PR.

## Catalog

### DragAndDrop · `Xaml.Behaviors.Interactions.DragAndDrop`

Use when: Drag sources and drop targets with context objects, handlers or commands; file/text drops [ContextDragBehavior + ContextDropBehavior/IDropHandler for list-to-graph drags].

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

### ManagedDragDrop · `Xaml.Behaviors.Interactions.DragAndDrop`

Use when: In-process drag/drop without the platform DataTransfer (managed preview) [alternative for list-to-graph].

| Name | Kind | What it does |
|---|---|---|
| `ManagedContextDragBehavior` | Behavior | Behavior that initiates an in-process managed drag with an optional preview window. This avoids OS drag-drop and integrates with ManagedDragDropService. |
| `ManagedContextDropBehavior` | Behavior | Drop target behavior that integrates with ManagedDragDropService. It mirrors the semantics of ContextDropBehavior but works entirely in-process. |

### Draggable · `Xaml.Behaviors.Interactions.Draggable` (package not referenced)

Use when: Move elements or reorder list items by dragging, auto-scroll while dragging [list reordering of methods/variables if ever needed].

| Name | Kind | What it does |
|---|---|---|
| `AutoScrollDuringDragBehavior` | Behavior | Automatically scrolls the associated ScrollViewer when the pointer is dragged near its edges. |
| `CanvasDragBehavior` | Behavior | Enables dragging of child controls within a Canvas. |
| `GridDragBehavior` | Behavior | Allows dragging of grid child controls with optional layout copying. |
| `ItemDragBehavior` | Behavior | Allows dragging items within an ItemsControl. |
| `ListReorderDragBehavior` | Behavior | Allows reordering of items inside an ItemsControl while displaying a placeholder at the insertion point. |
| `MouseDragElementBehavior` | Behavior | Enables dragging of a control with the mouse using a TranslateTransform. |
| `MultiMouseDragElementBehavior` | Behavior | Enables dragging of multiple controls with the mouse using TranslateTransform. |
