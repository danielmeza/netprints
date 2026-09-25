# Feature Specification: Grid Rendering

**Feature Branch**: `002-grid-rendering`

**Created**: 2026-09-25

**Status**: Draft

**Input**: Coordinator description: "Implement the approved grid research as phase P0.1: replace the
tiled DrawingBrush background of the graph canvas with a crisp, pixel-aligned grid with major and
minor lines and level-of-detail fading. Primary path: an SkSL shader when a GPU context exists and
the effect compiles; fallback: a pixel-identical CPU path. Both paths share one definition."

**Roadmap phase**: P0.1 (small follow-up to P0, PR #1). **Research**:
[`docs/research/2026-09-25-grid-rendering/`](../../docs/research/2026-09-25-grid-rendering/README.md).

## Clarifications

### Session 2026-09-25 (owner decisions, via the coordinator)

- Q: Which render paths? → A: SkSL runtime shader when the renderer has a GPU context and the effect compiles; otherwise a CPU path (two paths: minor and major) that produces the same pixels. Both paths share one style/frame definition.
- Q: Major line period? → A: Every 8 cells. Lines, not dots. No origin line.
- Q: Minor-line fade? → A: Minor lines fade out between a cell size of 14 DIP (full) and 6 DIP (hidden).
- Q: Colors? → A: From theme resources (theme dictionaries).
- Q: How is the path chosen? → A: A `Mode` property (Auto, Shader, Cpu) plus a `NETPRINTS_GRID` environment override.
- Q: What happens to the old DrawingBrush grid? → A: Removed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crisp grid at every zoom and display scale (Priority: P1)

A NetPrints user pans and zooms the graph canvas. The background grid stays sharp: every line is
exactly one device pixel wide (at 100% display scale), lines are not drawn twice, and there is no
blur, dotting or moiré at any zoom between 0.3 and 1, on any display scale.

**Why this priority**: The current grid looks blurry/dotted and turns into moiré at low zoom.

**Independent Test**: Render the canvas at several zoom levels, pan offsets and display scales; every
grid line covers whole device pixels with a constant color, and minor lines fade smoothly as the
user zooms out.

**Acceptance Scenarios**:

1. **Given** zoom 1, **When** the canvas renders, **Then** minor lines appear every 28 graph units and major lines every 8 cells (224 units), each 1 device pixel wide with a uniform color.
2. **Given** the user zooms out, **When** a cell becomes smaller than 14 DIP, **Then** minor lines fade continuously and are hidden below 6 DIP; major lines stay.
3. **Given** a display scale of 125%, 150% or 200%, **When** the canvas renders, **Then** lines still cover whole device pixels (width rounded to whole device pixels, at least 1).
4. **Given** the view is panned far from the origin (±1,000,000 units), **When** the canvas renders, **Then** the grid is still exact.

---

### User Story 2 - Same look on GPU and software rendering (Priority: P1)

Whether the editor renders on a GPU or in software (headless tests, VMs, remote desktops), the
grid looks the same, and rendering stays fast.

**Independent Test**: Headless snapshot rendered with the shader forced and with the CPU path forced;
the two images are pixel-identical and match the committed baseline.

**Acceptance Scenarios**:

1. **Given** a GPU renderer and a compiled effect, **When** Mode is Auto, **Then** the shader path draws the grid.
2. **Given** a software renderer, a failed shader compile, or `NETPRINTS_GRID=cpu`, **When** Mode is Auto, **Then** the CPU path draws the grid.
3. **Given** the same frame, **When** drawn by either path, **Then** the pixels are identical.

---

### User Story 3 - Themed grid (Priority: P2)

The grid colors come from the editor theme, so a theme variant change recolors the grid without
restarting.

**Independent Test**: Switch the theme variant; the grid control's colors follow the theme resources.

### Edge Cases

- No Skia renderer (headless drawing without pixels): nothing is drawn, no error.
- The shader fails to compile: a warning is logged once and the CPU path is used.
- Control opacity below 1: both paths apply the same opacity (one layer around the grid).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The graph canvas background MUST draw minor lines every grid cell (28 graph units) and major lines every 8 cells, with no origin line.
- **FR-002**: Lines MUST be snapped to whole device pixels, with a width of `max(1, round(width DIP × display scale))` device pixels; each line MUST be drawn once (major lines win where they cross minor lines).
- **FR-003**: Minor lines MUST fade continuously (smoothstep) from full at a cell size of 14 DIP to hidden at 6 DIP.
- **FR-004**: The canvas background and line colors MUST come from theme resources in theme dictionaries (dark and light).
- **FR-005**: The grid MUST follow the canvas viewport location and zoom without allocating transforms per viewport change.
- **FR-006**: A shader path and a CPU path MUST share one definition of the grid (style and per-frame parameters) and produce pixel-identical output.
- **FR-007**: The path MUST be selectable with a `Mode` property (Auto, Shader, Cpu); in Auto the shader is used only with a GPU context and a compiled effect; `NETPRINTS_GRID` (`auto`, `cpu`, `shader`) overrides Auto.
- **FR-008**: The old DrawingBrush grid MUST be removed.
- **FR-009**: The path used for the last frame MUST be observable in the automation tree for tests.

### Key Entities

- **Grid style**: cell size, major period, line widths (DIP), minor/major colors, fade thresholds.
- **Grid frame**: per-frame device-pixel parameters derived from the style, viewport and display scale.

## Success Criteria *(mandatory)*

- **SC-001**: Headless renders with the shader forced and the CPU path forced are pixel-identical (0 differing pixels).
- **SC-002**: At zoom 0.3 no minor line is drawn at more than ~25% of its base alpha, and no grid line is wider than its snapped width at any zoom.
- **SC-003**: The CPU path renders a 1080p frame in under 5 ms in software; the shader path costs O(1) CPU work per frame.
- **SC-004**: All existing tests stay green; affected canvas snapshots are regenerated and reviewed.

## Assumptions

- Zoom range stays 0.3–1 (hierarchical grid levels are not needed; recorded as a follow-up if the range grows).
- Grid colors: dark theme white at 8% (minor) and 18% (major) alpha; light theme black at the same alphas (research §7.1).
- The grid is decorative (WCAG 1.4.11 does not apply), but stays visible on both themes.
