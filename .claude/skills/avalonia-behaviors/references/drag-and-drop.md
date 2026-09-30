# Drag and drop

Drag item VMs onto drop targets, reorder lists, and move elements by dragging.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- The NetPrints pattern
- Catalog: DragAndDrop; ManagedDragDrop; Draggable

## The NetPrints pattern

Nothing in the repo uses the drag-and-drop behaviors yet: `MemberVariableView.axaml` starts its variable drag from
pointer handlers in code-behind, which D1 allows as gesture mechanics. For a new drag, use this pattern.

- **Drag an item VM onto a target, or reorder a list:** `ContextDragBehavior` on the source and `ContextDropBehavior`
  with a `DropHandlerBase` subclass on the target. The handler only works out the items and calls one VM command,
  which changes the model through an undoable edit. The command gets the unit test; the handler stays thin.
- **When the row already starts another drag** (a name dragged onto the graph), put the new drag on a separate grip,
  so the two gestures don't fight over one pointer press. Give keyboard users a Move up/down command too (D13).
- **Not the Draggable package.** `ItemDragBehavior` reorders by calling `RemoveAt`/`Insert` on the bound
  `ItemsSource` list itself, and `ListReorderDragBehavior` only draws a drop placeholder. Neither calls a command, so
  the model and undo miss the move. The package isn't referenced either.

Sketch, not compiled (`ItemVM`, `ListVM`, `MoveRequest` and `MoveCommand` stand for your own types; the
`DropHandlerBase` signatures are the 12.0.7 ones):

```xml
<ListBox ItemsSource="{Binding Items}">
  <Interaction.Behaviors>
    <ContextDropBehavior Context="{Binding}" Handler="{x:Static edb:ReorderDropHandler.Instance}" />
  </Interaction.Behaviors>
  <ListBox.ItemTemplate>
    <DataTemplate x:DataType="ed:ItemVM">
      <Grid ColumnDefinitions="Auto,*" Background="Transparent">
        <mi:MaterialIcon Kind="DragVertical" Background="Transparent">
          <Interaction.Behaviors>
            <ContextDragBehavior Context="{Binding}" />
          </Interaction.Behaviors>
        </mi:MaterialIcon>
        <TextBlock Grid.Column="1" Text="{Binding Name}" />
      </Grid>
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

```csharp
public sealed class ReorderDropHandler : DropHandlerBase
{
    public static ReorderDropHandler Instance { get; } = new();

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state) =>
        sourceContext is ItemVM && targetContext is ListVM && TargetItem(e) is not null;

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (sourceContext is not ItemVM moved || targetContext is not ListVM list || TargetItem(e) is not { } target)
        {
            return false;
        }

        var request = new MoveRequest(moved, target);
        if (!list.MoveCommand.CanExecute(request))
        {
            return false;
        }

        list.MoveCommand.Execute(request);
        return true;
    }

    private static ItemVM? TargetItem(DragEventArgs e) =>
        (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as ItemVM;
}
```

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

Use when: Move elements or reorder list items by dragging, auto-scroll while dragging [avoid: edits the bound list directly, bypassing commands and undo; use ContextDrag/ContextDrop and a VM command].

| Name | Kind | What it does |
|---|---|---|
| `AutoScrollDuringDragBehavior` | Behavior | Automatically scrolls the associated ScrollViewer when the pointer is dragged near its edges. |
| `CanvasDragBehavior` | Behavior | Enables dragging of child controls within a Canvas. |
| `GridDragBehavior` | Behavior | Allows dragging of grid child controls with optional layout copying. |
| `ItemDragBehavior` | Behavior | Allows dragging items within an ItemsControl. |
| `ListReorderDragBehavior` | Behavior | Allows reordering of items inside an ItemsControl while displaying a placeholder at the insertion point. |
| `MouseDragElementBehavior` | Behavior | Enables dragging of a control with the mouse using a TranslateTransform. |
| `MultiMouseDragElementBehavior` | Behavior | Enables dragging of multiple controls with the mouse using TranslateTransform. |
