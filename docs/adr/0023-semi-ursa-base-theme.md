# 0023: The editor's base theme and controls are Semi.Avalonia and Ursa.Avalonia

## Status

Accepted (2026-10-07, P3a, integration of the UI polish branch). Supersedes the "Fluent base with token
dictionaries" choice in `specs/005-editor-shell/research.md` for the base theme; Dock and Nodify are unchanged.

## Context

- The editor shipped on Avalonia's Fluent theme with a hand-made emerald palette. The polish pass wanted a more
  finished look (spacing, dialogs, inspectors, tabs) and richer controls without writing each template.
- Semi.Avalonia is an Avalonia theme inspired by Semi Design. Ursa.Avalonia is a control library whose recommended
  theme is the Semi one (`Irihi.Ursa.Themes.Semi`).
- The editor runs on Avalonia 12.1.3 and .NET 10.

## Decision

- **Pinned versions** (`Directory.Packages.props`): `Semi.Avalonia` 12.1.0.1, `Irihi.Ursa` 2.2.0 and
  `Irihi.Ursa.Themes.Semi` 2.2.0, referenced by `NetPrints.Editor` only.
- **Avalonia 12.1.3 compatibility.** On NuGet, Semi.Avalonia 12.1.0.1 needs Avalonia >= 12.1.0, Ursa and its Semi
  theme need >= 12.0.2, and all three target net8.0 and net10.0. 12.1.3 satisfies every range. The UI test suite,
  the snapshot baselines and the Desktop end-to-end run go green on it.
- **License.** All three packages and both repositories are MIT. `THIRD-PARTY-NOTICES.md` does not exist yet; it
  is created in sub-phase G (ADR-0021) and must list these three packages with their licence and copyright.
- **Application styles.** `FluentTheme` stays first, then `SemiTheme`, then `UrsaSemiTheme`, then the Nodify,
  AvaloniaEdit, icon, `EditorStyles.axaml` and `DockStyles.axaml` includes. Semi restyles the standard controls on
  top of Fluent. Dock keeps `DockFluentTheme` (ADR-0018) and takes its surface tokens from `SystemAltMediumHighColor` and
  `SystemRegionColor`, which Semi defines for Dark and Light. Nodify keeps its own control templates, restyled by `EditorStyles.axaml`.
- **What it replaces.** The custom `ColorPaletteResources` for Dark and Light in `EditorApp.axaml` (and its only
  E2 allowlist entry). Light and Dark come from Semi's own theme variants.
- **Not adopted.** The `Dock` companion theme of Semi.Avalonia: its README says it is delivered through NuGet for
  free but is not open source. Dock keeps the Fluent theme. No other Semi companion package is added.
- Editor colours, spacing and type sizes stay tokens and classes in `EditorStyles.axaml` (XAML hygiene rules E2 and
  E8); a view never sets a literal because Semi is the base.

## Consequences

- Three new dependencies from one vendor (irihiTech), MIT, on the Avalonia 12 line only since mid-2026.
- Risks: (1) the Semi and Ursa releases track Avalonia minor versions, so an Avalonia upgrade can wait on them;
  (2) three themes (Fluent, Semi, Dock Fluent) apply to the same visual tree, so a control can pick the wrong
  template after an upgrade, which the snapshot baselines are there to catch; (3) the Semi README fetched still
  documents the 11.x lines, so the Avalonia 12 support statement rests on the NuGet metadata and on our tests;
  (4) the vendor's closed-source Dock companion must not creep in.
- `SemiTheme` creates the `zh-CN` culture in a static initializer, which throws in globalization-invariant mode
  (the Desktop E2E harness and some containers run in it). `NetPrints.Desktop` sets the runtime option
  `System.Globalization.PredefinedCulturesOnly` to `false`, so the editor starts there too.
- Exit path: remove the three package references and the two `Styles` lines, restore the Fluent palette as theme
  dictionaries (task T090) and re-baseline the snapshots. Views use classes and tokens, so no view changes; only
  `EditorStyles.axaml` re-points its brushes at the Fluent resources.
- Sources, all read on 2026-10-07: the NuGet pages of the three packages and the GitHub pages of Semi.Avalonia and
  Ursa.Avalonia (irihitech).
