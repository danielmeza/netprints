# 0035: Editor themes are data

## Status

Accepted (owner, 2026-10-13, after the P3a sub-phase G review and a research with an adversarial audit; lands in P3).
It takes the first free number after 0034.

## Context

- The editor's look is one set of XAML tokens in `EditorStyles.axaml` with two variants, Dark and Light, and a
  Dark/Light/System switch. There is no token catalogue in product code, and no theme contribution kind is planned.
- The owner asked for the look to be configurable: "todo elemento visual del editor tiene que ser configurable por los
  temas que se van a poder modificar por la comunidad como plugins" (every visual element of the editor must be
  configurable by themes that the community can modify as plugins).
- The shipped defaults (diamond type pins, `#262626` canvas, `#D9141414` card, wire opacity 0.78) were chosen by the
  owner's delegation in sub-phase G. The old trio (triangle pins, `#000` canvas, `#AA202020` card) is worth keeping as a
  preset, and the contrast of user-made themes will often fail, so a policy is needed.
- Semi.Avalonia (ADR-0023) already themes the controls and is open source. The owner does not want NetPrints to
  maintain Semi: "nosotros solo lo aplicaremos" (we only apply it).
- What P3a already ships, as the starting point:
  - tokens: `Pin.TypeShape` with both geometries (`Pin.TypeShape.Triangle` and `Pin.TypeShape.Diamond`), `Canvas.Background`,
    `Node.CardBackground` and the shared per-variant `Wire.Opacity`;
  - test-only overrides that rebuild the old trio ("Classic Dark") in the contact sheet;
  - a type ramp of font-size tokens, enforced by the E8 hygiene test (`E8_EveryFontSizeIsARampToken`) over every
    `.axaml` file;
  - tests that gate the colours: `PinContrastTests`, `CanvasPaletteTests`, a card-over-canvas separation test (at least
    1.15:1) and `PinDistinctnessTests` (CIEDE2000 of at least 12 between pin families).
- The research (theme-presets research, 2026-10-13) compared presets compiled into the app, user theme files and
  community themes. Its audit corrected it on four points that shape this decision: the contrast failure is on the
  canvas, so the card cannot fix it; Aquatic, Desert, Dusk and NightSky are Semi's own high-contrast variants; custom
  `ThemeVariant`s collide with System mode and with those variants; and a theme-only package has no `assembly`, which
  the extension loader requires.

## Decision

1. **Every visual element of the editor is a theme token.** Colours, shapes, sizes, thicknesses, opacities, radii,
   spacing and fonts, plus whatever later phases add (wire thickness, density, exec wire style). A visual literal that
   a theme cannot reach is a defect. The E2/E8 hygiene ratchets are the enforcement path.
2. **Themes are data.** A theme never runs code and never loads XAML. It is a sparse set of token values over a base,
   so new tokens inherit the base value.
3. **Staging, with B0 first.**
   - **B0, in P3:**
     - an ADR (this one);
     - a token catalogue in product code, with the contrast math moved there so the tests call it;
     - a per-variant overlay loader that writes into `Application.Resources.ThemeDictionaries[Dark|Light]`, so a
       `darkPreset` never leaks into Light (a flat key in `Resources` beats its theme dictionaries);
     - the embedded presets: **NetPrints Dark** (the new defaults), **NetPrints Light**, **Classic Dark** (triangle pins,
       `#000` canvas, `#AA202020` card) (removed by Amendment 1) and **High contrast**, built on Semi's own high-contrast variants (Aquatic,
       Desert, Dusk, NightSky; Aquatic derives from Dark and Desert from Light) plus NetPrints tokens tuned for them;
     - wire opacity and thickness as per-variant tokens, and the inspector tokens that ADR-0036 needs;
     - a theme settings page, and the reserved `themes` field in the extension manifest, before the Extensibility
       packages are published.
   - **B1:** user `*.nptheme.json` files in `<config>/NetPrints/themes/` (ADR-0033), validation messages, Reload themes
     and Duplicate theme (writes the active preset as a theme file). In P3 if capacity allows, otherwise P6.
   - **C:** themes declared by ordinary extensions (the ADR-0013 precedent of an extension shipping data), listed
     through B17's package service. Same rule as B1: in P3 if capacity allows, otherwise P6. Theme-only packages
     without an assembly come later; they need the loader to make `assembly` optional, with a `netprintsApi` minor
     bump. There is no hosted gallery for now; B17's Browse tab serves when C lands.
   - Whether B1 and C fit in P3 is open until P3 is planned in detail.
