# 0010: Extension testing, coexistence rules and API compatibility gates

## Status

Accepted (2026-09-29, P2 spec `specs/004-catalog-cli/`; decision 3's opt-in clause amended by ADR-0017). Drafted as Proposed in
`docs/research/2026-09-29-extension-testing/` §7; this version keeps its decisions 1–3, replaces its decision 4
("decide the hazards in a separate ADR") with the decision itself, and adds the P2/P3 split of the author kit.

## Context

NetPrints loads extensions into per-extension, non-collectible `ExtensionLoadContext`s. Node kinds are namespaced
by manifest id, other contribution ids are first-wins with `NPX006`, duplicate extension ids are `NPX004`, and
`dependsOn` orders loading topologically. Compatibility is declared (`netprintsApi` major equal, minor ≤ host)
but not verified against binaries.

The research found two unpinned behaviours:

1. **Shared-prefix private dependency.** Sharing is decided by name prefix only (`NetPrints`, `Microsoft.Build`,
   `Microsoft.CodeAnalysis`, `CommunityToolkit.Mvvm`, `System.Reactive`, `DynamicData`, `Avalonia`, plus
   `Microsoft.Extensions.*.Abstractions` and the host's platform assemblies). A private dependency whose name
   merely starts with one of those prefixes (`NetPrintsUnreal.Runtime.dll`, a third-party `Avalonia.*` add-on)
   is deferred to the Default context, where it does not exist, and fails with `NPX007` or a
   `FileNotFoundException` at first use. NetPrintsUnreal plans assemblies named `NetPrints.Unreal.*`.
2. **`dependsOn` does not share types.** It only orders loading. Extension B cannot resolve extension A's
   assembly (`Private=false` leaves it out of B's folder), and if B ships a copy it gets a second type identity
   for A's types.

There is no multi-extension test suite, no kit for extension authors, and no API tracking. `NetPrints.Core` and
`NetPrints.Reflection` shipped in `v0.1.0`/`v0.1.1`; `NetPrints.Extensibility` and `NetPrints.Serialization` are
not packable yet (P3 publishes them).

## Decision

1. **Author kit.** `NetPrints.Extensibility.Testing` (framework-agnostic `ExtensionTest<TExtension>`, an
   `ExtensionHarness`, a twelve-check conformance suite with a "noisy neighbour" co-load, and an optional xUnit
   adapter) is the author-facing kit. `NetPrints.TestExtension` is tested only through it once it ships.
2. **Host multi-extension suite.** Fixture extensions (baseline pair with `dependsOn`, id squatter, duplicate
   id, private dependency v1/v2, host-assembly skew, shared-prefix private dependency, type provider/consumer,
   throws-mid-register, native dependency) and scenarios (id conflicts, load-order permutation invariance of
   the registry and the generated C#, dependency-version isolation, extension-on-extension types, documents
   across extension subsets, failure isolation, scale, reload caching) run in the Core test job on every PR.
   A nightly job co-loads published third-party extensions once any exist.
3. **API compatibility.**
   - `Microsoft.CodeAnalysis.PublicApiAnalyzers` tracks the public API of `NetPrints.Extensibility`,
     `NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Serialization` and `NetPrints.Catalog`. The API
     released in `v0.1.1` (Core, Reflection) is recorded as Shipped; everything else is Unshipped until its
     first release.
   - Unstable API carries `[Experimental]` with these ids and a link to the extensions guide's "API
     stability" section: `NPXE0001` host channel, `NPXE0002` extension settings, `NPXE0003` class and member
     emitters, `NPXE0004` catalog engine and profiles. The repository's own projects opt in through one
     property (`NetPrintsExperimentalOptIn`, appended to `NoWarn` in `Directory.Build.targets`); that line is
     the only `<NoWarn>` the hygiene gate allows, and it may hold only `NPXE` ids (ADR-0003 ledger row).
     Amended by ADR-0017: opt-in is per project and per id.
   - `PackageValidationBaselineVersion` is set when the extension API is first published (P3), not in P2:
     0.x packages may change, and the tracked API files already make every change visible in review. From then
     on a release check ties `ExtensionApi.Version` bumps to Shipped/Unshipped changes.
   - Before the first third-party release, `netprints-verify` (a static verifier of an extension's member
     references against given host versions) and a reusable author CI workflow.
4. **Coexistence rules (the two hazards).**
   - **Share by what the host provides, not by name.** An extension's dependency is taken from the host only
     when the host provides it: the name is one of the host application's trusted platform assemblies, or an
     assembly of that name is already loaded in the Default context (at the time the extension's context is created), or it belongs to a family the host
     resolves itself (`Microsoft.Build*`, via MSBuildLocator). Every other dependency resolves from the
     extension's own folder, whatever its name. The prefix list is removed. A copy of a host assembly inside an
     extension folder is ignored (the host's wins) with a logged warning.
   - **`dependsOn` shares types.** An extension's load context resolves assemblies from the load contexts of the
     extensions it depends on — in declared order, then transitively depth-first — before its own folder. The
     dependency's assemblies are loaded once, in the dependency's context, so both extensions see one type
     identity. Consumers reference providers with `Private=false`; a shipped copy is ignored with a warning.
     With diamonds, the first dependency in declared order wins.
   - No new diagnostic codes. A host-skew failure (an extension compiled against a newer host assembly) stays
     that extension's load failure (`NPX005` when it surfaces in `Register`); static detection is
     `netprints-verify`'s job.
   Both rules are pinned by the multi-extension suite before and after the loader change.
5. **What lands when.** P2 ships decisions 2, 3 (tracking and `[Experimental]`) and 4, plus the kit's internal
   core: an `ExtensionHarness` in the repository's test-support library, used by the suite. P3 ships the
   public kit (decision 1) together with the first packable `NetPrints.Extensibility`, and sets the validation
   baseline. `netprints-verify`, the author workflow, the nightly job and a `dotnet new netprints-extension`
   template come before the first NetPrintsUnreal release.

## Consequences

- NetPrintsUnreal can name its assemblies freely and split into cooperating extensions whose types flow
  between them with one identity.
- An extension can no longer rely on accidentally sharing a prefixed assembly the host does not actually ship;
  such an assembly now loads privately, which is what its author meant.
- Every public API change of the tracked libraries is a reviewed diff; experimental API needs an explicit
  opt-in outside the repository.
- The suite adds about a dozen small fixture projects and one test category to the Core job. Unload testing
  stays deferred until collectible contexts are adopted.
- The kit's public surface is designed once, in P3, against the harness that P2 already exercises.
