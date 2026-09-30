# Contract: catalog format, engine API and `netprints catalog` (P2)

Implements FR-012–FR-024, FR-029 (tool side), FR-037 (ADR-0012, ADR-0013). Sources: `src/NetPrints.Catalog/`,
`src/NetPrints.Cli/Commands/CatalogCommand.cs`. Tests: `tests/NetPrints.Catalog.Tests/`, `tests/NetPrints.Cli.Tests/`.

## 1. Catalog file (`*.npcat.json`)

Shape: [data-model.md](../data-model.md) §1. Example (abridged from `tests/NetPrints.Catalog.Tests/Snapshots/public-api.npcat.json`; members and types are left out, the shape is the writer's):

```json
{
  "$schema": "https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json",
  "schemaVersion": 1,
  "id": "catalogfixturelib",
  "version": "1.0.0.0",
  "profile": "public-api",
  "assemblies": [
    {
      "name": "CatalogFixtureLib",
      "version": "1.0.0.0"
    }
  ],
  "types": [
    {
      "id": "T:Fixture.Geometry.Vector2",
      "namespace": "Fixture.Geometry",
      "name": "Vector2",
      "kind": "struct",
      "baseType": { "name": "System.ValueType" },
      "summary": "A two-dimensional vector: struct, operators, implicit conversions, interface implementation.",
      "constructors": [
        {
          "id": "M:Fixture.Geometry.Vector2.#ctor(System.Single,System.Single)",
          "visibility": "public",
          "parameters": [
            { "name": "X", "type": { "name": "System.Single" }, "summary": "The horizontal component." },
            { "name": "Y", "type": { "name": "System.Single" }, "summary": "The vertical component." }
          ]
        }
      ],
      "methods": [
        {
          "id": "M:Fixture.Geometry.Vector2.Add(Fixture.Geometry.Vector2)",
          "name": "Add",
          "visibility": "public",
          "parameters": [
            { "name": "other", "type": { "name": "Fixture.Geometry.Vector2" }, "summary": "The other vector." }
          ],
          "returnType": { "name": "Fixture.Geometry.Vector2" },
          "returnSummary": "The sum.",
          "summary": "Adds another vector to this one."
        }
      ],
      "variables": [
        {
          "id": "P:Fixture.Geometry.Vector2.X",
          "name": "X",
          "kind": "property",
          "type": { "name": "System.Single" },
          "get": "public",
          "set": "public",
          "summary": "The horizontal component."
        }
      ]
    }
  ]
}
```

Canonical writing rules (`CanonicalCatalogWriter`, shared): UTF-8 without BOM; LF; two-space indentation; one
property per line except that a parameter, a `TypeRef`, a `node` hint and an `obsolete` record are written inline
on one line (short, and keeps member diffs to one line each); property order exactly as data-model.md §1; string
escaping: `"`, `\`, control characters as `\uXXXX` (lower-case hex) except `\n`, `\r`, `\t`; non-ASCII written as
is; trailing newline. No `null`, empty arrays or `false` flags.

Reader (`CatalogReader`, net10.0): tolerant of unknown properties and any whitespace; `schemaVersion > 1` →
`CatalogFormatException` with code NPC101; malformed or missing required fields → NPC102.

## 2. Public API (`NetPrints.Catalog`)

| Type | Members (summary) | Stability |
|---|---|---|
| `CatalogDocument` and model records | Immutable records per data-model.md §1 | stable |
| `CatalogReader` | `static CatalogDocument Read(Stream)`, `Read(string json)` | stable |
| `CanonicalCatalogWriter` | `static string Write(CatalogDocument)` | stable |
| `CatalogLoader` | `static ITypeCatalog Load(CatalogDocument)`, `LoadFile(string path)`, `LoadJson(string json)`, `LoadEmbedded(Assembly)` | stable |
| `CatalogTypeCatalog` | `ITypeCatalog` over a `CatalogDocument` | stable |
| `EmbeddedCatalogReader` | `static IReadOnlyList<CatalogDocument> Read(string assemblyPath)` (falls back from a package `ref/<tfm>/` path to `lib/<tfm>/`), `Read(Assembly)` | stable |
| `CatalogBuilder` | `static CatalogBuildResult Build(Compilation, IReadOnlyList<IAssemblySymbol>, ICatalogFilter, IDocumentationSource, CatalogIdentity)` | `[Experimental("NPXE0004")]` |
| `ICatalogFilter`, `CatalogProfile`, `CatalogProfileFilter`, `BuiltInCatalogProfiles` | per data-model.md §2 | `[Experimental("NPXE0004")]` |
| `CatalogIdentity`, `IDocumentationSource`, `XmlDocumentationSource` | inputs of `CatalogBuilder` | `[Experimental("NPXE0004")]` |
| `CatalogSourceResolver`, `CatalogSourceSet`, `CatalogCompilationFactory`, `CatalogCompilationInput`, `CatalogSourceException` | the sources pipeline of the CLI (assembly, package, project to compilation) | `[Experimental("NPXE0004")]` |
| `CatalogCSharpEmitter` | `static string Emit(CatalogDocument, string namespace, string className)`, the `--format csharp` output | `[Experimental("NPXE0004")]` |
| `CatalogBuildResult` | `CatalogDocument Document`, `IReadOnlyList<CatalogDiagnostic> Diagnostics` | stable |
| `CatalogDiagnostic`, `CatalogDiagnosticCodes` | code, severity, message, source | stable |
| `CatalogConfig`, `CatalogSourceConfig`, `CatalogOutputConfig`, `CatalogOutputFormat`, `CatalogOverrides`, `ResolvedCatalogConfig`, `CatalogConfigResolver`, `CatalogConfigException` | data-model.md §3; `Resolve(CatalogConfig? file, string? configDirectory, CatalogOverrides cli, string currentDirectory)` | stable |

Everything else is internal: `SymbolIds`, `Glob`, `CSharpLiteral`, `CatalogSchema`, `CatalogConfigSchema` (visible to `NetPrints.Catalog.Tests` only), and the walker, filters, normalizer and JSON helpers. Every public type not listed here is a defect (ADR-0017, FR-045).

## 3. Diagnostics (`CatalogDiagnosticCodes`, shared)

| Code | Severity | Meaning |
|---|---|---|
| NPC001 | Error | A catalog request names an assembly that is not referenced |
| NPC002 | Error | Unknown profile id |
| NPC003 | Error | Profile file unreadable or invalid |
| NPC004 | Warning | Annotation on a member or type that is not public; ignored |
| NPC005 | Warning | Member skipped: a type it uses comes from an assembly that is not available |
| NPC006 | Error | Two catalogs with the same id in one build |
| NPC101 | Error | Catalog `schemaVersion` not supported by this reader |
| NPC102 | Error | Catalog file malformed |
| NPC103 | Warning | Two loaded catalogs share an id; the later one is ignored |

## 4. `netprints catalog`

```
netprints catalog [--config <file>]
                  [--assembly <path>]... [--package <id>@<version>]... [--project <path> [--assemblies <name>]...]
                  [--reference-path <dir>]... [--framework <tfm>]
                  [--include <glob>]... [--exclude <glob>]... [--profile <id|file.npprofile.json>]
                  [--id <id>] [--catalog-version <version>]
                  [--output <path>] [--format catalog|csharp] [--class-name <name>] [--namespace <ns>]
                  [--extension <folder>]... [--check]
```

- Config lookup: `--config`, else `./netprints.catalog.json`; neither and no source option → exit 2; an unreadable,
  invalid or newer config file → exit 1.
- Merge: data-model.md §3. Default output: `<id>.npcat.json` (`catalog`) or `<ClassName>.g.cs` (`csharp`) next
  to the config file (or in the current directory).
- Profile resolution order: explicit option/config → (with `--project`) the project profile's `CatalogProfileId`
  → `public-api`. Ids resolve among built-ins, then profiles contributed by `--extension` folders and the
  project's extensions (registry order); a path ending in `.npprofile.json` is read as a file.
- Sources (research R9): each resolved through an SDK project; temporary projects under
  `obj/netprints-catalog/<hash>/` next to the config (or current directory), deleted (with `obj/` when empty) after
  the write phase, kept when the run stops before it (restore failure, project errors, an error diagnostic); a
  directory that cannot be deleted is a `warning:` line on stderr and leaves the exit code alone. A package target
  is matched on `<root>/<id lower>/`, so `@1.0` and `@1.0.0+build` find the resolved version. Dependencies of an
  assembly resolve through `--reference-path` and the assembly's own directory. Option-value checks (option values,
  `--class-name`, `--namespace`, a missing `--config`) run before the SDK check.
- Output: written only when content differs. `--check`: nothing written; exit 1 when missing or different.
- Output path printed by `wrote`/`up to date`/`stale` is absolute.
- Diagnostics: `<source>: <severity> <code>: <message>`; NPC002/NPC003 print as `catalog: error NPC00x: <message>`
  on stderr.
- Exit: 0 written/up to date; 1 errors (NPC error diagnostics, restore failure, project errors, check difference,
  an unreadable or invalid config or profile file, an unknown profile id from the config or the project profile,
  an unwritable output); 2 usage (no source, an invalid option value, an unknown `--profile` id, a `--config` that
  does not exist); 3 no SDK.

`csharp` format (`CatalogCSharpEmitter`, shared with the generator through `EmbeddedCatalogEmitter`'s data encoding):

```csharp
// <auto-generated/> netprints catalog
namespace <Namespace>
{
    internal static partial class <ClassName>
    {
        public static byte[] JsonUtf8 { get; } = new byte[]
        {
            123, 10, 32, ...     // UTF-8 of the canonical json, decimal, 32 bytes per line
        };

        public static string Json => global::System.Text.Encoding.UTF8.GetString(JsonUtf8);

        public static global::NetPrints.Reflection.ITypeCatalog Create() =>
            global::NetPrints.Catalog.CatalogLoader.LoadJson(Json);
    }
}
```

The output compiles at C# 7.3 (block-scoped namespace, no C# 10 syntax; netstandard2.0 consumers default to 7.3).
The JSON is UTF-8 bytes in an array initializer, which the compiler stores as raw data, never a `const string`:
string literals go to the user string heap (UTF-16, 16 MB limit for the whole assembly), and a few
`System.Runtime`-sized catalogs reached CS8103 (review D-R3). `LargeCatalogEmitTests` compiles three of them
together at C# 7.3. `Json` decodes on each access; callers that read it more than once keep the value.

Not cataloged (data-model.md §1): indexers, events, finalizers, explicit interface implementations, pointer and
function-pointer members and compiler-named members. The live provider lists an indexer (as `this[]`); the parity
test excludes it by name (`ParityTests.NotCatalogedByDesign`).

## 5. Configuration file example

```json
{
  "$schema": "https://danielmeza.github.io/netprints/schemas/netprints.catalog.v1.schema.json",
  "schemaVersion": 1,
  "sources": [
    { "package": "Newtonsoft.Json", "version": "13.0.3" },
    { "assembly": "libs/MyLib.dll" }
  ],
  "include": [ "MyLib.Public.*" ],
  "exclude": [ "*.Internal.*" ],
  "profile": "public-api",
  "output": { "format": "catalog", "path": "catalogs/mylib.npcat.json" }
}
```

## 6. Profile file example (`fixture-flags.npprofile.json`)

```json
{
  "schemaVersion": 1,
  "id": "fixture-flags",
  "base": "none",
  "memberAttributes": [
    { "rule": "require", "attribute": "Fixture.Attributes.ExposeAttribute", "argument": { "name": "Flags", "contains": "Callable" } }
  ]
}
```

## 7. Test obligations

| Id | Test (file) | Case |
|---|---|---|
| CT-T01 | `Format/CatalogWriterTests` | Canonical rules: order, inline records, omissions, escapes, LF, trailing newline (golden) |
| CT-T02 | `Engine/DeterminismTests` | Same bytes on two runs, with references and syntax trees in shuffled order |
| CT-T03 | `Format/CatalogReaderTests` | Round trip = identity; unknown properties tolerated; version 2 → NPC101; malformed → NPC102 |
| CT-T04 | `Engine/PublicApiProfileTests` | Fixture `public-api` = `Snapshots/public-api.npcat.json`, covering every edge case of spec §Edge Cases |
| CT-T05 | `Engine/AnnotatedProfileTests` | Fixture `annotated` = `Snapshots/annotated.npcat.json` (incl. `[NetPrintsIgnore]`, node hints) |
| CT-T06 | `Engine/CustomProfileTests` | `fixture-flags` with id `catalogfixturelib-flags` = `Snapshots/fixture-flags.npcat.json` (argument `contains` on enum flags; `equals` by position) |
| CT-T07 | `Engine/GlobTests` | `*`, `?`, literal dots, nested names, ordinal case |
| CT-T08 | `Runtime/ParityTests` | Live provider vs `CatalogTypeCatalog` over the fixture: 0 differences for every query (research R10) |
| CT-T09 | `Runtime/CatalogTypeCatalogTests` | Subclass (base + interfaces, transitive), implicit casts (identity, reference, `op_Implicit`), documentation per parameter and return, enum names, overridable methods |
| CT-T10 | `Config/CatalogConfigTests` | Parse, relative paths, override and append rules, inline profile, invalid file → 1, `schemaVersion: 2` → error naming both versions (1); uses `Config/netprints.catalog.json` (the §5 example) |
| CT-T11 | `CatalogCommandTests` (Cli.Tests) | Real SDK: `--assembly` fixture DLL → equals CT-T04 snapshot; second run writes nothing; `--check` 0, then 1 after changing `--exclude` |
| CT-T12 | `CatalogCommandTests` | `--package CatalogFixtureLib@1.0.0` from a temporary feed (packed fixture) → same catalog as CT-T11; unknown package → 1 |
| CT-T13 | `CatalogCommandTests` | `--project` of a temp NetPrints project whose extension contributes `fixture-flags` and whose project profile's `CatalogProfileId` is `fixture-flags` → CT-T06 snapshot without `--profile` |
| CT-T14 | `Emit/CSharpFormatTests` | Emitted C# compiles (Roslyn, in test) against NetPrints.Catalog; `Create()` returns a catalog equal to the JSON |
| CT-T15 | `EndToEnd/ExtensionCatalogTests` (Core.Tests) + `Reflection/CatalogSearchTests` (Editor.Tests) | `fx.catalog` extension contributes the fixture catalog: editor search finds `Vector2.Add`; a graph calling it builds and runs in a temp project |
| CT-T16 | `Format/CatalogSchemaTests` | Committed `npcat.v1` and `netprints.catalog.v1` schemas equal the generated ones; snapshots validate (also `eng/validate-schemas.sh`) |
| CT-T17 | `Engine/CatalogPerformanceTests` | Fixture < 5 s; `System.Runtime` reference assembly < 30 s |
| CT-T18 | `Runtime/CatalogTypeCatalogTests` | Two catalogs with one id in a registry → first used, NPC103 logged |
| CT-T19 | `Engine/MissingDependencyTests` | A member whose parameter type's assembly is absent is omitted with NPC005; the rest is produced |
