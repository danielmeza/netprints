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
- [0005: Release, packaging and docs stack](0005-release-and-docs-stack.md)
- [0006: Parallel desktop E2E via a worker pool, one class per scenario](0006-parallel-desktop-e2e.md)
- [0007: XAML practices for the Avalonia editor](0007-xaml-practices.md)
- [0008: CI code coverage uses static instrumentation only](0008-static-only-code-coverage.md)
- [0009: Split `NetPrints.Generation` out of `NetPrints.Generator`](0009-generation-library-split.md)
