# Feature Specification: Catalog Tooling and Spectre CLI

**Feature Branch**: `004-catalog-cli`

**Created**: 2026-09-29

**Status**: Draft

**Input**: Coordinator description: "Phase P2 of the NetPrints modernization, 'Catalog tooling + Spectre
CLI', as scoped in `.specify/memory/roadmap.md` (P2 section): the `NetPrints.Catalog` engine (versioned
schema, `ICatalogFilter` profiles), the tool flavor (`netprints.catalog.json` + CLI overrides), the
annotations flavor (`NetPrints.Annotations` + a netstandard2.0 source generator), cross-flavor snapshot
tests; `NetPrints.Cli` on Spectre.Console.Cli with `build`, `generate`, `run`, `catalog`, `migrate`
(`migrate` stays a stub until the schema version cut); the graph-format follow-ups (`format --check`,
`regen --check`, `netprints git-install` with a `textconv` diff and the optional `netprints merge` driver,
SchemaStore registration of the `.netpc.json` schema); the host multi-extension suite; PublicApiAnalyzers
and `[Experimental]`; ADR-0010 deciding the shared-prefix private-dependency hazard and the
`dependsOn`-doesn't-share-types hazard. Also decide whether any of the P3 author conformance kit lands
in P2. Include every P2 deferral from P1."

