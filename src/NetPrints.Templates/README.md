# NetPrints.Templates

The `dotnet new` templates of NetPrints.

```bash
dotnet new install NetPrints.Templates
dotnet new netprints-extension -n Acme.Widgets --ExtensionId acme.widgets
```

`netprints-extension` creates an extension project and a test project that derives from `ConformanceTests` of the NetPrints extension test kit. The project builds and its tests pass with no edits, and `dotnet pack` of the extension project produces an extension package (package type `NetPrintsExtension`) that holds the extension folder under `extension/`.

| Option | Default | Meaning |
|---|---|---|
| `--ExtensionId` | `my.extension` | The extension id: the manifest id and the prefix of every contribution id |
| `--NetPrintsVersion` | the version of this package | The version of the NetPrints packages the projects reference |

## See also

- [Extensions guide](https://danielmeza.github.io/netprints/guide/extensions)
- [Testing extensions](https://danielmeza.github.io/netprints/guide/extension-testing)
