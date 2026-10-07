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
- [0010: Extension testing, coexistence rules and API compatibility gates](0010-extension-testing-and-coexistence.md)
- [0011: The sourcemeta `jsonschema` CLI validates `schemas/netpc.v1.schema.json`](0011-jsonschema-cli-for-schema-validation.md)
- [0012: One catalog engine over Roslyn symbols, shared as source with the generator](0012-catalog-engine-and-schema.md)
- [0013: Catalog profiles are declarative data](0013-declarative-catalog-profiles.md)
- [0014: `NetPrints.Annotations` is a generator-only package with embedded catalogs](0014-annotations-generator-only-package.md)
- [0015: The `netprints` CLI: command surface and exit-code contract](0015-cli-commands-and-exit-codes.md)
- [0016: Git integration for graph files: text conversion, merge driver and SchemaStore](0016-git-integration-for-graphs.md)
- [0017: Experimental API opt-in is per project and per id](0017-experimental-api-opt-in.md)
- [0018: The editor shell docks with Dock.Avalonia, behind a shell seam](0018-editor-shell-docking.md)
- [0019: CI runs one job per test project, and a Windows job checks the CLI](0019-ci-test-matrix-and-windows-cli-leg.md)
- [0020: One contribution registry drives the editor's commands, panels and menus](0020-editor-contribution-registry.md)
- [0021: Editor icons come from one vector family, Fluent UI System Icons, named by icon id](0021-editor-icon-family-and-icon-ids.md)
- [0022: A pre-release editor build writes the latest released SDK version](0022-sdk-version-written-by-pre-release-builds.md)
- [0023: The editor's base theme and controls are Semi.Avalonia and Ursa.Avalonia](0023-semi-ursa-base-theme.md)
