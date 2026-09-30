---
name: avalonia-styling
description: "How NetPrints Avalonia 12 views look: IValueConverter rules (what a converter may and may not decide), colors and brushes as theme tokens in ThemeDictionaries with DynamicResource, Fluent System* resources, Light and Dark variants, Style plus class versus ControlTheme, style class naming, where app-wide styles and resources live (EditorStyles.axaml, EditorApp.axaml), and the rendering cost of templates. Use it whenever a change adds or edits a converter, a color or brush, a style or style class, a ControlTheme, a theme resource or a Nodify theme override, even if the request only says 'make it look like…' or 'fix the colors'. Always load avalonia-xaml alongside it."
paths:
  - "**/*.axaml"
  - "**/*.axaml.cs"
  - "**/NetPrints.Editor/**/*.cs"
  - "**/NetPrints.Desktop/**/*.cs"
---

# Styling and theming in NetPrints

**Invoke the `avalonia-xaml` skill too, before you edit anything, unless it is already loaded in this session.** It
holds the enforced rules (E1-E6, which `XamlHygieneTests` checks at build time) and the defaults that every XAML change
follows; this skill only adds D5, D7 and D8. Rule IDs are shared across the `avalonia-*` skills.
Two enforced rules bite most often here: **E2**, no color literals in views (only `Transparent`), and **E6**, a theme
token is always a `DynamicResource`, never a `StaticResource`.

The tiers (Enforced, Default, Consider) and E1-E6 are defined in `avalonia-xaml`. List any Default rule you deviate from in the PR.

## Default

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

## Consider

- Use `IsHitTestVisible="False"` on decorative overlays. Use `Background="Transparent"` only on elements that must catch the pointer.
- `BoxShadow` and stacked translucency cost fill rate in repeated templates.
- Use `ThemeVariantScope` to force a variant on a subtree, such as a dark code pane in the light theme.

## Gotchas

- A brush that a converter builds in C# doesn't follow a theme switch, because the binding isn't re-evaluated when
  the variant changes. Map the state to a style class instead (`Classes.pure="{Binding IsPure}"`) and let the style
  set a `DynamicResource` token.
- A `ControlTheme` replaces the whole template and only one applies at a time. Base it on the default
  (`BasedOn="{StaticResource {x:Type nodify:ItemContainer}}"`) or the control loses the parts you didn't restyle.

## Before you finish

1. `dotnet build -v q -tl:off --nologo`, then run `XamlHygieneTests` (E2, E6).
2. A new converter gets a plain xUnit test with no Avalonia app.
3. Check both variants. In a headless test, set `RequestedThemeVariant` (on the app or a `ThemeVariantScope`) to
   `ThemeVariant.Light` and then `ThemeVariant.Dark` and assert what resolves, as `GridRenderTests` does; for a quick
   look, run the editor and switch the variant.
