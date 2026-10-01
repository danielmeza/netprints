---
name: avalonia-xaml
description: "Rules for creating or changing Avalonia 12 views in NetPrints (src/NetPrints.Editor, src/NetPrints.Desktop): bindings, data templates, lists, accessibility, dialogs, and the XamlHygieneTests rules. Use when a change touches *.axaml, *.axaml.cs or a VM command bound from XAML. For event handlers, shortcuts, focus, drag and drop also use avalonia-behaviors; for converters, colors, styles, themes also use avalonia-styling."
paths:
  - "**/*.axaml"
  - "**/*.axaml.cs"
  - "**/NetPrints.Editor/**/*.cs"
  - "**/NetPrints.Desktop/**/*.cs"
---

# Avalonia XAML in NetPrints

Stack: .NET 10, Avalonia 12, Nodify.Avalonia 2, CommunityToolkit.Mvvm 8, AvaloniaEdit, Material.Icons and
Xaml.Behaviors 12 (exact versions: `Directory.Packages.props`). Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault`).
For questions about Avalonia's own API, controls or behavior, use the `avalonia-docs` MCP tools when they are available;
where they differ from this repo's code or these rules, the repo and the rules win.

The rules come in three tiers:
- **Enforced**: a test checks it (`XamlHygieneTests`), so a violation fails the build. To make an exception, add an allowlist entry with a reason.
- **Default**: follow it unless you have a reason not to. If you deviate, give the reason in the PR description.
- **Consider**: tips that are worth knowing. Nobody has to follow them.

Every rule has a boundary, and the boundary is part of the rule. When a rule seems to forbid something reasonable, read it
again: the rules target *misplaced logic*, not the tools themselves.

## Related skills

The XAML rules are split across three skills that share one numbering, so a rule ID cited in code, a test or a PR
(E1-E6, D1-D16) means the same thing wherever it lives:
- `avalonia-xaml` (this skill): E1-E6, D1-D4, D6, D10, D12-D16. It applies to every XAML change.
- `avalonia-behaviors`: D9 (keyboard shortcuts) and D11 (behaviors, with the Xaml.Behaviors catalog). Load it as well
  when a change adds or replaces an event handler, calls `Focus()`, `Close()` or `ScrollToEnd()` from a view, or adds
  a shortcut, drag and drop or a dialog result.
- `avalonia-styling`: D5 (converters), D7 (theme tokens) and D8 (styles and ControlThemes). Load it as well when a
  change adds a converter, a color or brush, a style class, a ControlTheme or a theme resource.

## Enforced

- **E1. No compiled-binding opt-outs.** `x:CompileBindings="False"` and `{ReflectionBinding}` are allowlist-only.
  *Why:* a reflection binding fails silently at runtime. A compiled one fails at build time and is faster.
  Fix: give the template a type (`<DataTemplate x:DataType="core:MethodSpecifier">`). A path-less
  `{Binding Converter=...}` then compiles, as it already does in `SelectMethodDialog.axaml`.
- **E2. No color literals in views.** A view is any `.axaml` file whose root is not `Application`, `Styles` or `ResourceDictionary`. Views may not use hex values or named colors on brush or color properties.
  `Transparent` is allowed, because it makes an element hit-testable.
  *Why:* a hardcoded color does not follow the Light/Dark variant. Put a token in `EditorStyles.axaml` instead (D7, `avalonia-styling`).
- **E3. Command-shaped events are not wired in XAML.** `Click`, `Tapped`, `DoubleTapped`, `KeyDown`/`KeyUp`,
  `SelectionChanged`, `TextChanged`, `GotFocus`/`LostFocus` and similar handlers need an allowlist entry.
  Pointer and drag/drop events (`PointerPressed/Moved/Released`, `DragDrop.*`) are allowed, because gestures are view mechanics.
  *Why:* each UI action is one view model (VM) command with its own unit test. Wire it with `Command`, `KeyBinding` or a behavior (D9, D11, `avalonia-behaviors`).
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
that no behavior covers. Keep such handlers small: they translate the gesture and then call **one** VM command.
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
<!-- src/NetPrints.Editor/Search/NodeSearchView.axaml, inside the result item template -->
Command="{Binding $parent[ListBox].((edsearch:SuggestionListViewModel)DataContext).SelectCommand}"
```
A view with no bindings, such as the code-behind dialogs, doesn't need `x:DataType`. Give it one when it gets a VM.

