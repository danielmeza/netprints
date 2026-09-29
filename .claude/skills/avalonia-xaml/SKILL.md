---
name: avalonia-xaml
description: "Rules and examples for creating or editing Avalonia 12 .axaml views, code-behind (*.axaml.cs), styles, ControlThemes, resources, converters and Xaml.Behaviors in NetPrints (src/NetPrints.Editor, src/NetPrints.Desktop). Use it whenever a change touches *.axaml, *.axaml.cs, an IValueConverter, a Style/ControlTheme/theme resource, or a view model command bound from XAML."
---

# Avalonia XAML in NetPrints

Stack: .NET 10, Avalonia 12.1, Nodify.Avalonia 2.0, CommunityToolkit.Mvvm 8.4, AvaloniaEdit 12,
Material.Icons, Xaml.Behaviors 12.x. Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault`).

The rules come in three tiers:
- **Enforced**: a test checks it (`XamlHygieneTests`), so a violation fails the build. To make an exception, add an allowlist entry with a reason.
- **Default**: follow it unless you have a reason not to. If you deviate, give the reason in the PR description.
- **Consider**: tips that are worth knowing. Nobody has to follow them.

Every rule has a boundary, and the boundary is part of the rule. When a rule seems to forbid something reasonable, read it
again: the rules target *misplaced logic*, not the tools themselves.

## Enforced

- **E1. No compiled-binding opt-outs.** `x:CompileBindings="False"` and `{ReflectionBinding}` are allowlist-only.
  *Why:* a reflection binding fails silently at runtime. A compiled one fails at build time and is faster.
  Fix: give the template a type (`<DataTemplate x:DataType="core:MethodSpecifier">`). A path-less
  `{Binding Converter=...}` then compiles, as it already does in `SelectMethodDialog.axaml`.
- **E2. No color literals in views.** A view is any `.axaml` file whose root is not `Application`, `Styles` or `ResourceDictionary`. Views may not use hex values or named colors on brush or color properties.
  `Transparent` is allowed, because it makes an element hit-testable.
  *Why:* a hardcoded color does not follow the Light/Dark variant. Put a token in `EditorStyles.axaml` instead (see D7).
- **E3. Command-shaped events are not wired in XAML.** `Click`, `Tapped`, `DoubleTapped`, `KeyDown`/`KeyUp`,
  `SelectionChanged`, `TextChanged`, `GotFocus`/`LostFocus` and similar handlers need an allowlist entry.
  Pointer and drag/drop events (`PointerPressed/Moved/Released`, `DragDrop.*`) are allowed, because gestures are view mechanics.
  *Why:* each UI action is one view-model command with its own unit test. Wire it with `Command`, `KeyBinding` or a behavior (D11).
- **E4. AutomationIds come from `AutomationIds`.** Write `AutomationProperties.AutomationId="{x:Static ed:AutomationIds.X}"`.
  Never use a string literal. *Why:* the UI and E2E tests share one set of constants, which lets renames compile-check.
- **E5. Icon-only buttons have an accessible name.** Any `Button` whose only content is a `MaterialIcon`, `PathIcon` or `Image`
  must set `AutomationProperties.Name` (or `LabeledBy`). *Why:* the automation peer reads only Name/LabeledBy,
  so `ToolTip.Tip` alone leaves a screen reader announcing "button".
  ```xml
  <Button Classes="icon" ToolTip.Tip="Remove variable" AutomationProperties.Name="Remove variable"
          Command="{Binding RemoveCommand}"><mi:MaterialIcon Kind="Minus" /></Button>
  ```
- **E6. Theme tokens are never looked up with `StaticResource`.** A key defined under `ThemeDictionaries` must be
  referenced with `DynamicResource`. *Why:* `StaticResource` cannot see theme dictionaries, so the lookup throws at runtime or freezes one variant.

## Default

**D1. XAML first; code-behind only for view mechanics.** Wire behavior in XAML: bindings, `Command`, styles,
`KeyBinding` and behaviors. Code-behind is fine for work that is purely about the view and would be awkward in XAML. Examples: drag gestures, the
Nodify viewport math (`GraphEditorView.ToGraph`), AvaloniaEdit interop (`CodeView`), and focus or scroll plumbing
that no behavior covers. Keep such handlers small: they translate the gesture and then call **one** view-model command.
They must not decide domain questions. *Why:* a command can be tested without a window, and a handler cannot.
- Every new UI action gets a `[RelayCommand]` on the VM and a unit test that runs it without the view.
- `InitializeComponent`, `DataContext` setup and a named-control accessor are not "logic". Leave them alone.

**D2. Commands are generated `[RelayCommand]`s.** Don't bind `Command` to a plain VM method. Avalonia 12.1 accepts
only a parameterless method or one that takes a single `object`, and a plain method has no `CanExecute`. Use `CanExecute = nameof(...)` plus
`[NotifyCanExecuteChangedFor]` on the properties it reads. A button's enabled state comes from
`CanExecute`, not from binding `IsEnabled` separately.

**D3. Every binding scope is typed.** Put `x:DataType` on the root, on every `DataTemplate`, and on any `Style` or
`ControlTheme` that contains bindings. When a binding crosses into another scope, cast it:
```xml
Command="{Binding $parent[ListBox].((edclasseditor:ClassEditorVM)DataContext).RemoveMethodCommand}"
```
A view with no bindings, such as the code-behind dialogs, doesn't need `x:DataType`. Give it one when it gets a VM.

**D4. Keep reach-ups short.** Bind to the *nearest* stable owner: the `ListBox` or `ItemsControl` that holds the items, or
the `UserControl`. Avoid `$parent[Window]` from inside a template, and don't use numeric hops such as `$parent[Border;2]`. Both break
when the layout is refactored. Better options, in order: (1) the item VM exposes the command itself; (2) `$parent[ItemsHost]`;
(3) restructure so that no reach-up is needed. For example, the inspectors in `ClassEditorWindow` read
`$parent[Window]...ShowVariableInspector` only because their DataContext is overridden. Wrap them instead:
```xml
<Panel IsVisible="{Binding ShowVariableInspector}">
  <edinspectors:VariableInspectorView DataContext="{Binding SelectedVariable}" />