**Roadmap phase**: P2 (depends on P1, merged as PR #6, released as `v0.1.0`/`v0.1.1`). **Done when**
(roadmap, owner decision 2026-09-28): the phase's features work end-to-end and the docs are updated
(guides, API reference, ADRs).

**Sources reused** (see [research.md](./research.md) §1): roadmap P2 and the P3 kit bullet; constitution
1.2.3; the plan page (NetPrints Unreal Roadmap artifact, sections "Catalog tooling", "Two-repo
architecture", P2 and U1); `docs/research/2026-09-29-extension-testing/` (§1, §6, draft ADR 0010);
`docs/research/2026-09-25-graph-format/` (§5.3 merge and diff drivers, §5.4 roadmap, §7 schema
publication); `specs/003-core-refactor/implementation-notes.md` ("Deferred from the PR #6 review");
`specs/003-core-refactor/spec.md` (Assumptions: P1 follow-ups).

## Clarifications

### Session 2026-09-29

No interactive user was available (owner instruction: decide autonomously and record each non-trivial
decision as an ADR). Each question was answered with the default most consistent with the constitution
and the roadmap; the ADR or research entry that records it is named.

- Q: Does any part of the P3 author conformance kit (`NetPrints.Extensibility.Testing`) land in P2? → A:
  Only its internal core: an `ExtensionHarness` in the repository's test-support library that the host
  multi-extension suite uses. The public package, `ExtensionTest<T>`, the twelve conformance checks, the
  xUnit adapter and moving `NetPrints.TestExtension`'s tests onto the kit stay in P3, which publishes the
  extension packages the kit depends on (ADR-0010, decision 5).
- Q: Does the new CLI keep the P1 flags (`-p/--project-path`, `-r/--run`) as aliases? → A: No. The CLI is
  0.x and has one release; the new commands replace the flags, the guide and the release notes say so,
  and an old-style invocation fails with a usage error that names the replacement (ADR-0015).
- Q: How does the tool flavor get a NuGet package's assemblies? → A: Through a project: a `package` source
  is restored in a temporary SDK project with the user's own NuGet configuration and read from that
  project's resolved references, the same way a `project` source is read. The tool never downloads
  anything itself (ADR-0012).
- Q: Is the `netprints merge` driver implemented in P2, or only the `textconv` diff? → A: Both.
  `git-install` always installs the diff and installs the merge driver only when asked (`--merge`); the
  driver falls back to git's own text merge with conflict markers whenever it cannot merge by identity
  (ADR-0016).
- Q: How do catalogs reach the editor and the build, and does a catalog replace live type discovery? → A:
  Two ways, both automatic: an extension contributes a catalog (the P1 extension point), or a referenced
  library carries an embedded catalog that the annotations flavor produced. Either way the catalog stands
  in for the assemblies it covers, whose types the live provider then skips (P1 rule) (ADR-0012,
  ADR-0014).

Coverage scan result: every other taxonomy category was Clear or covered by a default. Observability of
the CLI is stream discipline plus a `--verbose` switch (FR-010); structured logs and telemetry are out of
scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One command-line tool for every project workflow (Priority: P1)

A developer or a CI job builds, runs and regenerates a NetPrints project with `netprints`. Each command
has its own help, `--help` and `--version` succeed, and every outcome maps to a documented exit code, so
scripts can rely on it.

**Why this priority**: The CLI is the entry point for every other P2 feature (catalogs, checks, git
integration) and U1's first Unreal loop (`netprints generate` into UnrealSharp's `Script/` folder). It
also fixes the P1 deferral: `--version`/`--help` exit 2 today.

**Independent Test**: In a clean clone, `netprints run samples/HelloWorld/HelloWorld.csproj` prints
`Hello, World!` and exits 0; `netprints generate` on the same project leaves the committed `.netpc.g.cs`
unchanged; `netprints --version` exits 0.

**Acceptance Scenarios**:

1. **Given** the installed tool, **When** the user runs `netprints --help`, `netprints -h`, `netprints
   --version` or `netprints <command> --help`, **Then** it prints the help or version and exits 0.
2. **Given** a project that builds, **When** the user runs `netprints build <project>`, **Then** the
   project builds and the tool exits 0; **given** a compile error, it lists each error as
   `file(line,column): code: message` and exits 1.
3. **Given** a project whose program exits with code 7, **When** the user runs `netprints run <project>
   -- a b`, **Then** the program receives the arguments `a b` and the tool exits 7.
4. **Given** a project with a stale `.netpc.g.cs`, **When** the user runs `netprints generate <project>`,
   **Then** only the stale file is rewritten, diagnostics use the build's canonical format, and the tool
   exits 0 (1 if any diagnostic is an error).
5. **Given** a project with one stale generated file, **When** CI runs `netprints regen --check
   <project>`, **Then** nothing is written, the stale file is named, and the tool exits 1; with no stale
   file it exits 0.
6. **Given** graphs that are all at schema version 1, **When** the user runs `netprints migrate`,
   **Then** it reports that no migration is needed, writes nothing and exits 0.
7. **Given** a directory that holds no project file, or several, **When** the user runs a project
   command without a project argument, **Then** the tool explains the problem and exits 2.

---

### User Story 2 - Catalog any .NET library with the tool (Priority: P1)

A developer turns a library they do not own (a DLL, a NuGet package, or the references of a project)
into a NetPrints type catalog with `netprints catalog`, driven by a `netprints.catalog.json` file, command
options, or both, and a profile that decides which types and members the catalog offers. An extension
contributes the catalog, and the library's types show up in the editor's node search and translate to
code that builds and runs.

**Why this priority**: Precomputed catalogs are how NetPrints offers large libraries (UnrealSharp in U1)
without loading them through the compiler on every start, and how an ecosystem curates what a graph may
call. U1 depends on it.

**Independent Test**: Catalog the repository's fixture library with the `public-api` profile; the catalog
equals the committed snapshot, offers exactly the types and members the live provider offers for that
library, and a sample extension that contributes it lets a graph call a cataloged method that builds and
runs.

**Acceptance Scenarios**:

1. **Given** a `netprints.catalog.json` naming one assembly and the `public-api` profile, **When** the
   user runs `netprints catalog`, **Then** a catalog file is written with the schema address and version
   first, every public type and member of that assembly with its documentation summary, sorted, with no
   timestamps, and running it again produces the same bytes.
2. **Given** the same file, **When** the user adds `--exclude "*.Internal.*" --output other.npcat.json`,
   **Then** the options override the file's values and the excluded namespaces are absent.
3. **Given** a `package` source (id and version), **When** the user runs `netprints catalog`, **Then**
   the package is restored through the user's NuGet configuration and its assemblies are cataloged;
   **given** a package that cannot be restored, the tool reports the restore error and exits 1.
4. **Given** `--format csharp`, **When** the user runs `netprints catalog`, **Then** the tool writes a C#
   source file that an extension project compiles to obtain the same catalog.
5. **Given** an existing catalog file and `--check`, **When** the inputs changed, **Then** nothing is
   written and the tool exits 1; when nothing changed it exits 0.
6. **Given** an extension that contributes the fixture catalog, **When** the editor opens a project that
   uses the extension, **Then** node search offers the cataloged methods, and a graph that calls one
   builds and runs.
7. **Given** an extension that contributes a catalog profile (for example `unreal-blueprint`), **When**
   the user runs `netprints catalog --project <p> --profile unreal-blueprint`, **Then** the profile is
   found through the project's extensions; with no `--profile`, the project profile's default catalog
   profile is used.

---

### User Story 3 - Ship prints with a library through annotations (Priority: P1)

A library author adds `NetPrints.Annotations` as a development dependency and marks types and methods
with `[NetPrintsType]` and `[NetPrintsNode]`, or names a referenced assembly with `[assembly:
NetPrintsCatalog("Name")]`. Each build embeds a catalog in the library. A NetPrints project that
references the library offers exactly those nodes, with no extension and no extra step.

**Why this priority**: It is the second flavor the roadmap commits to, and the cross-flavor snapshot tests
are what prove both flavors are one engine.

**Independent Test**: Build the fixture library with the annotations package; the embedded catalog is
byte-identical to what the tool produces for the same assembly and profile, and a NetPrints project that
references the library finds the annotated nodes in search and builds a graph that calls one.

**Acceptance Scenarios**:

1. **Given** a library with two `[NetPrintsNode]` static methods and one `[NetPrintsType]` class, **When**
   it builds, **Then** its assembly carries a catalog with exactly those members (and the class's public
   members), using the `annotated` profile.
2. **Given** a project that references assembly `Lib` and declares `[assembly: NetPrintsCatalog("Lib",
   Profile = "public-api")]`, **When** it builds, **Then** it carries a catalog of `Lib` byte-identical to
   `netprints catalog --assembly Lib.dll --profile public-api`.
3. **Given** `[assembly: NetPrintsCatalog("Missing")]` for an assembly the project does not reference,
   **When** it builds, **Then** the compiler reports an error diagnostic naming the assembly and no
   catalog is embedded.
4. **Given** a NetPrints project referencing the annotated library, **When** the editor loads it, **Then**
   node search offers the annotated nodes and not the library's other public members.
5. **Given** a library project that targets an older framework than the NetPrints tools, **When** it
   uses the annotations package, **Then** it builds, because the package adds nothing the library needs at
   run time.

---

### User Story 4 - Graphs stay canonical and reviewable in git (Priority: P2)

A team keeps graph files canonical and generated code fresh in CI, reads graph changes as a readable
summary in `git diff`, merges parallel edits of the same graph without spurious conflicts, and gets
schema validation and completion for `.netpc.json` files in their editor.

**Why this priority**: These are the graph-format follow-ups P1 deferred. They make the format's
merge-friendliness usable day to day, but none blocks the catalog or the Unreal loop.

**Independent Test**: In a temporary git repository, `format --check` and `regen --check` flag a
hand-edited and a stale file; after `netprints git-install --merge`, `git diff` shows the summary, and two
branches that each add nodes to the same method merge cleanly where plain git reports a conflict.

**Acceptance Scenarios**:

1. **Given** a graph file hand-edited into non-canonical form, **When** CI runs `netprints format
   --check`, **Then** the file is named, nothing is written, and the tool exits 1; `netprints format`
   rewrites it canonically and touches no already-canonical file.
2. **Given** a graph file, **When** the user runs `netprints show <file>`, **Then** it prints one line
   per member, node, connection and value in a stable order, and a graph with nodes of an extension
   that is not loaded still prints (those nodes by kind).
3. **Given** a git repository, **When** the user runs `netprints git-install`, **Then** `git diff` of a
   graph shows the summary lines; with `--merge`, git also uses `netprints merge` for graphs; running it
   twice changes nothing the second time.
4. **Given** two branches that add different nodes and connections to the same method, **When** they
   are merged with the driver installed, **Then** the merge succeeds with no conflict, the result is
   canonical, and it contains both branches' changes.
5. **Given** two branches that change the same property of the same node differently, **When** they are
   merged with the driver, **Then** the driver writes git-style conflict markers over the canonical text,
   exits non-zero, and no change from either side is lost.
6. **Given** the published JSON Schema, **When** the owner submits the prepared SchemaStore entry, **Then**
   editors that use SchemaStore validate and complete `*.netpc.json` (and `netprints.catalog.json`) files
   without a `$schema` property (submission is an owner action; the repository only prepares it).

---

### User Story 5 - Extensions coexist safely (Priority: P2)

NetPrints loads several extensions at once, including extensions that depend on each other's types and
extensions that bring their own versions of the same library, with deterministic results. The two
behaviours the extension-testing research found unpinned are decided (ADR-0010) and tested.

**Why this priority**: NetPrintsUnreal will ship several cooperating extensions; the hazards must be
decided before it does. Nothing user-visible in P2 depends on it, so it follows the P1 stories.

**Independent Test**: The host multi-extension suite runs on every pull request and passes: id
conflicts, load-order permutations giving byte-identical generated C#, private-dependency version
isolation, extension-on-extension types, documents across extension subsets, failure isolation, scale,
reload caching and native dependencies.

**Acceptance Scenarios**:

1. **Given** an extension whose private dependency's name starts like a host assembly (for example
   `NetPrintsFixture.Runtime`), **When** it loads, **Then** the dependency is loaded from the extension's
   folder and the extension works.
