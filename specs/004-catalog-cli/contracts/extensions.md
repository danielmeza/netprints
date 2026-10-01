# Contract: extension coexistence, multi-extension suite and API tracking (P2)

Implements FR-038–FR-045 (ADR-0010). Sources: `src/NetPrints.Extensibility/Loading/`, `src/*/PublicAPI.*.txt`,
`Directory.Build.props`, `Directory.Build.targets`. Tests: `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/`,
`tests/NetPrints.Core.Tests/Architecture/`, fixtures in `tests/Fixtures/Extensions/`.

## 1. Loader rules

`ExtensionLoadContext.Load(AssemblyName name)`:

1. `IsHostProvided(name.Name)` → return `null` (the Default context supplies it). Provided =
   in `TRUSTED_PLATFORM_ASSEMBLIES`, or an assembly with that simple name was loaded in
   `AssemblyLoadContext.Default` when this context was created (a snapshot taken in the constructor, also used by the
   shadow check), or the name is `Microsoft.Build` or starts with `Microsoft.Build.` (MSBuildLocator family).
2. For each dependency context in `Dependencies` (declared `dependsOn` order), depth-first through their own
   dependencies, each context visited once: if it has loaded an assembly with that name, return it; else if its
   resolver resolves the name, load it into *that* context and return it.
3. Own resolver → `LoadFromAssemblyPath`; else `null`.

Version check: after the extension assembly loads, each of its referenced assemblies that a dependency provides
(the same lookup as step 2) is compared with the reference. A provided version lower than the referenced one fails
the extension with `NPX008` (`DependencyVersion`), the message naming the assembly and both versions; equal or higher
is accepted, and only the extension's direct references are checked. `NPX007` stays "assembly missing or could not be loaded".

Warnings (`NetPrints.Extensibility.Log`, `src/NetPrints.Extensibility/Log.cs`): `HostAssemblyShadowed(extensionId, assemblyName, path)` when an
extension folder contains a file whose name is a provided host assembly; `DependencyAssemblyShadowed(extensionId,
assemblyName, dependencyId)` when a folder contains a copy of an assembly a dependency provides. Checked once per
extension at load.

`ExtensionLoader` creates contexts in topological order and passes each its dependency contexts;
`ExtensionLoadContextCache` keys stay manifest paths.

## 2. Catalog profile contribution

`IExtensionBuilder AddCatalogProfile(CatalogProfile profile)` (`[Experimental("NPXE0004")]`);
`ExtensionRegistry.CatalogProfiles : IReadOnlyList<CatalogProfile>` in registry order. A profile id that is a
built-in id or already registered → `NPX006` contribution issue, first wins.

## 3. Fixtures

| Fixture | Kind | Folder / id | Content |
|---|---|---|---|
| Alpha | project | `tests/Fixtures/Extensions/Fx.Alpha` / `fx.alpha` | One contribution of every kind MX-T07 checks: node kind `fx.alpha/Ping` (with a CLR node type and a JSON resolver), a class emitter adding a `[System.ComponentModel.Description("fx.alpha")]` attribute, a settings section, a project profile `fx.alpha.profile`, a host channel factory `fx.alpha.channel`, a document type, a catalog profile `fx-alpha`, a project property |
| Beta | project | `Fx.Beta` / `fx.beta`, `dependsOn: [fx.alpha]` | Node kind `fx.beta/Pong`, a member emitter that adds a `[System.ComponentModel.Description("fx.beta")]` attribute to methods whose graph holds a `fx.alpha/Ping` node |
| LibV1 / LibV2 | project | `Fx.LibV1` / `fx.libv1`, `Fx.LibV2` / `fx.libv2` + `Fixture.SharedLib.V1/.V2` (assembly `Fixture.SharedLib` 1.0.0.0/2.0.0.0, `MinVerSkip`) | Each registers a node whose translator calls `SharedLib.Describe()` (V1: no argument; V2: an argument overload, plus the no-argument one) |
| PrefixedPrivate | project | `Fx.PrefixedPrivate` / `fx.private-prefix` + `NetPrintsFixture.Runtime` | Registers a node whose translator uses `NetPrintsFixture.Runtime.Helper` |
| TypesProvider / TypesConsumer | project | `Fx.TypesProvider` (`fx.types-provider`), `Fx.TypesConsumer` (`dependsOn` provider, `Private=false` reference) | Consumer's node pin type and emitter use `ProviderType`; exposes `typeof(ProviderType)` for identity checks |
| Diamond | project | `Fx.Diamond` / `fx.diamond`, `dependsOn: [fx.libv1, fx.libv2]`, references `Fixture.SharedLib` with `Private=false` | Registers a node whose translator calls `SharedLib.Describe()`; MX-T05 expects v1 (first in `dependsOn` order) |
| Native | project | `Fx.Native` / `fx.native` | Private `SkiaSharp.NativeAssets.Linux.NoDependencies`; `Register` calls `sk_version_get_milestone` via `DllImport("libSkiaSharp")` |
| Catalog | project | `Fx.Catalog` / `fx.catalog` | Contributes the fixture catalog (`CatalogLoader.LoadFile`) and the `fixture-flags` profile (used by CT-T13, CT-T15) |
| Squatter | Roslyn | `fx.squatter` | Claims alpha's profile, host channel, settings and catalog-profile ids and a `fx.alpha/…` kind |
| Duplicates | Roslyn | two folders with id `fx.dup` | |
| ThrowsMidway | Roslyn | `fx.throws` | Adds two kinds, then throws in `Register` |
| HostSkew | Roslyn | `fx.hostskew` | Compiled against an in-test reference assembly named `NetPrints.Core` with an extra public method, which `Register` calls |
| Scale | Roslyn | `fx.scale.00`…`fx.scale.49` | One node kind each, random `dependsOn` chains generated from a fixed seed |

