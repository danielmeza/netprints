---
slug: /
sidebar_position: 1
---

# NetPrints

NetPrints is a visual, node-based programming language for .NET, in the spirit of Unreal Engine's
Blueprints. You wire up nodes instead of writing C# by hand, and NetPrints compiles the graph into
a .NET binary or into C# source you can read, keep and build on. Any .NET assembly — Framework,
Core or Standard — can be referenced and used from a graph, so the goal is: if it's made in C#, you
can call it from NetPrints.

This is a cross-platform, Linux-first continuation of the
[original NetPrints](https://github.com/RobinKa/netprints) by
[Robin Kahlow](https://github.com/RobinKa), rebuilt on [Avalonia](https://avaloniaui.net/): the
same editor and language on Linux, Windows and macOS, on the current .NET 10.

## Where to go next

- **[Install](guide/install.md)** — the editor, the `netprints` CLI tool and the `NetPrints.Sdk`
  package, and what each one needs.
- **[Guide](guide/projects.md)** — how a NetPrints project is laid out, the `.netpc` graph format,
  and writing editor extensions.
- **[API reference](https://danielmeza.github.io/netprints/api/)** — the generated reference for
  `NetPrints.Core` and `NetPrints.Reflection`.
- **[Contributing](contributing/releasing.md)** — the local package feed and the release process,
  for anyone packaging or releasing NetPrints.
- **[Decisions](adr/README.md)** — the architecture decision records behind the above, plus a
  Research notes section in the sidebar with the dated write-ups behind them.

## Source and downloads

The source, issue tracker and releases are on
[GitHub](https://github.com/danielmeza/netprints); built editors, the `dotnet tool`, and the SDK
package are published from the
[Releases page](https://github.com/danielmeza/netprints/releases).