2. **Given** extension B that declares `dependsOn: A` and uses A's types, **When** both load, **Then** B
   resolves A's assemblies from A, and a type of A seen from B is the same type A sees.
3. **Given** two extensions that each ship a different version of the same private library, **When**
   both load, **Then** each uses its own version.
4. **Given** the same set of extensions discovered in any order, **When** a project is generated,
   **Then** the load order, the registry order and the generated C# are identical.
5. **Given** an extension that throws halfway through registering, **When** the set loads, **Then** none
   of its contributions is used, the failure is reported, and the other extensions work.
6. **Given** a graph with nodes from extensions A and B, **When** it is opened with only A, only B, or
   neither, **Then** the missing nodes are kept, re-saving is byte-identical, and generation reports the
   missing extension.

---

### User Story 6 - Extension API changes are visible and deliberate (Priority: P3)

A reviewer sees every change to the public API that extensions compile against as a diff of a tracked
API file, and API that is still expected to change is marked experimental so extension authors opt in
knowingly.

**Why this priority**: It protects future extension authors, but no P2 user-facing feature depends on it.
It lands early in the implementation order (foundational) so later P2 API additions are tracked as they
are made.

**Independent Test**: Adding a public member to a tracked library without declaring it fails the build;
using an experimental API from outside the repository without opting in fails that consumer's build with
the documented diagnostic id.