4. **Contrast policy: warn and apply.** The built-in presets are gated by the contrast tests. A user or community theme
   applies anyway, with a warning that names the failing pairs.
5. **Scope: NetPrints tokens, plus Semi resources passed through.**
   - NetPrints does not curate or maintain a list of Semi keys. A theme may override Semi resources (accents, fonts,
     density) and NetPrints applies them.
   - Validation checks each key against the resources actually loaded at runtime: an unknown key gets a warning, and a
     value is parsed by the type of the resource it replaces.
   - The theming guide documents the NetPrints tokens and links to Semi's repository and docs for Semi keys.
6. **Closed sets where the format needs them.** The pin shape is `triangle|diamond`, never raw path data.
7. **Settings.** `theme` (dark, light, system) stays. Presets are chosen per variant by `darkPreset` and `lightPreset`,
   which are strings parsed at the boundary, not enum members: an unknown enum value discards the whole
   `extensions.netprints.editor` section (the settings store returns the descriptor default and logs it).
8. **File and format.** `*.nptheme.json`, with `schemaVersion`, `id`, `label`, `base` and a `tokens` map. An unknown
   token is warned about and ignored, a bad value is warned about and keeps the base value, a newer schema is skipped,
   a renamed token maps through an alias table (with a warning), and the file size is capped. No URLs, fonts or images
   in v1.
9. **No custom `ThemeVariant`s.** Presets are overlays on the Dark and Light variants, so System mode and Semi's
   high-contrast variants keep working. Under a Semi high-contrast variant, a NetPrints token without a value for it
   falls back to the Dark or Light value, which the High contrast preset tunes.

## Alternatives rejected

- **Presets only, as compiled XAML dictionaries on custom `ThemeVariant`s (research option A).** It gives users no
  themes of their own, would be thrown away when user themes arrive, and the custom variants collide with System mode
  and Semi's high-contrast variants. B0 costs about the same (4-5 half-days in the research).
- **Themes as code or XAML.** It would break the trim-safety constraint for P5 and let community content run code.
- **Maintaining Semi's keys in a NetPrints schema.** The owner rejected it; Semi is open source.
- **A hosted gallery.** Not sized and not needed while B17 exists.

## Consequences

- Token names become public API once B1 ships: a rename needs an alias forever.
- Every new token needs a value in every preset, and the contact sheet gains tiles per preset (one grid tile per
  preset, to keep the snapshot count down).
- A token used through `StaticResource` cannot change at runtime. EditorStyles uses it 39 times, so those move to
  `DynamicResource` as they are made themable.
- Controls that Semi, Ursa and Dock draw sit outside the NetPrints tokens; themes reach them only through the
  passed-through Semi keys.
- Light's wire contrast is already near 3:1, so almost any tweak to it fails the warning check; this is accepted by the
  warn-and-apply policy.
- Estimates in half-days: B0 about 4-5, B1 about 9 more, C more still (not re-sized after the audit, which asked to start
  from extension-declared themes). The test of Avalonia's resource precedence (an overlay in
  `Application.Resources.ThemeDictionaries` wins) is part of B0.
- The extension conformance kit gains a check that a declared theme loads no code (ADR-0010 amendment).
- Related: the product mark is approved for now and will be redesigned later (roadmap candidate); the details panel's
  editors use these tokens ([ADR-0036](0036-member-defaults-and-editor-metadata.md)).

## Amendment 1 (owner, 2026-10-18): the Classic preset is removed; it only mimicked the legacy editor

The owner: "Este producto nunca salió o se usó, no creo que valga la pena mantener el classic … que sea el último error o
esfuerzo que le dedicamos a algo del editor anterior" (this product never shipped or was used; the classic is not worth
keeping; let it be the last effort spent on anything of the old editor). The embedded presets are NetPrints Dark, NetPrints
Light and High contrast for each variant. The contrast gates compare the exact ratio, without the two-decimal rounding that
let Classic's card (1.14998:1) pass the 1.15:1 gate. A stored `darkPreset` of `classic.dark` is an unknown id: the variant's
default applies with a log entry (decision 7). The contact sheet's Classic Dark tiles are gone. Anything that exists only to
mimic the legacy editor is removed, not fixed.
