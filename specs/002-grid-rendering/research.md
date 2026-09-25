# Research: Grid Rendering

The full research (survey of Unreal, Unity, Godot, Blender, Figma, Excalidraw, tldraw, React Flow,
Nodify, ImGui, Qt and the pristine-grid shader; API verification; measurements; prototypes) is in
[`docs/research/2026-09-25-grid-rendering/README.md`](../../docs/research/2026-09-25-grid-rendering/README.md).
This file records only the decisions for P0.1.

## Decisions

| # | Decision | Rationale | Alternatives rejected |
|---|---|---|---|
| D1 | A `GridBackground` control behind a transparent `NodifyEditor`, drawing through one `ICustomDrawOperation` with the leased `SKCanvas`, in device pixels (`ResetMatrix`, scale and offset taken from `TotalMatrix`). | Exact pixel snapping at any display scale; the Avalonia tile brush re-records a picture and resamples it (the cause of blur, dotting and moiré). | Tiled `DrawingBrush`/`VisualBrush` (cannot be made crisp); `Control.Render` with `DrawLine` in DIPs (snapping error-prone, crossings blend twice). |
| D2 | Primary path: SkSL `SKRuntimeEffect` on one rect, used only when `lease.GrContext != null` and the effect compiled. | O(1) CPU per frame on GPU. SkSL on the raster backend is ~100× slower (310 ms at 1080p), so never in software. | Shader everywhere. |
| D3 | Fallback: minor and major line rects in two reused `SKPath`s, each filled once, non-AA. | Union fill draws each pixel once; 2.7 ms at 1080p in software; ~0 managed allocations. Pixel-identical to D2 (see D9). | Per-line `DrawLine` (crossings double-blend). |
| D4 | One shared definition: `GridStyle` (look) and `GridFrame.Compute` (per-frame device parameters in double precision: phase reduced modulo one major period, integer widths, smoothstep fade, opacity). Both paths use the snapping rule `col0 = floor(L + 0.5 − w/2)` and "major over minor" compositing. | Guarantees identical output; far-from-origin precision. | Separate parameters per path. |
| D5 | No `fwidth` in SkSL (ES2 restriction, verified compile error). The derivative is constant in a 2D orthographic view, so the shader is analytic. Lines are hard-snapped (no AA). | Crisper than AA for axis-aligned 1-px lines; matches the CPU path exactly. | Pristine-grid AA (optional follow-up). |
| D6 | Owner: major every 8 cells, lines, no origin line, minor fade 6→14 DIP, colors from theme dictionaries, `Mode` property + `NETPRINTS_GRID` override, old brush removed. | Owner decisions (2026-09-25). | Major every 5; dot grid. |
| D7 | `NETPRINTS_GRID` (`auto`/`cpu`/`shader`) applies only while `Mode` is `Auto`; an explicit `Mode` wins. | Tests that force a path stay deterministic whatever the environment. | Env var overriding everything. |
| D9 | The grid paints the canvas background itself. `GridFrame` pre-composites the (faded) line colors over the background in double precision; both paths write these final colors with `SKBlendMode.Src` (major wins at crossings), and control opacity is applied with one layer around the draw. | Found during implementation: Skia's raster pipeline blends a solid paint in low precision, while a runtime shader forces high precision, so translucent lines differed by one level (e.g. 49 vs 50) between the paths headless. Writing opaque final colors removes blending from both paths and makes them pixel-identical on every backend. | Translucent lines blended by Skia; a trivial SkSL color shader for the CPU path (5× slower and not available when SkSL fails). |
| D8 | Viewport sync: the view copies `ViewportLocation`/`ViewportZoom` into typed styled properties with `AffectsRender` from the existing `PropertyChanged` handler. | No transform allocation per change (the old code allocated a `MatrixTransform`). | Bindings (boxing, overhead). |

## Follow-ups (not in P0.1)

- Hierarchical grid levels if `MinViewportZoom` drops below ~0.2.
- Optional anti-aliased mode (analytic box-filter coverage) for sub-pixel widths.
- Verify the shader on Windows (ANGLE) and macOS (Metal); only Linux/OpenGL was tested.
