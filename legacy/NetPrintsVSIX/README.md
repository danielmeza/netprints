# NetPrintsVSIX (pending P4 rework)

This legacy Visual Studio extension is **not part of `NetPrints.slnx`**, does **not build**, and is
**not built or tested in CI**. It still references the removed WPF editor (`NetPrintsEditor`) and is
kept only as a reference for the P4 phase of the modernization roadmap
(`.specify/memory/roadmap.md`), which has been deferred by the project owner.

When P4 resumes, the extension is rebuilt as an SDK-style project hosting the Avalonia editor, and
it gets its own Windows workflow chained after the main `CI` workflow (`workflow_run`), see
`specs/001-modernize-build/contracts/ci-workflow.md`.
