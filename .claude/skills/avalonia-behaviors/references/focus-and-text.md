# Focus and text input

Focus a control when it opens or appears, select all text, and auto-complete boxes.

Part of the `avalonia-behaviors` skill: its SKILL.md routes each job here, and `catalog-guide.md` explains the stance
tags. Prebuilt types need no xmlns prefix.

## Recipes

### Focus and select all when a view opens

*Not yet used in the repo.* Replaces `Focus()` and `SelectAll()` calls in `OnOpened` or `OnAttachedToVisualTree`:

```xml
<TextBox Text="{Binding Name}">
  <Interaction.Behaviors>
    <FocusOnAttachedToVisualTreeBehavior />
    <TextBoxSelectAllOnGotFocusBehavior />
  </Interaction.Behaviors>
</TextBox>
```

For a control that appears later (an inline rename box that becomes visible), use `FocusOnVisibleBehavior`.

## Catalog

### Focus · `Xaml.Behaviors.Interactions.Custom`

Use when: Focus on attach/visible/pointer, focus the selected item, trap focus [replaces Focus() calls in code-behind].

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

### TextBox · `Xaml.Behaviors.Interactions.Custom`

Use when: Select-all on focus / auto-select [inspector name boxes].

| Name | Kind | What it does |
|---|---|---|
| `AutoSelectBehavior` | Behavior | Automatically selects the entire content of the associated TextBox when it is loaded. |
| `TextBoxSelectAllOnGotFocusBehavior` | Behavior | A behavior that allows to select all TextBox text on got focus event. |
| `TextBoxSelectAllTextBehavior` | Behavior | TextBoxSelectAllTextBehavior (no XML summary; see source). |

### AutoCompleteBox · `Xaml.Behaviors.Interactions.Custom`

Use when: AutoCompleteBox focus/drop-down/selection glue [SelectTypeDialog candidate].

| Name | Kind | What it does |
|---|---|---|
| `AutoCompleteBoxOpenDropDownOnFocusBehavior` | Behavior | Opens the drop-down of an AutoCompleteBox when it receives focus. |
| `AutoCompleteBoxSelectionChangedTrigger` | Trigger | Triggers actions when the AutoCompleteBox selection changes. |
| `ClearAutoCompleteBoxSelectionAction` | Action | Clears the selection and text of an AutoCompleteBox. |
| `FocusAutoCompleteBoxTextBoxBehavior` | Behavior | FocusAutoCompleteBoxTextBoxBehavior (no XML summary; see source). |