</Panel>
```

**D5. Converters decide how a state *looks*; the VM decides *what* the state is.** Writing a converter is
normal and encouraged for reusable, view-only transformations. A converter may map app or business *state* to
visuals: brushes, colors, icons, visibility, opacity, thickness, text formatting.
- Allowed: `NodeKindBrushConverter` (`NodeVisualKind` to header brush), or a `CompileStatus` to status-brush converter.
- Not allowed: a `CanConnectConverter` that asks the type system whether two pins are compatible, or a converter
  that resolves a type name through the reflection provider. Those compute the state. Expose `IsCompatible` or
  `ResolvedType` on the VM, then (if you like) convert *that* into a brush.
- A converter must never validate, apply rules, call services or the model, or cause side effects.
- Shape: `public sealed`, a `static readonly Instance` referenced with `{x:Static}` (the repo convention), and
  `ConvertBack` throwing `NotSupportedException` unless the binding is TwoWay. Return
  `AvaloniaProperty.UnsetValue` for unexpected input. Give it a plain xUnit test with no Avalonia app required.
- Built-ins that often remove the need for a converter: `!`/`!!` negation, `ObjectConverters.IsNotNull`,
  `StringConverters.IsNullOrEmpty`, `BoolConverters.And`, `StringFormat`, `MultiBinding`, and `FuncValueConverter` for a one-off.

**D6. Assign typed data templates by `DataType`.** Match views to VMs with `DataTemplate x:DataType`, either in place or in
`DataTemplates`. There is no reflection `ViewLocator`. *Why:* the templates are compile-checked and trimming-safe.

**D7. Colors and brushes are theme tokens.** Define them in `ThemeDictionaries` (`Dark`, `Light`, and optionally `Default`
as a fallback) in `EditorStyles.axaml`, named `Area.Role` (for example `GraphGrid.MinorColor`, `Node.HeaderForeground`). Reference them with
`DynamicResource`. Use Fluent's `System*` keys (`SystemAccentColor`, `SystemControlForegroundBaseMediumBrush`)
before inventing a new one. Use `StaticResource` for things that never vary by theme: templates, `ControlTheme`s, sizes.
Colors computed in C# (`GraphBrushes`) should move to tokens once they need a light variant.

**D8. Styles for tweaks, ControlThemes for re-templating.**
- `Style` + class (`<Style Selector="Button.flat">`) is for additive property changes. Styles cascade and stack.
- `ControlTheme` (`TargetType`, `BasedOn="{StaticResource {x:Type nodify:ItemContainer}}"`) replaces a control's
  whole look or template. Only one theme applies at a time. Use `^` for nested selectors.
- App-wide styles go in `EditorStyles.axaml`. The palette (`ColorPaletteResources`) and app resources go in `EditorApp.axaml`.
  Keep a resource local (`UserControl.Resources` or `.Styles`) when only one view uses it, and promote it when a second view needs it.
- Style classes are lowercase, and kebab-case when multi-word (`round`, `flat`, `icon`, `drop-target`). Existing classes
  such as `pinValue` keep their names. Don't rename them just to conform.
- Prefer a style class to repeating the same five inline setters. For a color change, use a style, not a template.

**D9. Keyboard shortcuts are bindings to commands.** Put them in `<Window.KeyBindings>` or `<UserControl.KeyBindings>`, using a
`KeyBinding` with a `Gesture` and a `Command`. A `KeyBinding` fires only while focus is inside that element. Use `HotKey` on the
`Button` or `MenuItem` that already shows the action, and `InputGesture` to display it in menus. Write `Ctrl+`: Avalonia maps
it to Cmd on macOS (per the docs; confirm on the Mac mini). When the focused control swallows the key first (a
TextBox or Nodify), use a tunnel-routed behavior (`ExecuteCommandOnKeyDownBehavior EventRoutingStrategy="Tunnel"`)
before falling back to code-behind.

**D10. Long work goes in async commands and shows busy state.** Write `[RelayCommand] async Task XAsync(CancellationToken ct)`.
- Bind busy UI to `XCommand.IsRunning`. Use a VM `IsBusy` flag with `try/finally` only when one flag spans
  several operations (as `MainEditorVM` does). Delay indicators for fast operations (`BusyIndicatorDelay`).
- `AllowConcurrentExecutions` stays `false`, which disables the button while a run is in progress. Add `IncludeCancelCommand = true` when the user can cancel.
- Keep the default of rethrowing: a fault reaches the error dialog. Don't use `FlowExceptionsToTaskScheduler`, and don't
  write `ExecuteAsync(...).Forget()` from a view.
- Test with `await vm.XCommand.ExecuteAsync(null)`. Don't block on the UI thread, and do heavy work off it.

**D11. Use a prebuilt behavior before you write code-behind or a custom behavior.** The package is
`Xaml.Behaviors.*` 12.x, and its types sit in the default `https://github.com/avaloniaui` xmlns, so no prefix is needed. Prefer the typed
behaviors and triggers over `EventTriggerBehavior EventName="..."`, which uses reflection and is not trim-safe. The full generated
catalog (383 types across 10 packages) is `behaviors-catalog.md`, next to this file; the table below is the short version.

