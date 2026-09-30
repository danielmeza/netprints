# NetPrints.Annotations

Roslyn analyzer and build target for embedding NetPrints type catalogs in your assemblies.

Annotating a library is optional. Any library works in NetPrints without annotations; the editor reads its public API directly. Use this package if you want to choose which members become nodes, customize their names and documentation, and ship that curated catalog inside your library. For a library you don't own, use the `netprints catalog` CLI instead to build a catalog file.

This is a `developmentDependency` package. Add it to your `.csproj` with `PrivateAssets="all"` so it stays out of consumers' dependency graphs. Then mark the public types and methods you want to expose with `[NetPrintsType]` and `[NetPrintsNode]` attributes. The Roslyn generator embeds the catalog as an assembly attribute, which the NetPrints editor reads automatically when a project references your assembly.

## Attributes

`[NetPrintsType]` marks a class, struct, interface or enum for inclusion. Supports `DisplayName` and `Category` for customization.

`[NetPrintsNode]` marks a public method for inclusion as a node. Supports `DisplayName`, `Category` and `Keywords`.

`[NetPrintsIgnore]` excludes a type or member from the catalog.

`[assembly: NetPrintsCatalog]` references an external assembly to include in the embedded catalog.

## Example

In `MyLib.csproj`, add the package reference with `PrivateAssets="all"`. Then in your code:

```csharp
using NetPrints.Annotations;

[NetPrintsType(DisplayName = "Greeter", Category = "Greeting")]
public class GreetingService
{
    [NetPrintsNode(DisplayName = "Greet")]
    public string Greet(string name) => $"Hello, {name}!";
}
```

## Troubleshooting

Generator diagnostics (NPC001 through NPC006) are listed in the [guide](https://danielmeza.github.io/netprints/docs/guide/catalogs.html#diagnostics).
