# 0021: Editor icons come from one vector family, Material Design Icons, named by icon id

## Status

Accepted (2026-10-06, P3a spec `specs/005-editor-shell/`, research R17, FR-084 and FR-085). Amended 2026-10-08 by
the owner (Amendment 1, below): the family is Material Design Icons, not Fluent UI System Icons. The Context,
Decision and Consequences sections are kept as first accepted; read them with the amendment, which wins where they
differ. Implemented in sub-phase G (T090a, T090b, T092b).

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

## Amendment 1: Material Design Icons, the set already shipped (owner decision, 2026-10-08)

### Context

- Batch E-F8 (sub-phase E fixes, before any G task) fixed the Dock chrome icons with Material Design Icons: the
  menu, pin, close, maximize, restore, minimize, add, float and dock glyphs, and the document tab's close button,
  are path data copied from Material.Icons 3.0.2 into `src/NetPrints.Editor/Icons.axaml` on one 24-px grid. The
  project tree, the document buttons and the start page tiles already drew MDI through `Material.Icons.Avalonia`,
  so the editor ships one set today.
- ADR-0023 (2026-10-07) made Semi.Avalonia the base theme. The main reason given above for Fluent UI System Icons,
  "the same design language as the Avalonia Fluent and Dock Fluent themes", no longer holds for the controls.
- Switching to Fluent now would redo E-F8's geometries and re-baseline the same snapshots (`editor-shell-*`,
  `inspector-*`, `search-popup`, `start-page-*`) a second time, for no gain the registry does not already give.

### Decision

- **The one family is Material Design Icons** (Pictogrammers, https://pictogrammers.com/library/mdi/; icons under
  Apache-2.0), drawn through `Material.Icons.Avalonia` 3.0.2, already pinned in `Directory.Packages.props` and
  referenced by `NetPrints.Editor` only. `FluentIcons.Avalonia` is not added, and no other icon package is.
- Everything else in the Decision above stands: icon ids and `IconId` on descriptors, one `IconRegistry` that is
  the only code naming the library's types (here `MaterialIconKind`), one `IconPresenter`, the `Icon.Small` and
  `Icon.Medium` tokens, the unknown-id fallback with one warning, rule E9 and no raster icons.
- **Active state.** MDI has no regular and filled style per glyph; many glyphs come as an `…Outline` and a filled
  pair (`FolderOutline` and `Folder`). Where a pair exists, the registry maps the id's regular state to the outline
  glyph and its active state to the filled one; otherwise both states draw the same glyph.
- **Dock chrome.** Dock asks for geometries by its own resource keys, not for a control, so E-F8's `Icon.*`
  `StreamGeometry` resources stay as copied path data. They move into `src/NetPrints.Editor/Icons/` with the
  registry and count as part of the family.
- **No brand glyphs.** No id maps to an MDI brand or logo icon; those carry the trademarks of their owners.
- **Notices.** `THIRD-PARTY-NOTICES.md` lists Material Design Icons (Pictogrammers, Apache-2.0, with the glyphs
  copied by E-F8), `Material.Icons.Avalonia` (MIT), Semi.Avalonia, Irihi.Ursa and Irihi.Ursa.Themes.Semi (MIT,
  ADR-0023), Cascadia Mono and Inter (SIL OFL 1.1), each with the copyright line from its licence file, and it
  carries the Apache-2.0 licence text. This replaces the Fluent UI System Icons and FluentIcons.Avalonia entries
  above.

### Consequences

- No dependency is swapped, and no snapshot is re-baselined for the family itself; T090a and T092b re-baseline only
  where the registry, the icon tokens or the removed PNG icons change what a view draws.
- The risk named above for `FluentIcons.Avalonia` (one maintainer, lagging an Avalonia release) applies to
  `Material.Icons.Avalonia` instead. It runs on Avalonia 12.1.3 today, the registry is still its only consumer, and
  the fallback, copying the used glyphs' path data into `StreamGeometry` behind the same ids, is already in use for
  the Dock chrome.
- The Material look (a 24-dp grid and mostly filled glyphs) sits on the Semi controls. The sub-phase G contact sheet
  (T098b) and its review (T100) check that icons look consistent across panes, both themes and 200 %.
- Apache-2.0 asks that the licence text travel with the redistributed glyphs; the notices file ships with the
  Desktop app, so it does.
- Sources: implementation-notes.md "Batch E-F8 (icons)" and the spec's Clarifications, session 2026-10-08.
