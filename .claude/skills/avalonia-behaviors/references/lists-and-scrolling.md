# Lists, trees, tabs and scrolling

Items controls, list and tree items, selection, tabs and scrolling to new items.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Contents

- Recipes: Follow a growing output list
- Catalog: Behaviors; ItemsControl; ListBox; ListBoxItem; SelectingItemsControl; TreeView; TreeViewItem; ScrollViewer; TabControl; Carousel

## Recipes

### Follow a growing output list

*No repo example: check the properties with `scripts/behavior-props.cs` and build.* Replaces a `CollectionChanged` handler that calls `ScrollToEnd()`. How it works (checked in
the 12.0.7 assembly): attached to a `ScrollViewer`, it scrolls to the end whenever the content's height grows;
attached to an `ItemsControl`/`ListBox`, it finds the inner `ScrollViewer` and also scrolls when items are added. It
stops following while the user has scrolled up and resumes once they are back at the bottom. It does nothing on a
`TextBox`, so for text output wrap the growing text (`SelectableTextBlock`, or a `TextBox` without its own fixed
height) in a `ScrollViewer` and attach the behavior to that:

```xml
<ListBox ItemsSource="{Binding OutputLines}">
  <Interaction.Behaviors>
    <AutoScrollToBottomBehavior />
  </Interaction.Behaviors>
</ListBox>
```

```xml
<ScrollViewer>
  <Interaction.Behaviors>
    <AutoScrollToBottomBehavior />
  </Interaction.Behaviors>
  <SelectableTextBlock Text="{Binding Output}" FontFamily="monospace" />
</ScrollViewer>
```

## Catalog

### Behaviors · `Xaml.Behaviors.Interactions.Custom`

Use when: Auto-scroll log/output lists to newest item [program output / error list candidates].

| Name | Kind | What it does |
|---|---|---|
| `AutoScrollToBottomBehavior` | Behavior | A behavior that automatically scrolls to the bottom of a ScrollViewer or ItemsControl when new items are added. |

### ItemsControl · `Xaml.Behaviors.Interactions.Custom`

Use when: Container lifecycle triggers, nudge-on-drop, add/insert/clear items [container triggers only; VM owns items].

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

### ListBox · `Xaml.Behaviors.Interactions.Custom`

Use when: Select/unselect all, remove item [VM should own removal].

| Name | Kind | What it does |
|---|---|---|
| `ListBoxSelectAllBehavior` | Behavior | ListBoxSelectAllBehavior (no XML summary; see source). |
| `ListBoxUnselectAllBehavior` | Behavior | ListBoxUnselectAllBehavior (no XML summary; see source). |
| `RemoveItemInListBoxAction` | Action | Allows a user to remove the item from a ListBox ItemTemplate. |

### ListBoxItem · `Xaml.Behaviors.Interactions.Custom`

Use when: Hover-to-select lists (menus, suggestion lists).

| Name | Kind | What it does |
|---|---|---|
| `SelectListBoxItemOnPointerMovedBehavior` | Behavior | Sets ListBoxItem.IsSelected property to true of the associated ListBoxItem control on InputElement.PointerMoved event. |

### SelectingItemsControl · `Xaml.Behaviors.Interactions.Custom`

Use when: Selection events and type-to-search on lists.

| Name | Kind | What it does |
|---|---|---|
| `SelectingItemsControlSearchBehavior` | Behavior | Filters SelectingItemsControl items based on the text of a search box. |

### TreeView · `Xaml.Behaviors.Interactions.Custom`

Use when: Filter a TreeView from a text box [future class/type browsers].

| Name | Kind | What it does |
|---|---|---|
| `ApplyTreeViewFilterAction` | Action | Filters a TreeView using the provided query string. |
| `TreeViewFilterBehavior` | Behavior | Filters TreeView items based on the text of a search box. |
| `TreeViewFilterTextChangedTrigger` | Trigger | Executes actions when the search box text changes. |

### TreeViewItem · `Xaml.Behaviors.Interactions.Custom`

Use when: Expand/collapse on double-tap.

| Name | Kind | What it does |
|---|---|---|
| `ToggleIsExpandedOnDoubleTappedBehavior` | Behavior | Toggles TreeViewItem.IsExpanded property of the associated TreeViewItem control on InputElement.DoubleTapped event. |

### ScrollViewer · `Xaml.Behaviors.Interactions.Custom`

Use when: Scroll offset binding, scroll-to-offset, viewport info [ScrollIntoView replacements].

| Name | Kind | What it does |
|---|---|---|
| `HorizontalScrollViewerBehavior` | Behavior | Enables horizontal scrolling of a ScrollViewer using the mouse wheel. |
| `ScrollChangedTrigger` | Trigger | Executes actions when the ScrollViewer.ScrollChanged event occurs. |
| `ScrollToOffsetAction` | Action | Scrolls a ScrollViewer to the specified offsets when executed. |
| `ScrollViewerOffsetBehavior` | Behavior | Sets the ScrollViewer.Offset of the associated ScrollViewer. |
| `ViewportBehavior` | Behavior | Listens for the associated element entering or exiting the parent ScrollViewer viewport. |

### TabControl · `Xaml.Behaviors.Interactions.Custom`

Use when: Keyboard tab navigation and tab selection triggers [bottom tabs in ClassEditorWindow].

| Name | Kind | What it does |
|---|---|---|
| `TabControlKeyNavigationBehavior` | Behavior | Enables keyboard navigation for a TabControl using arrow keys. |
| `TabControlNextAction` | Action | Advances the target TabControl to the next tab. |
| `TabControlPreviousAction` | Action | Moves the target TabControl to the previous tab. |
| `TabControlSelectionChangedTrigger` | Trigger | Executes actions when the associated TabControl changes selection. |

### Carousel · `Xaml.Behaviors.Interactions.Custom`

Use when: Carousel navigation [not used].

| Name | Kind | What it does |
|---|---|---|
| `CarouselKeyNavigationBehavior` | Behavior | Enables keyboard navigation for a Carousel using arrow keys. |
| `CarouselNextAction` | Action | Advances the target Carousel to the next page. |
| `CarouselPreviousAction` | Action | Moves the target Carousel to the previous page. |
| `CarouselSelectionChangedTrigger` | Trigger | Executes actions when the associated Carousel changes selection. |
