---
description: "Task list for P0.1 — Grid rendering"
---

# Tasks: Grid Rendering

**Input**: Design documents from `/specs/002-grid-rendering/`

**Prerequisites**: plan.md, spec.md, research.md

**Tests**: REQUIRED (constitution V). Unit tests for the shared definition, headless snapshot tests
for both render paths, regenerated canvas baselines; full suite and the E2E suite once.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1…US3 from spec.md

## Phase 1: Setup

- [x] T001 Add `Avalonia.Skia` to `src/NetPrints.Editor/NetPrints.Editor.csproj` (central version 12.1.3)

## Phase 2: Foundational (shared definition)

- [x] T002 [US1] `GridStyle` and `GridFrame.Compute` in `src/NetPrints.Editor/Graph/GridStyle.cs` (FR-001–FR-003, D4)
- [x] T003 [P] [US1] Unit tests in `tests/NetPrints.Editor.Tests/Graph/GridFrameTests.cs`: width snapping at scales 1/1.25/1.5/2, phase reduction at ±1e6 pan, fade endpoints and midpoint, opacity, major index preserved by the phase

## Phase 3: User Story 1 + 2 — crisp grid, identical paths (P1)

- [x] T004 [US1] CPU path and SkSL shader path in `src/NetPrints.Editor/Graph/GridRenderer.cs` (D2, D3, D5)
- [x] T005 [US2] `GridBackground` control, draw operation and mode selection (`Mode`, `NETPRINTS_GRID`, GPU check, compile failure logged once) in `src/NetPrints.Editor/Graph/GridBackground.cs` (FR-006, FR-007)
- [x] T006 [P] [US2] Mode resolution unit tests in `tests/NetPrints.Editor.Tests/Graph/GridFrameTests.cs`
- [x] T007 [US1] Place the grid behind a transparent editor in `GraphEditorView.axaml`; sync viewport in `GraphEditorView.axaml.cs`; remove the DrawingBrush (FR-005, FR-008)
- [x] T008 [US2] Expose `GridRenderPath` in `Hosting/Automation/AutomationTree.cs` with a new `AutomationIds.GraphGrid` (FR-009)
- [x] T009 [US2] Headless tests in `tests/NetPrints.Editor.UITests/Graph/GridRenderTests.cs`: shader forced vs CPU forced are pixel-identical and match baseline `grid-background`; Auto uses the CPU path headless; the editor canvas reports its path

## Phase 4: User Story 3 — theming (P2)

- [x] T010 [US3] `GraphGrid.BackgroundColor` / `GraphGrid.MinorColor` / `GraphGrid.MajorColor` in theme dictionaries (`EditorStyles.axaml`) bound with `DynamicResource` (FR-004)

## Phase 5: Polish

- [x] T011 Regenerate affected canvas baselines (`NETPRINTS_UPDATE_SNAPSHOTS=1`) and review them visually
- [x] T012 Run the full suite (`dotnet test --solution NetPrints.slnx`) and the E2E suite once on a private Xvfb (≥ :140; `NETPRINTS_E2E_DISPLAY_START` added to the E2E fixture for this)
- [ ] T013 PR "P0.1: Grid rendering" against `danielmeza/netprints:master`; CI green

## Dependencies & Execution Order

T001 → T002 → T004 → T005 → T007 → T008 → T009 → T011 → T012 → T013; T003/T006 after T002/T005;
T010 with T007.
