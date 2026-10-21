---
sidebar_position: 10
---

# Themes

Every colour, shadow, pin shape, size, spacing, thickness, radius, font, duration and easing of the editor is a theme token. A theme is data: a
set of token values laid over the editor's own, never code and never XAML.

## Modes and presets

The **mode** (`netprints.editor.theme`) is `dark`, `light` or `system`. The mode picks the variant: `system` follows the
operating system.

Each variant has its own **preset**, chosen on **Settings › Appearance** or with `darkPreset` and `lightPreset`
in `settings.json`. A preset is applied at once, with no restart, and a preset of the Dark variant never changes a
token of the Light variant.

| Preset | Variant | Id |
|---|---|---|
| NetPrints Dark | Dark | `netprints.dark` |
| NetPrints Light | Light | `netprints.light` |
| High contrast | Dark | `high-contrast.dark` |
| High contrast | Light | `high-contrast.light` |

- **NetPrints Dark** and **NetPrints Light** are the defaults: diamond type pins, a `#262626` canvas and a `#D9141414` card
  in Dark.
- **High contrast** runs on Semi's own high-contrast variant of its slot (Aquatic for Dark, Desert for Light) with NetPrints
  tokens tuned for it: a solid border on every node, yellow or dark-blue focus and selection, stronger grid lines and
  an opaque, thicker wire. In `system` mode the tuned tokens apply, and Semi's variant is not selected.

An id that names no preset of its variant falls back to the variant's default, with an entry in the log. The other
appearance keys still apply.

The **Preview** strip under the two choices shows the canvas, the node card, four pin colours and the selected wire of each
chosen preset.

## Contrast policy

The built-in presets pass every gate below, and a test fails the build when one does not. A theme of your own, when
user themes arrive, applies even if it fails them; the editor warns and names the pairs.

| Gate | Pair | Minimum |
|---|---|---|
| Header | each node role colour against the header text | 4.5:1 |
| Title | the node title against the node body | 4.5:1 |
| Pin | each pin colour against the node body and the canvas | 3:1 |
| Wire | each wire, drawn at its opacity, against the canvas and the node body | 3:1 |
| Card | the node body against the canvas | 1.15:1 |

The ratio is compared as measured, with no rounding.

## The theme file

A preset is a `*.nptheme.json` file with `schemaVersion` 1, an `id`, a `label`, a `base` (`dark` or `light`), an
optional `semiVariant` and a `tokens` map from a token name to a value written as text.

```json
{
  "schemaVersion": 1,
  "id": "my.dark",
  "label": "My Dark",
  "base": "dark",
  "tokens": {
    "Canvas.Background": "#FF101010",
    "Node.CardBackground": "#E0181818",
    "Wire.Thickness": "5"
  }
}
```

An unknown token is warned about and ignored. A bad value is warned about and keeps the base value. A newer schema, a
file that is not JSON and a file over 262144 characters are skipped. A token you leave out keeps its base value.

Values by kind: a colour or brush is `#RRGGBB` or `#AARRGGBB`; a number is a plain decimal; a shadow is a list such as
`4 4 8 0 #80000000`; the pin shape is `triangle` or `diamond`. A thickness is `4`, `8,2` or `1,0,1,0`; a corner radius is
`4` or `4,4,0,0`; a grid length is `120`, `Auto` or `2*`; a font family is a family name such as `Cascadia Mono` (no URL or
font file); a duration is `h:m:s` such as `0:0:0.15`; an easing is the four numbers `x1,y1,x2,y2` of a cubic Bézier curve,
with x from 0 to 1.

