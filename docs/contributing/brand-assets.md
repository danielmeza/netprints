# Brand assets

The NetPrints mark has one master, `assets/brand/netprints-mark.svg`: a node with a header bar and two pins with wire stubs
on each side, drawn from rectangles. Every other copy is an export or a transcription of it:

| Asset | Where |
|---|---|
| PNG exports, 16 to 256 px | `assets/brand/netprints-mark-<n>.png` |
| Multi-size `.ico` (16, 32, 48, 256) | `src/NetPrints.Desktop/NetPrintsLogo.ico`, copied to `src/NetPrints.Editor/Assets/NetPrintsLogo.ico` |
| NuGet package icon | `assets/icons/netprints-icon.png` (the 128 px export) |
| Docs site logo and favicon | `scripts/build-docs.sh` copies the SVG and the 32 px export into `website/static/img/` |
| In-app mark | `App.Mark` in `src/NetPrints.Editor/Icons/AppMark.axaml`, colours as `Brand.*` tokens |

After changing the SVG, run the export by hand (CI does not run it) and commit the results:

```bash
eng/brand/export-mark.sh
```

It needs `rsvg-convert` or ImageMagick. Then update `App.Mark` by hand so it draws the same rectangles in the same colours.
`BrandAssetTests` checks the sizes, the `.ico` frames, the copies and `THIRD-PARTY-NOTICES.md`.

## Third-party notices

`THIRD-PARTY-NOTICES.md` at the repository root lists every bundled third-party asset (icon glyphs, fonts and the theme
packages) with its licence and copyright line. Add an entry when you bundle a new asset; the Desktop project copies the file
to its output and publish folders.