**Acceptance Scenarios**:

1. **Given** a tracked library, **When** a public member is added without updating its unshipped API
   file, **Then** the build fails naming the member.
2. **Given** an experimental API, **When** an external project uses it without opting in, **Then** its
   build fails with the documented `NPXE` id and a link to the stability guide.

---

### Edge Cases

- A catalog file with a newer `schemaVersion` than the reader supports: loading fails with a clear
  diagnostic naming both versions; the editor keeps working without that catalog.
- Two catalogs (an extension's and an embedded one, or two extensions') claim the same catalog id: the
  first in registry order wins, the other is reported and ignored.
- A catalog covers an assembly the project does not reference: the catalog's types are still offered, and
  code using them fails to compile with the compiler's normal error (documented).
- A cataloged assembly references types from assemblies that are not available: those members are
  omitted with a warning diagnostic naming the missing assembly; the catalog is still produced.
- Generic types and methods, `ref`/`out`/`in` and `params` parameters, default values, nested types,
  enums, static classes, extension methods, operators and obsolete members are cataloged exactly as the
  live provider presents them (covered by the parity test).
- An annotation on a member that is not public is ignored with a warning diagnostic.
- An annotated library packaged with reference assemblies (`ref/<tfm>/X.dll` next to `lib/<tfm>/X.dll`): the
  consumer's resolved reference is the reference assembly, and its embedded catalog is still found (from the
  reference assembly if it survived there, else from the matching implementation assembly).
- `format` on a file that is not valid JSON, or not a graph: the file is reported as unreadable, nothing
  is written, exit 1; other files are still processed.
- `merge` when base, ours or theirs is unreadable (for example a conflict-marked file): the driver falls
  back to the text merge and exits non-zero; it never exits 0 with lost content.
- `git-install` outside a git work tree: usage error, exit 2. With a `.gitattributes` that already names
  another driver for `*.netpc.json`: the existing line is kept and reported; nothing is overwritten.
- `generate` or `regen --check` on a project whose extension fails to load: the NPX error is printed and
  the tool exits 1 without writing.
- The project argument names a directory: the single project file in it is used, as with no argument.
- `migrate` on a graph with a `schemaVersion` newer than the tool supports: error naming the file and the
  supported version, exit 1.
- An extension's folder contains a copy of a host assembly (for example `NetPrints.Core.dll`): the host
  copy is used and a warning is logged.
- An extension depends on two extensions that both provide an assembly with the same name: the first in
  `dependsOn` order wins, deterministically.

## Requirements *(mandatory)*

### Functional Requirements