| For | Prefer the prebuilt |
|---|---|
| Tap / double-tap / right-tap runs a command | `ExecuteCommandOnTappedBehavior`, `ExecuteCommandOnDoubleTappedBehavior`, `ExecuteCommandOnRightTappedBehavior` |
| A key in one control (Enter in a box, Down to the list) | `ExecuteCommandOnKeyDownBehavior Key="Enter"` (or `Gesture`), `KeyTrigger` + actions |
| Several actions for one event | `KeyDownTrigger`, `DoubleTappedTrigger`, `ClickEventTrigger`… + `InvokeCommandAction`, `FocusControlAction`, `ChangePropertyAction` |
| OK or Close button closes the dialog with no result | `ButtonClickEventTriggerBehavior` + `CloseWindowAction` |
| Accept/cancel button closes the dialog *with* a result | no prebuilt fits (batch X2b): a VM deriving from `DialogVM<TResult>` (`NetPrints.Editor.Dialogs`) plus the custom `DialogCloseBehavior` (`NetPrints.Editor.Behaviors`) below |
| Focus on open, show or click | `FocusOnAttachedToVisualTreeBehavior`, `FocusOnVisibleBehavior`, `FocusControlAction`, `FocusSelectedItemBehavior` |
| Select all or commit on Enter in text boxes | `TextBoxSelectAllOnGotFocusBehavior`, `LoseFocusOnEnterBehavior` |
| Drag an item VM from a list onto a target | `ContextDragBehavior Context="{Binding}"` + `ContextDropBehavior Handler=...` (`DropHandlerBase`) |
| Reordering a list by drag | `ListReorderDragBehavior`, `ItemDragBehavior`, `AutoScrollDuringDragBehavior` |
| Lifecycle (Loaded, DataContext changed, theme changed) | `LoadedTrigger`, `DataContextChangedTrigger`, `ActualThemeVariantChangedTrigger` |
| Follow a growing log or list | `AutoScrollToBottomBehavior` |
| Popups and flyouts | `PopupOpenedTrigger`, `HideFlyoutAction`, `ButtonHideFlyoutOnClickBehavior` (canvas popups stay `CanvasPopup`, per ADR-0004) |
| One event, one command, no typed behavior fits | `EventTriggerBehavior EventName="..."` + `InvokeCommandAction Command="..." CommandParameter="{Binding}"` (reflection-based; prefer a typed behavior above when one exists) |

