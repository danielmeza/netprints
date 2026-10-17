# NetPrints.Extensibility.Abstractions

The contracts for NetPrints extensions. Reference this package to write an extension; it brings `NetPrints.Core`, `NetPrints.Reflection`, `NetPrints.Catalog` and `NetPrints.Serialization` with it, together with the NetPrints authoring analyzers (they check node kinds and contributions while you build).

An extension is a class library with a `netprints-extension.json` manifest and one class that implements `INetPrintsExtension`. In `Register` it adds what it contributes through `IExtensionBuilder`: node kinds, node libraries, suggestion providers, document migrations, project templates and, through the editor facet, menus, panels, settings pages and themes.

```xml
<ItemGroup>
  <PackageReference Include="NetPrints.Extensibility.Abstractions" Version="0.4.0" />
</ItemGroup>
```

Types marked `[Experimental]` with an `NPXE` id are still settling: using one needs an explicit opt-in, and its shape may change in a minor release. Everything else follows the API version in `ExtensionApi.Version`.

## See also

- [Extensions guide](https://danielmeza.github.io/netprints/guide/extensions)
- [NetPrints.Extensibility](https://www.nuget.org/packages/NetPrints.Extensibility) — the host that loads extensions