**D4. Keep reach-ups short.** Bind to the *nearest* stable owner: the `ListBox` or `ItemsControl` that holds the items, or
the `UserControl`. Avoid `$parent[Window]` from inside a template, and don't use numeric hops such as `$parent[Border;2]`. Both break
when the layout is refactored. Better options, in order: (1) the item VM exposes the command itself; (2) `$parent[ItemsControl]`, the nearest items host;
(3) restructure so that no reach-up is needed. For example, an inspector whose DataContext is overridden would need
`$parent[Window]...ShowVariableInspector`; `ClassEditorWindow` wraps it instead:
```xml
<Panel IsVisible="{Binding ShowVariableInspector}">
  <edinspectors:VariableInspectorView DataContext="{Binding SelectedVariable}" />
</Panel>
```

**D6. Assign typed data templates by `DataType`.** Match views to VMs with `DataTemplate x:DataType`, either in place or in
`DataTemplates`. There is no reflection `ViewLocator`. *Why:* the templates are compile-checked and trimming-safe.

**D10. Long work goes in async commands and shows busy state.** Write `[RelayCommand] async Task XAsync(CancellationToken ct)`.
- Bind busy UI to `XCommand.IsRunning`. Use a VM `IsBusy` flag with `try/finally` only when one flag spans
  several operations (as `MainEditorViewModel` does). Delay indicators for fast operations (`BusyIndicatorDelay`).
- `AllowConcurrentExecutions` stays `false`, which disables the button while a run is in progress. Add `IncludeCancelCommand = true` when the user can cancel.
- Keep the default of rethrowing: a fault reaches the error dialog. Don't use `FlowExceptionsToTaskScheduler`, and don't
  write `ExecuteAsync(...).Forget()` from a view.
- Test with `await vm.XCommand.ExecuteAsync(null)`. Don't block on the UI thread, and do heavy work off it.

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
Avalonia generates a field for each name, so an unused name is noise (for example `InputPins`, `ReferenceList`).

**D16. Windows and popups go through the existing hosts.** Open dialogs through `IWindowService` and return results
from a VM, as `SelectTypeDialogViewModel.ResolveSelection` does; the view doesn't compute them. Canvas overlays use
`CanvasPopup` (ADR-0004, which is already enforced).

## Consider

- `NodeView` is rendered for every node, so prefer one `Grid` to nested `StackPanel`s there.
- Display-only text is a `TextBlock`, unless it must be selectable (as in `ErrorDialog`).
- Use `#Name` element bindings to connect two views without code, for example
  `ViewportLocation="{Binding #Editor.ViewportLocation}"` instead of syncing it in `PropertyChanged`.
- `WeakReferenceMessenger` is for cross-window notifications only. Between a parent and child VM, use direct references or events.
- Headless tests (`[AvaloniaFact]`) cover wiring: a behavior fires its command, and a template resolves. VM tests cover logic.

## Gotchas

- Avalonia 12 renamed APIs that older samples still use: `DataTransfer`/`DoDragDropAsync`, `FocusChangedEventArgs`,
  gesture events without the `Gestures.` prefix, `PlaceholderText` instead of `Watermark`,
  `TopLevel.GetTopLevel(visual)` and `AttachDeveloperTools()`.
- A `null` `Background` is not hit-test visible in its own empty space, so an item row reacts only on its text. Give
  the row `Background="Transparent"` (E2 allows it for this reason).
- Headless tests have no window manager, real cursor or OS drag and drop (see `UiCapabilities`). A test that needs
  them belongs in the Desktop E2E project.

## Known debt

- `ClassEditorWindow.axaml` still reaches `$parent[Window]` from several item templates (D4). Don't copy it.

## Before you finish a XAML change

1. `dotnet build -v q -tl:off --nologo`. Compiled bindings report broken paths here.
2. `dotnet build tests/NetPrints.Core.Tests -v q -tl:off --nologo`, then `tests/NetPrints.Core.Tests/bin/Debug/net10.0/NetPrints.Core.Tests -class '*XamlHygieneTests'` and the VM tests for any command you added; fix and re-run until green.
3. The PR lists each Default rule you deviated from, and why.