**Command-line tool (US1)**

- **FR-001**: The `netprints` tool MUST offer the commands `build`, `run`, `generate` (alias `regen`),
  `migrate`, `catalog`, `format`, `show`, `merge` and `git-install`, each with its own help text and
  examples.
- **FR-002**: `--help` and `-h` MUST exit 0 at the top level and for every command; `--version` MUST exit 0 at
  the top level and print one line, `NetPrints.Cli <informational version>`.
- **FR-003**: Every command MUST follow one exit-code contract: 0 success; 1 the operation failed or a
  check found differences or conflicts; 2 invalid usage (unknown command or option, bad value, missing or
  ambiguous project); 3 no compatible .NET SDK was found; 4 internal error. `run` returns the program's
  own exit code once the build succeeded. The contract MUST be documented and each code covered by a
  test.
- **FR-004**: Project commands (`build`, `run`, `generate`, `catalog --project`) MUST accept a project file, a
  directory holding exactly one project file, or nothing (the current directory, same rule). `migrate` takes
  graph files, directories (searched recursively) or a project file, and applies the same rule when given no
  argument.
- **FR-005**: `build` MUST build the project through the installed .NET SDK and print each error as
  `file(line,column): code: message`.
- **FR-006**: `run` MUST build, then run the project, passing every argument after `--` to the program
  and forwarding its standard output and error.
- **FR-007**: `generate` MUST regenerate the project's `.netpc.g.cs` files in process, with the same
  generator library, extension set and profile as the build, without compiling the project; it MUST
  rewrite only files whose content changes and print diagnostics in the build's canonical format.
- **FR-008**: `generate --check` (and `regen --check`) MUST write nothing, list every generated file that
  is missing or differs, and exit 1 if any does.
- **FR-009**: `migrate` MUST report the schema version of each graph it finds and, while schema version 1
  is the only version, write nothing and exit 0 ("no migrations are available"); a graph with a newer or
  unknown version MUST be an error (exit 1).
- **FR-010**: The tool MUST print plain text without color or decoration when output is redirected or
  `NO_COLOR` is set, so CI logs and git drivers get stable text. Command results and diagnostics go to
  standard output; logs (warnings by default, informational with `--verbose`) and internal-error details
  go to standard error, so `show` output is exactly the summary.
- **FR-011**: An invocation that uses the P1 flags (`-p`/`--project-path`, `-r`/`--run`) MUST fail with
  exit 2 and a message naming the replacement command.

**Catalog engine and schema (US2, US3)**

- **FR-012**: The catalog engine MUST produce a catalog from assembly metadata and XML documentation under
  a profile. A catalog MUST hold: its id and version, the profile id, the covered assemblies (name and
  version), and every included type (namespace, name, kind, generic parameters, base type and interfaces,
  static/abstract/sealed, nested declaring type, enum member names) with its included constructors,
  methods (static/instance, virtual/abstract/override, generic parameters, parameters with passing mode,
  `params` and default value, return type, visibility) and fields/properties (type, static, readable,
  writable, visibility), plus documentation summaries for types, members, parameters and returns, and
  optional node hints (display name, category, keywords).
- **FR-013**: Catalog files MUST be deterministic and diff-friendly: `$schema` then `schemaVersion` first,
  entries sorted by documentation id (ordinal), two-space indentation, LF line endings, a trailing newline,
  no timestamps or machine paths. Producing a catalog twice from the same inputs MUST give identical bytes.
- **FR-014**: The catalog format MUST be versioned (`schemaVersion: 1`) with a published JSON Schema;
  readers MUST reject a newer version with a clear error and MUST tolerate unknown properties. Catalog
  configuration files and profile files MUST carry the same optional `schemaVersion` (default 1) with the same
  rule.
- **FR-015**: Profiles MUST be declarative data that both flavors apply identically: the built-in
  profiles `public-api` (every public type and member, plus protected members of unsealed types, as the
  live provider presents them) and `annotated` (only annotated types and members), and custom profiles
  (a base profile plus namespace and type include/exclude patterns, required or excluded attributes on
  types and members with optional argument matching, and obsolete handling) given as a JSON file, inline
  in `netprints.catalog.json`, or contributed by an extension.
- **FR-016**: A `public-api` catalog of an assembly MUST offer exactly the types, members, documentation,
  subclass relations and implicit conversions the live provider offers for that assembly (parity).