Shared settings: `tests/Fixtures/Extensions/Directory.Build.props` (TestExtension's properties; output
`bin/$(Configuration)/extensions/<id>/`). `tests/NetPrints.Core.Tests` references each project with
`ReferenceOutputAssembly="false"`; `FixtureExtensions.CopyTo(root, id)` copies a built fixture.

## 4. `ExtensionHarness` (`tests/NetPrints.Testing/Extensions/ExtensionHarness.cs`)

`static Task<ExtensionHarness> CreateAsync(IReadOnlyList<string> folders, IReadOnlyList<INetPrintsExtension> inProcess,
CancellationToken)`; `ExtensionRegistry Registry`; `string Translate(ClassGraph)`; `Task<byte[]> RoundTripAsync(byte[] document,
CancellationToken)`; `Task<IReadOnlyList<GeneratedFileResult>> GenerateAsync(string projectDirectory, CancellationToken)`;
`IAsyncDisposable`. Internal test support; not packaged (P3 lifts it into the public kit).

## 5. API tracking and `[Experimental]`

- `Microsoft.CodeAnalysis.PublicApiAnalyzers` via `src/Directory.Build.props` for projects that set
  `<NetPrintsTrackPublicApi>true</NetPrintsTrackPublicApi>` (Extensibility, Core, Reflection, Serialization, Catalog),
  adding `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` as `AdditionalFiles`.
- Ids in `src/NetPrints.Core/ExperimentalApiIds.cs` (public, used by Core and Extensibility): `NPXE0001` host channel,
  `NPXE0002` settings, `NPXE0003` emitters, `NPXE0004` catalog engine/profiles; `UrlFormat`
  `https://danielmeza.github.io/netprints/guide/extensions#api-stability`. The shared
  `src/NetPrints.Catalog/Engine/ExperimentalApis.cs` re-declares `NPXE0004` internally because the generator, which
  compiles it, cannot reference Core (the one allowed duplicate, allowlisted in the literal gate).
- The site serves `docs/` at its root (`routeBasePath: '/'`), so `UrlFormat` has no `/docs/` segment. Every public symbol
  whose signature mentions an `[Experimental]` type carries the same id, and AP-T02 checks both.
- Opt-in is per project and per id (ADR-0017): a project that uses an experimental API adds
  `<NetPrintsExperimentalOptIn Include="NPXE0003" />` items for only the ids it uses; `Directory.Build.targets` turns
  the items into `NoWarn` on one line, the only allowed `<NoWarn>`. No repo-wide opt-in, nothing inherited. A defining
  assembly opts in only if the build shows the diagnostic for its own usage. External authors use
  `<NoWarn>$(NoWarn);NPXE000n</NoWarn>` or a narrow `#pragma`.

## 6. Test obligations