Don't use the behaviors that bypass the VM or its services. These are the clipboard, file-system, storage-picker, HTTP, `SetViewModelProperty`,
`ToggleViewModelBoolean`, `ConditionalAction`/`SwitchCaseAction`, `Collections`, `Scripting` and dialog behaviors. The
decisions and side effects they perform belong in commands and services (`IClipboardService`, `IFilePickerService`, `IWindowService`).
Write a custom behavior (derive from `StyledElementBehavior<T>`) only when no prebuilt one fits and the logic is reusable view
mechanics. Keep it in `NetPrints.Editor/Behaviors/` and give it a headless test.

**Dialog-close-with-result pattern (batch X2b).** `Window.Close(object? dialogResult)` needs a value, and no
prebuilt behavior can hand it one from a VM, so this is the one case D11 keeps a custom behavior for:
`SelectMethodDialog`, `SelectTypeDialog` and `TrustDialog` each have a small VM deriving from
`DialogVM<TResult>`, whose accept/cancel `[RelayCommand]`s call `RequestClose(result)` (sets `Result`,
raises `IDialogCloseSource.CloseRequested`). `DialogCloseBehavior`, attached once on the dialog `Window`,
watches its own (auto-synced) `DataContext` for `IDialogCloseSource` and calls `window.Close(source.Result)`.
The `Window` subclass still implements `IDialogResult<T>` by forwarding `Result` to the VM, so
`EditorDialogs`'s existing no-owner fallback and any test reading `dialog.Result` keep working unchanged.
A dialog that closes with no result (`ErrorDialog`, `IssuesDialog`, `ReferencesDialog`) does not need this:
`ButtonClickEventTriggerBehavior` + `CloseWindowAction` (row above) is enough.
```xml
<ListBox.ItemTemplate><DataTemplate x:DataType="edevents:EventGraphVM">
  <TextBlock Text="{Binding Name}">
    <Interaction.Behaviors>
      <ExecuteCommandOnDoubleTappedBehavior CommandParameter="{Binding}"
        Command="{Binding $parent[ListBox].((edclasseditor:ClassEditorVM)DataContext).OpenEventGraphCommand}" />
    </Interaction.Behaviors>
  </TextBlock>
</DataTemplate></ListBox.ItemTemplate>
```