- **FR-017**: A catalog loaded at run time MUST work as a type catalog of the P1 extension point: it
  answers every reflection query for the assemblies it covers, and the live provider skips those
  assemblies.
- **FR-018**: Extensions MUST be able to contribute catalog profiles; a duplicate profile id MUST be
  rejected with the existing contribution-conflict diagnostic (first wins).

**Tool flavor (US2)**

- **FR-019**: `netprints catalog` MUST read `netprints.catalog.json` (from `--config`, else the current
  directory) and accept options that override each of its settings: sources (`--assembly`, `--package
  id@version`, `--project`), `--include`, `--exclude`, `--profile`, `--id`, `--catalog-version`,
  `--output`, `--format catalog|csharp`, `--check`, and `--extension` folders for profiles. With no
  config file and no source option, it MUST exit 2.
- **FR-020**: An `assembly` source MUST read the assembly and its sibling XML documentation file; its
  dependencies MUST resolve from the assembly's directory, configured reference paths, and the .NET
  reference pack of the configured target framework (default `net10.0`) located through the installed
  SDK.
- **FR-021**: A `project` source MUST catalog the named assemblies among the project's resolved
  references (with their documentation); a `package` source MUST be restored in a temporary SDK project
  with the user's NuGet configuration and cataloged the same way. The tool MUST NOT download anything by
  itself.
- **FR-022**: `--format csharp` MUST write a deterministic C# file holding the catalog and a factory that
  returns it as a type catalog, compilable in an extension project that references the catalog library.
- **FR-023**: `--check` MUST write nothing and exit 1 when the output file is missing or differs.
- **FR-024**: Without `--profile` or a configured profile, a `--project` run MUST use the project
  profile's catalog profile id (P1 `CatalogProfileId`), else `public-api`.

**Annotations flavor (US3)**

- **FR-025**: `NetPrints.Annotations` MUST be a development-only package: it adds the attributes
  `NetPrintsCatalog` (assembly), `NetPrintsType`, `NetPrintsNode` and `NetPrintsIgnore` to the consuming
  project at compile time and a source generator, and adds no run-time dependency, so projects of any
  target framework the .NET 10 SDK compiles can use it.
