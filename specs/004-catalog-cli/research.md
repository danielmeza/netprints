# Research: Catalog Tooling and Spectre CLI (P2)

Date: 2026-09-29. Base: `master` at `7fba032` (= tag `v0.1.1`). Each entry: **Decision**, **Rationale**,
**Alternatives**. Entries that decide something expensive to reverse are recorded as ADRs (named).

## 1. Scope sources

| Source | What it contributes | Where it lands |
|---|---|---|
| Roadmap P2 (catalog engine, schema, `ICatalogFilter` profiles) | Catalog engine, schema v1, profiles | FR-012–FR-018, R6–R10, ADR-0012, ADR-0013 |
| Roadmap P2 (tool flavor, `netprints.catalog.json` + CLI overrides) | `netprints catalog` | FR-019–FR-024, R9, contracts/catalog.md |
| Roadmap P2 (annotations flavor, netstandard2.0 generator) | `NetPrints.Annotations` | FR-025–FR-031, R11–R12, ADR-0014 |
| Roadmap P2 (cross-flavor snapshot tests) | Parity tests | FR-029, SC-003, R13 |
| Roadmap P2 (Spectre CLI: `build`, `generate`, `run`, `catalog`, `migrate`) | New CLI | FR-001–FR-011, R2–R5, ADR-0015 |
| Roadmap P2 graph-format follow-ups (`format --check`, `regen --check`, `git-install`, `textconv`, `merge`, SchemaStore) | Graph tooling | FR-008, FR-032–FR-037, R14–R18, ADR-0016 |
| Roadmap P2 extension testing (multi-extension suite) | Fixture extensions + scenarios | FR-041–FR-043, R20 |
| Roadmap P2 API compatibility tracking | PublicApiAnalyzers, `[Experimental]` | FR-044–FR-045, R22–R23, ADR-0010 §3 |
| Roadmap P2 ADR on the two hazards | Loader rules | FR-038–FR-040, R19, ADR-0010 §4 |
| Roadmap P3 "author conformance kit (may start in P2)" | Internal harness only | R21, ADR-0010 §5 |
| `specs/003-core-refactor/implementation-notes.md`, "Deferred from the PR #6 review" | CLI `--version`/`--help` exit 0, CI smoke flipped to 0; extension testing → P2/P3 | FR-002, T-tasks in sub-phase C, R3 |
| `specs/003-core-refactor/spec.md` Assumptions (P1 follow-ups) | merge driver + `git-install`, `textconv`, `format --check`, `regen --check`, SchemaStore; in-editor visual diff | US4; visual diff deferred to P6 (R27) |
| P1 contract extension-points.md: `IProjectProfile.CatalogProfileId` "used by P2 catalog tooling" | Default catalog profile of a project | FR-024, R8 |
| P1 research §"Entry point": "P2's `netprints generate` calls the same `GraphCodeGenerator`" | In-process generate | FR-007, R4 |
| Plan page (NetPrints Unreal Roadmap artifact, "Catalog tooling") | Flavors, config example, generator limits | R6–R12 |
| `docs/research/2026-09-25-graph-format/` §5.3–§5.4, §7 | Merge driver algorithm, textconv, CI `regen --check`, SchemaStore `fileMatch` | R14–R17 |
| `docs/research/2026-09-29-extension-testing/` §1, §6, §7 | Hazards, fixtures, scenarios, API layers, phasing, draft ADR 0010 | R19–R23, ADR-0010 |

## 2. CLI framework and composition (ADR-0015)

