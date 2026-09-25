# Rendering the NetPrints graph background grid (Avalonia 12 / Skia / Nodify.Avalonia 2.0)

Date: 2026-09-25. Research only, no repository changes. Target stack as found in the repo: Avalonia 12.1.3, Avalonia.Skia 12.1.3, SkiaSharp 3.119.4, Nodify.Avalonia 2.0.0, .NET 10. The editor uses `GridCellSize="28"`, `MinViewportZoom="0.3"`, `MaxViewportZoom="1"`, `RequestedThemeVariant="Dark"`.

Prototype code used for the measurements below (scratch only): `prototypes/headless/GridCore.cs`, `prototypes/headless/GridBackground.cs`, and the GPU run in `prototypes/gpu/`.

---

## 1. TL;DR

**Recommendation (owner's preferred path):** a `GridBackground` control placed behind a transparent `NodifyEditor`. It issues one `ICustomDrawOperation`, leases the `SKCanvas`, and draws the grid in **device pixels** with one of two paths. Both paths read one shared definition (`GridStyle` → `GridFrame`):

1. **Primary: an SkSL `SKRuntimeEffect` shader** on a single full-viewport rect. O(1) CPU work, one draw call. It is used when the lease has a `GRContext` (GPU backend) and the effect compiled.
2. **Fallback: a CPU `SKPath` path.** All minor-line rects go into one reused `SKPath` and all major-line rects into another. Each is filled once with a non-AA paint, and the visible lines are culled first. This path is used under software rendering, in headless tests, and when the effect fails to compile.

Both paths apply the **same integer pixel-snapping rule, the same LOD fade and the same "major over minor" compositing**, so they produce the same pixels:

- **Headless Skia (raster):** the forced shader path and the CPU path gave **0 differing pixels** over a 320×200 frame with a fractional control offset.
- **Real GPU (OpenGL, NVIDIA, 2.5× scaling, 4000×2250 device px):** both paths gave the **same SHA-1 of the rendered region**.
- **Raw SKSurface tests** at zooms 0.3–1, scalings 1–2 and pan offsets up to ±2·10⁶: geometry was identical, with at most **1/255** channel rounding difference.

### Two findings that shape the design

- **SkSL runtime effects have no `fwidth`/`dFdx`/`dFdy`.** Compiling Ben Golus's pristine-grid code fails in SkiaSharp 3.119.4 with `error: no match for fwidth(float2)`. Runtime effects are held to ES2 restrictions ([Skia bug 12202](https://groups.google.com/a/skia.org/g/bugs/c/lC0vFyC9Kzw), [Flutter #180959](https://github.com/flutter/flutter/issues/180959), [SkiaSharp #2649](https://github.com/mono/SkiaSharp/issues/2649)). That does not matter here: for a 2D orthographic grid the derivative is a known constant (1 / cell size in device px), so it goes in as a uniform. The pristine-grid ideas carry over analytically.
- **SkSL on the CPU raster backend is about 100× slower than drawing lines.**
  - At 1080p: shader **310 ms/frame**, CPU path **2.7 ms**.
  - At 4K: shader **1241 ms**, CPU path **10.8 ms**.
  - So `Auto` must never pick the shader when `GrContext == null`. Headless tests can still force the shader path on small frames to check that both paths match.

The same trend shows up outside Avalonia. Blender recently replaced its full-screen pixel-shader viewport grid with line drawing, reporting **~40 µs vs ~300 µs** and an easier implementation ([Blender PR #148733](https://projects.blender.org/blender/blender/pulls/148733)). Most 2D node editors simply draw the visible lines each frame: Unreal, Unity, Godot, Excalidraw, imnodes and imgui-node-editor.

---

## 2. Why the current implementation looks wrong (root causes)

`GraphEditorView.axaml.cs` sets up the grid as follows:

- It builds a `DrawingBrush` from a 28×28 `RectangleGeometry` stroked with a 0.5 px pen.
- It uses `TileMode.Tile` and assigns the brush to `Editor.Background`.
- It allocates a new `MatrixTransform` on every viewport change.

Avalonia.Skia 12 renders a `DrawingBrush` in two steps (see [`DrawingContextImpl.ConfigureSceneBrushContentWithPicture`](https://github.com/AvaloniaUI/Avalonia/blob/release/12.1.0/src/Skia/Avalonia.Skia/DrawingContextImpl.cs)):

1. It records the tile into an `SKPicture`, a new `PictureRenderTarget` plus picture plus shader **every time the brush is drawn**.
2. It turns that picture into a tiled picture shader, with `Brush.Transform` folded into the shader matrix.

Skia's picture shader rasterizes the tile into a cached bitmap and then **resamples** it. That leads to the following problems:

| Symptom | Cause |
|---|---|
| Shared edges drawn twice | The rectangle strokes all 4 edges, so every edge is stroked by two neighbouring tiles. |
| Blurry or dotted lines | The pen scales with the brush transform: 0.5 px × zoom 0.3 = 0.15 px. The stroke lands at fractional device positions and is then bilinearly resampled. Fractional DPI makes it worse ([Avalonia #1614](https://github.com/AvaloniaUI/Avalonia/issues/1614)). |
| Moiré at zoom 0.3 | Cells of 8.4 px are resampled from a tile bitmap whose phase is not pixel-aligned, with no fading. |
| No hierarchy or LOD | There is a single brush with a single colour. |
| Theme changes ignored | The line brush is resolved once in the constructor. |
| Allocation per pan/zoom | A `new MatrixTransform(...)` each time, plus the picture re-recorded on every draw. |

---

## 3. Survey: how other tools draw the grid

| Tool | Technique | LOD / fading | Pixel handling | Source |
|---|---|---|---|---|
| **Unreal Engine 5.8, Blueprint `SNodePanel::PaintBackgroundAsLines`** (Slate) | Loops over the visible range and calls one `FSlateDrawElement::MakeLines` per horizontal and vertical line. Rule (major) lines go on a higher layer. | **Power-of-two inflation:** `while (zoom*Inflation*GridSnapSize <= 8) Inflation *= 2`, so the spacing doubles whenever cells fall to 8 px or below. Rule period 8 (`Graph.Panel.GridRulePeriod`). Separate centre-line colour. | Optional AA (`bAntiAliasGrid = true` by default). Offset reduced with `FancyMod` over one rule period (the same trick as the phase reduction used here). | Local engine source `Engine/Source/Editor/GraphEditor/Private/SNodePanel.cpp` (UE 5.8.3); styles in `StarshipStyle.cpp` (GridLineColor 0.024, GridRuleColor 0.010, GridCenterColor 0.005, linear); [SNodePanel API](https://docs.unrealengine.com/4.26/en-US/API/Editor/GraphEditor/SNodePanel/) |
| **Unity GraphView `GridBackground`** (UI Toolkit) | `ImmediateModeElement.ImmediateRepaint()` with `GL.Begin(GL.LINES)` per visible line. First minor lines, then "thick" lines every `--thick-lines` (default 10), `--spacing` 50. | None. Spacing scales with zoom and lines stay 1 px wide. | Offset via `%` of the spacing, so it is phase-reduced. Colours come from USS custom properties (`--line-color`, `--thick-line-color`, `--grid-background-color`), so they are themeable. | [GridBackground.cs](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/GraphViewEditor/Decorators/GridBackground.cs), [docs](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Experimental.GraphView.GridBackground.html) |
| **Godot 4 `GraphEdit::_draw_grid`** | `draw_line` per visible line (the Lines pattern), or 3×3 `draw_rect` per point (the Dots pattern). | Major every 10 lines or every 5 dots. **Dots:** minor alpha × `clamp(zoom − 0.4, 0, 1)`, a linear fade instead of a pop. | Theme colours `grid_major` / `grid_minor`. | [graph_edit.cpp](https://github.com/godotengine/godot/blob/master/scene/gui/graph_edit.cpp) (`GRID_MINOR_STEPS_PER_MAJOR_LINE = 10`, `..._DOT = 5`) |
| **Blender node editor** (`view2d_dot_grid_draw`) | GPU point sprites, AA, one batch per level. | Up to 3 levels, each **5×** the previous. The level comes from `log(min_step/zoom)/log(5)`. Point size is clamped and alpha = (precise/drawn size)² to keep brightness constant. **The last level fades with `1 − fract(view_level)`** so nothing pops. | Dots are offset to align centres as their size changes, and scale with `U.pixelsize` (DPI). | [view2d.cc](https://github.com/blender/blender/blob/main/source/blender/editors/interface/view2d/view2d.cc), [node_draw.cc](https://github.com/blender/blender/blob/main/source/blender/editors/space_node/node_draw.cc) |
| **Blender 3D viewport grid** | Historically a full-screen **fragment shader** (`overlay_grid_frag.glsl`) with derivative-based AA and blended levels. It is now being **replaced by line geometry** ("infinite grid using line draw"). | Levels alpha-blend in and out, with stipple fading at low alpha. | The PR reports **~40 µs vs ~300 µs**, and says the shader was hard to untangle. | [PR #148733](https://projects.blender.org/blender/blender/pulls/148733), [current shader](https://github.com/blender/blender/blob/main/source/blender/draw/engines/overlay/shaders/overlay_grid_frag.glsl) |
| **Figma / FigJam** (C++→WASM, WebGL, tiled renderer) | Implementation not public. The documented behaviour is that the pixel grid appears only at **≥ 400%** zoom, a hard LOD threshold. | Threshold-based. | Aligned to document pixels. | [Figma zoom & view options](https://help.figma.com/hc/en-us/articles/360041065034-Adjust-your-zoom-and-view-options) |
| **Excalidraw** (Canvas2D) | Draws only the visible lines each frame (`strokeGrid`). Bold line every `gridStep` (5). | Regular lines are **skipped when the cell is under 10 px**, a hard cutoff. | **Explicit device-pixel snapping:** a line at least 1 device px wide gets a whole number of device px and is centred on a half pixel when that number is odd. Thinner lines keep their sub-pixel width because "their lightness is the point". The scroll offset is snapped to device px too. | [staticScene.ts](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/renderer/staticScene.ts) |
| **tldraw** (SVG) | One SVG `<pattern>` of dots per grid step, with the pattern size in screen units. | `gridSteps` of 64/16/4/1, each with `min`/`mid` zoom. Opacity = `modulate(z, [min, mid], [0, 1])`, a **continuous fade**. | `+0.5` offset to hit pixel centres; the radius stays 1 in screen units. | [DefaultGrid.tsx](https://github.com/tldraw/tldraw/blob/main/packages/editor/src/lib/components/default-components/DefaultGrid.tsx), [options.ts](https://github.com/tldraw/tldraw/blob/main/packages/editor/src/lib/options.ts) |
| **React Flow / xyflow `<Background>`** (SVG) | One `<pattern>` whose size is `gap × zoom`. The tile draws **only two half-edges**: `M w/2 0 V h M 0 h/2 H w`, a cross at the tile centre offset by −w/2. So each line is drawn once. | None. Users stack two `<Background>` instances for major and minor. | **Stroke width is not scaled by zoom**; only the gap is. Line/dot/cross variants. | [Background.tsx](https://github.com/xyflow/xyflow/blob/main/packages/react/src/additional-components/Background/Background.tsx), [Patterns.tsx](https://github.com/xyflow/xyflow/blob/main/packages/react/src/additional-components/Background/Patterns.tsx) |
| **Nodify (WPF)** | `VisualBrush` tiled with `Viewport="0 0 30 30"` and `Transform="{Binding ViewportTransform}"`, where the tile is a 1×1 rectangle (a dot). | None. | Relies on WPF brush tiling. | [CanvasView.xaml](https://github.com/miroiu/nodify/blob/master/Examples/Nodify.Shapes/Canvas/CanvasView.xaml) |
| **Nodify.Avalonia samples** (the package's repo) | Two `DrawingBrush`es (small, and large at ×10, opacity 0.5) with `Transform` = the editor's `ViewportTransform` and `TransformOrigin="0,0"`. This is the pattern NetPrints copied. | None. | Same resampling issues as today. The Shapes sample assigns `grid.Transform = Editor.ViewportTransform` once, because the `TransformGroup` is mutated in place. | [Playground NodifyEditorView.axaml](https://github.com/trrahul/nodify-avalonia/blob/master/Examples/Nodify.Avalonia.Playground/Editor/NodifyEditorView.axaml), [CanvasView.axaml.cs](https://github.com/trrahul/nodify-avalonia/blob/master/Examples/Nodify.Avalonia.Shapes/Canvas/CanvasView.axaml.cs) |
| **imnodes** (ImDrawList) | `AddLine` per visible line with `fmodf(panning, spacing)`. Optional primary line at the origin. | None. | ImGui AA lines. | [imnodes.cpp `DrawGrid`](https://github.com/Nelarius/imnodes/blob/master/imnodes.cpp) |
| **imgui-node-editor** (thedmd) | `AddLine` per visible line (32 px). | **Grid alpha = clamp(scale², 0, 1)**, a continuous fade as you zoom out. | ImGui AA lines. | [imgui_node_editor.cpp](https://github.com/thedmd/imgui-node-editor/blob/master/imgui_node_editor.cpp) |
| **Qt `QGraphicsView::drawBackground`** | Overrides the method, builds the visible lines into a `QVarLengthArray<QLineF>`, and calls a single `painter->drawLines()`. | None by default. | Uses a **cosmetic pen**, which keeps constant device width under zoom. | [QGraphicsView docs](https://doc.qt.io/qt-6/qgraphicsview.html), [Qt Centre thread](https://www.qtcentre.org/threads/5609-Drawing-grids-efficiently-in-QGraphicsScene) |
| **Ben Golus "pristine grid"** (GPU shader) | Full-surface fragment shader. Line width is specified in UV space. `uvDeriv = length(ddx,ddy)`. `drawWidth = clamp(targetWidth, uvDeriv, 0.5)`. AA of `1.5·uvDeriv` via smoothstep. **"Phone-wire AA":** coverage × `targetWidth/drawWidth` so lines thinner than a pixel get dimmer instead of thinner. **Moiré fade:** `lerp(grid, targetWidth, saturate(uvDeriv*2−1))` fades to the average coverage once cells approach a pixel. The origin is camera-snapped for float precision. | Built-in moiré fade. Major/minor means two evaluations. | Designed for 3D perspective, where derivatives vary per pixel. | [Article](https://bgolus.medium.com/the-best-darn-grid-shader-yet-727f9278b9d8), [gist](https://gist.github.com/bgolus/d49651f52b1dcf82f70421ba922ed064), also [madebyevan grid](https://madebyevan.com/shaders/grid/) |

### Patterns worth copying

- **Constant line width in device pixels.** React Flow, Qt cosmetic pens, Excalidraw and Unity all do this. It fixes "0.15 px lines at zoom 0.3".
- **Snap to device pixels.** Excalidraw has the most careful rule. In an orthographic 2D view, hard-snapped 1-device-px lines look crisper than AA'd lines that straddle two pixels. Blender's line PR even turns off AA output on straight aligned lines because it shimmers.
- **Phase reduction.** Take the offset modulo the major period in double precision before handing it to float code (Unreal `FancyMod`, Golus camera snapping). Without it, float precision breaks far from the origin.
- **Continuous LOD fade instead of pop.** tldraw `modulate`, Blender `1 − fract(level)`, imgui-node-editor `scale²` and Godot's dots all fade. Add hierarchical spacing (×2 in Unreal, ×5 in Blender) only if the zoom range needs it.
- **Draw each line once.** Every per-line renderer does this. React Flow gets it in a tile by drawing only two edges.

---

## 4. API verification (Avalonia 12.1.3 / SkiaSharp 3.119.4)

All of the following were checked by reflection against the NuGet packages in `~/.nuget/packages` and exercised in running prototypes.

**Avalonia rendering**

| API | Status |
|---|---|
| `Avalonia.Rendering.SceneGraph.ICustomDrawOperation` | Exists: `Rect Bounds`, `bool HitTest(Point)`, `void Render(ImmediateDrawingContext)`, plus `IEquatable<ICustomDrawOperation>` and `IDisposable`. |
| `DrawingContext.Custom(ICustomDrawOperation)` | Exists. |
| `ImmediateDrawingContext.TryGetFeature(Type)` | Exists, plus the generic extension. It also offers `FillRectangle(IImmutableBrush, Rect, float)`, `DrawLine(ImmutablePen, …)` and `PushClip`, which a non-Skia third tier could use. |
| `Avalonia.Skia.ISkiaSharpApiLeaseFeature.Lease()` | Returns `ISkiaSharpApiLease` with `SKCanvas SkCanvas`, `GRContext GrContext` (null on raster), `SKSurface SkSurface`, `double CurrentOpacity` and `TryLeasePlatformGraphicsApi()`. |
| `CompositionCustomVisualHandler` / `Compositor.CreateCustomVisual` | Exist. This is an alternative host that renders on the render thread (see §5e). |
| `RenderOptions.EdgeMode` / `DrawingContext.PushRenderOptions` | Exist. They give aliased edges for option (b). |
| Docs | [Custom rendering docs](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering), [CustomSkiaPage sample](https://github.com/AvaloniaUI/Avalonia/blob/master/samples/RenderDemo/Pages/CustomSkiaPage.cs). |

**Nodify.Avalonia 2.0.0**

- `ViewportLocation` (`StyledProperty<Point>`) and `ViewportZoom` (`StyledProperty<double>`).
- `ViewportTransform`: a `TransformGroup` of `ScaleTransform` and `TranslateTransform`, **mutated in place** and never re-created.
- `ViewportSize`, the `ViewportUpdated` routed event, and `GridCellSize` (`uint`).
- There is no built-in grid renderer. The internal `OnRenderBackground` belongs to `LayeredShape`, not the editor.
- The editor template paints `Background` on its root `Border`, so a transparent background still hit-tests.

**SkiaSharp**

- `SKRuntimeEffect.CreateShader(string, out string errors)`, `SKRuntimeEffectUniforms`, `SKRuntimeEffect.ToShader(uniforms)` and `SKRuntimeShaderBuilder` all exist in 3.119.4. See [SkSL docs](https://skia.org/docs/user/sksl/): `main()` receives **local** coordinates and must return **premultiplied** colour.
- **`fwidth`/`dFdx` are rejected** (tested).
- Runtime effects **work on the raster backend**, but slowly (tested, §6).

**Headless**

- `UseSkia()` plus `UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`, which is what `NetPrints.Editor.UITests/TestAppBuilder.cs` already does. It provides `ISkiaSharpApiLeaseFeature` with `GrContext == null`, and `CaptureRenderedFrame()` returns real pixels ([docs](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform)).
- With `UseHeadlessDrawing = true` there is no Skia feature at all, and `TryGetFeature` returns null.

---

## 5. Options compared

Measurements were taken on this machine: Linux, Skia raster via `SKSurface`, Release build, averaged over 20–30 frames, plus one real window on OpenGL (NVIDIA).

| | (a) Tiled DrawingBrush / VisualBrush, fixed 2-edge tile | (b) Custom `Control.Render()` with `DrawLine` / cached `StreamGeometry` | (c) `ICustomDrawOperation` + `SKCanvas` (reused `SKPath` of rects, non-AA) | (d) SkSL `SKRuntimeEffect` on one rect | (e) Composition custom visual hosting (c)/(d) |
|---|---|---|---|---|---|
| CPU per frame | Picture re-record, picture-shader cache keyed on scale, plus a re-raster whenever zoom changes. Low when static, spikes while zooming. | O(visible lines): 60–150 `DrawLine` calls through the Avalonia recorder. Cached geometry needs a rebuild on every zoom anyway because snapping depends on zoom. | O(visible lines) path building. **Raster: 2.7 ms at 1080p, 10.8 ms at 4K** (full-height rect fills dominate). On GPU, only path upload. | **O(1)**: a few uniforms and one draw. | Same as (c)/(d), but a pan can update without a UI-thread `Render()`. |
| GPU per frame | One textured quad. | Many small line draws, batched by Skia. | 2 path fills (the GPU tessellates rects). | One full-screen fragment program with ~30 ALU ops per pixel: trivial. | Same. |
| CPU if no GPU | OK. | OK. | **OK (2.7 ms at 1080p)**. | **Very slow: 310 ms at 1080p, 1241 ms at 4K.** Never use on raster. | Same. |
| Managed allocations per frame | New `MatrixTransform` plus picture/shader wrappers every draw. | Pens and brushes cacheable, geometry re-created per zoom. `DrawLine` itself does not allocate much. | **≈0 B** measured: `[ThreadStatic]` `SKPath` with `Rewind()` and a reused `SKPaint`. One ~80 B draw-op per invalidation. | **~1.4 KB** measured (uniforms, `SKShader` and `SKPaint` wrappers). It could be cut by caching the uniforms object and paint. | Handler is persistent; messages are small. |
| Crispness at any zoom | Poor: the tile is resampled and the stroke scales with zoom. | Good if snapped manually via `RenderScaling`. Avalonia coordinates are DIPs, so it is error-prone at 1.25 or 1.5. | **Exact**: works in device px after `ResetMatrix()`, using integer columns and rows. | **Exact**: same rule, per pixel. | Same. |
| AA | Implicit, from bilinear resampling. | Via `EdgeMode`. | Off by design; an optional AA variant is possible (§7.4). | Analytic coverage is possible (§7.4). | Same. |
| DPI scaling | Tile raster DPI follows `_intermediateSurfaceDpi`. Blurry at fractional DPI (#1614). | Must query `TopLevel.RenderScaling`. | Automatic: `TotalMatrix.ScaleX` already contains `RenderScaling`. Tested at 1, 1.25, 1.5, 2 and 2.5. | Same. | Same. |
| LOD / fading | Would need several brushes whose opacity is animated from code. | Easy. | Easy, and hidden minor lines are skipped entirely. | Easy (uniform alpha). | Same. |
| Major/minor | Two brushes, as in the Nodify sample. Crossings double-blend. | Easy. | Easy, single-blend (union fill). | Easy. | Same. |
| Dot variant | Easy (1×1 tile). | `DrawRectangle` per dot, so O(n²). | `DrawPoints` with square cap and no AA: 1.1 ms for 42k dots at 4K raster. | Trivial (hitX && hitY). | Same. |
| Theming | `DynamicResource` brush. | StyledProperties. | StyledProperties passed into the op. | Uniforms. | Same. |
| Headless snapshots | Deterministic but blurry. | Deterministic. | **Deterministic; the default in tests.** | Works under headless Skia (tested) but slowly; use small frames for the equivalence test only. | Same. |
| Complexity | Low, but you cannot fix the quality. | Medium. | Medium: ~80 lines. | Medium: ~40 lines of SkSL plus a fallback. | Higher: message passing and lifecycle. |

**(a) cannot be fixed** within Avalonia's tile brush. Drawing only two edges per tile (the React Flow trick) removes double-drawing. It does not fix resampling, zoom-scaled strokes, fractional-DPI blur or LOD.

**(b) works but is weaker than (c).** It works in DIPs, so pixel snapping has to divide by `RenderScaling`. Crossings blend twice unless the lines are merged into one geometry. `StreamGeometry` caching gains nothing, because the snapped positions change with every zoom step.

**(e)** is only worth it if profiling shows the UI-thread `Render()` round trip matters during pan. Nodify already updates the `TransformGroup` in place and re-renders items anyway.

---

## 6. Evidence gathered in this session

1. **SkSL derivatives:** `SKRuntimeEffect.CreateShader` with `fwidth(g)` fails with `error: 5: no match for fwidth(float2)`. With the derivative supplied as a uniform, it compiles.
2. **Shader vs CPU path, raw SKSurface:** tested at zoom/scaling/pan combinations (1, 1, 0/0), (0.3, 1, 13.7/−5.2), (0.47, 1.25, −12345.6/987.1), (0.83, 2, 3.3/4.4) and (0.61, 1.5, 1e6/−2e6). The **max channel difference was 1/255** (premultiply rounding). There were no geometric differences.
3. **Headless Avalonia**, with `UseSkia` and `UseHeadlessDrawing = false`, a `Border` with a fractional margin (7.3, 3.1):
   - `Auto` → cpu, and `GrContext` = null (raster).
   - Forced `Shader` → shader.
   - **Shader vs CPU: 0 / 64000 pixels differ.**
4. **Real window on X11/OpenGL (NVIDIA), render scaling 2.5, 4000×2250 device px:** the shader path reported `gpu=True backend=OpenGL`. The **shader and CPU path frames had the same SHA-1**.
5. **Raster timings:**

   | Case | 1080p | 4K |
   |---|---|---|
   | CPU path | 2.69 ms | 10.8 ms |
   | Shader on raster | 310 ms | 1241 ms |
   | 4K reference: per-line `DrawLine`, non-AA snapped | | 6.0 ms |
   | 4K reference: per-line `DrawLine`, AA fractional | | 9.5 ms |
   | 4K reference: single AA stroked `SKPath` | | 3.4 ms |

6. **Contrast** on the dark background `#202020`, white at alpha α (WCAG ratio):

   | α | 0.06 | 0.08 | 0.12 | 0.18 | 0.25 | 0.35 |
   |---|---|---|---|---|---|---|
   | Ratio | 1.18 | 1.27 | 1.45 | 1.78 | 2.29 | 3.20 |

   On light `#F3F3F3`, black at α = 0.08, 0.18 and 0.35 gives 1.19, 1.52 and 2.41.

---

## 7. Recommendation for NetPrints

### 7.1 Architecture

```
Panel
 ├─ GridBackground   (IsHitTestVisible=False, Background painted by the Panel/theme)
 └─ NodifyEditor     (Background="Transparent" – still hit-testable for panning/selection)
```

- **One shared definition.**
  - `GridStyle` is an immutable record holding: `CellSize` (= `GraphConstants.GridCellSize`, 28), `MajorEvery` (5), `MinorWidthDip`/`MajorWidthDip` (1/1), `MinorColor`/`MajorColor`, and `MinorFadeOutDip`/`MinorFadeInDip` (6/14).
  - `GridFrame.Compute(style, location, zoom, deviceScale, deviceOrigin, opacity)` is the **only** place that does maths in double precision. It computes:
    - device cell size;
    - phase modulo one major period;
    - integer device line widths, `max(1, round(dip·scale))`;
    - the smoothstep minor fade, folded into the minor colour's alpha;
    - opacity.
  - Both renderers consume only `GridFrame`, and they share the snapping rule `col0 = floor(L + 0.5 − w/2)`.
- **Path selection** happens per frame, inside `ICustomDrawOperation.Render`:

  | Condition | Path |
  |---|---|
  | No `ISkiaSharpApiLeaseFeature` (non-Skia backend, `UseHeadlessDrawing = true`) | Third tier: draw nothing, or use `ImmediateDrawingContext.FillRectangle` with the same `GridFrame` maths. |
  | `Mode == Cpu`, or `lease.GrContext is null` (software rendering, headless Skia, `RenderingMode = Software`, VMs/RDP without GL) | CPU `SKPath` path. |
  | `GridRenderer.ShaderAvailable == false` (compile failed; log the `errors` string once) | CPU `SKPath` path. |
  | Otherwise | Shader. |

  - Allow an override for field issues: an app setting or environment variable such as `NETPRINTS_GRID=cpu|shader|auto`. Expose `Mode` as a StyledProperty so tests can force either path.
  - Detection is cheap and exact. `GrContext` is non-null only on GPU backends (OpenGL/ANGLE on Windows, Metal on macOS, Vulkan/GL on Linux), and the effect is compiled once, lazily and thread-safely.
- **Coordinate handling.** Inside the draw op:
  - read `canvas.TotalMatrix` (which includes `RenderScaling` and the control's offset);
  - map the bounds to a device rect;
  - `Save` → `ResetMatrix` → `ClipRect(deviceRect)` → draw → `Restore`.

  Everything is then in true device pixels on every DPI. That is why fractional scalings (1.25, 1.5, 2.5) stay crisp.
- **Compositing** is identical in both paths:
  - Minor lines skip major indices.
  - The CPU path fills all minor rects as one `SKPath`. Non-zero fill takes the union, so crossings blend once.
  - It then fills the major path, which is drawn over the minor one.
  - The shader computes `major + minor·(1 − major.a)` from the same coverages.
- **LOD** for NetPrints' zoom range, 0.3–1:
  - Minor cells span 8.4–28 DIP.
  - Minor alpha = base × smoothstep over [6, 14] DIP. That is full at zoom ≥ 0.5, ≈ 20% at 0.3, and never pops.
  - Major cells (140 units) span 42–140 DIP, so they never need a fade.
  - If `MinViewportZoom` is lowered later, add Blender-style hierarchical levels: when minor alpha reaches 0, the step becomes `cell × MajorEvery`, cross-faded with `1 − fract(level)`. Put this in `GridFrame.Compute` so both paths inherit it.
- **Dot variant.**
  - Shader: `coverage = hitX && hitY`.
  - CPU: `DrawPoints(SKPointMode.Points)` with a square cap, no AA, and stroke width = w, centred at `col0 + w/2`. Or add w×w rects to the path.
  - Both use the same snapping.
- **Sync with Nodify without allocations.** Keep the existing `Editor.PropertyChanged` filter, but set `Grid.ViewportLocation = Editor.ViewportLocation; Grid.ViewportZoom = Editor.ViewportZoom;`. These are typed StyledProperties with `AffectsRender`, so there is no boxing and no transform objects. This replaces `UpdateGridTransform()` and its `new MatrixTransform`. A XAML binding (`ViewportZoom="{Binding #Editor.ViewportZoom}"`) also works; the code route avoids binding overhead.
- **Theming and accessibility.**
  - Put `GraphGrid.MinorColor`, `GraphGrid.MajorColor` and `GraphGrid.BackgroundBrush` in `ThemeDictionaries` (Dark/Light) and bind them with `DynamicResource`. Theme switches then re-render automatically, unlike today's one-time lookup.
  - The grid is decorative. WCAG 1.4.11 (3:1) applies only to graphics needed to understand content, so it is exempt. It should still be visible without competing with wires and nodes.
  - Suggested targets:
    - dark: minor ≈ 1.25–1.3:1 (white α ≈ 0.08), major ≈ 1.7–1.8:1 (α ≈ 0.18);
    - light: black α ≈ 0.08 / 0.18.
  - Keep node and wire contrast well above the major lines.
  - For a high-contrast theme, use opaque `SystemColors`-like resources and consider `MajorWidthDip = 2`.
  - Colours are sRGB with premultiplied alpha. The shader receives premultiplied `SKColorF` and does no colour-space conversion. Do not use `layout(color)` if you want both paths to match, because on SRGB surfaces Skia would otherwise convert only the shader's colours.

### 7.2 Fallback quality

The CPU path is not a placeholder: it gave the same pixels under both backends tested. It also costs less than the current DrawingBrush on raster (2.7 ms at 1080p), with near-zero managed allocations.

If the SkSL route ever proves troublesome, for example because of a driver bug, the **CPU path alone is a complete solution**. It is what Unreal, Unity, Godot, Excalidraw and Blender's new grid effectively do.

### 7.3 Testing (Avalonia.Headless)

- **Default snapshot tests** run the CPU path. Under headless Skia, `GrContext == null`, so `Auto` picks CPU. The result is deterministic and uses integer pixel coverage, with no AA.
- **Equivalence test:** render the same small window (e.g. 320×200, with a fractional offset and scalings 1/1.25/2 via `TopLevel` scaling if the harness supports it) with `Mode=Shader` and `Mode=Cpu`. Assert a max channel difference ≤ 1 (0 was observed). This proves the shader and fallback agree without a GPU. Keep frames small because the shader is slow on raster.
- **Unit tests for `GridFrame.Compute`:** phase reduction far from the origin (±1e6), fade endpoints, and width rounding at scalings 1.25 and 1.5.
- **Assert the chosen path** via a test hook, for example a `LastPath` diagnostic or an event. In the automation tree, expose the grid mode next to the existing `GridCellSize` property.

### 7.4 Pristine-grid-style anti-aliasing (optional mode)

Golus's formula ports directly with `uvDeriv = 1/cellDevicePx`, a uniform. Both paths can still match with AA on:

- **Shader:** coverage = overlap of the pixel `[col, col+1]` with the line `[L − w/2, L + w/2]`, clamped to [0, 1]. This is the analytic box filter.
- **CPU:** fill the unsnapped rect `[L − w/2, L + w/2]` with `IsAntialias = true`. Skia's analytic AA for axis-aligned rect fills is exactly area coverage.

Use this only for sub-pixel widths or if the owner prefers softer lines. For 1-px axis-aligned lines in an orthographic view, hard snapping is crisper. Excalidraw and Blender's grid PR reach the same conclusion.

---

## 8. Code sketch (Avalonia 12, C#; compiled and run in the prototype)

```csharp
// ---- Shared definition (one source of truth) -------------------------------------------
public sealed record GridStyle(
    double CellSize = 28, int MajorEvery = 5,
    double MinorWidthDip = 1, double MajorWidthDip = 1,
    SKColor MinorColor = default, SKColor MajorColor = default,
    double MinorFadeOutDip = 6, double MinorFadeInDip = 14);

public readonly record struct GridFrame(float PhaseX, float PhaseY, float Cell, float MajorEvery,
                                        float MinorW, float MajorW, SKColor Minor, SKColor Major)
{
    public static GridFrame Compute(GridStyle s, double locX, double locY, double zoom, double scaling,
                                    double devOriginX, double devOriginY, double opacity)
    {
        double cellDip = s.CellSize * zoom, cellDev = cellDip * scaling, period = cellDev * s.MajorEvery;
        double px = Mod(devOriginX - locX * zoom * scaling, period);        // phase: small floats, major index kept
        double py = Mod(devOriginY - locY * zoom * scaling, period);
        double t = Math.Clamp((cellDip - s.MinorFadeOutDip) / (s.MinorFadeInDip - s.MinorFadeOutDip), 0, 1);
        double fade = t * t * (3 - 2 * t);                                   // smoothstep: fade, never pop
        return new((float)px, (float)py, (float)cellDev, s.MajorEvery,
            (float)Math.Max(1, Math.Round(s.MinorWidthDip * scaling)),
            (float)Math.Max(1, Math.Round(s.MajorWidthDip * scaling)),
            s.MinorColor.WithAlpha((byte)Math.Round(s.MinorColor.Alpha * fade * opacity)),
            s.MajorColor.WithAlpha((byte)Math.Round(s.MajorColor.Alpha * opacity)));
    }
    static double Mod(double a, double m) => a - m * Math.Floor(a / m);
}

// ---- Renderers --------------------------------------------------------------------------
public static class GridRenderer
{
    const string Sksl = @"
uniform float2 phase; uniform float cell; uniform float majorEvery;
uniform float minorW; uniform float majorW;
uniform half4 minorColor; uniform half4 majorColor;          // premultiplied
float2 hits(float c, float o) {                               // (minor, major) coverage on one axis
    float col = floor(c);
    float k = floor((c - o) / cell + 0.5);                    // nearest line index
    float m = k - majorEvery * floor(k / majorEvery);
    bool isMajor = m < 0.5 || m > majorEvery - 0.5;
    float w = isMajor ? majorW : minorW;
    float col0 = floor(o + k * cell + 0.5 - 0.5 * w);         // SAME rule as DrawCpu
    float hit = (col >= col0 && col < col0 + w) ? 1.0 : 0.0;
    return isMajor ? float2(0.0, hit) : float2(hit, 0.0);
}
half4 main(float2 p) {                                        // p = device pixel centre
    float2 hx = hits(p.x, phase.x), hy = hits(p.y, phase.y);
    half4 mi = minorColor * half(max(hx.x, hy.x));
    half4 ma = majorColor * half(max(hx.y, hy.y));
    return ma + mi * (1.0 - ma.a);                            // major over minor, like DrawCpu
}";
    static readonly Lazy<SKRuntimeEffect?> effect = new(() =>
    {
        var e = SKRuntimeEffect.CreateShader(Sksl, out var errors);
        if (e is null) Trace.TraceWarning($"Grid shader unavailable: {errors}");   // log once
        return e;
    });
    public static bool ShaderAvailable => effect.Value is not null;

    public static void DrawShader(SKCanvas c, SKRect r, in GridFrame f)
    {
        var e = effect.Value!;
        using var u = new SKRuntimeEffectUniforms(e)
        {
            ["phase"] = new SKPoint(f.PhaseX, f.PhaseY), ["cell"] = f.Cell, ["majorEvery"] = f.MajorEvery,
            ["minorW"] = f.MinorW, ["majorW"] = f.MajorW,
            ["minorColor"] = Premul(f.Minor), ["majorColor"] = Premul(f.Major),
        };
        using var shader = e.ToShader(u);
        using var paint = new SKPaint { Shader = shader, IsAntialias = false };
        c.DrawRect(r, paint);
    }

    [ThreadStatic] static SKPath? minorPath, majorPath;  // render thread: reused, zero managed allocs
    [ThreadStatic] static SKPaint? fill;

    public static void DrawCpu(SKCanvas c, SKRect r, in GridFrame f)
    {
        var minor = minorPath ??= new SKPath(); var major = majorPath ??= new SKPath();
        minor.Rewind(); major.Rewind();
        AddAxis(minor, major, f, r.Left, r.Right, f.PhaseX, vertical: true, r);
        AddAxis(minor, major, f, r.Top, r.Bottom, f.PhaseY, vertical: false, r);
        var p = fill ??= new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        if (f.Minor.Alpha > 0) { p.Color = f.Minor; c.DrawPath(minor, p); }   // union fill: crossings blend once
        if (f.Major.Alpha > 0) { p.Color = f.Major; c.DrawPath(major, p); }
    }

    static void AddAxis(SKPath minor, SKPath major, in GridFrame f, float from, float to, float o, bool vertical, SKRect r)
    {
        int n = (int)f.MajorEvery;
        for (int k = (int)MathF.Floor((from - o) / f.Cell) - 1, end = (int)MathF.Ceiling((to - o) / f.Cell) + 1; k <= end; k++)
        {
            bool isMajor = ((k % n) + n) % n == 0;
            if (!isMajor && f.Minor.Alpha == 0) continue;                    // LOD: hidden minors cost nothing
            float w = isMajor ? f.MajorW : f.MinorW;
            float col0 = MathF.Floor(o + k * f.Cell + 0.5f - 0.5f * w);
            (isMajor ? major : minor).AddRect(vertical ? new SKRect(col0, r.Top, col0 + w, r.Bottom)
                                                       : new SKRect(r.Left, col0, r.Right, col0 + w));
        }
    }
    static SKColorF Premul(SKColor c) { float a = c.Alpha / 255f; return new(c.Red / 255f * a, c.Green / 255f * a, c.Blue / 255f * a, a); }
}

// ---- Control ------------------------------------------------------------------------------
public enum GridRenderMode { Auto, Shader, Cpu }

public sealed class GridBackground : Control
{
    public static readonly StyledProperty<Point> ViewportLocationProperty = AvaloniaProperty.Register<GridBackground, Point>(nameof(ViewportLocation));
    public static readonly StyledProperty<double> ViewportZoomProperty = AvaloniaProperty.Register<GridBackground, double>(nameof(ViewportZoom), 1.0);
    public static readonly StyledProperty<Color> MinorColorProperty = AvaloniaProperty.Register<GridBackground, Color>(nameof(MinorColor));
    public static readonly StyledProperty<Color> MajorColorProperty = AvaloniaProperty.Register<GridBackground, Color>(nameof(MajorColor));
    public static readonly StyledProperty<GridRenderMode> ModeProperty = AvaloniaProperty.Register<GridBackground, GridRenderMode>(nameof(Mode));
    static GridBackground() => AffectsRender<GridBackground>(ViewportLocationProperty, ViewportZoomProperty, MinorColorProperty, MajorColorProperty, ModeProperty);
    // CLR wrappers omitted for brevity

    GridStyle? style;   // rebuilt only when colours change
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == MinorColorProperty || e.Property == MajorColorProperty) style = null;
    }

    public override void Render(DrawingContext context)
    {
        style ??= new GridStyle(CellSize: GraphConstants.GridCellSize, MinorColor: ToSk(MinorColor), MajorColor: ToSk(MajorColor));
        context.Custom(new GridDrawOp(new Rect(Bounds.Size), style, ViewportLocation, ViewportZoom, Mode)); // ~80 B per invalidation
    }
    static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

    sealed class GridDrawOp(Rect bounds, GridStyle style, Point loc, double zoom, GridRenderMode mode) : ICustomDrawOperation
    {
        readonly (Rect, GridStyle, Point, double, GridRenderMode) key = (bounds, style, loc, zoom, mode);
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => other is GridDrawOp o && o.key.Equals(key);
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)       // render thread
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature) return; // non-Skia: tier 3
            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var m = canvas.TotalMatrix;                             // includes RenderScaling + control offset
            var device = m.MapRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            var frame = GridFrame.Compute(style, loc.X, loc.Y, zoom, m.ScaleX, m.TransX, m.TransY, lease.CurrentOpacity);
            bool shader = mode switch
            {
                GridRenderMode.Cpu => false,
                GridRenderMode.Shader => GridRenderer.ShaderAvailable,
                _ => lease.GrContext is not null && GridRenderer.ShaderAvailable,   // never SkSL on raster
            };
            canvas.Save();
            canvas.ResetMatrix();                                   // work in device pixels
            canvas.ClipRect(device);
            if (shader) GridRenderer.DrawShader(canvas, device, frame); else GridRenderer.DrawCpu(canvas, device, frame);
            canvas.Restore();
        }
    }
}
```

XAML and view wiring (replaces the `DrawingBrush` and `UpdateGridTransform`):

```xml
<Panel Background="{DynamicResource GraphGrid.BackgroundBrush}">
  <edgraph:GridBackground x:Name="Grid" IsHitTestVisible="False"
                          MinorColor="{DynamicResource GraphGrid.MinorColor}"
                          MajorColor="{DynamicResource GraphGrid.MajorColor}" />
  <nodify:NodifyEditor x:Name="Editor" Background="Transparent" GridCellSize="28" ... />
</Panel>
```

```csharp
Editor.PropertyChanged += (_, e) =>
{
    if (e.Property == NodifyEditor.ViewportLocationProperty) Grid.ViewportLocation = Editor.ViewportLocation;
    else if (e.Property == NodifyEditor.ViewportZoomProperty) Grid.ViewportZoom = Editor.ViewportZoom;
};
```

Theme resources (App or theme dictionary):

```xml
<ResourceDictionary.ThemeDictionaries>
  <ResourceDictionary x:Key="Dark">
    <SolidColorBrush x:Key="GraphGrid.BackgroundBrush" Color="#202020" />
    <Color x:Key="GraphGrid.MinorColor">#14FFFFFF</Color>  <!-- ≈1.27:1 -->
    <Color x:Key="GraphGrid.MajorColor">#2EFFFFFF</Color>  <!-- ≈1.78:1 -->
  </ResourceDictionary>
  <ResourceDictionary x:Key="Light">
    <SolidColorBrush x:Key="GraphGrid.BackgroundBrush" Color="#F3F3F3" />
    <Color x:Key="GraphGrid.MinorColor">#14000000</Color>
    <Color x:Key="GraphGrid.MajorColor">#2E000000</Color>
  </ResourceDictionary>
</ResourceDictionary.ThemeDictionaries>
```

---

## 9. Risks and open questions

1. **GPU float and rounding differences.** On some GPUs or ANGLE/D3D, `floor((c − o)/cell + 0.5)` evaluated in fp32 on the GPU could differ from the CPU result exactly at .5 boundaries, shifting one line by 1 px in rare frames. Snapshot tests run on raster, so this does not affect CI.
   - Mitigation: keep the phase small (already done), and optionally pass `1/cell` so that both paths multiply instead of divide.
   - Open: verify on Windows (ANGLE) and macOS (Metal). Only Linux/OpenGL/NVIDIA was tested.
2. **SkSL availability.** `SKRuntimeEffect` is present in all SkiaSharp 3.x native builds that NetPrints ships, including `NativeAssets.Linux.NoDependencies`. Compile failure is detected, and the result falls back to CPU. There is no runtime signal for a driver miscompile, so keep the `NETPRINTS_GRID=cpu` escape hatch.
3. **Software rendering performance.** The CPU path takes 2.7 ms at 1080p and 10.8 ms at 4K on raster. That is fine for pan/zoom, but at 4K software rendering it is a noticeable share of a 16 ms frame.
   - If needed: skip minor lines earlier on raster, or fill only the visible part of each rect when an ancestor clips. Skia clips anyway.
   - Worth measuring: raster fill of full-height rects vs `DrawLine`.
4. **Draw-op lifetime.** `ICustomDrawOperation.Render` runs on the render thread after `Control.Render`, so the op must be immutable. The sketch captures values, and `[ThreadStatic]` caches are render-thread-only. Do not share the `SKPath` with the UI thread.
5. **`Equals` semantics.** Avalonia uses `Equals` to skip redundant redraws of an unchanged op. The key includes every input; if new inputs are added, add them to the key.
6. **Opacity and layering.** The grid uses `lease.CurrentOpacity`. If an ancestor applies an opacity mask or effects, test that the leased canvas respects them.
7. **Major period and LOD policy (design decisions for the owner).**
   - Major every 5 cells (140 units, as in the brief)? Or 8, matching Unreal's rule period?
   - Should there be a centre/origin line (Unreal `GridCenterColor`)?
   - Fade thresholds (6/14 DIP) or tldraw-like zoom-based fades?
   - Lines or a dot grid by default?
8. **Wider zoom ranges.** If `MinViewportZoom` drops below about 0.2, add hierarchical levels (§7.1) in `GridFrame.Compute`. Both paths inherit them automatically.
9. **Blender precedent.** Blender's move away from a fragment-shader grid shows that the shader route is not automatically faster or simpler, even on GPU. The reason for keeping the shader here is the O(1) CPU cost during pan and zoom on GPU backends. Consider profiling both paths on GPU; the CPU path might be enough everywhere. The shared-definition design makes that choice a one-line change.
