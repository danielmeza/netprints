# Projects

A NetPrints project is an ordinary SDK-style `.csproj`. There is no separate project format and
nothing to convert: any tool that understands MSBuild — `dotnet build`, Visual Studio, Rider, VS
Code — understands a NetPrints project too.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MyNamespace</RootNamespace>
    <NetPrintsProfile>netprints.default</NetPrintsProfile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetPrints.Sdk" Version="0.1.0" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

The `NetPrints.Sdk` package is the only thing that makes it a NetPrints project: it contributes the
`NetPrintsGraph` item (every `*.netpc.json` file under the project by default) and a build target
that turns each graph into C#. Nothing else about the project file is special — references,
package references, target framework and output type all work exactly as they do in any other .NET
project.

## Generated code (`.netpc.g.cs`)

Building the project runs the NetPrints generator before compilation. For each graph file
`Foo.netpc.json` it writes `Foo.netpc.g.cs` next to it, and the project's normal `Compile` items
pick it up. This file is:

- **committed.** It is checked into source control like any other generated-but-reviewed file (the
  pattern .NET already uses for `.g.cs` from source generators you want to see in review).
- **regenerated, never hand-merged.** If you edit it directly, the next build overwrites your
  changes. If a merge conflicts on it, resolve the conflict in the `.netpc.json` graph (or just take
  either side) and rebuild — do not try to hand-merge the generated C#.
- **kept visible in PRs.** It is not marked `linguist-generated`, so GitHub shows it in diffs. This
  is deliberate: the generated C# is often the easiest way to review what a graph change actually
  does, and a reviewer should not have to open the editor to see it.

A build only regenerates a `.netpc.g.cs` file when its graph (or the generator, or the project file,
or a referenced extension's manifest) actually changed; an up-to-date build does nothing (MSBuild
reports the generate target as skipped).

## Editing outside the editor

Because the graph is text and the generated code is committed, a lot of a NetPrints project can be
read and reasoned about without opening the editor: the diff of a `.netpc.json` file, or the diff of
its `.netpc.g.cs`, both show a plain-text review. See [Graph file format](graph-format.md) for what
those diffs look like.

VS Code has no built-in awareness of the pair of files, but its generic file-nesting feature groups
them in the Explorer. Add this to `.vscode/settings.json`:

```json
{
  "explorer.fileNesting.enabled": true,
  "explorer.fileNesting.patterns": { "*.netpc.json": "${capture}.netpc.g.cs" }
}
```

With this, `Foo.netpc.g.cs` nests under `Foo.netpc.json` in the file tree instead of appearing as a
separate sibling file.

## SDK requirement

Building a NetPrints project needs the .NET 10 SDK (the generator and the editor both target
`net10.0`); running a project's compiled output only needs the matching .NET runtime. Visual Studio
2022, Visual Studio 2026 and Rider all build NetPrints projects the same way `dotnet build` does,
since generation is a plain MSBuild target that shells out to the generator — there is no
IDE-specific integration to install.
