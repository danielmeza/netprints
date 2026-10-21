# NetPrints.Serialization

Graph documents, document formats and the project store of the NetPrints node editor.

This package reads and writes NetPrints graph documents (`*.netpc.json`): the JSON format, the document mapper that turns a document into the `NetPrints.Core` model and back, the data migrations that upgrade versioned node data, and the file-system document store that watches a project for changes. Unknown members in a document are kept when it is read and written back.

Most projects do not reference this package directly. Extension authors get it through [NetPrints.Extensibility.Abstractions](https://www.nuget.org/packages/NetPrints.Extensibility.Abstractions), whose contracts use its document types.

## See also

- [NetPrints.Core](https://www.nuget.org/packages/NetPrints.Core) — the graph model
- [Graph format guide](https://danielmeza.github.io/netprints/guide/graph-format)
