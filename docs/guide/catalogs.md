# Type catalogs

A type catalog is a JSON file (`*.npcat.json`) that describes the public surface of one or more assemblies: the types, their constructors, methods, properties and fields, the parameters and return types, and the XML documentation summaries. The editor reads a catalog instead of loading the assemblies through Roslyn, so node search over a large library stays fast and works on a machine that does not have the library installed. A catalog only describes types; the graphs still compile against the real assemblies.

The file format is defined by [`schemas/npcat.v1.schema.json`](https://danielmeza.github.io/netprints/schemas/npcat.v1.schema.json). Catalogs are written in a canonical form (fixed member order, LF line endings, one trailing newline, no property that holds its default), so the same input always produces the same bytes and a catalog diffs cleanly in version control.

The `netprints catalog` tool, described below, catalogs assemblies, NuGet packages or the references of a project. Extensions contribute catalogs to the editor with `IExtensionBuilder.AddTypeCatalog`, see [Consuming a catalog from an extension](#consuming-a-catalog-from-an-extension).

## The `netprints catalog` tool

```
netprints catalog [--config <file>]
                  [--assembly <path>]... [--package <id@version>]... [--project <path> [--assemblies <name>]...]
                  [--reference-path <dir>]... [--framework <tfm>]
                  [--include <glob>]... [--exclude <glob>]... [--profile <id|file>]
                  [--id <id>] [--catalog-version <version>]
                  [--output <path>] [--format catalog|csharp] [--class-name <name>] [--namespace <ns>]
                  [--extension <folder>]... [--check]
```

Examples:

```bash
netprints catalog --assembly libs/MyLib.dll --output catalogs/mylib.npcat.json
netprints catalog --package Newtonsoft.Json@13.0.3 --exclude "*.Internal.*"
netprints catalog --config netprints.catalog.json --check
```

Every source is resolved through a temporary SDK project under `obj/netprints-catalog/<hash>/`, next to the configuration file (or in the current directory), so a package's dependencies restore from your configured NuGet sources. The temporary directories (`obj/netprints-catalog/<hash>/`, and `obj/` when it ends up empty) are deleted once the output has been written or checked, including when `--check` reports a difference. They are kept when the run stops earlier, so that you can inspect them: a restore failure, an error in the project you gave to `--project`, or an error diagnostic such as NPC001. A directory that cannot be deleted is reported as `warning: ...` on stderr and does not change the exit code.

### Sources

Give at least one source, on the command line or in the configuration file:

- `--assembly <path>` catalogs an assembly. The value can be a glob of file names (`libs/*.dll`).
- `--package <id>@<version>` catalogs a NuGet package at an exact version. After the restore, the package's folder under the temporary project's package root holds the one resolved version, so a version written as `1.0`, `1.0.0.0` or `1.0.0+build` finds the same package as `1.0.0`.
- `--project <path>` catalogs assemblies referenced by a project (a `.csproj`, or a directory holding one). `--assemblies <name>` picks references by simple name and can be repeated; it needs `--project`. Only one `--project` is allowed per run.
- `--reference-path <dir>` adds a directory searched for the dependencies of the assemblies. The dependencies of a cataloged assembly are resolved through these directories and through the assembly's own directory; a dependency found in none of them makes the members that use its types skip with NPC005.
- `--framework <tfm>` sets the target framework the temporary project restores for (default `net10.0`).

A source option on the command line replaces the `sources` of the configuration file.

### Configuration file

Without `--config` the tool reads `./netprints.catalog.json` when it exists. With neither a file nor a source option the command exits with 2. A file that cannot be read, is invalid or has a newer `schemaVersion` exits with 1 and names both versions.

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

The schema is [`schemas/netprints.catalog.v1.schema.json`](https://danielmeza.github.io/netprints/schemas/netprints.catalog.v1.schema.json). Paths in the file are relative to the file; paths on the command line are relative to the current directory.

| Field | Meaning | Command-line override |
| --- | --- | --- |
| `schemaVersion` | The configuration version, `1`. | none |
| `sources` | One or more sources: `{ "assembly" }`, `{ "package", "version" }` or `{ "project", "assemblies" }`. | `--assembly`, `--package`, `--project` and `--assemblies` |
| `referencePaths` | Extra directories for dependencies. | `--reference-path` |
| `targetFramework` | Target framework of the temporary project. | `--framework` |
| `include`, `exclude` | Type-name globs added to the profile's own. | `--include`, `--exclude` |
| `profile` | A profile id, or an inline profile object. | `--profile` |
| `id`, `version` | The catalog id (default: the first assembly's name, lower-cased) and version (default: its version). | `--id`, `--catalog-version` |
| `output` | `path`, `format` (`catalog` or `csharp`), and for `csharp` the `className` and `namespace`. | `--output`, `--format`, `--class-name`, `--namespace` |
| `extensions` | Extension folders whose catalog profiles can be selected by id. | `--extension` |

### Overrides

Options on the command line override the scalar settings of the file and replace its lists, with one exception: `--include` and `--exclude` are appended to the file's lists. The globs of `include` are combined with OR, so an extra `--include` widens the catalog; only `--exclude` narrows it.

### Output and `--check`

The tool writes the catalog to `--output`, by default `<id>.npcat.json` next to the configuration file (or in the current directory). It writes only when the content differs, so an up-to-date file keeps its timestamp.

```
wrote /home/me/repo/catalogs/mylib.npcat.json (42 types, 310 members)
up to date: /home/me/repo/catalogs/mylib.npcat.json (42 types, 310 members)
```

The command prints the absolute path of the file (`generate` prints paths relative to the current directory).

`--check` writes nothing and exits with 1 when the output is missing or differs, printing `stale: <path>`. Use it in CI to make sure the committed catalog matches the library:

```bash
netprints catalog --config netprints.catalog.json --check
```

Diagnostics print as `<source>: <severity> <code>: <message>`. A warning leaves the exit code alone; an error exits with 1 before anything is built. An unknown profile and an invalid profile file print `catalog: error NPC002: ...` and `catalog: error NPC003: ...` on stderr. The exit codes are as in the other commands (see [Command-line tool](cli.md)):

| Exit | When |
| --- | --- |
| 0 | The catalog was written or is up to date. |
| 1 | An error diagnostic, a restore failure, a project with errors, a `--check` difference, an unreadable or invalid configuration or profile file, an unknown profile id read from the configuration file or the project's profile, or an output file that cannot be written. |
| 2 | Usage: no source, an invalid option value (`--format`, `--package` without a version, `--class-name`, `--namespace`), an unknown `--profile` id, a `--config` file that does not exist. |
| 3 | No .NET SDK is found. |

Option-value checks (`--format`, `--package`, `--class-name`, `--namespace`) and a missing `--config` file run before the SDK check, so they are reported even without an SDK; an unknown `--profile` id needs the extensions loaded first, so it comes after.

### `--format csharp`

`--format csharp` writes a C# file that holds the catalog instead of a `.npcat.json` file, for code that wants a catalog without shipping a data file:

```bash
netprints catalog --assembly libs/MyLib.dll --format csharp --class-name MyLibCatalog --namespace MyCompany.Catalogs --output MyLibCatalog.g.cs
```

```csharp
// <auto-generated/> netprints catalog
namespace MyCompany.Catalogs
{
    internal static partial class MyLibCatalog
    {
        public static byte[] JsonUtf8 { get; } = new byte[]
        {
            123, 10, 32, ...
        };

        public static string Json => global::System.Text.Encoding.UTF8.GetString(JsonUtf8);

        public static global::NetPrints.Reflection.ITypeCatalog Create() =>
            global::NetPrints.Catalog.CatalogLoader.LoadJson(Json);
    }
}
```

The catalog is UTF-8 bytes in a `byte[]` initializer (stored as raw data), not a `const string`: a large catalog as a string literal exceeds the compiler's user string limit (CS8103) once a project embeds a few of them. The block-scoped namespace and the plain array keep the file valid at C# 7.3, the default for `netstandard2.0` projects.

Without `--class-name` the class is the id in Pascal case followed by `Catalog`, and the namespace defaults to `NetPrints.Catalogs`. `--check` works for this format too. The generated file references `NetPrints.Catalog`.

### What a catalog does not list

Indexers, events, finalizers, explicit interface implementations, pointer and function-pointer members and compiler-named members are not cataloged. The live reflection provider lists an indexer as `this[]`; the catalog leaves it out on purpose.

### An assembly the project does not reference

`--assembly` and `--package` catalog what you name, whether or not a project references it. With `--project` and `--assemblies`, a name the project does not reference is NPC001. A cataloged assembly that the graphs' project does not reference still shows up in node search, but code that uses its types fails to compile with the compiler's normal error until the project references it.

## Profiles

A profile decides which types and members a catalog lists. The tool starts from a base and applies the profile's rules in a fixed order: base selection, namespace globs, type globs, type attribute rules, member attribute rules, the obsolete rule, and finally `[NetPrintsIgnore]`, which always excludes.

### Built-in profiles

- `public-api` lists every public type and member, plus the protected members of unsealed types. It is the default.
- `annotated` lists only what the library marks with the NetPrints annotation attributes (`[NetPrintsType]`, `[NetPrintsNode]`).

### A custom profile file

A profile is a small JSON file. Pass its path to `--profile`; it must end in `.npprofile.json`. This one lists members that carry an `Expose` attribute whose `Flags` argument contains `Callable`:

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

| Field | Meaning |
| --- | --- |
| `id` | Required. `public-api` and `annotated` are reserved. |
| `base` | `public-api` (default), `annotated` or `none`. |
| `includeNamespaces`, `excludeNamespaces` | Namespace globs (`*` and `?`); an empty include list means all. |
| `includeTypes`, `excludeTypes` | Globs over full type names (`Ns.Type`, nested `Ns.Outer.Inner`). |
| `typeAttributes`, `memberAttributes` | Rules `{ "rule": "require" or "exclude", "attribute", "argument"? }`. An `argument` names one parameter by `name` or `position` and compares it with `equals` or `contains`. |
| `obsolete` | `include`, `exclude` or `excludeErrors` (default: drop only members marked `[Obsolete(error: true)]`). |

An unknown profile id is NPC002; a profile file that cannot be read or is invalid is NPC003 (a `schemaVersion` above 1 included). Both print as `catalog: error <code>: <message>`.

### Profiles from extensions

An extension can contribute profiles with `IExtensionBuilder.AddCatalogProfile`. Point the tool at the extension's folder and select the profile by id:

```bash
netprints catalog --assembly libs/MyLib.dll --extension path/to/extension --profile my.profile
```

### The project default

With `--project`, and no `--profile` in the command or the file, the tool uses the catalog profile of the project's own profile (`CatalogProfileId` of the `NetPrintsProfile` the project selects, resolved through the project's extensions). The resolution order is: an explicit option or file setting, then the project profile's, then `public-api`. Ids resolve among the built-ins first, then the profiles contributed by `--extension` folders and by the project's extensions. A `--profile` id that no one provides exits with 2 and lists the available ones; the same id read from the configuration file, or a project profile whose catalog profile id no one provides, exits with 1.

## Consuming a catalog from an extension

An extension contributes a catalog through the builder it is handed. The catalog can be a `.npcat.json` file next to the extension's assembly, or the C# class from `--format csharp`:

```csharp
public sealed class MyLibExtension : INetPrintsExtension
{
    public void Register(IExtensionBuilder builder)
    {
        string folder = Path.GetDirectoryName(typeof(MyLibExtension).Assembly.Location)
            ?? throw new InvalidOperationException("The extension assembly has no location.");

        builder.AddTypeCatalog(CatalogLoader.LoadFile(Path.Combine(folder, "mylib.npcat.json")));
        // or: builder.AddTypeCatalog(MyLibCatalog.Create());
    }
}
```

The editor then offers the catalog's types and members in node search, and the generated code compiles against the real assembly, which the project references. When two loaded catalogs share an id, the first is used and NPC103 is logged. `LoadFile` throws `CatalogFormatException` (NPC101 for a newer schema, NPC102 otherwise); catch it around each file if the extension should keep working without that catalog.

The editor also reads the catalogs that the assemblies a project references embed (`NetPrints.Annotations`). They come after the extensions' catalogs, so an extension's catalog wins a shared id, and a referenced assembly whose catalog cannot be read (for example a newer schema) is skipped with a warning while the other references still contribute.

The catalog profile APIs (`CatalogProfile`, `AddCatalogProfile`, and the catalog builder) are experimental: using them needs the `NPXE0004` opt-in, see [API stability](extensions.md#api-stability). Loading a catalog (`CatalogLoader`, `ITypeCatalog`) is stable. The repository's fixture extension, `tests/Fixtures/Extensions/Fx.Catalog`, is a complete example, and the test `ExtensionCatalogTests` builds and runs a graph that calls a cataloged method.

## Diagnostics

Catalog diagnostics use stable codes. The NPC0xx codes come from building a catalog, the NPC1xx codes from reading one.

| Code | Severity | Meaning |
| --- | --- | --- |
| NPC001 | Error | A catalog request names an assembly that is not referenced. |
| NPC002 | Error | Unknown profile id. |
| NPC003 | Error | Profile file unreadable or invalid. |
| NPC004 | Warning | An annotation sits on a member or type that is not public; it is ignored. |
| NPC005 | Warning | A member was skipped because a type it uses comes from an assembly that is not available. Add the assembly's folder with `--reference-path`. |
| NPC006 | Error | Two catalogs with the same id in one build. |
| NPC101 | Error | The catalog `schemaVersion` is not supported by this reader. |
| NPC102 | Error | The catalog file is malformed. |
| NPC103 | Warning | Two loaded catalogs share an id; the later one is ignored. |