Semi's own resources (accents, fonts and density) are not NetPrints tokens and are not listed here; see the
[Semi.Avalonia repository](https://github.com/irihitech/Semi.Avalonia) for their keys.

## The token list

The list is generated from the token catalogue. Update it with `NETPRINTS_UPDATE_SNAPSHOTS=1` and `ThemesGuideTests`.

<!-- tokens:begin -->
| Token | Kind | One value per variant |
|---|---|---|
| `Canvas.Background` | Color | yes |
| `Tab.StripBorder` | Color | yes |
| `GraphGrid.MinorColor` | Color | yes |
| `GraphGrid.MajorColor` | Color | yes |
| `StartPage.RecentTileBackground` | Brush | yes |
| `Focus.Ring` | Brush | yes |
| `State.Hover` | Brush | yes |
| `State.Pressed` | Brush | yes |
| `Row.Separator` | Brush | yes |
| `Tab.SelectedAccent` | Brush | yes |
| `GraphWatermark.Foreground` | Brush | yes |
| `Node.CardBackground` | Brush | yes |
| `Node.TitleForeground` | Brush | yes |
| `Node.SubtitleForeground` | Brush | yes |
| `OpeningGraphIndicator.Background` | Brush | yes |
| `OpeningGraphIndicator.Foreground` | Brush | yes |
| `BusyOverlay.Background` | Brush | yes |
| `Popup.Background` | Brush | yes |
| `Pin.HoverStroke` | Brush | yes |
| `Diagnostic.Error.Foreground` | Brush | yes |
| `Diagnostic.Warning.Foreground` | Brush | yes |
| `Diagnostic.Info.Foreground` | Brush | yes |
| `CommandBar.BadgeBackground` | Brush | yes |
| `CommandBar.BadgeForeground` | Brush | yes |
| `Icon.Method.Foreground` | Brush | yes |
| `Icon.Property.Foreground` | Brush | yes |
| `Icon.Event.Foreground` | Brush | yes |
| `Icon.Class.Foreground` | Brush | yes |
| `Node.HeaderForeground` | Brush | yes |
| `Node.Border` | Brush | yes |
| `Pin.DefaultValue` | Brush | yes |
| `Pin.Hint` | Brush | yes |
| `Canvas.SelectionBorder` | Brush | yes |
| `Canvas.MarqueeFill` | Brush | yes |
| `Canvas.MarqueeBorder` | Brush | yes |
| `Canvas.WireSelected` | Brush | yes |
| `Inspector.CategoryHeaderBackground` | Brush | yes |
| `Inspector.CategoryHeaderForeground` | Brush | yes |
| `Inspector.ModifiedMarker` | Brush | yes |
| `Inspector.EditorBackground` | Brush | yes |
| `Inspector.EditorBorder` | Brush | yes |
| `Node.Header.Entry` | Brush | yes |
| `Node.Header.Call` | Brush | yes |
| `Node.Header.Pure` | Brush | yes |
| `Node.Header.Flow` | Brush | yes |
| `Node.Header.Variable` | Brush | yes |
| `Node.Header.Constructor` | Brush | yes |
| `Node.Header.Async` | Brush | yes |
| `Node.Header.Throw` | Brush | yes |
| `Pin.Exec` | Brush | yes |
| `Pin.Type` | Brush | yes |
| `Pin.Bool` | Brush | yes |
| `Pin.Integer` | Brush | yes |
| `Pin.Float` | Brush | yes |
| `Pin.String` | Brush | yes |
| `Pin.Object` | Brush | yes |
| `Pin.ValueType` | Brush | yes |
| `Pin.Delegate` | Brush | yes |
| `Pin.Generic` | Brush | yes |
| `Node.CardShadow` | Shadow | yes |
| `Popup.Shadow` | Shadow | yes |
| `Wire.Opacity` | Number | yes |
| `Wire.Thickness` | Number | yes |
| `Inspector.RowSpacing` | Number | yes |
| `Pin.TypeShape` | Shape | no |
| `Pin.OutlineThickness` | Number | no |
| `StartPage.RecentTileSize` | Number | no |
| `StartPage.LogoSize` | Number | no |
| `Font.Caption` | Number | no |
| `Font.Body` | Number | no |
| `Font.Subtitle` | Number | no |
| `Font.Title` | Number | no |
| `Font.Watermark` | Number | no |
| `Space.XS.Size` | Number | no |
| `Space.S.Size` | Number | no |
| `Space.M.Size` | Number | no |
| `Space.L.Size` | Number | no |
| `Space.XL.Size` | Number | no |
| `Icon.Small` | Number | no |
| `Icon.Medium` | Number | no |
| `Icon.Large` | Number | no |
| `Tab.Height` | Number | no |
| `Tab.CloseButtonSize` | Number | no |
| `Tab.InactiveOpacity` | Number | no |
| `Density.RowHeight` | Number | no |
| `Density.CommandBarHeight` | Number | no |
| `VariableChip.Size` | Number | no |
| `Dialog.MinWidth` | Number | no |
| `Dialog.MaxWidth` | Number | no |
| `Dialog.ListMaxHeight` | Number | no |
| `Flyout.MethodPickerWidth` | Number | no |
| `Space.XS` | Thickness | no |
| `Space.S` | Thickness | no |
| `Space.M` | Thickness | no |
| `Space.L` | Thickness | no |
| `Space.XL` | Thickness | no |
| `Focus.RingThickness` | Thickness | no |
| `Tab.SelectedBorderThickness` | Thickness | no |
| `Tab.StripOverlap` | Thickness | no |
| `Tab.InactiveInset` | Thickness | no |
| `Tab.BodyPadding` | Thickness | no |
| `Tab.SelectedBodyPadding` | Thickness | no |
| `Tab.AccentThickness` | Thickness | no |
| `Density.RowPadding` | Thickness | no |
| `Row.Padding` | Thickness | no |
| `Row.ChipMargin` | Thickness | no |
| `Row.TypeMargin` | Thickness | no |
| `SectionHeader.Margin` | Thickness | no |
| `SectionHeader.SpacedMargin` | Thickness | no |
| `Radius.Control` | CornerRadius | no |
| `Radius.Tab` | CornerRadius | no |
| `Inspector.LabelColumnWidth` | GridLength | no |
| `Row.RemoveColumnWidth` | GridLength | no |
| `Font.Mono` | FontFamily | no |
| `Motion.Fast` | Duration | no |
| `Motion.Normal` | Duration | no |
| `Motion.Easing` | Easing | no |
<!-- tokens:end -->