| Id | Test (file) | Case |
|---|---|---|
| MX-T01 | existing `ExtensionLoaderTests`, `ContributionTests` + `MultiExtension/CharacterizationTests` | Before the loader change: host types identity (EX-T01), NPX001–NPX007, ordering — all green and unchanged after |
| MX-T02 | `MultiExtension/SharedAssemblyRuleTests` | `fx.private-prefix` loads, its node translates using `NetPrintsFixture.Runtime` (red before T-loader) |
| MX-T03 | `MultiExtension/SharedAssemblyRuleTests` | A copy of `NetPrints.Core.dll` in a fixture folder is ignored; `HostAssemblyShadowed` logged |
| MX-T04 | `MultiExtension/DependencyTypeSharingTests` | Consumer's `typeof(ProviderType)` equals provider's; consumer emitter output uses the type (red before T-loader); provider failing → consumer NPX003 |
| MX-T05 | `MultiExtension/DependencyTypeSharingTests` | Diamond: two providers of `Fixture.SharedLib` → first in `dependsOn` order wins, stable across runs |
| MX-T06 | `MultiExtension/VersionIsolationTests` | LibV1 + LibV2 load together; each translator calls its own version; two distinct `Assembly` instances with versions 1.0.0.0 and 2.0.0.0 |
| MX-T07 | `MultiExtension/IdConflictTests` | Squatter vs alpha across kinds, profiles, host channels, settings, document types, CLR node types, JSON resolvers, catalog profiles; project properties dedupe; loser named in `Issues`; alpha's behaviour unchanged. JSON resolvers: a document type is answered by its owner extension's resolvers first, and another extension's resolver that also claims it is reported (NPX006, against that extension) but never answers for it |
| MX-T08 | `MultiExtension/IdConflictTests` | Duplicate ids → NPX004; in each folder order the first folder wins, the loser gets NPX004, and the registry content is the same |
| MX-T09 | `MultiExtension/LoadOrderPermutationTests` | Alpha, Beta, LibV1, PrefixedPrivate: all 24 discovery orders → same `Loaded` order, registry order, byte-identical generated C# of a graph using all four |
| MX-T10 | `MultiExtension/FailureIsolationTests` | ThrowsMidway: none of its kinds registered; neighbours intact; NPX005 |
| MX-T11 | `MultiExtension/FailureIsolationTests` | HostSkew → NPX005 (`MissingMethodException`), others load |
| MX-T12 | `MultiExtension/DocumentSubsetTests` | Graph with alpha and beta nodes reopened with {α,β}, {α}, {} → unknown nodes preserved, re-save byte-identical; `generate` with {α} reports NPT003. Beta needs alpha, so {β} equals {}: the independent case is a graph with alpha and libv1 nodes reopened with {libv1} and {α}, and `generate` with {libv1} reports NPT003 naming `fx.alpha/Ping` |
| MX-T13 | `MultiExtension/FailureIsolationTests` | Each NPX001–NPX007 fixture with alpha + beta: others load, registry usable |
| MX-T14 | `MultiExtension/ScaleTests` | 50 Roslyn fixtures, each touching a private dependency assembly (so the `dependsOn` chains are walked), load in < 10 s, same order on two runs |
| MX-T15 | `MultiExtension/ReloadTests` | `ExtensionHost` reload reuses contexts; `AssemblyLoadContext.All` count stable over 3 reloads; a cached context whose dependency contexts changed is replaced, so a consumer sees the new provider's type |
| MX-T16 | `MultiExtension/NativeDependencyTests` | `fx.native` loads and its native call returns a milestone > 0 (Linux; explicit skip reason elsewhere) |
| MX-T17 | `MultiExtension/DependencyVersionTests` | Consumer built against `Fixture.SharedLib` 2.0 with a provider shipping 1.0 → NPX008 naming both versions, consumer not registered, provider loaded; built against 1.0 with 1.0 or 2.0 provided → loads and translates |
| AP-T01 | `Architecture/PublicApiTrackingTests` | Every tracked project references the analyzer and has both files starting `#nullable enable`; an in-test compilation with the analyzer and an undeclared public member reports RS0016 |
| AP-T02 | `Architecture/ExperimentalApiTests` | For each `NPXE` id, an in-test external compilation using a marked API without opt-in reports that id as an error; with the id in `NoWarn` it compiles; `UrlFormat` maps to an existing `docs/**/*.md` page with an `## API stability` heading; every public symbol in Core and Extensibility whose signature mentions an `[Experimental]` type carries that id |
| AP-T03 | `Core/SourceHygieneTests` | The only `<NoWarn>` in the repository is `Directory.Build.targets`' opt-in line; every `NetPrintsExperimentalOptIn` item is an id declared in `ExperimentalApiIds` (unknown or stale ids fail); no `#pragma warning disable NPXE*`; no `.editorconfig`/globalconfig severity entries for `NPXE` ids (ADR-0017) |
