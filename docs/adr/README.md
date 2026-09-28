# Architecture Decision Records

Short records of decisions that are expensive to reverse or easy to re-litigate without a written
rationale. One file per decision, numbered sequentially (`0001-*.md`, `0002-*.md`, ...), never
renumbered or deleted once merged — a superseded decision gets a new ADR that says so and links
back.

Use the lightweight format: Status, Context, Decision, Consequences. Keep it to what a future
contributor needs to not redo the argument; put implementation detail in `specs/` or code comments
instead.

## Index

- [0001: Repository layout](0001-repo-layout.md)
- [0002: Designer-authored comments become a P3b feature, not a P1 addition](0002-designer-comments-scheduled-p3b.md)
- [0003: Analyzer promotion and string literal discipline](0003-analyzer-promotion-and-string-literal-discipline.md)
- [0004: Canvas overlays go through a pointer-anchored host, `CanvasPopup`](0004-canvas-overlays-pointer-anchored-host.md)
- 0005: reserved for the release ADR (T121)
- [0006: Parallel desktop E2E via a worker pool, one class per scenario](0006-parallel-desktop-e2e.md)
