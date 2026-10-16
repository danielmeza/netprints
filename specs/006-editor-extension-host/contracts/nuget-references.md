# Contract: NuGet packages in the References dialog

FR-089–FR-093, SC-012, ADR-0042 (planned, sub-phase H), ADR-0015, ADR-0033; research R32–R35. Data shapes in
[data-model.md](../data-model.md) §12. Messages are NPW codes ([analyzer-diagnostics.md](analyzer-diagnostics.md) §8).
P3c's extension manager and samples gallery reuse the service of §1 and add their own operations; nothing here is
part of the published extension API.

## 1. The package service (`NetPrints.Packages`)

One service per editor process, `IPackageService`, registered as a singleton (ADR-0031). Every operation is async,
takes a `CancellationToken` and never throws for a source problem: it returns the result with the source's message.

| Operation | Input | Result |
|---|---|---|
| Sources | the project folder | the enabled sources of the `nuget.config` hierarchy, in file order (name, URL, declaring file) |
| Search | source or all, query, prerelease, skip, take | rows: id, newest version, description, authors, offline flag; per-source messages |
| Versions | source or all, id, prerelease | versions newest first, each with its compatibility with every target framework of the project |
| Metadata | source, id, version | description, authors, license, project URL, dependency groups, deprecation (reasons, alternative), vulnerabilities (severity, advisory URL) |
| Installed | a project snapshot | direct references (declared and resolved version, editable or the NPW013 reason) and transitive packages (read-only), from `obj/project.assets.json` |
| Removal impact | a project snapshot, an id | the assemblies only that package brings and the nodes that use them (§5) |

- **Sources and credentials**: `nuget.config` as `dotnet restore` reads it from the project folder; credentials only
  from `packageSourceCredentials` and the NuGet credential plugins, run non-interactively. NetPrints never prompts for,
  stores or logs a credential. A source that needs credentials no provider supplied returns NPW012 with the hint "run
  `dotnet restore --interactive` once".
- **Logs**: a source appears as its name and its URL without user info, query or fragment.
- **Cache**: in memory, 5 minutes per source, operation and arguments (injected `TimeProvider`); Refresh bypasses it.
- **Offline**: a source that cannot be reached returns NPW011; search and versions then come from the global packages
  folder, marked offline. Installed packages never need the network.

## 2. Package reference edits

New `ProjectEdit` records in Core, experimental under NPXE0018 (they land after the publish gate), applied by
`IProjectSystem.ApplyAsync` with formatting, comments and order preserved.

| Edit | Plain project | Central Package Management | With "Use VersionOverride" (CPM) |
|---|---|---|---|
| `AddPackageReference(id, version, false)` | `<PackageReference Include="id" Version="v" />` in the project | `<PackageVersion Include="id" Version="v" />` in `Directory.Packages.props`; `<PackageReference Include="id" />` in the project | — |
| `AddPackageReference(id, version, true)` | refused (no CPM) | — | `<PackageReference Include="id" VersionOverride="v" />`; props untouched |
| `SetPackageVersion(id, version, …)` | the item's `Version`, in its existing form (attribute or element) | the `PackageVersion` item | the item's `VersionOverride` |
| `RemovePackageReference(id)` | the item | the project item only; `PackageVersion` stays | the item |

- A range is shown as written and replaced only by an explicit version.
- An item with its own `Condition`, or in a conditioned item group, is edited in place when it is the only item for the
  id. Several conditioned items for one id, or a version that is an MSBuild property expression, make the reference
  read-only (NPW013, with the reason); no edit touches it.
- `NetPrints.Sdk` can be updated, not removed (`RemovePackageReference` for it is refused with the reason).
- An unknown id or an unparsable version is refused before any file is written.

## 3. Applying a change

1. Only one change runs per project; the tab disables its commands meanwhile.
2. Record the exact bytes of every file the change writes (the project file, and `Directory.Packages.props` under CPM).
3. Write them atomically, then reload the project (`LoadAsync`), which restores.
4. **Success**: the session takes the new snapshot, refreshes the reflection provider and re-validates the open graphs.
5. **Failure or cancellation** (restore failed, the load threw, or the token was cancelled): write every recorded file
   back with its original bytes (the rollback ignores the token); report NPW010 with the restore's first error lines;
   keep the session's snapshot and reflection provider. `obj/` is left to the next restore.

## 4. The Packages tab

- A tab of the References dialog with sub-tabs Installed, Browse and Updates; a source drop-down (All sources, then
  each enabled source); a Prerelease switch; Refresh; an offline banner when any source returned NPW011.
- Installed: direct references with declared and resolved versions; transitive packages in a collapsed read-only group.
- Browse: a search box and the result rows. Updates: direct references with a newer version in the selected sources.
- Details: id, description, authors, license and project URL as plain text; the version picker (incompatible versions
  hidden unless "Show incompatible" is on); deprecated and vulnerable badges with the reason; Add, Update, Remove and,
  under CPM, "Use VersionOverride"; a read-only reference shows its NPW013 reason instead of the actions.
- Remove shows the removal impact (§5) first; Cancel changes nothing.
- No remote image is loaded (no package icons in P3).
- Automation ids under `References.Packages.*`; text from resources (L1).

## 5. Removal impact

The assemblies only the package brings are its compile assemblies and those of its dependencies, minus those any other
direct reference also brings (from the assets file's dependency graph). The nodes that use them are those whose
referenced member (`IMemberReferencingNode`) has a declaring type in one of those assemblies. The confirmation lists
each (graph, node, member). Proceeding removes the package; those nodes load as unresolved nodes and saving keeps them.

## 6. Tests that pin this contract

- Sources: a fixture `nuget.config` hierarchy (project folder, parent, user) gives the expected sources in order; a
  credential in `packageSourceCredentials` never appears in a log line or in NetPrints state; URLs in logs are redacted.
- Search, versions and metadata against a local folder feed packed from `tests/Fixtures/Packages/` once per run; the
  5-minute cache with a fake `TimeProvider`; Refresh bypasses it; an unreachable source gives NPW011 and offline
  results from a fixture global packages folder.
- Deprecation and vulnerability mapping on NuGet's own metadata types (no network).
- Edits: each row of §2 on the plain and CPM fixtures; the plain and CPM add, update and remove compared byte for byte
  with `dotnet package add --no-restore` and `dotnet package remove` on a second copy (SC-012); the `VersionOverride`
  golden; NPW013 for a property version and for several conditioned items; `NetPrints.Sdk` cannot be removed.
- Apply: success refreshes the reflection provider and re-validates; a forced restore failure (a version the feed
  lacks) and a cancellation during the restore each leave the project and props files byte-identical and report
  NPW010 (SC-012).
- Removal impact: a fixture graph whose node calls a method of the fixture package is listed; a package another
  reference also brings lists nothing.
- View models: the three sub-tabs, the source filter, prerelease, compatibility filter, badges, offline banner, read-only
  rows, the remove confirmation (fake `IPackageService`).
- Headless UI: the tab's controls and automation ids. Desktop E2E: `ManagePackages` (add from a local folder feed,
  update, remove with the impact list, a forced restore failure rolled back).
