# Data Model: Catalog Tooling and Spectre CLI (P2)

Entities introduced or changed by P2. JSON names are camelCase; optional properties are omitted when empty
(never written as `null`, `[]` or `false`). Wire-level detail and examples: [contracts/catalog.md](./contracts/catalog.md).

## 1. Catalog document (`*.npcat.json`, schema v1)

Model records live in `src/NetPrints.Catalog/Model/` (shared source, ADR-0012).

| Entity | Field | Type | Rules |
|---|---|---|---|
| **CatalogDocument** | `$schema` | string | Always `https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json`; first property |
| | `schemaVersion` | int | `1`; second property; reader rejects `> 1` (NPC101) |
| | `id` | string | Required; `[a-z0-9][a-z0-9._-]*`; default = first covered assembly's simple name, lower-cased |
| | `version` | string | Required; default = first covered assembly's version (`major.minor.build.revision`) |
| | `profile` | string | Id of the profile that produced it |
| | `assemblies` | CatalogAssembly[] | ≥ 1; sorted by name (ordinal); these are the covered assemblies |
| | `types` | CatalogType[] | Sorted by `id` |
| **CatalogAssembly** | `name`, `version` | string | Simple name; assembly version from metadata identity |
| **CatalogType** | `id` | string | Documentation id (`T:Ns.Outer.Inner`1`); unique in the document |
| | `namespace` | string? | Omitted for the global namespace |
| | `name` | string | Metadata name without arity suffix (`Dictionary`) |
| | `kind` | enum | `class` \| `struct` \| `interface` \| `enum` \| `delegate` |
| | `modifiers` | string[]? | Subset of `static`, `abstract`, `sealed`, in that order |
| | `genericParameters` | string[]? | Declared order |
| | `declaringType` | string? | Documentation id of the containing type (nested types) |
| | `baseType` | TypeRef? | Omitted for interfaces, `System.Object` itself, and when the base is `System.Object` |
| | `interfaces` | TypeRef[]? | All implemented interfaces (`AllInterfaces`), sorted by rendered name |
| | `enumMembers` | string[]? | Enums only; declared order |
| | `summary` | string? | Normalized summary (§6) |
| | `node` | NodeHint? | From `[NetPrintsType]` |
| | `constructors` | CatalogConstructor[]? | Sorted by `id` |
| | `methods` | CatalogMethod[]? | Sorted by `id` |
| | `variables` | CatalogVariable[]? | Sorted by `id` |
| **CatalogConstructor** | `id` | string | `M:…#ctor(…)` |
| | `visibility` | enum | `public` \| `protected` |
| | `parameters` | CatalogParameter[]? | Declared order |
| | `summary` | string? | |
| **CatalogMethod** | `id` | string | `M:…` |
| | `name` | string | Metadata name (`op_Implicit` for operators) |
| | `visibility` | enum | `public` \| `protected` |
| | `modifiers` | string[]? | Subset of `static`, `abstract`, `virtual`, `override`, `sealed`, `extension`, `operator`, in that order |
| | `genericParameters` | string[]? | |
| | `parameters` | CatalogParameter[]? | Declared order (extension methods include `this`) |
| | `returnType` | TypeRef? | Omitted for `void` |
| | `returnSummary` | string? | From `<returns>` |
| | `summary` | string? | |
| | `obsolete` | ObsoleteInfo? | `{ "message"?: string, "error"?: true }` |
| | `node` | NodeHint? | From `[NetPrintsNode]` |
| **CatalogParameter** | `name` | string | |
| | `type` | TypeRef | |
| | `passType` | enum? | `reference` \| `out` \| `in` (omitted = by value) |
| | `params` | bool? | `true` for `params` arrays |
| | `default` | TypedValue? | `{ "type": string, "value": string? }` — the graph format's typed-value shape |
| | `summary` | string? | From `<param>` |
| **CatalogVariable** | `id` | string | `P:…` or `F:…` |
| | `name` | string | |
| | `kind` | enum | `property` \| `field` |
| | `type` | TypeRef | |
| | `modifiers` | string[]? | Subset of `static`, `readonly`, `const` |
| | `get` | enum? | Getter visibility (`public` \| `protected`); omitted when not readable |
| | `set` | enum? | Setter visibility; omitted when not writable (readonly field, get-only property) |
| | `summary` | string? | |
| **TypeRef** | `name` | string | Full name with `.` for namespaces and `Outer+Inner` for nested types, as the live provider renders it; generic parameter name when `generic` |
| | `generic` | bool? | Unbound generic parameter |
| | `isEnum`, `isInterface` | bool? | |
| | `args` | TypeRef[]? | Generic arguments; arrays are `System.Array`-shaped as the live provider renders them |
| **NodeHint** | `displayName`, `category` | string? | |
| | `keywords` | string[]? | Sorted |

**Not cataloged** (deliberately): indexers, events, finalizers, explicit interface implementations, pointer and
function-pointer members, and members whose name starts with `<`. The live provider lists an indexer as `this[]`;
the parity test excludes it by name (`ParityTests.NotCatalogedByDesign`).

Validation: ids unique per document; every `declaringType` refers to a type in the document; `enumMembers`
only on enums; `constructors` never on interfaces or static classes. Converting a catalog to NetPrints
specifiers (`CatalogTypeCatalog`) MUST produce the same `TypeSpecifier`/`MethodSpecifier`/`VariableSpecifier`/
`ConstructorSpecifier` values as `ReflectionConverter` does for the same symbols (parity, FR-016).

## 2. Catalog profile (`*.npprofile.json` or inline)

`src/NetPrints.Catalog/Engine/CatalogProfile.cs` (shared source).

| Field | Type | Rules |
|---|---|---|
| `schemaVersion` | int? | Default 1; a newer version is rejected (NPC003) |
| `id` | string | Required; same charset as catalog ids; built-ins `public-api`, `annotated` are reserved |
| `base` | enum | `public-api` \| `annotated` \| `none` (default `public-api`) |
| `includeNamespaces`, `excludeNamespaces` | string[]? | Globs (`*`, `?`) over namespace names; include empty = all |
| `includeTypes`, `excludeTypes` | string[]? | Globs over full type names (`Ns.Type`, nested `Ns.Outer.Inner`) |
| `typeAttributes`, `memberAttributes` | AttributeRule[]? | Evaluated after the base and the globs |
| `obsolete` | enum | `include` \| `exclude` \| `excludeErrors` (default) |
| **AttributeRule** `rule` | enum | `require` \| `exclude` |
| `attribute` | string | Attribute full name (`UnrealSharp.Attributes.UFunctionAttribute`) |
| `argument` | ArgumentMatch? | `{ "name"?: string, "position"?: int, "equals"?: string, "contains"?: string }`; exactly one of `name`/`position`, exactly one of `equals`/`contains` |

Order of evaluation: base profile selection → namespace globs → type globs → type attribute rules →
member attribute rules → obsolete rule → `[NetPrintsIgnore]` (always excludes). A type excluded removes its
members; a type with no remaining members is still listed if the base selected it (static helper types stay
visible).

## 3. Catalog configuration (`netprints.catalog.json`)

`src/NetPrints.Catalog/Config/CatalogConfig.cs` (net10.0, STJ source-generated context; schema
`schemas/netprints.catalog.v1.schema.json`).

| Field | Type | CLI override |
|---|---|---|
| `$schema` | string? | — |
| `schemaVersion` | int? | Default 1; a newer version is an error naming both versions (exit 1) |
| `sources` | Source[] (≥ 1) | `--assembly <path>` (repeatable), `--package <id>@<version>`, `--project <path>`; any source option replaces the file's `sources` |
| **Source** `assembly` | string | Path or glob, relative to the config file |
| **Source** `package` + `version` | string | NuGet id and exact version |
| **Source** `project` + `assemblies` | string, string[] | Project path; simple names of references to catalog |
| `referencePaths` | string[]? | `--reference-path` (repeatable) |
| `targetFramework` | string? | `--framework` (default `net10.0`) |
| `include`, `exclude` | string[]? | `--include`, `--exclude` (repeatable; appended to the profile's namespace/type globs) |
| `profile` | string \| CatalogProfile | `--profile <id|file>` |
| `id`, `version` | string? | `--id`, `--catalog-version` |
| `output` | `{ "path": string, "format": "catalog"\|"csharp", "className"?: string, "namespace"?: string }` | `--output`, `--format`, `--class-name`, `--namespace` |
| `extensions` | string[]? | `--extension <folder>` (repeatable; for extension-contributed profiles) |

Merge rule: CLI options override scalar settings and replace list settings, except `include`/`exclude`, which
are appended. Paths in the file are relative to the file; paths on the command line are relative to the
current directory.

## 4. Embedded catalog

`[assembly: NetPrints.Annotations.NetPrintsEmbeddedCatalogAttribute(string id, int schemaVersion, string json)]`
— one per catalog, `json` is the canonical catalog document. Readers: `EmbeddedCatalogReader.Read(string
assemblyPath)` (metadata only) and `EmbeddedCatalogReader.Read(Assembly)` (loaded). Accessor for
referenced-assembly catalogs: `internal static partial class NetPrintsCatalogs { public const string
<PascalCaseId> = "<json>"; }` in the consuming project's root namespace.

## 5. CLI

| Entity | Fields / states |
|---|---|
| **ExitCode** | `Success = 0`, `Failed = 1`, `Usage = 2`, `NoSdk = 3`, `InternalError = 4` (`src/NetPrints.Cli/ExitCodes.cs`) |
| **ProjectArgument** | Resolution: file → as is; directory → its single `*.csproj`; none → current directory's single `*.csproj`; 0 or ≥ 2 candidates → `Usage` |
| **GenerationMode** | `Write` (default) \| `Check` (`src/NetPrints.Generation/GraphCodeGenerator.cs`) |
| **GeneratedFileResult** | Adds `UpToDate` (`bool`): the file on disk equals the rendered content |
| **FormatResult** (per file) | `Canonical` \| `Rewritten` \| `WouldRewrite` (check) \| `Unreadable` |

## 6. Summary normalization (shared)

Input: the XML of a `<summary>`, `<param>` or `<returns>` element. Output: its text with `<see cref="X:Full.Name"/>`
and `<seealso>` rendered as the last segment of the name (generic arity suffix removed), `<paramref name>`,
`<typeparamref name>` as the name, `<c>` and `<code>` as their text, `<para>` as a blank line, every other element
as its text; runs of whitespace collapsed to one space inside paragraphs; trimmed; empty → omitted.

## 7. Merge (internal to the CLI)

| Entity | Fields |
|---|---|
| **MergeInput** | `Base`, `Ours`, `Theirs`: `ClassDocument` (or raw text when unreadable) |
| **MergeConflict** | `Kind` (`Scalar`, `DeleteModify`, `NodeProperty`, `PinValue`, `DataInputTwice`, `DanglingConnection`, `DuplicateMember`), `Path` (e.g. `methods[m1].nodes[n3].pins[value]`) |
| **MergeOutcome** | `Clean(ClassDocument)` \| `Conflicted(IReadOnlyList<MergeConflict>)` → text fallback |

State: read → merge by identity → validate → `Clean` writes canonical bytes to `ours`, exit 0; otherwise
`git merge-file` over canonical texts, exit 1.

## 8. Extension loading (changed)

| Entity | Change |
|---|---|
| **ExtensionLoadContext** | Gains `IReadOnlyList<ExtensionLoadContext> Dependencies` (declared `dependsOn` order) and `bool TryResolveFromDependencyChain(AssemblyName, out Assembly)`; the prefix list is replaced by `HostAssemblies.IsProvided(name)` (TPA ∪ Default-loaded ∪ `Microsoft.Build*`) |
| **ExtensionLoader** | Creates contexts in topological order and passes the dependency contexts; logs `HostAssemblyShadowed` and `DependencyAssemblyShadowed` warnings |
| **IExtensionBuilder** | Adds `AddCatalogProfile(CatalogProfile)` (`[Experimental("NPXE0004")]`) |
| **ExtensionRegistry** | Adds `CatalogProfiles` (registry order; duplicates rejected with NPX006) |

## 9. API tracking files

`src/<Project>/PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` for Extensibility, Core, Reflection,
Serialization and Catalog; first line `#nullable enable`. Shipped for Core and Reflection = API at `v0.1.1`.
