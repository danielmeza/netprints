# 0004: Canvas overlays go through a pointer-anchored host, `CanvasPopup`

## Status

Accepted (2026-09-27).

## Context

Commit 4b8e580 fixed one report of the Get/Set chooser opening away from the mouse — dragging a
variable onto the canvas — by adding `GetSetChooserViewModel.ScreenPosition` (an `Avalonia.Point`, set by
`NodeGraphViewModel.Drop(MemberVariableViewModel, GraphPoint, Point)`) and binding `GetSetPopup`'s
`HorizontalOffset`/`VerticalOffset` to it in `GraphEditorView.axaml`. That fix was per-caller: the
other path that opens the same popup, `SuggestionListViewModel.SelectAsync`'s `case VariableSpecifier`
(picking a property/field from the member search, e.g. after dragging from an object pin), never set
`ScreenPosition`, so it opened at the origin instead. The owner reported this as the same bug
recurring under a different trigger.

Root cause: `Placement="Pointer"` reads Avalonia's own last-pointer-position tracking, which is
`internal` and is not kept current by drag-and-drop (`DragEventArgs`, not `PointerEventArgs`). The
per-caller fix threaded a screen coordinate through the view model (`GetSetChooserViewModel.ScreenPosition`,
the 3-arg `Drop` overload) to work around this, which put a view concern (screen pixels) in the view
model and had to be repeated for every new opening path and every new kind of popup.

## Decision

Positioning a canvas overlay is a view concern, solved once, centrally:

- **`CanvasPopup`** (`src/NetPrints.Editor/Controls/CanvasPopup.cs`), a sealed `Popup` subclass, is
  the only way a canvas overlay opens. It uses `Placement="Custom"` with a
  `CustomPopupPlacementCallback` that anchors the popup at the last pointer position tracked for its
  window, clamped so the popup's known size (`CustomPopupPlacement.PopupSize`) stays fully inside the
  window's `ClientSize` — not the screen, which can be much larger than the window. The anchor is
  frozen the moment the popup opens (recomputed only clamp-wise on further layout passes), so it does
  not chase the pointer while open.
- **`CanvasPointerTracker`** (same folder), one per `TopLevel`, listens for `PointerMoved`,
  `PointerPressed`, `DragDrop.DragOverEvent` and `DragDrop.DropEvent` at the top level and remembers
  the last position in that top level's coordinates. The drag events are exactly what Avalonia's own
  `Placement="Pointer"` tracking misses, and what caused both bug reports. `Invalidate()` clears the
  tracked position, for a keyboard-triggered open that had no pointer involvement.
- If no pointer position is tracked, `CanvasPopup` raises `FallbackPositionRequested`; the host view
  supplies a window-relative point (in `GraphEditorView`, the selected node's position, or the canvas
  center if nothing is selected). The same "selected node, else canvas center" rule also picks the
  graph-space position for a keyboard-triggered node search (Ctrl+Space).
- `CanvasPopup` also centralizes the behavior `NodeSearchView` and `GetSetChooserView` used to
  duplicate or omit inconsistently: Esc closes it (a `KeyDown` handler on `Child`, attached only while
  open); light-dismiss on an outside click (`IsLightDismissEnabled`, built into `Popup`); focus moves
  to the first focusable descendant of `Child` on open, and returns to whatever had focus before the
  popup opened; and only one `CanvasPopup` is open per `TopLevel` at a time (opening one closes any
  other already open for the same window).
- `SearchPopup` and `GetSetPopup` in `GraphEditorView.axaml` are `CanvasPopup`s. The screen-coordinate
  plumbing this replaces is gone: `GetSetChooserViewModel.ScreenPosition`, the 3-arg
  `NodeGraphViewModel.Drop(MemberVariableViewModel, GraphPoint, Point)` overload (now 2-arg, graph coordinates
  only), `NodeSearchView`'s own Escape handling, and `GetSetChooserView`'s pointer-exit-to-close
  handler.
- Every pointer-related canvas overlay, today and future, goes through `CanvasPopup`: the node, pin,
  canvas and connection context menus (P6, and P3a's context-menu contribution point); connection,
  node and pin tooltips with designer docs (P3a, ADR-0002); inline editors (event graph/entry rename,
  pin default value, variable rename); choosers like Get/Set, cast/conversion and override pickers.
  `NoRawPopupOutsideCanvasPopup` (`SourceHygieneTests`) enforces this: no `src/**/*.axaml` file may
  declare a raw `<Popup`.
- The command palette and go-to-anything (P3a) are explicitly out of scope: they get one shared
  centered host, not pointer-anchored, since "near the last click" is not a useful position for a
  global command search.
- Dialogs stay centralized in `EditorDialogs`, owner-centered: a modal dialog answers a question the
  user asked deliberately, not something that should open where their pointer happens to be.

## Consequences

- A new canvas overlay gets pointer anchoring, Esc, light-dismiss, initial focus and "only one open"
  for free by using `CanvasPopup`, instead of re-deriving screen coordinates or re-implementing
  lifecycle behavior per popup.
- View models keep only graph-space positions (`GraphPoint`); no view model holds an `Avalonia.Point`
  or any other screen/pixel concept.
- `CanvasPopup`'s anchor is frozen per-open from whichever `CanvasPointerTracker` position was current
  when it opened; a popup that must visibly track the pointer while open (none exists today) would
  need a different control.
- The hygiene test only catches a raw `<Popup` element in `.axaml`; a popup constructed purely in code
  (`new Popup()`) would not be caught. No such construction exists in `src/` today.