**D12. Lists that can grow virtualize.** `ListBox` virtualizes by default, but a plain `ItemsControl` does not. For a list that can grow, either
use `ListBox` or give the `ItemsControl` an `ItemsPanel` of `VirtualizingStackPanel`. Never put a virtualizing list inside
a `StackPanel` or `ScrollViewer`, because it then gets infinite height and realizes every item. Constrain it with a `Grid` row instead.
A short, fixed list (dialog rows, a node's pins) can stay an `ItemsControl` in a `ScrollViewer`. Keep item templates flat,
because every element in them is multiplied by the item count.

**D13. Accessibility beyond E4 and E5.** Label inspector fields with `AutomationProperties.LabeledBy="{Binding #NameLabel}"`
or `AutomationProperties.Name`. Keep everything reachable by keyboard: tab order follows the visual order, and don't set `IsTabStop="False"` on
something that can be activated. Set decorative images to `AutomationProperties.AccessibilityView="Raw"`. An element the UI tests touch
gets an `AutomationIds` constant.

**D14. Design-time data stays out of runtime.** A view with a non-trivial layout gets
`<Design.DataContext><vm:XDesignVM /></Design.DataContext>` or a static design instance so that the previewer renders it.
Design VMs live beside the view and are never used at runtime. Also use `Design.PreviewWith` for style files.

**D15. Use `x:Name` only when something reads it.** That means code-behind, a `#Name` binding, or a behavior's `TargetControl`. Names are
PascalCase nouns with a role suffix (`SearchBox`, `ResultList`, `GraphEditor`). Tests find elements by AutomationId, not by name.
Avalonia generates a field for each name, so an unused name is noise (for example `InputPins`, `NamespaceBox`, `ReferenceList`).

**D16. Windows and popups go through the existing hosts.** Open dialogs through `IWindowService` and return results
from a VM. The view should not compute them (`SelectTypeDialog.ResolveSelection` should move into a VM). Canvas overlays use
`CanvasPopup` (ADR-0004, which is already enforced).

## Consider

- `Mode=OneTime` for values that never change after load, such as labels built from immutable specifiers.
- Use `IsHitTestVisible="False"` on decorative overlays. Use `Background="Transparent"` only on elements that must catch the pointer.
- One `Grid` beats nested `StackPanel`s in repeated templates (`NodeView` is rendered for every node). Use the
  panel's own `Background`/`BorderBrush` instead of wrapping it in an extra `Border`. `BoxShadow` and stacked translucency cost fill rate.
- Use `TextBlock` rather than a read-only `TextBox` for display-only text, unless the text must be selectable (as in `ErrorDialog`).
- Use `#Name` element bindings to connect two views without code, for example
  `ViewportLocation="{Binding #Editor.ViewportLocation}"` instead of syncing it in `PropertyChanged`.
- Avalonia 12 API names: `DataTransfer`/`DoDragDropAsync`, `FocusChangedEventArgs`, gesture events without the `Gestures.` prefix,
  `PlaceholderText` instead of `Watermark`, `TopLevel.GetTopLevel(visual)`, and `AttachDeveloperTools()`.
- `WeakReferenceMessenger` is for cross-window notifications only. Between a parent and child VM, use direct references or events.
- Headless tests (`[AvaloniaFact]`) cover wiring: a behavior fires its command, and a template resolves. VM tests cover logic.
- Use `ThemeVariantScope` to force a variant on a subtree, such as a dark code pane in the light theme.

## Before you finish a XAML change

1. `dotnet build -v q -tl:off --nologo`. Compiled bindings report broken paths here.
2. Run `XamlHygieneTests` and the VM tests for any command you added.
3. The PR lists each Default rule you deviated from, and why.
