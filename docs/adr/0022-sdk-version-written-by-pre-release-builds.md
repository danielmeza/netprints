# 0022: A pre-release editor build writes the latest released SDK version

## Status

Accepted (2026-10-07, P3a Review E finding R2 and R11).

## Context

- A new project and a copied sample reference `NetPrints.Sdk` at a version the editor chooses. The editor used its own
  version, which MinVer sets to `0.2.1-alpha.0.N` between releases. No feed has that version, so the restore fails and
  the project opened with no classes and no message.
- The bundled sample pinned `NetPrints.Sdk` 0.1.0 for every copy, whatever the editor's version.
- NuGet treats a plain version as a minimum, so an older published release restores and builds.

## Decision

- A release build (no pre-release suffix, or equal to the latest tag) writes its own version.
- A pre-release build writes the latest released version: the build stamps the nearest `v*` git tag into the assembly
  (`NetPrints.LatestRelease` metadata, target `StampLatestRelease` in `NetPrints.Editor.csproj`). With no git or no tag
  it writes its own version.
- New project and the sample copy use the same choice (`EditorSdkVersion.Resolve`); the copy rewrites only the
  `NetPrints.Sdk` reference version in the copied project file, never the bundled one.
- A failed restore or a project with graph files but no loaded graphs is shown in the "Project loaded with issues"
  dialog, naming the package and version and what to do.

## Consequences

- A dev build creates projects that restore online; their generator is the latest release's, not the working tree's.
  Contributors who need the working tree's SDK use the in-repo layout, as the Desktop E2E does.
- The stamped tag is the last tag reachable at build time: building without tags (a shallow clone) falls back to the
  editor's own version, and the restore message explains the failure.
- Offline use still fails to restore until the package is cached; the user now sees why.
