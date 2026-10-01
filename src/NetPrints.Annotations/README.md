# NetPrints.Annotations

Roslyn analyzer and build target for embedding NetPrints type catalogs in your assemblies.

Annotating a library is optional. Any library works in NetPrints without annotations; the editor reads its public API directly. Use this package if you want to choose which members become nodes, customize their names and documentation, and ship that curated catalog inside your library. For a library you don't own, use the `netprints catalog` CLI instead to build a catalog file.

This is a `developmentDependency` package. Add it to your `.csproj` with `PrivateAssets="all"` so it stays out of consumers' dependency graphs:

```xml
<ItemGroup>
  <PackageReference Include="NetPrints.Annotations" Version="<version>" PrivateAssets="all" />
</ItemGroup>
```

The generator needs the compiler of the .NET 10 SDK (Roslyn 5.0 or later); an older compiler cannot load it. Then mark the public types and methods you want to expose with `[NetPrintsType]` and `[NetPrintsNode]` attributes. The Roslyn generator embeds the catalog as an assembly attribute, which the NetPrints editor reads automatically when a project references your assembly.

## Attributes

`[NetPrintsType]` marks a class, struct, interface or enum for inclusion. `DisplayName` and `Category` are optional hints.

`[NetPrintsNode]` marks a public method for inclusion as a node. `DisplayName`, `Category` and `Keywords` are optional hints.

The hints are recorded in the catalog for tools that read it; the editor does not use them yet.

`[NetPrintsIgnore]` excludes a type or member from the catalog.

`[assembly: NetPrintsCatalog("MyDependency")]` embeds a separate catalog of a referenced assembly and generates an accessor for it (`NetPrintsCatalogs.MyDependency`), for example to register it from an extension. Its `Id`, `Profile`, `Include`, `Exclude` and `AccessorName` properties are described in the [guide](https://danielmeza.github.io/netprints/guide/catalogs#catalogs-of-referenced-assemblies).

## Example

In a project with the package reference above:

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

Generator diagnostics (NPC001 through NPC007) are listed in the [guide](https://danielmeza.github.io/netprints/guide/catalogs#diagnostics).

The documentation summaries of your own catalog come from your source comments, which the compiler parses only when the project sets the `GenerateDocumentationFile` property to `true`. Without it the catalog is still embedded, but has no summaries, and the generator reports NPC007.