**Decision**: `NetPrints.Cli` becomes a `Spectre.Console.Cli` `CommandApp` (0.57.2; `Spectre.Console.Testing`
0.57.2 for tests). Commands derive from `AsyncCommand<TSettings>`; settings classes carry `[CommandArgument]`
and `[CommandOption]` with descriptions and `Validate()` overrides. Services come from
`Microsoft.Extensions.DependencyInjection` (10.0.12) through a small `ITypeRegistrar`/`ITypeResolver` pair
(`src/NetPrints.Cli/Infrastructure/`), so tests replace `IProjectSystem`, `IProcessRunner`, `IAnsiConsole` and the
file system root without MSBuild. `Program.Main` only builds the service collection and calls
`CliApplication.RunAsync(args, services, cancellationToken)`, which is what tests call. `config.SetApplicationName("netprints")`,
`config.SetApplicationVersion("NetPrints.Cli " + informationalVersion)`, `config.PropagateExceptions()` off and
`config.SetExceptionHandler(...)` mapping parse and validation failures to exit 2 and anything else to 4
(Spectre's own default for errors is -1). `config.ValidateExamples()` runs in a test. Cancellation: `Program.Main`
links a `CancellationTokenSource` to `Console.CancelKeyPress` and passes its token to `CommandApp.RunAsync(args,
cancellationToken)`, which hands it to `AsyncCommand<TSettings>.ExecuteAsync(context, settings, cancellationToken)`
(verified in the 0.57.2 API). Tests use `Spectre.Console.Testing.TestConsole` as the `IAnsiConsole`.
Microsoft.Build types stay behind the existing `MsBuildRegistration.EnsureRegistered()` + `NoInlining` split:
every project command calls one `ProjectCommandBase.EnsureSdkAsync()` first (exit 3 when no SDK).

**Rationale**: The constitution names Spectre.Console.Cli as the CLI stack. DI keeps commands testable the
way `CliBuildTests` already tests `BuildAsync` with fakes. `CommandLineParser` becomes dead and is removed from
`Directory.Packages.props` (constitution VIII).

**Alternatives**: `System.CommandLine` (not the constitution's choice; still preview-heavy); keeping
CommandLineParser with verbs (does not fix help/version exit codes cleanly, no per-command help layout).

## 3. Exit-code contract (ADR-0015)

**Decision**: 0 success; 1 operation failed (build or generation errors, check differences, merge conflict,
restore failure, unreadable input files); 2 invalid usage (parse/validation errors, a path argument that does
not exist, a missing or ambiguous project, P1 flags, `git-install` outside a work tree); 3 no compatible .NET SDK; 4 internal error (unhandled exception;
the exception goes to stderr, with the stack trace only under `--verbose`). `run` returns the child's code
after a successful build. Codes live in one `ExitCodes` class (`src/NetPrints.Cli/ExitCodes.cs`), the guide
documents them, and `CliExitCodeTests` covers each. CI's "CLI smoke" step asserts 0 for `--version` and
`--help` (P1 deferral closed).

**Rationale**: Keeps P1's 0/1/2/3 meanings (documented since 6dd89f2) — except "project not found", which
moves from 1 to 2 because it is a bad argument, not a failed build — and adds a distinct internal-error code
so CI can tell a crash from a failed build. Git needs non-zero for a conflicted merge; 1 fits "operation
failed".

**Alternatives**: sysexits.h codes (64/65/70…): unfamiliar to .NET users and would break the P1 contract.

## 4. `generate` / `regen` in process

**Decision**: `generate [project] [--check]` (alias `regen`) loads the project through the P1
`MsBuildProjectSystem.LoadAsync` (graphs, extension folders, profile, root namespace), builds a
`GenerateRequest` with a new `GenerateRequestFactory.FromSnapshot(ProjectSnapshot)` in `NetPrints.Generation`
(output path = graph path with `.json` replaced by `.g.cs`, the SDK targets' rule), loads extensions with
`GraphCodeGenerator.LoadExtensions` and generates in process. `GraphCodeGenerator.GenerateAsync` gains a
`GenerationMode` (`Write` default, `Check`) and `GeneratedFileResult` gains `bool UpToDate`; in `Check` mode
nothing is written. A `--graph <file>` filter limits the run to named graphs. The SDK's build keeps execing the
internal `NetPrints.Generator` host (ADR-0009): one library, two hosts.

**Rationale**: Same code path as the build (FR-007), no compile needed, and U1's loop ("`netprints generate`
writes into `Script/`") works without building UnrealSharp's project.

**Alternatives**: shelling out to `dotnet build -t:NetPrintsGenerate` (slower, needs a restore, harder to
report stale files); replacing the Generator host by the CLI inside the SDK (the SDK must not depend on a
globally installed tool, ADR-0009).

## 5. `migrate`

**Decision**: `migrate [paths]` finds graphs (files, directories searched recursively, or a project's graphs),
reads each through the `IDocumentFormat` that `DocumentFormatRegistry` resolves (constitution VII; a newer version
surfaces as P1's `DocumentVersionException`), and prints `<file>: schema 1 (current)`.
With `DocumentMigrator.CurrentSchemaVersion == 1` and no registered migrations it prints "No migrations are
available; N graph(s) are at schema version 1." and exits 0. A newer or missing version is an error (exit 1).
The command already routes through `DocumentMigrator` so the first v2 migration only adds the migration and a
write step.

**Rationale**: Roadmap: the schema stays v1 until the version cut; the command surface ships now so scripts
and docs don't change later.

## 6. Catalog engine architecture (ADR-0012)

**Decision**: One engine over Roslyn symbols (`IAssemblySymbol`, `INamedTypeSymbol`, `IMethodSymbol`, …) in
`src/NetPrints.Catalog` (net10.0, packable). The parts both flavors run — the model (`Model/`), the engine
(`Engine/`: `CatalogBuilder`, `ICatalogFilter`, `CatalogProfile` and its compiled filter, built-in profiles,
documentation-id helpers, XML-doc summary normalizer, glob matcher, diagnostics ids) and the canonical writer
(`Json/CanonicalCatalogWriter.cs`) and C# emitters (`Emit/`) — are **shared source**: the netstandard2.0
generator compiles the same files through `<Compile Include="..\NetPrints.Catalog\Model\**\*.cs" Link=... />`.
Shared files use only APIs available on both targets, a `Guard` helper instead of `ArgumentNullException.ThrowIfNull`
(CA1510 would otherwise fire on net10.0 and the API is missing on netstandard2.0), and one
`Polyfills.cs` (`#if NETSTANDARD2_0`: `IsExternalInit`, nullable flow attributes). The writer is
hand-written (a `StringBuilder` with the canonical rules), so neither target needs System.Text.Json at write
time; net10.0-only parts (`Json/CatalogReader.cs` on STJ source generation, `Runtime/`, `Config/`, `Sources/`)
are excluded from the link. The polyfill file also declares `ExperimentalAttribute` and the required-member
attributes for netstandard2.0, and `Engine/ExperimentalApis.cs` holds the `NPXE0004` id so linked files compile
without Core. The link is added in the first catalog batch, so the netstandard2.0 build guards every shared file
from the start; documentation arrives as XML text (the generator's RS1035 forbids file access).

**Rationale**: Constitution IV allows netstandard2.0 only for the generator itself, so `NetPrints.Catalog`
cannot multi-target; sharing source keeps one implementation, which is what makes the cross-flavor snapshot
byte-identical by construction. Roslyn symbols are what both flavors naturally have (the tool builds a
compilation from metadata references; the generator already has one), and what `ReflectionProvider` uses, so
parity with the live provider is testable. A dependency-free writer avoids loading System.Text.Json inside
compiler hosts.

**Alternatives**: `System.Reflection.Metadata` for the tool and Roslyn for the generator (two engines, parity
by testing only); multi-targeting `NetPrints.Catalog` (violates IV); a separate `NetPrints.Catalog.Tool`
package (the `netprints` tool already exists; one tool is simpler to install and document).

## 7. Catalog schema v1

**Decision**: File extension `.npcat.json`; `$schema`
`https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json`; `schemaVersion: 1`. Entries sorted by
documentation-comment id (`ISymbol.GetDocumentationCommentId()`, ordinal) at every level; members grouped
`constructors`, `methods`, `variables`. Type references reuse the graph format's `TypeRef` shape
(`name`, `generic`, `isEnum`, `isInterface`, `args`) so conversion to NetPrints specifiers is the same as for
graphs; default parameter values reuse the graph format's typed-value shape. Summaries are normalized
(whitespace collapsed; `<see cref>`/`<paramref>`/`<typeparamref>`/`<c>` rendered as their plain text) by one
shared function. Optional properties are omitted, not written as `null` or `[]`. Full shape in
[data-model.md](./data-model.md) §1 and [contracts/catalog.md](./contracts/catalog.md) §1.

**Rationale**: Constitution VI (deterministic, versioned, diff-friendly); reuse of the graph `TypeRef` keeps one
type vocabulary. Documentation ids are unique, stable and sortable.

**Alternatives**: sorting by display name (not unique); a binary format (not diffable); embedding NetPrints
specifier JSON (couples the catalog to Core internals).

## 8. Profiles are declarative (ADR-0013)

**Decision**: `ICatalogFilter` (engine interface: `IncludeType`, `IncludeMember`, `DescribeNode`) is
implemented by `CatalogProfileFilter`, compiled from a `CatalogProfile` record: `id`, `base` (`public-api` |
`annotated` | `none`), `includeNamespaces`/`excludeNamespaces`/`includeTypes`/`excludeTypes` (globs: `*` any
run of characters, `?` one, matched ordinally against full names), `typeAttributes`/`memberAttributes`
(`require` or `exclude` entries: attribute full name plus optional `argument` `{ name | position, equals |
contains }` matched against the argument's rendered constant, enum flags rendered as member names joined with
`, `), and `obsolete` (`include` | `exclude` | `excludeErrors`, default `excludeErrors`). Built-ins:
`public-api` (every public type and member of the assembly plus protected members of unsealed public types,
the live provider's member rules, `[NetPrintsIgnore]` excluded) and `annotated` (types with `[NetPrintsType]`
and their public members; methods with `[NetPrintsNode]` and their declaring type only). Custom profiles come
from a `*.npprofile.json` file, an inline `profile` object in `netprints.catalog.json`, or an extension
(`IExtensionBuilder.AddCatalogProfile(CatalogProfile)`, conflicts → NPX006). A project's default catalog
profile is its `IProjectProfile.CatalogProfileId`. Programmatic `ICatalogFilter` implementations are
accepted by the library API (`CatalogBuilder`) only; the tool and the generator take profiles.

**Rationale**: The generator cannot load net10.0 extension code, so only data can mean the same thing in both
flavors (FR-029). U1's `unreal-blueprint` rule ("`[UFunction]` with `BlueprintCallable`, editable properties")
is expressible as attribute-argument rules; the custom-profile snapshot test models it on a fixture attribute.

**Alternatives**: code filters loaded from extensions in the tool (breaks parity); an expression language
(overkill; harder to validate with a schema).

## 9. Tool sources go through SDK projects

**Decision**: Every catalog source resolves through an SDK project evaluated by the P1 `MsBuildProjectSystem`,
whose `ProjectSnapshot.References` already carry each assembly's path and XML documentation path:
- `project`: the user's project; `assemblies` names which references to catalog (simple names).
- `assembly`: a temporary project with `<Reference Include="<path>" />` for each assembly (MSBuild's
  reference resolution finds sibling dependencies and the framework reference pack of `targetFramework`,
  default `net10.0`).
- `package`: a temporary project with `<PackageReference Include="<id>" Version="<version>" />`, restored with
  `dotnet restore` through `IProcessRunner`; the package's own assemblies are the references under
  `<global packages folder>/<id lower>/<version>/`.
The temporary project lives under `obj/netprints-catalog/<hash of the config>/` next to the config file (so
`NuGet.config` files above it apply). Because `Sdk.props` reads `ImportDirectoryBuildProps` and friends before a
`<Project Sdk=…>` body, the file uses explicit `<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />` after a
`PropertyGroup` that sets `ImportDirectoryBuildProps`, `ImportDirectoryBuildTargets`,
`ImportDirectoryPackagesProps` and `ManagePackageVersionsCentrally` to `false` (a test runs it under a directory
with CPM and `Directory.Build.props` files). It is deleted after a successful run. The catalog compilation is a `CSharpCompilation` over those references with no
syntax trees. No SDK → exit 3. The tool never downloads packs or packages itself.

**Rationale**: Reuses the P1 reference resolution (and its documentation lookup) instead of re-implementing
reference-pack discovery; respects the user's feeds and credentials; `NoRuntimeDirectoryReferencesTests`
stays green (nothing enumerates the runtime directory).

**Alternatives**: reading the NuGet global packages folder directly (misses dependencies, TFM selection);
locating `packs/Microsoft.NETCore.App.Ref` by hand (duplicates MSBuild); downloading with NuGet.Protocol (new
dependency, bypasses user configuration).

## 10. Run-time consumption and parity

**Decision**: `CatalogTypeCatalog` (`src/NetPrints.Catalog/Runtime/`) implements `ITypeCatalog` over a loaded
catalog: `Info` from the catalog id, version and covered assemblies; specifiers built from `TypeRef`s with the
same rules as `ReflectionConverter`; `TypeSpecifierIsSubclassOf` from the stored base types and interfaces,
transitively within the catalog (a type outside it answers false and the composite asks the other providers);
`HasImplicitCast` for identity, reference conversions within the catalog and stored `op_Implicit` operators;
documentation for methods, parameters and returns. `CatalogLoader` reads a stream, a file, a string or an
assembly's embedded catalogs. Two catalogs with the same id: first in registry order wins, a warning is
logged. Parity (FR-016, SC-004) is a test that builds a `ReflectionProvider` over the fixture library and a
`CatalogTypeCatalog` over its `public-api` catalog and compares every query result the editor uses
(non-static types, methods by static/instance, variables, constructors, enum names, overridable methods,
public overloads, documentation, subclass and implicit-cast answers for every pair of fixture types).

**Rationale**: The P1 extension point already skips covered assemblies in the live provider
(`ReflectionHost`), so a catalog must answer everything the live provider would have for them.

**Alternatives**: wrapping `InMemoryTypeCatalog` (has no inheritance or conversion data, so subclass and cast
queries for catalog types would be lost).

## 11. `NetPrints.Annotations` package and generator (ADR-0014)

**Decision**:
- One project, `src/NetPrints.Annotations` (netstandard2.0, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`,
  `IncludeBuildOutput=false`, `DevelopmentDependency=true`), packs its own assembly into
  `analyzers/dotnet/cs/` and `build/NetPrints.Annotations.targets`. It references `Microsoft.CodeAnalysis.CSharp`
  with `VersionOverride="5.0.0"` (the compiler of the .NET 10.0.100 SDK that `global.json` and `NetPrints.Sdk`
  require; the central version stays 5.9.0 for the rest of the repo, which transitive pinning needs), private;
  `Microsoft.CodeAnalysis.Analyzers` comes with it transitively and enforces the extended analyzer rules.
- The attributes are injected with `RegisterPostInitializationOutput` as `internal sealed` classes in namespace
  `NetPrints.Annotations`, marked `[Microsoft.CodeAnalysis.Embedded]` via `AddEmbeddedAttributeDefinition()`,
  so they never clash across assemblies and the package adds no run-time reference.
- Own annotated symbols: `SyntaxProvider.ForAttributeWithMetadataName` for `NetPrintsTypeAttribute` and
  `NetPrintsNodeAttribute`, transformed to equatable catalog-model entries per symbol with the shared engine,
  collected, then written once. Only annotated symbols are walked.
- Referenced assemblies: the `[assembly: NetPrintsCatalog(...)]` requests are read from
  `CompilationProvider.Select(c => c.Assembly.GetAttributes())` into equatable request records; the catalog
  is computed from `MetadataReferencesProvider.Collect()` combined with the requests, inside a private
  `CSharpCompilation` over those references, so edits to source files do not re-walk big assemblies.
- Documentation of referenced assemblies arrives as `AdditionalFiles` with metadata
  `NetPrintsReferenceDocumentation=true`, added by a target in `NetPrints.Annotations.targets` after
  `ResolveAssemblyReferences` for each non-framework `ReferencePath` whose `.xml` exists (framework ones too
  when `NetPrintsCatalogFrameworkDocumentation=true`), and made visible with `CompilerVisibleItemMetadata`.
- Output: `[assembly: global::NetPrints.Annotations.NetPrintsEmbeddedCatalogAttribute("<id>", 1, "<json>")]`
  per catalog (regular escaped string literal, valid at any language version), plus for `NetPrintsCatalog`
  requests an accessor `internal static partial class NetPrintsCatalogs { public const string <Name> = ...; }`.
- Diagnostics `NPC001`–`NPC006` (contracts/annotations.md §4), tracked in `AnalyzerReleases.Unshipped.md`
  (RS2008).

**Rationale**: No run-time dependency means library authors on any framework can adopt it (FR-025); an
assembly attribute is readable from metadata without loading (FR-026, FR-031); the incremental pipeline keeps
IDE typing cheap; the XML files are the only way a generator sees referenced documentation (the compiler does
not attach documentation providers to command-line references), and passing them in is what makes summaries
identical across flavors.

**Risk and check**: the tool flavor must still see the injected marker attributes on a compiled assembly
although their types are `internal` and `[Embedded]`; AN-T01 and CT-T05 verify this first (batch D2). If
`[Embedded]` hides them from metadata import, drop `[Embedded]` from the marker attributes only (they are
`internal`, so a clash across `InternalsVisibleTo` is a CS0436 warning the generator avoids by emitting them
only when the compilation cannot already see them) and record the change in implementation-notes.md.

**Alternatives**: a net10.0 `NetPrints.Annotations` attribute library (forces a run-time reference and a
TFM); a generated class holding the catalog as a UTF-8 literal (needs loading and running the assembly to
discover it); compressed payloads (not diffable; deferred to P8).
- **Manifest resource (`EmbeddedResource`).** A source generator can only add source, not resources, so this
  needs an extra MSBuild step after the generator. It is no cheaper to read: a resource is also read from the PE
  bytes with System.Reflection.Metadata. Its only gain is size (raw, compressible bytes), which belongs with the
  P8 compression deferral; revisit it there.
- **A catalog file inside the NuGet package** (for example `netprints/*.npcat.json`). It reaches only package
  consumers (not project references or loose DLLs), needs a package-to-assembly mapping, and can drift from the
  DLL. Shipping a tool-flavor `.npcat.json` in a package stays possible, but it is not the annotations flavor.

## 12. Embedded catalog discovery

"Embedded" means the catalog lives inside the DLL as an assembly-level attribute blob, read from the file's bytes
with System.Reflection.Metadata: no `Assembly.Load` and no run-time reflection.

**Decision**: `EmbeddedCatalogReader.Read(string assemblyPath)` (`src/NetPrints.Catalog/Runtime/`) opens the
PE with `System.Reflection.Metadata`, finds assembly attributes whose constructor's declaring type is
`NetPrints.Annotations.NetPrintsEmbeddedCatalogAttribute`, and decodes the `(string, int, string)` blob; the
loaded-assembly overload uses `CustomAttributeData`. The editor's `ReflectionHost` adds the embedded catalogs of
`snapshot.References` after the registry's catalogs (first id wins). `NetPrints.Editor` and
`NetPrints.Generation` reference `NetPrints.Catalog`, so the library is a host assembly in every host (editor,
generator, CLI) and extensions share it (R19).

**Reference assemblies**: a package may ship `ref/<tfm>/X.dll` beside `lib/<tfm>/X.dll`, and the consumer's resolved
reference is then the reference assembly, from which Roslyn's reference-assembly emission may drop internal types
or their attributes. Decision: `EmbeddedCatalogReader.Read(path)` reads the resolved path first; when it finds no
catalog and the path matches `…/<id>/<version>/ref/<tfm>/X.dll`, it reads `…/<id>/<version>/lib/<tfm>/X.dll`. The
generator does nothing special until AN-T15 has shown what Roslyn actually keeps; if the attribute is lost, the
fix is to emit `NetPrintsEmbeddedCatalogAttribute` as `public` + `[Embedded]` (still invisible to consumer
compilations, but kept by reference-assembly emission), and the observed behaviour is recorded in
implementation-notes.md either way.

**Rationale**: Metadata reading costs microseconds per reference and never loads user code (constitution II,
VII). Translation and generation do not query reflection, so only the editor needs discovery.

## 13. Cross-flavor snapshot tests

**Decision**: Fixture library `tests/Fixtures/Catalog/CatalogFixtureLib` (net10.0, `GenerateDocumentationFile`,
references `NetPrints.Annotations` as an analyzer project reference) covers every edge case in the spec
(generics, ref/out/in/params/defaults, nested types, enums, static classes, extension methods, operators and
`op_Implicit`, obsolete members, protected members, indexers, `[NetPrintsIgnore]`, a fixture attribute with an
enum-flags argument for the custom profile). `tests/NetPrints.Catalog.Tests` (xUnit v3) references
`NetPrints.Annotations` with `Aliases="Annotations"` (an extern alias), because the generator compiles the
shared engine types too and they would otherwise clash with `NetPrints.Catalog`'s. It runs:
1. the tool path (`CatalogBuilder` over a compilation of the built fixture DLL + its XML) for `public-api`,
   `annotated` and `tests/NetPrints.Catalog.Tests/Profiles/fixture-flags.npprofile.json`;
2. the generator in-test with `CSharpGeneratorDriver` over (a) the fixture's own sources (annotated) and (b) a
   consumer compilation with `[assembly: NetPrintsCatalog("CatalogFixtureLib", ...)]` and the fixture's XML as an
   additional file;
3. the built fixture assembly's embedded catalog (read back with `EmbeddedCatalogReader`),
all compared byte-for-byte with each other and with `tests/NetPrints.Catalog.Tests/Snapshots/*.npcat.json`
(update with `NETPRINTS_UPDATE_SNAPSHOTS=1`, the P1 convention). One end-to-end test packs
`NetPrints.Annotations` into a temporary feed and builds a throwaway library with it through `dotnet build`.

**Rationale**: The driver keeps most runs fast and hermetic; one real build proves the package layout and the
targets.

## 14. `format` and `show`

**Decision**: `format`, `show` and `merge` never name a concrete format: they resolve the `IDocumentFormat` for a
file through `DocumentFormatRegistry` (by file name; `merge` uses `--path`, the real name, because git hands the
driver temporary files without the extension) and open files through P1's `FileSystemDocumentStore`
(constitution VII). `format` reads each file (tolerant read; unknown nodes preserved), writes it through the
same format to memory, and compares bytes; `--check` lists files that differ
or fail to read. No extensions are loaded (unknown nodes are re-emitted canonically). `show` reads the same way
and prints the summary grammar in contracts/git.md §1 (one line per class header, member, node, pin value,
connection, local; nodes and connections sorted by id; members in file order).

## 15. `merge` driver (ADR-0016)

**Decision**: `merge <base> <ours> <theirs> [--marker-size N] [--path P]` reads the three files as
`ClassDocument`s through the `IDocumentFormat` resolved from `--path` (default: the `.netpc.json` format) and merges by identity: scalar class fields three-way; members (variables, methods,
constructors, event graphs) by id (add/delete/modify; delete-vs-modify is a conflict); graphs: nodes by id
(whole-node three-way, then per-pin `(Pin, Name, Value)` three-way when both changed the same node), connections
as sets, locals by name, layout per graph and node with ours winning; node order is base order with ours'
additions then theirs'. The result is validated (every connection endpoint exists; at most one connection into
each data input pin; member ids and names unique); any conflict or invalid result falls back to
`git merge-file -p --marker-size N -L ours -L base -L theirs` over the three files' canonical texts, written to
`<ours>`, exit 1. Any read failure falls back the same way on the raw texts. The implementation is internal to
`NetPrints.Cli` (`Git/GraphMerger.cs`), tested through `InternalsVisibleTo`.

**Rationale**: Graph-format research §5.3 (identity merge, gdmerge-style fallback). Using git's own text merge
for conflicts gives users familiar markers and guarantees no lost content.

**Alternatives**: writing our own conflict markers (re-implements git); returning ours unchanged on conflict
(silently drops theirs).

## 16. `git-install`

**Decision**: `git-install [--merge] [--global] [--uninstall] [--command <cmd>]` runs `git config` (local by
default) to set `diff.netprints.textconv = <cmd> show`, and with `--merge`
`merge.netprints.name = NetPrints graph merge` and `merge.netprints.driver = <cmd> merge %O %A %B --marker-size %L --path %P`,
then ensures `.gitattributes` at the work-tree root contains `*.netpc.json diff=netprints` (plus `merge=netprints`
with `--merge`) — `--global` writes the user's global attributes file (`core.attributesFile`, default
`~/.config/git/attributes`). `<cmd>` defaults to `netprints`; `--command "dotnet netprints"` suits a local tool
manifest. Idempotent (a second run reports "already installed" and writes nothing); an existing line for
`*.netpc.json` naming another driver is left untouched and reported; `--uninstall` removes exactly what install
adds.

## 17. SchemaStore

**Decision**: Prepare `eng/schemastore/catalog-entries.json` (two entries: "NetPrints graph" with
`fileMatch: ["*.netpc.json"]` → `https://danielmeza.github.io/netprints/schemas/netpc.v1.schema.json`, and
"NetPrints catalog configuration" with `fileMatch: ["netprints.catalog.json"]` →
`.../schemas/netprints.catalog.v1.schema.json`) and `eng/schemastore/PULL_REQUEST.md` (the upstream PR text and
the checklist SchemaStore asks for). A test checks each entry's URL matches a committed schema's `$id`.
Submitting to `SchemaStore/schemastore` is an owner action after the docs site serves the schemas (AGENTS.md:
nothing is opened upstream without approval).

## 18. Schemas for the new files

**Decision**: `schemas/npcat.v1.schema.json` and `schemas/netprints.catalog.v1.schema.json` are generated with
`JsonSchemaExporter` from the STJ source-generation contexts of the catalog reader and the config model (the
same `TransformSchemaNode` approach as `NetPrintsJsonSchema`), committed, compared with the generated output by
tests (`CatalogSchemaTests`), linted and validated by `eng/validate-schemas.sh` (extended to all
`schemas/*.schema.json`, with instances `**/*.npcat.json`, `**/netprints.catalog.json`), and published by
`scripts/build-docs.sh` (which already copies every schema; a `cmp` check per schema is added).

## 19. Extension coexistence rules (ADR-0010 §4)

**Decision**:
- *Shared assemblies (hazard 1)*: `ExtensionLoadContext.Load` defers to the host when the host provides the
  assembly: the name is in the host's `TRUSTED_PLATFORM_ASSEMBLIES`, or an assembly of that name is already
  loaded in `AssemblyLoadContext.Default`, or it belongs to a host-resolved family (`Microsoft.Build*`, supplied
  by MSBuildLocator). The name-prefix list is removed. Everything else resolves from the extension's folder,
  whatever its name, so `NetPrintsFixture.Runtime.dll` or a third-party `Avalonia.*` add-on the host doesn't ship
  loads privately.
- *Extension-on-extension types (hazard 2)*: an extension's context receives the contexts of its `dependsOn`
  extensions (already loaded, since loading is topological). `Load` asks, in declared order and then
  transitively depth-first, each dependency context to resolve the name (already loaded there, or resolvable
  by its resolver, loaded into *that* context) before its own resolver. The consumer references the provider
  with `Private=false`; a shipped copy is ignored with a warning. Diamonds resolve to the first dependency in
  declared order.
- A host assembly copy in an extension folder is ignored with a logged warning (`Log.HostAssemblyShadowed`).
- Host skew (an extension compiled against a newer host assembly) stays a load-time failure of that extension:
  `MissingMethodException` inside `Register` → `NPX005`, host unaffected; a static verifier is future work
  (`netprints-verify`).
- No new NPX codes.

**Rationale**: "Share only what the host has" is what P1 research R8 intended and removes the naming trap for
NetPrintsUnreal (`NetPrints.Unreal.*` assemblies are not host assemblies). Delegating to dependency contexts is
the OSGi `Require-Bundle`/IntelliJ parent-classloader model and gives one type identity.

**Alternatives**: reserving the `NetPrints` prefix (forces NetPrintsUnreal renames, still breaks for other
prefixes); loading dependent extensions into one shared context (breaks version isolation between unrelated
extensions).

## 20. Multi-extension suite

**Decision**: Scenarios in `tests/NetPrints.Core.Tests/Extensibility/MultiExtension/*` (trait
`Category=MultiExtension`, collection `RealExtensionLoadCollection` where real assemblies load), run by the
existing Core test step. Prebuilt fixtures under `tests/Fixtures/Extensions/` (one shared
`Directory.Build.props` with the TestExtension settings: `EnableDynamicLoading`, output
`bin/$(Configuration)/extensions/<id>/`, host projects `Private=false ExcludeAssets=runtime`):
`Fx.Alpha`, `Fx.Beta` (dependsOn alpha), `Fx.LibV1`/`Fx.LibV2` with `Fixture.SharedLib.V1`/`.V2` (same
assembly name `Fixture.SharedLib`, AssemblyVersion 1.0/2.0, divergent API), `Fx.PrefixedPrivate` with
`NetPrintsFixture.Runtime`, `Fx.TypesProvider`/`Fx.TypesConsumer`, `Fx.Native` (private
`SkiaSharp.NativeAssets.Linux.NoDependencies`, calls `sk_version_get_milestone` through `DllImport`; skipped
with an explicit reason off Linux). Core.Tests references each with `ReferenceOutputAssembly=false`
(TestExtension pattern), and `FixtureExtensions.CopyTo(...)` copies them per test. Roslyn-at-runtime fixtures
(existing `ExtensionTestSupport.Compile`) cover the squatter, duplicate ids, throws-midway, host skew (a
reference assembly named `NetPrints.Core` with an extra member) and the 50-extension scale set. The
`ExtensionHarness` (R21) runs registry + translation + round trip for the documents-across-subsets and
permutation scenarios.

## 21. Conformance kit in P2? (ADR-0010 §5)

**Decision**: Only `ExtensionHarness` lands, in `tests/NetPrints.Testing/Extensions/ExtensionHarness.cs`
(internal test support, not packaged): builds a real `ExtensionRegistry` from folders or in-process
extensions, exposes `Translate(ClassGraph)`, `RoundTripAsync(document)`, `GenerateAsync(project graphs)` and
the registry. The public `NetPrints.Extensibility.Testing` package, `ExtensionTest<T>`, C1–C12, `KnownNeighbours`,
the xUnit adapter and the TestExtension migration stay in P3.

**Rationale**: The harness is needed now by the suite; publishing the kit requires packable
`NetPrints.Extensibility`/`NetPrints.Serialization` (P3) and a stable surface; P2's budget goes to the roadmap's
P2 bullets.

## 22. PublicApiAnalyzers

**Decision**: `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 (same line as BannedApiAnalyzers) on
`NetPrints.Extensibility`, `NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Serialization` and
`NetPrints.Catalog`, with `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` (first line `#nullable enable`,
RS0037) as `AdditionalFiles`. Core and Reflection: the API at `v0.1.1` goes to Shipped (they were released);
the others list everything in Unshipped until their first release. Files are produced with the analyzers' code
fix (`dotnet format analyzers <csproj> --diagnostics RS0016 --severity info`) before any P2 API change, then
reviewed. RS0026/RS0027 hits on existing API are fixed for real (0.x may break), never suppressed. The rules
fail the build through `TreatWarningsAsErrors`; no `.editorconfig` change.

## 23. `[Experimental]`

**Decision**: `System.Diagnostics.CodeAnalysis.ExperimentalAttribute` with ids and `UrlFormat =
"https://danielmeza.github.io/netprints/docs/guide/extensions#api-stability"`:
`NPXE0001` host channel (`IHostChannel`, `IHostChannelFactory`, `HostMessage`, `IExtensionBuilder.AddHostChannel`);
`NPXE0002` extension settings (`ExtensionSettingsDescriptor`, `ISettingsStore`, `IExtensionBuilder.AddSettings`);
`NPXE0003` class and member emitters (`IClassEmitter`, `IMemberEmitter`, `AddClassEmitter`, `AddMemberEmitter`);
`NPXE0004` catalog engine and profiles (`CatalogBuilder`, `ICatalogFilter`, `CatalogProfile`,
`IExtensionBuilder.AddCatalogProfile`). In-repo projects opt in per project and per id
(ADR-0017, amending ADR-0010 §3): each declares `<NetPrintsExperimentalOptIn Include="NPXE000n" />` items for only
the ids it uses, and `Directory.Build.targets` turns them into `NoWarn` on one line. `SourceHygieneTests` allows
only that `<NoWarn>` line, requires every item to be a declared `ExperimentalApiIds` id, and forbids
`#pragma warning disable NPXE*` and `.editorconfig`/globalconfig severity entries for `NPXE` ids; ADR-0003's
ledger gets a row pointing to ADR-0017. A probe test compiles an external project
(Roslyn, in-test) that uses each marked API without opting in and asserts the `NPXE` error.

**Rationale**: These are the surfaces P3 (host channel, settings pages), P3b (emission engine) and P2 itself
(catalog profiles) are expected to reshape. External authors must opt in; the repository owns the API and each
consuming project opts in visibly, per id.

## 24. Packaging and release

**Decision**: `NetPrints.Catalog` packable with `EnablePackageValidation` (no baseline, ADR-0010);
`NetPrints.Annotations` packable as a development-dependency analyzer package with `IncludeSymbols=false` (like
`NetPrints.Sdk`: a package without build output fails pack with NU5017 when symbols are on). `scripts/verify-packages.sh`
expects the two new `.nupkg`/`.snupkg` pairs (Annotations: `.nupkg` only, no symbols package for an analyzer-only
package) and checks their layout (`lib/net10.0/NetPrints.Catalog.dll`; `analyzers/dotnet/cs/NetPrints.Annotations.dll`,
`build/NetPrints.Annotations.targets`, `developmentDependency`). `release.yml` packs the solution, so no workflow
change beyond what the script checks. Nothing is published.

## 25. Documentation

**Decision**: New guides `docs/guide/cli.md`, `docs/guide/catalogs.md`, `docs/guide/git.md`; updates to
`docs/guide/extensions.md` ("Coexistence rules", "Depending on another extension", "API stability"),
`docs/guide/install.md` (commands), `docs/guide/graph-format.md` (checks, SchemaStore), `docs/guide/projects.md`
(`netprints generate`); `docs/api/docfx.json` adds `NetPrints.Catalog`; `docs/adr/README.md` index; README CLI
section. No screenshots (editor UI unchanged).

## 26. CI

**Decision**: `ci.yml`: "CLI smoke" asserts rc 0 for `--version` and `--help`; "CLI sample compile and run"
uses `netprints run samples/HelloWorld/HelloWorld.csproj`; new steps "Test (Catalog)", "Test (CLI)",
"Graph checks" (`netprints format --check samples` and `netprints regen --check samples/HelloWorld`). All
Linux.

## 27. Deferred (not P2)

| Item | Goes to | Why |
|---|---|---|
| In-editor visual diff | P6 | Needs the P3a shell; UX work |
| Public conformance kit, KnownNeighbours, xUnit adapter | P3 | Needs published extension packages |
| `PackageValidationBaselineVersion` | First extension-API publish (P3) | 0.x; tracked API files suffice now |
| `netprints-verify`, author CI workflow, nightly ecosystem job, `dotnet new` template | Before the first NetPrintsUnreal release | Research (d) |
| Catalog compression | P8 | Performance |
| Real schema migrations | First schema v2 (after the version cut) | Owner 2026-09-26 |
| Collectible contexts and unload tests | Only if hot reload is scheduled | Research (d) |
| Windows leg for `Fx.Native` | With the first Windows workflow | Linux-only CI |
