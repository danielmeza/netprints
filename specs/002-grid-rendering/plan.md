# Implementation Plan: Grid Rendering

**Branch**: `002-grid-rendering` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-grid-rendering/spec.md`

## Summary

Replace the tiled DrawingBrush grid of the graph canvas with a `GridBackground` control that draws a
pixel-snapped major/minor grid with LOD fading, through an SkSL shader on GPU renderers and an
identical CPU `SKPath` path otherwise, both driven by one `GridStyle`/`GridFrame` definition
(research.md D1–D9).

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: Avalonia 12.1.3, Avalonia.Skia 12.1.3 (new direct reference of
NetPrints.Editor, for `ISkiaSharpApiLeaseFeature`), SkiaSharp 3.119.4, Nodify.Avalonia 2.0.0

**Storage**: N/A

**Testing**: xUnit v3; `NetPrints.Editor.Tests` (unit: `GridFrame.Compute`, mode resolution);
`NetPrints.Editor.UITests` (Avalonia.Headless + Skia: shader vs CPU pixel identity, baseline
snapshot, regenerated canvas baselines); E2E suite once on a private Xvfb

**Target Platform**: Linux, Windows, macOS (desktop); headless Linux in CI

**Project Type**: Desktop application (editor library)

**Performance Goals**: CPU path < 5 ms at 1080p in software; shader O(1) CPU; no per-frame transform allocations

**Constraints**: Pixel-identical paths; comment density per AGENTS.md

**Scale/Scope**: 3–4 new source files in `NetPrints.Editor/Graph`, view/XAML/theme edits, tests

## Constitution Check

- I. Cross-platform: Skia is the renderer on all three OSes; no platform APIs. ✅
- II. UI-agnostic core: changes are confined to `NetPrints.Editor` (the UI layer). ✅
- III. Extension-first: no target-specific code. ✅
- IV. net10.0: no new targets; Avalonia.Skia is already a centrally managed package. ✅
- V. Tests gate: unit + headless snapshot tests; full suite and E2E run. ✅
- VI. Deterministic output: snapshots are deterministic (integer pixel coverage, no AA). ✅
- VII. I/O abstractions: N/A. ✅
- VIII. Simplicity: one control, one draw operation, two small render paths; hierarchical levels deferred. ✅

## Project Structure

### Documentation (this feature)

```text
specs/002-grid-rendering/
├── spec.md
├── plan.md
├── research.md      # decisions; the full study is in docs/research/2026-09-25-grid-rendering/
├── tasks.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
NetPrints.Editor/
├── NetPrints.Editor.csproj          # + Avalonia.Skia
├── EditorStyles.axaml               # + GraphGrid.* theme dictionaries
├── AutomationIds.cs                 # + GraphGrid
├── Hosting/Automation/AutomationTree.cs   # + GridRenderPath property
└── Graph/
    ├── GridStyle.cs                 # GridStyle, GridFrame (shared definition)
    ├── GridRenderer.cs              # SkSL shader path and CPU SKPath path
    ├── GridBackground.cs            # control, draw operation, mode selection
    ├── GraphEditorView.axaml        # grid behind a transparent editor
    └── GraphEditorView.axaml.cs     # viewport sync, old brush removed
NetPrints.Testing.Ui/Graph/GraphCanvas.cs          # + grid page-object members
NetPrints.Desktop.E2ETests/Hosting/XServer.cs      # + NETPRINTS_E2E_DISPLAY_START
NetPrints.Editor.Tests/Graph/GridFrameTests.cs
NetPrints.Editor.UITests/Graph/GridRenderTests.cs
NetPrints.Editor.UITests/Snapshots/Baselines/*.png   # grid-background + regenerated canvas baselines
```

**Structure Decision**: Everything lives in the existing editor and test projects; no new projects.

## Complexity Tracking

None.
