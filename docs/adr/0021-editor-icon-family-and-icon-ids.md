# 0021: Editor icons come from one vector family, Fluent UI System Icons, named by icon id

## Status

Accepted (2026-10-06, P3a spec `specs/005-editor-shell/`, research R17, FR-084 and FR-085). Implemented in
sub-phase G (T090a, T090b, T092b).

## Context

- The editor draws icons from two sources. Material.Icons.Avalonia 3.0.2 (Pictogrammers MDI) supplies about 40 icon
  kinds in nine XAML files, and the contribution descriptors carry an `IconKind` documented as a "Material icon kind
  name". Sixteen raster `*_16x.png` files in `src/NetPrints.Editor/Assets/`, inherited from the old editor, mark the
  node categories (21 uses). They look like Visual Studio Image Library art, no file records their licence, and they
  blur at 150 % and 200 % scaling.
- No file lists the licences of the bundled icons and fonts (Cascadia Mono, Inter).
- P3 publishes the contribution API (ADR-0020). If a descriptor names a library enum member, that library becomes
  part of the public contract, and changing the family later breaks every extension.
- The icon family has to cover commands (menu, command bar, palette), panels, tree items, node categories and kind
  glyphs, pin kinds, project templates, dialogs and empty states.

Options weighed:

| Criterion | Fluent UI System Icons | MDI (Material.Icons.Avalonia) | Codicons |
|---|---|---|---|
| Node and graph concepts | Thousands of glyphs, each regular and filled: code, braces and variable, math formula, flow, branch, repeat, cube, box | The widest set, over 7,000 icons, including function, variable, lambda and code braces | About 650 IDE icons, few node concepts |
| Fit with the Avalonia Fluent and Dock Fluent themes | Same design language as the controls: line weight, rounded corners, regular and filled pairs | Material look: heavier, filled glyphs on a 24 dp grid | VS Code look, outline only |
| Licence | MIT (© Microsoft Corporation); keep the notice | Pictogrammers Free License: icons Apache-2.0, some icons under their own licences | Icons CC-BY-4.0 (attribution), code MIT |
| Package on Avalonia 12 | `FluentIcons.Avalonia` 2.1.343 (MIT), net8.0 and net10.0, depends on Avalonia 12.0.0 | `Material.Icons.Avalonia` 3.0.2, in use today | None; geometries copied by hand |
| Churn | The id registry re-points every use anyway; a switch adds one mapping table | Lowest | As Fluent, plus hand-made geometries |

## Decision

- **One family: Fluent UI System Icons**, through the `FluentIcons.Avalonia` package (pinned in
  `Directory.Packages.props`). The regular style is the default; the filled style marks an active or selected
  state, such as a toggled command or the active tab. Where Fluent has no glyph for a concept, the registry maps the
  id to the nearest glyph; drawing custom glyphs is out of scope.
- **Icon ids, not library types.** Contributions and view models name an icon by id, a string such as
  `netprints.icon.save`, in a property named `IconId` (renamed from `IconKind` on `CommandDescriptor`,
  `PanelDescriptor`, `ProjectTemplateDescriptor` and `PanelViewModel`). Ids follow the command id pattern
  `^[a-z0-9]+(\.[a-zA-Z0-9]+)+$`. Built-in ids are constants in `IconIds` (namespace `NetPrints.Editor.Icons`).
- **One registry.** `IconRegistry` maps each id to a glyph and a variant. It is the only code that names
  `FluentIcons` types, and one control, `IconPresenter` (an `IconId` styled property), draws an id. Icon sizes come
  from tokens: `Icon.Small` (16) for menus, lists and tree rows, and `Icon.Medium` (20) for the command bar.
- **Unknown ids.** An id the registry does not know draws the `netprints.icon.unknown` glyph and is logged once at
  warning level (`[LoggerMessage]`). The contribution still registers. Every built-in id resolves, and a test
  checks it.
- **No raster icons.** The 16 PNG files are replaced by ids and deleted, and `Material.Icons.Avalonia` is removed
  once nothing uses it. XAML hygiene rule E9 forbids icon-library elements and bitmap `Image` sources in views
  outside `src/NetPrints.Editor/Icons/`, and E5 names `IconPresenter`. The product mark is the only image asset
  outside the family (FR-085).
- **Notices.** `THIRD-PARTY-NOTICES.md` at the repository root lists every bundled third-party asset with its
  licence and copyright: Fluent UI System Icons (MIT), FluentIcons.Avalonia (MIT), Cascadia Mono (SIL OFL 1.1) and
  Inter (SIL OFL 1.1). The Desktop app ships it, About links to it, and a test checks the entries.

## Consequences

- Descriptors carry a stable string, so P3 can publish them without exposing an icon library. A later change of
  family touches the registry's table and the snapshot baselines only. How extensions contribute glyphs of their
  own is decided with the P3 contribution API; until then they use the built-in ids.
- One dependency is swapped: `FluentIcons.Avalonia` comes in, `Material.Icons.Avalonia` goes. Its icon fonts add to
  the size of the Desktop package.
- `FluentIcons.Avalonia` has one maintainer and could lag an Avalonia release. The registry is its only consumer,
  so the fallback stays contained: copy the used glyphs' SVG path data (MIT) into `StreamGeometry` resources behind
  the same ids.
- Snapshots that show icons are re-baselined once, in the tasks above, and each commit says why.
- Visibility overlays on tree items (Rider style) and pin glyphs coloured by type are P6 work. They combine ids and
  tokens and need no second family.
- Whoever bundles a new third-party asset adds it to `THIRD-PARTY-NOTICES.md` in the same change.