- **FR-026**: For a project with annotated types or methods, the generator MUST embed a catalog of them
  (profile `annotated`, covering the project's own assembly) in the project's assembly, readable from the
  assembly's metadata without loading or running it. The catalog, with its documentation summaries baked in,
  travels inside the compiled and packaged DLL: a consumer needs neither the library's `.xml` file nor
  `NetPrints.Annotations`.
- **FR-027**: For each `[assembly: NetPrintsCatalog("Name", ...)]`, the generator MUST embed a catalog of
  the referenced assembly `Name` with the given profile, include and exclude settings, and generate an
  accessor so an extension can register it.
- **FR-028**: The generator MUST obtain documentation for referenced assemblies from their XML files
  (passed to it by the package's build logic), so both flavors produce the same summaries.
- **FR-029**: For the same assemblies, profile and settings, the tool and the generator MUST produce
  byte-identical catalog JSON; the repository MUST test this against committed snapshots for every
  built-in profile and one custom profile.
- **FR-030**: The generator MUST report its problems as compiler diagnostics with stable `NPC` ids
  (assembly not referenced, unknown profile, invalid profile file, annotation on a non-public member,
  unresolved dependency).
- **FR-031**: The editor MUST discover embedded catalogs in a project's referenced assemblies, without
  loading them, and use them like extension-contributed catalogs (translation and generation do not query
  type information, so the build and the CLI need no discovery).

**Graph-format tooling and git (US4)**

- **FR-032**: `format [paths]` MUST rewrite `.netpc.json` files (directories are searched recursively;
  default the current directory) into canonical form, touching only files that change; `format --check`
  MUST write nothing, list non-canonical and unreadable files, and exit 1 if any.
- **FR-033**: `show <graph>` MUST print a stable, line-oriented summary of the graph (header, members,
  nodes, connections, values), independent of the node order in the file, and MUST NOT fail on nodes of
  unloaded extensions.
- **FR-034**: `merge <base> <ours> <theirs>` MUST perform a three-way merge by identity (members by id,
  nodes by id, connections as sets, node values per pin, layout per node with ours winning), write the
  canonical result to `<ours>` and exit 0 when there is no semantic conflict; otherwise it MUST write
  git-style conflict markers over the canonical text of both sides and exit 1. It MUST never exit 0 when a
  change from either side was dropped.
- **FR-035**: `git-install` MUST configure the current repository (or the user's global configuration
  with `--global`) so graph files use `show` as their diff text conversion and, with `--merge`, `merge` as
  their merge driver, and MUST add the matching `.gitattributes` lines; it MUST be idempotent, keep
  conflicting existing lines, and support `--uninstall`.
- **FR-036**: The repository MUST contain a ready-to-submit SchemaStore catalog entry for `*.netpc.json`
  and `netprints.catalog.json` with the pull-request text; submitting it is an owner action.
- **FR-037**: The JSON Schemas for catalog files and `netprints.catalog.json` MUST be generated from code,
  committed, validated by the repository's schema checks, and published on the docs site at their
  `$schema` addresses, like the graph schema.

**Extension coexistence (US5, ADR-0010)**

- **FR-038**: An extension's assembly dependency MUST be taken from the host only when the host provides
  it (it is one of the host application's assemblies or already loaded by the host, or it belongs to a
  family the host resolves itself: MSBuild); every other dependency, whatever its name, MUST resolve from
  the extension's own folder.
- **FR-039**: An extension that declares `dependsOn` MUST resolve its dependencies' assemblies (their
  main and private assemblies, transitively, in declared order) from those extensions' load contexts
  before its own folder, so the types are shared with one identity.
- **FR-040**: A copy of a host assembly in an extension's folder MUST be ignored in favour of the host's,
  with a logged warning.
- **FR-041**: Loading MUST be deterministic: the same set of extensions in any discovery order MUST give
  the same load order, registry order and byte-identical generated C#.
- **FR-042**: An extension whose registration throws MUST have none of its contributions committed; the
  others MUST be unaffected.
- **FR-043**: The repository MUST have a host multi-extension test suite with purpose-built fixture
  extensions (baseline pair with `dependsOn`, id squatter, duplicate id, private dependency v1/v2,
  host-assembly skew, shared-prefix private dependency, type provider/consumer, throws-mid-register,
  native dependency) covering id conflicts, load-order permutation invariance, dependency-version
  isolation, extension-on-extension types, documents across extension subsets, failure isolation, scale
  (50 extensions) and reload caching, running on every pull request on Linux.

**API tracking (US6)**

- **FR-044**: The public API of `NetPrints.Extensibility`, `NetPrints.Core`, `NetPrints.Reflection`,
  `NetPrints.Serialization` and `NetPrints.Catalog` MUST be tracked in API files checked by the build;
  the API released in `v0.1.1` (Core, Reflection) MUST be recorded as shipped.
- **FR-045**: API expected to change (host channel, extension settings, class and member emitters, the
  catalog engine's programmatic API and catalog profiles) MUST be marked experimental with documented ids
  (`NPXE0001`–`NPXE0004`) and a link to the stability guide; each of the repository's own projects opts in
  per id in its own project file (ADR-0017).

**Documentation and release (all stories)**

- **FR-046**: The docs site MUST gain guides for the CLI (commands, options, exit codes, CI recipes),
  catalogs (both flavors, profiles, schema, consumption) and git integration (format and regen checks,
  diff and merge drivers, SchemaStore), updates to the extensions guide (coexistence rules, `dependsOn`
  type sharing, API stability) and the install guide (new commands), the `NetPrints.Catalog` API
  reference, and an ADR index covering ADR-0010 and ADR-0012–ADR-0017.
- **FR-047**: `NetPrints.Catalog` and `NetPrints.Annotations` MUST be packed by the release workflow's dry
  run and verified by the package verification script; nothing is published in this phase.

### Key Entities

- **Catalog**: a versioned, deterministic document describing the types and members a set of assemblies
  offers to graphs, under one profile; identified by id and version; covers named assemblies.
- **Catalog profile**: declarative selection rules (base profile, patterns, attribute rules, obsolete
  handling) with an id; built in, from a file, inline, or contributed by an extension.
- **Catalog configuration** (`netprints.catalog.json`): sources, selection, profile, identity and output
  of one catalog, overridable by command options.
- **Embedded catalog**: a catalog stored in a compiled assembly's metadata by the annotations flavor,
  discoverable without loading the assembly.
- **Command**: a named CLI operation with arguments, options, help and an exit code under the contract.
- **Merge result**: the merged graph, or conflict-marked text plus a non-zero exit, for one three-way
  merge.
- **Fixture extension**: a purpose-built test extension exercising one coexistence scenario.
- **API file**: the tracked list of public API (shipped and unshipped) of one library.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `netprints --help`, `-h`, `--version` and `<command> --help` for every command exit 0 (the CI
  smoke step asserts 0 for the top level; a test covers every command); each of the five exit codes is produced
  by at least one automated test.
- **SC-002**: In CI, `netprints run samples/HelloWorld/HelloWorld.csproj` prints `Hello, World!` and exits
  0, and `netprints regen --check` and `netprints format --check` pass on every sample project and fail
  (exit 1, naming the file) in tests that make one graph stale or non-canonical.
- **SC-003**: For the fixture library, the tool and annotations flavors produce byte-identical catalogs
  for `public-api`, `annotated` and one custom profile, equal to the committed snapshots; producing each
  twice gives identical bytes.
- **SC-004**: A `public-api` catalog of the fixture library differs from the live provider's view of the
  same library in 0 types, members, documentation strings, subclass answers and implicit-cast answers.
- **SC-005**: End to end, a graph that calls a cataloged method builds and runs with the expected output
  in two setups: the catalog contributed by an extension, and embedded in a referenced annotated library;
  editor node search finds that method in both.
- **SC-006**: Cataloging the fixture library takes under 5 seconds and cataloging the framework's
  `System.Runtime` reference assembly takes under 30 seconds on the CI runner.
- **SC-007**: The multi-extension suite runs in the pull-request CI with 0 failures; all 24 discovery
  orders of a four-extension set give byte-identical generated C#; 50 extensions load in deterministic
  order in under 10 seconds.
- **SC-008**: Both hazards are closed: an extension with a host-like private dependency loads and runs,
  and a dependent extension's view of its dependency's type is the same type (identity check passes).
- **SC-009**: In an automated git test, two branches that add different nodes to the same method merge
  with 0 conflicts through the driver (plain git gives 1), and the merged file is canonical and generates
  the same C# as a hand-merged reference; a same-property conflict exits non-zero with markers and both
  values present.
- **SC-010**: After `git-install`, `git diff` of a changed graph shows summary lines instead of JSON;
  running `git-install` a second time changes 0 files and 0 configuration entries.
- **SC-011**: Adding an undeclared public member to a tracked library fails the build; an external probe
  project using an experimental API fails with its `NPXE` id and builds once it opts in.
- **SC-012**: The docs site builds on the pull request with 0 broken links and contains the three new
  guides, the updated extensions guide, the `NetPrints.Catalog` API reference and the two new schemas
  byte-identical to the committed files at their `$schema` addresses.
- **SC-013**: The release dry run on the pull request packs `NetPrints.Catalog` and
  `NetPrints.Annotations` in addition to the existing packages and the package verification passes;
  nothing is published.

## Assumptions

- Commands, file names, attribute names, profile ids and exit codes named in this spec are product
  surface (what users type and read), not implementation choices.
- The CLI is the existing `netprints` dotnet tool (`NetPrints.Cli`); there is no separate catalog tool
  package. The build keeps using the internal generator host (ADR-0009); `netprints generate` calls the
  same generator library in process.
- The graph schema stays at version 1 until the version cut after the planned phases (owner, 2026-09-26),
  so `migrate` ships as the command surface plus the version report, and real migrations are implemented
  with the first schema v2.
- `show` is the text conversion git runs for diffs; hosted web UIs ignore text conversions and custom merge
  drivers, so the canonical JSON and the committed `.netpc.g.cs` remain the review surface there.
- The merge driver treats layout conflicts as non-conflicts (ours wins), as the graph-format research
  recommends, and resolves a conflict on a generated `.netpc.g.cs` by regeneration, never by merging it.
- The in-editor visual diff listed among P1's follow-ups is not in P2: it needs the P3a editor shell and
  belongs to P6 usability work (recorded in plan.md follow-ups).
- Catalog data is stored uncompressed in P2 (diffable, simple); compressing large embedded catalogs is a P8
  performance follow-up.
- Package validation baselines are not set in P2: the extension API is not published before P3 and 0.x
  packages may change; the tracked API files make every change visible (ADR-0010).
- Only Linux runs in CI (constitution I); the native-dependency fixture uses a Linux native library.
- NetPrintsUnreal's `unreal-blueprint` profile is not part of this repository; P2 provides the profile
  format, the extension contribution point and a custom-profile test that exercises attribute-argument
  matching the way that profile will.
- SchemaStore submission, tagging, NuGet publishing and wiki changes are owner actions after merge.
