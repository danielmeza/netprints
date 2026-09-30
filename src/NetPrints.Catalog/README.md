# NetPrints.Catalog

Type catalog engine and reader for the NetPrints node editor.

This package provides APIs for building, reading and loading type catalogs (`*.npcat.json` files). A type catalog describes the public surface of one or more assemblies: the types, their constructors, methods, properties and fields, the parameters and return types, and XML documentation summaries. The NetPrints editor reads catalogs instead of loading assemblies through Roslyn, so node search stays fast and works on machines without the libraries installed.

## Command-line tool

The `netprints catalog` command (part of `NetPrints.Cli`) builds catalogs from assemblies, NuGet packages or project references:

```bash
netprints catalog --assembly libs/MyLib.dll --output catalogs/mylib.npcat.json
netprints catalog --package Newtonsoft.Json@13.0.3 --exclude "*.Internal.*"
netprints catalog --format csharp --class-name MyLibCatalog --namespace MyCompany
```

See the [CLI guide](https://danielmeza.github.io/netprints/docs/guide/catalogs.html) for full documentation.

## Loading catalogs

Use `CatalogLoader` to read catalogs from files or from assemblies that embed them:

```csharp
// Load from a file
var catalog = CatalogLoader.LoadFile("mylib.npcat.json");

// Load embedded catalogs from a referenced assembly
var catalogs = CatalogLoader.LoadEmbedded(typeof(MyClass).Assembly);

// Load from JSON string
var catalog = CatalogLoader.LoadJson(jsonString);
```

## Profiles

A profile controls which types and members a catalog includes:

- `public-api` — every public type and member (the default).
- `annotated` — only types and members marked with `[NetPrintsType]` and `[NetPrintsNode]` from `NetPrints.Annotations`.

Custom profiles are JSON files (`.npprofile.json`) that define inclusion and exclusion rules.

## See also

- [Type catalogs guide](https://danielmeza.github.io/netprints/docs/guide/catalogs.html)
- [NetPrints.Annotations](https://www.nuget.org/packages/NetPrints.Annotations) — embed catalogs in your assemblies
- [NetPrints.Cli](https://www.nuget.org/packages/NetPrints.Cli) — command-line catalog builder
